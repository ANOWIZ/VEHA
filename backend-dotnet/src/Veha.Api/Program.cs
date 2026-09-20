using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Veha.Api.Auth;
using Veha.Api.Config;
using Veha.Api.Errors;
using Veha.Api.Services;
using Veha.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var settings = builder.Configuration.GetSection("Veha").Get<VehaSettings>() ?? new VehaSettings();
builder.Services.Configure<VehaSettings>(builder.Configuration.GetSection("Veha"));

// Dev-вход физически запрещён в production (как dev_auth_allowed в Python).
var devAllowed = settings.AuthDevMode && !builder.Environment.IsProduction();

builder.Services.AddDbContext<VehaDbContext>(o => o.UseNpgsql(settings.DbConnection));

// Контракт JSON совпадает с Python/FastAPI: ключи snake_case, enum — строками
// snake_case ("draft", "per_user", "on_hold"). Фронт не меняется.
static void ConfigureJson(JsonSerializerOptions o)
{
    o.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.DictionaryKeyPolicy = null; // ключи словарей (валюты/категории) — как есть
    o.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
}

builder.Services.AddControllers().AddJsonOptions(o => ConfigureJson(o.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(o => ConfigureJson(o.SerializerOptions));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Ошибки валидации модели — в едином формате {"error":{code,message,details}} и 422
// (как ValidationError в Python), а не стандартный ProblemDetails ASP.NET.
builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = ctx =>
    {
        var details = ctx.ModelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .Select(kv => new
            {
                field = kv.Key,
                errors = kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray(),
            })
            .ToArray();
        return new ObjectResult(new
        {
            error = new { code = "validation_error", message = "Ошибка валидации данных", details },
        })
        { StatusCode = 422 };
    };
});

// --- Сервисы прикладного слоя (Фаза 2b) ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProjectAccessService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TimesheetService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<ResourceService>();
builder.Services.AddScoped<RiskService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<QuoteService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ArtifactStorage>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<OneCService>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(settings.CorsOrigins).AllowAnyHeader().AllowAnyMethod()));

// Аутентификация: «умная» схема выбирает Dev (по X-Dev-User в dev) или Keycloak JWT.
// Keycloak/dev кладут роли в realm_access.roles — переносим их в role-claims.
static Task MapRealmRoles(TokenValidatedContext ctx)
{
    if (ctx.Principal?.Identity is ClaimsIdentity id)
    {
        var realm = ctx.Principal.FindFirst("realm_access")?.Value;
        if (!string.IsNullOrEmpty(realm))
        {
            try
            {
                using var doc = JsonDocument.Parse(realm);
                if (doc.RootElement.TryGetProperty("roles", out var rolesEl))
                    foreach (var r in rolesEl.EnumerateArray())
                        id.AddClaim(new Claim(ClaimTypes.Role, r.GetString() ?? ""));
            }
            catch (JsonException) { /* некорректный realm_access — ролей не будет */ }
        }
    }
    return Task.CompletedTask;
}

builder.Services.AddAuthentication("smart")
    .AddPolicyScheme("smart", "smart", o =>
    {
        o.ForwardDefaultSelector = ctx =>
        {
            if (devAllowed && ctx.Request.Headers.ContainsKey("X-Dev-User"))
                return DevAuthenticationHandler.SchemeName;
            if (devAllowed && DevToken.IsDevBearer(ctx.Request))
                return "DevJwt";
            return JwtBearerDefaults.AuthenticationScheme;
        };
    })
    .AddJwtBearer(o =>
    {
        o.Authority = string.IsNullOrEmpty(settings.KeycloakAuthority) ? null : settings.KeycloakAuthority;
        o.Audience = settings.KeycloakAudience;
        o.RequireHttpsMetadata = !devAllowed;
        o.Events = new JwtBearerEvents { OnTokenValidated = MapRealmRoles };
    })
    // HS256 dev-токены (/auth/dev-login) — принимаются только когда dev разрешён.
    .AddJwtBearer("DevJwt", o =>
    {
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = DevToken.Issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.DevJwtSecret)),
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        };
        o.Events = new JwtBearerEvents { OnTokenValidated = MapRealmRoles };
    })
    .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(
        DevAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization();

var app = builder.Build();

// Миграции при старте (аналог `alembic upgrade head`). В масштабируемом стеке
// миграции выносятся в отдельный job, но для одного инстанса это безопасно
// (EF проверяет историю и применяет только новое). Отключается VEHA__AUTOMIGRATE=false.
if (settings.AutoMigrate)
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<VehaDbContext>().Database.Migrate();
}

app.UseMiddleware<ErrorHandlingMiddleware>();

// Swagger/доки — только вне production (раскрытие поверхности API).
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", environment = app.Environment.EnvironmentName }));

app.Run();

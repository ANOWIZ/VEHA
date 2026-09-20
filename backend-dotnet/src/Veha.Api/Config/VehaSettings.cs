namespace Veha.Api.Config;

/// <summary>Конфигурация приложения (секция "Veha" в appsettings/ENV).
/// 12-factor: переопределяется переменными окружения (VEHA__DBCONNECTION и т.п.).</summary>
public class VehaSettings
{
    public string DbConnection { get; set; } =
        "Host=localhost;Port=5432;Database=veha;Username=veha;Password=veha";

    /// <summary>Применять EF-миграции при старте (как alembic upgrade head).
    /// В масштабируемом стеке отключить (VEHA__AUTOMIGRATE=false) — миграции в job.</summary>
    public bool AutoMigrate { get; set; } = true;

    /// <summary>Разрешить dev-вход по заголовку X-Dev-User. В production
    /// физически игнорируется (см. Program: devAllowed = AuthDevMode &amp;&amp; !IsProduction).</summary>
    public bool AuthDevMode { get; set; } = true;

    /// <summary>Issuer realm Keycloak, напр. https://auth.example.com/realms/veha.</summary>
    public string KeycloakAuthority { get; set; } = "";
    public string KeycloakAudience { get; set; } = "account";

    /// <summary>Секрет для HS256 dev-токенов (/auth/dev-login). Только вне production.</summary>
    public string DevJwtSecret { get; set; } = "dev-secret-change-me-please-32bytes-min";

    public string[] CorsOrigins { get; set; } = ["http://localhost:5173"];

    /// <summary>Норма рабочих часов в неделю (для тепловой карты загрузки).</summary>
    public decimal DefaultWeekNormHours { get; set; } = 40m;

    /// <summary>Локальный каталог хранения артефактов портала Заказчика.</summary>
    public string ArtifactsDir { get; set; } = "artifacts";
    /// <summary>Лимит размера загружаемого артефакта, МБ.</summary>
    public int ArtifactMaxMb { get; set; } = 25;
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using DomainTaskStatus = Veha.Domain.Enums.TaskStatus;

namespace Veha.Infrastructure;

/// <summary>Контекст БД (PostgreSQL/Npgsql). Перечисления хранятся строками,
/// деньги/часы — numeric(15,2), доменные сущности — с soft delete (фильтр запроса),
/// JSON-словари (курсы/итоги/затраты/контакты) — в jsonb.</summary>
public class VehaDbContext(DbContextOptions<VehaDbContext> options) : DbContext(options)
{
    // --- Фаза 1 (ядро) ---
    public DbSet<User> Users => Set<User>();
    public DbSet<UserCostRate> UserCostRates => Set<UserCostRate>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectStageTransition> ProjectStageTransitions => Set<ProjectStageTransition>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<TimesheetWeek> TimesheetWeeks => Set<TimesheetWeek>();

    // --- Каталог / калькулятор ---
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();

    // --- Финансы / ресурсы ---
    public DbSet<ProjectBudget> ProjectBudgets => Set<ProjectBudget>();
    public DbSet<ActualCost> ActualCosts => Set<ActualCost>();
    public DbSet<Forecast> Forecasts => Set<Forecast>();
    public DbSet<ResourcePlan> ResourcePlans => Set<ResourcePlan>();

    // --- Риски / уведомления / портал / аудит ---
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<CustomerActionItem> CustomerActionItems => Set<CustomerActionItem>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IntegrationLog> IntegrationLogs => Set<IntegrationLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Деньги/часы — numeric(15,2), без double.
        b.Properties<decimal>().HavePrecision(15, 2);
        // Перечисления — строками (как в Python native_enum=False).
        b.Properties<ProjectType>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<ProjectStatus>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<Stage>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<ProjectMemberRole>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<DomainTaskStatus>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<TimeEntryStatus>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<LicensingModel>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<QuoteStatus>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<QuoteLineKind>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<CostCategory>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<CostSource>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<CustomerActionStatus>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<IntegrationDirection>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<RiskCategory>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<RiskStatus>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<RiskResponse>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<AuditAction>().HaveConversion<string>().HaveMaxLength(32);
    }

    // jsonb-конвертер для Dictionary<string,string> (курсы/итоги/затраты/контакты).
    private static readonly ValueConverter<Dictionary<string, string>, string> DictConverter =
        new(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null)
                 ?? new Dictionary<string, string>());

    private static readonly ValueComparer<Dictionary<string, string>> DictComparer =
        new((a, c) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                      == JsonSerializer.Serialize(c, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<Dictionary<string, string>>(
                     JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                     (JsonSerializerOptions?)null) ?? new Dictionary<string, string>());

    // Fallback для не-Npgsql провайдеров (in-memory SQLite в тестах): JsonDocument
    // не маппится на jsonb — конвертируем в text. На Postgres используется нативный jsonb.
    private static readonly ValueConverter<JsonDocument?, string?> JsonDocConverter =
        new(v => v == null ? null : v.RootElement.GetRawText(),
            v => v == null ? null : JsonDocument.Parse(v, default));

    private static void Jsonb(ModelBuilder mb, Type entity, string prop)
    {
        mb.Entity(entity).Property(typeof(Dictionary<string, string>), prop)
            .HasConversion(DictConverter, DictComparer)
            .HasColumnType("jsonb");
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // --- Индексы ядра ---
        mb.Entity<Project>().HasIndex(p => p.Code).IsUnique();
        mb.Entity<Project>().HasIndex(p => p.ClientId);
        mb.Entity<Project>().HasIndex(p => p.ManagerId);
        mb.Entity<ProjectMember>().HasIndex(m => m.ProjectId);
        mb.Entity<ProjectMember>().HasIndex(m => m.UserId);
        mb.Entity<ProjectTask>().HasIndex(t => t.ProjectId);
        mb.Entity<TimeEntry>().HasIndex(e => e.ProjectId);
        mb.Entity<TimeEntry>().HasIndex(e => e.UserId);
        mb.Entity<TimeEntry>().HasIndex(e => e.WorkDate);
        mb.Entity<TimeEntry>().HasIndex(e => e.Status);
        mb.Entity<User>().HasIndex(u => u.Username).IsUnique();
        mb.Entity<User>().HasIndex(u => u.Email).IsUnique();
        mb.Entity<TimesheetWeek>().HasIndex(w => new { w.UserId, w.WeekStart }).IsUnique();

        // --- Каталог / калькулятор ---
        mb.Entity<Vendor>().HasIndex(v => v.Name).IsUnique();
        mb.Entity<Product>().HasIndex(p => p.VendorId);
        mb.Entity<Product>().HasIndex(p => p.Name);
        mb.Entity<PriceListItem>().HasIndex(p => p.ProductId);
        mb.Entity<Quote>().HasIndex(q => q.ProjectId);
        mb.Entity<Quote>().HasIndex(q => new { q.ProjectId, q.Version }).IsUnique();
        mb.Entity<QuoteLine>().HasIndex(l => l.QuoteId);
        mb.Entity<Quote>().HasMany(q => q.Lines).WithOne(l => l.Quote!)
            .HasForeignKey(l => l.QuoteId).OnDelete(DeleteBehavior.Cascade);

        // --- Финансы / ресурсы ---
        mb.Entity<ProjectBudget>().HasIndex(x => x.ProjectId).IsUnique();
        mb.Entity<ActualCost>().HasIndex(x => x.ProjectId);
        // Идемпотентность 1С: одна активная затрата на (project, external_id).
        mb.Entity<ActualCost>()
            .HasIndex(x => new { x.ProjectId, x.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalId\" IS NOT NULL AND \"DeletedAt\" IS NULL");
        mb.Entity<Forecast>().HasIndex(x => x.ProjectId);
        mb.Entity<ResourcePlan>().HasIndex(x => new { x.UserId, x.ProjectId, x.WeekStart }).IsUnique();

        // --- Риски / уведомления / портал ---
        mb.Entity<Risk>().HasIndex(r => r.ProjectId);
        mb.Entity<Risk>().HasIndex(r => r.Score);
        mb.Entity<Risk>().HasIndex(r => r.Status);
        mb.Entity<Risk>().ToTable(t =>
        {
            t.HasCheckConstraint("probability_range", "\"Probability\" BETWEEN 1 AND 3");
            t.HasCheckConstraint("impact_range", "\"Impact\" BETWEEN 1 AND 3");
        });
        mb.Entity<Notification>().HasIndex(n => n.UserId);
        mb.Entity<Notification>().HasIndex(n => n.IsRead);
        mb.Entity<CustomerActionItem>().HasIndex(a => a.ProjectId);
        mb.Entity<CustomerActionItem>().HasIndex(a => a.Status);
        mb.Entity<CustomerActionItem>().HasMany(a => a.Artifacts).WithOne(x => x.ActionItem!)
            .HasForeignKey(x => x.ActionItemId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Artifact>().HasIndex(a => a.ProjectId);
        mb.Entity<AuditLog>().HasIndex(a => a.Entity);
        mb.Entity<AuditLog>().HasIndex(a => a.EntityId);
        mb.Entity<IntegrationLog>().HasIndex(a => a.System);
        mb.Entity<IntegrationLog>().HasIndex(a => a.ExternalId);

        // --- JSON-словари в jsonb ---
        Jsonb(mb, typeof(Client), nameof(Client.Contacts));
        Jsonb(mb, typeof(Quote), nameof(Quote.CurrencyRatesSnapshot));
        Jsonb(mb, typeof(Quote), nameof(Quote.Totals));
        Jsonb(mb, typeof(ProjectBudget), nameof(ProjectBudget.PlannedCosts));
        if (Database.IsNpgsql())
        {
            mb.Entity<AuditLog>().Property(a => a.Diff).HasColumnType("jsonb");
            mb.Entity<IntegrationLog>().Property(a => a.Payload).HasColumnType("jsonb");
        }
        else
        {
            mb.Entity<AuditLog>().Property(a => a.Diff).HasConversion(JsonDocConverter);
            mb.Entity<IntegrationLog>().Property(a => a.Payload).HasConversion(JsonDocConverter);

            // SQLite (тесты): не умеет ORDER BY по DateTimeOffset (храним как ticks/long)
            // и агрегировать decimal (храним как double). На Postgres — нативные
            // timestamptz/numeric, поведение/миграции не меняются, SUM идёт в БД.
            var dto = new ValueConverter<DateTimeOffset, long>(
                d => d.UtcTicks, t => new DateTimeOffset(t, TimeSpan.Zero));
            var dtoNullable = new ValueConverter<DateTimeOffset?, long?>(
                d => d.HasValue ? d.Value.UtcTicks : null,
                t => t.HasValue ? new DateTimeOffset(t.Value, TimeSpan.Zero) : null);
            var dec = new ValueConverter<decimal, double>(v => (double)v, v => (decimal)v);
            var decNullable = new ValueConverter<decimal?, double?>(
                v => v.HasValue ? (double)v.Value : null,
                v => v.HasValue ? (decimal)v.Value : null);
            foreach (var entityType in mb.Model.GetEntityTypes())
                foreach (var prop in entityType.GetProperties())
                {
                    if (prop.ClrType == typeof(DateTimeOffset)) prop.SetValueConverter(dto);
                    else if (prop.ClrType == typeof(DateTimeOffset?)) prop.SetValueConverter(dtoNullable);
                    else if (prop.ClrType == typeof(decimal)) prop.SetValueConverter(dec);
                    else if (prop.ClrType == typeof(decimal?)) prop.SetValueConverter(decNullable);
                }
        }

        // --- Soft delete: глобальные фильтры (видны только «живые») ---
        mb.Entity<User>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<UserCostRate>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<Client>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<Project>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<ProjectStageTransition>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<ProjectMember>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<ProjectTask>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<Milestone>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<TimeEntry>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<TimesheetWeek>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<Vendor>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<Product>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<PriceListItem>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<Quote>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<QuoteLine>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<ProjectBudget>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<ActualCost>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<Forecast>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<ResourcePlan>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<Risk>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<Notification>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<CustomerActionItem>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<Artifact>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<AuditLog>().HasQueryFilter(e => e.DeletedAt == null);
        mb.Entity<IntegrationLog>().HasQueryFilter(e => e.DeletedAt == null);
    }

    /// <summary>Проставляет CreatedAt/UpdatedAt автоматически.</summary>
    public override int SaveChanges()
    {
        Stamp();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        Stamp();
        return base.SaveChangesAsync(ct);
    }

    private void Stamp()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var e in ChangeTracker.Entries<BaseEntity>())
        {
            if (e.State == EntityState.Added)
            {
                if (e.Entity.CreatedAt == default) e.Entity.CreatedAt = now;
                e.Entity.UpdatedAt = now;
            }
            else if (e.State == EntityState.Modified)
            {
                e.Entity.UpdatedAt = now;
            }
        }
    }
}

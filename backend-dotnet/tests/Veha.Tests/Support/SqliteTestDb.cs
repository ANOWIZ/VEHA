using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Veha.Infrastructure;

namespace Veha.Tests.Support;

/// <summary>In-memory SQLite VehaDbContext для интеграционных тестов сервисов Фазы 2b.
/// Схема строится из EF-модели (EnsureCreated), без Postgres-миграций. In-memory БД
/// живёт, пока открыто соединение — держим его на время жизни харнесса. Каждый вызов
/// NewContext() — свежий контекст (как отдельная сессия/запрос) на том же соединении.</summary>
public sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<VehaDbContext> _options;

    public SqliteTestDb()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _options = new DbContextOptionsBuilder<VehaDbContext>()
            .UseSqlite(_conn)
            .Options;
        using var ctx = new VehaDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public VehaDbContext NewContext() => new(_options);

    public void Dispose() => _conn.Dispose();
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Veha.Infrastructure;

/// <summary>Design-time фабрика для `dotnet ef migrations` (работает без запущенного
/// приложения). Строку подключения берёт из VEHA_DB_CONNECTION или дефолт localhost.</summary>
public class VehaDbContextFactory : IDesignTimeDbContextFactory<VehaDbContext>
{
    public VehaDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("VEHA_DB_CONNECTION")
                   ?? "Host=localhost;Port=5432;Database=veha;Username=veha;Password=veha";
        var options = new DbContextOptionsBuilder<VehaDbContext>()
            .UseNpgsql(conn)
            .Options;
        return new VehaDbContext(options);
    }
}

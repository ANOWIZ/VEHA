using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Справочник пользователей + версионированные ставки себестоимости.
/// Порт services/user_service.py + repositories/user_repo.py. JIT-provisioning
/// (sync_from_principal) добавится вместе с текущим-пользователем для проектов.</summary>
public class UserService(VehaDbContext db)
{
    /// <summary>Список пользователей, сортировка по ФИО, active_only по умолчанию.</summary>
    public async Task<Page<UserPublicDto>> ListAsync(int limit, int offset, bool activeOnly, CancellationToken ct)
    {
        var query = db.Users.AsQueryable();
        if (activeOnly) query = query.Where(u => u.IsActive);

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderBy(u => u.FullName)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

        return new Page<UserPublicDto>(users.Select(ToPublic).ToList(), total, limit, offset);
    }

    /// <summary>Пользователь + (опционально) действующая ставка на сегодня.
    /// current_cost_rate заполняется только если includeFinancials (роль с финансами).</summary>
    public async Task<UserWithRateDto> GetAsync(Guid userId, bool includeFinancials, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("Пользователь не найден");

        decimal? currentRate = includeFinancials
            ? await EffectiveCostRateAsync(userId, Today, ct)
            : null;

        return new UserWithRateDto(
            user.Id, user.Username, user.Email, user.FullName,
            user.Department, user.Position, user.Grade, user.IsActive,
            user.Roles.ToList(), currentRate);
    }

    /// <summary>Назначить версию ставки. Закрывает открытую предыдущую днём до начала
    /// новой; при ином пересечении — конфликт (порт set_cost_rate).</summary>
    public async Task<CostRateOutDto> SetCostRateAsync(Guid userId, CostRateCreateDto dto, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
            throw new NotFoundException("Пользователь не найден");

        var overlap = await FindOverlapAsync(userId, dto.ValidFrom, dto.ValidTo, ct);
        if (overlap is not null && overlap.ValidTo is null && overlap.ValidFrom < dto.ValidFrom)
        {
            overlap.ValidTo = dto.ValidFrom.AddDays(-1);
        }
        else if (overlap is not null)
        {
            throw new ConflictException("Период ставки пересекается с существующей версией");
        }

        var rate = new UserCostRate
        {
            UserId = userId,
            CostRate = dto.CostRate,
            ValidFrom = dto.ValidFrom,
            ValidTo = dto.ValidTo,
        };
        db.UserCostRates.Add(rate);
        await db.SaveChangesAsync(ct);

        return new CostRateOutDto(rate.Id, rate.UserId, rate.CostRate, rate.ValidFrom, rate.ValidTo);
    }

    /// <summary>Ставка, действующая на дату (valid_from &lt;= date &lt;= valid_to|∞),
    /// самая поздняя по valid_from.</summary>
    public async Task<decimal?> EffectiveCostRateAsync(Guid userId, DateOnly onDate, CancellationToken ct)
    {
        var rate = await db.UserCostRates
            .Where(r => r.UserId == userId
                        && r.ValidFrom <= onDate
                        && (r.ValidTo == null || r.ValidTo >= onDate))
            .OrderByDescending(r => r.ValidFrom)
            .FirstOrDefaultAsync(ct);
        return rate?.CostRate;
    }

    // Пересекающаяся версия: valid_from <= (valid_to|max) И (valid_to пусто ИЛИ >= новая valid_from).
    private Task<UserCostRate?> FindOverlapAsync(Guid userId, DateOnly validFrom, DateOnly? validTo, CancellationToken ct)
    {
        var end = validTo ?? DateOnly.MaxValue;
        return db.UserCostRates
            .Where(r => r.UserId == userId
                        && r.ValidFrom <= end
                        && (r.ValidTo == null || r.ValidTo >= validFrom))
            .FirstOrDefaultAsync(ct);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static UserPublicDto ToPublic(User u) => new(
        u.Id, u.Username, u.Email, u.FullName,
        u.Department, u.Position, u.Grade, u.IsActive, u.Roles.ToList());
}

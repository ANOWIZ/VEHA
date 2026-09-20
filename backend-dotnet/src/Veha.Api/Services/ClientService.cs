using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Справочник клиентов: поиск, CRUD, soft-delete. Порт services/client_service.py
/// + repositories/client_repo.py. Клиенты НЕ аудируются (аудит только Project/Quote/
/// TimeEntry — CLAUDE.md §7). Soft-delete отфильтрован глобальным query-filter в DbContext.</summary>
public class ClientService(VehaDbContext db)
{
    /// <summary>Поиск: lower(name) LIKE %q% ИЛИ inn LIKE %q% (inn — без lower);
    /// сортировка по имени; страница + total (порт client_repo.search).</summary>
    public async Task<Page<ClientDto>> SearchAsync(string? q, int limit, int offset, CancellationToken ct)
    {
        var query = db.Clients.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var namePattern = $"%{q.ToLower()}%";
            var innPattern = $"%{q}%";
            query = query.Where(c =>
                EF.Functions.Like(c.Name.ToLower(), namePattern) ||
                (c.Inn != null && EF.Functions.Like(c.Inn, innPattern)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip(offset)
            .Take(limit)
            .Select(c => new ClientDto(c.Id, c.Name, c.Inn, c.Industry, c.Contacts, c.IsKii))
            .ToListAsync(ct);

        return new Page<ClientDto>(items, total, limit, offset);
    }

    public async Task<ClientDto> GetAsync(Guid id, CancellationToken ct)
        => ToDto(await LoadAsync(id, ct));

    public async Task<ClientDto> CreateAsync(ClientCreateDto dto, CancellationToken ct)
    {
        var client = new Client
        {
            Name = dto.Name,
            Inn = dto.Inn,
            Industry = dto.Industry,
            Contacts = dto.Contacts ?? new(),
            IsKii = dto.IsKii,
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);
        return ToDto(client);
    }

    /// <summary>Частичное обновление. Обновляются только переданные (не-null) поля —
    /// аналог model_dump(exclude_unset=True). Явная очистка поля в null не поддержана
    /// (фронт присылает полный объект либо изменённые поля, не null-сброс).</summary>
    public async Task<ClientDto> UpdateAsync(Guid id, ClientUpdateDto dto, CancellationToken ct)
    {
        var client = await LoadAsync(id, ct);
        if (dto.Name is not null) client.Name = dto.Name;
        if (dto.Inn is not null) client.Inn = dto.Inn;
        if (dto.Industry is not null) client.Industry = dto.Industry;
        if (dto.Contacts is not null) client.Contacts = dto.Contacts;
        if (dto.IsKii is not null) client.IsKii = dto.IsKii.Value;
        await db.SaveChangesAsync(ct);
        return ToDto(client);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var client = await LoadAsync(id, ct);
        client.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Client> LoadAsync(Guid id, CancellationToken ct)
        => await db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct)
           ?? throw new NotFoundException("Клиент не найден");

    private static ClientDto ToDto(Client c)
        => new(c.Id, c.Name, c.Inn, c.Industry, c.Contacts, c.IsKii);
}

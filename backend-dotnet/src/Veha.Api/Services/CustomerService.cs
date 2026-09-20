using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Портал Заказчика: ожидания работ (блокеры), артефакты, статус проекта.
/// Порт services/customer_service.py. Status=Waiting → проект «стоит» на стороне Заказчика.</summary>
public class CustomerService(VehaDbContext db, NotificationService notifier, ArtifactStorage storage)
{
    // ---------- Внутренняя сторона (РП) ----------
    public async Task<ActionItemOutDto> CreateActionItemAsync(Guid projectId, ActionItemCreateDto dto, Guid actorId, CancellationToken ct)
    {
        var item = new CustomerActionItem
        {
            ProjectId = projectId, CreatedBy = actorId, Status = CustomerActionStatus.Waiting,
            Title = dto.Title, Description = dto.Description, Stage = dto.Stage,
            ResponsibleName = dto.ResponsibleName, ResponsibleEmail = dto.ResponsibleEmail,
            ResponsibleUserId = dto.ResponsibleUserId, DueDate = dto.DueDate,
        };
        db.CustomerActionItems.Add(item);
        await db.SaveChangesAsync(ct);
        if (item.ResponsibleUserId is not null)
            await notifier.NotifyAsync(item.ResponsibleUserId.Value, "customer_action_assigned",
                "Требуется ваше участие по проекту", item.Title, "/portal", ct);
        return ToItemOut(item, []);
    }

    public async Task<List<ActionItemOutDto>> ListForProjectAsync(Guid projectId, CancellationToken ct)
        => (await db.CustomerActionItems.Include(i => i.Artifacts)
                .Where(i => i.ProjectId == projectId)
                .OrderByDescending(i => i.CreatedAt).ToListAsync(ct))
            .Select(i => ToItemOut(i, i.Artifacts)).ToList();

    public async Task<CustomerActionItem> GetItemEntityAsync(Guid itemId, CancellationToken ct)
        => await db.CustomerActionItems.Include(i => i.Artifacts).FirstOrDefaultAsync(i => i.Id == itemId, ct)
           ?? throw new NotFoundException("Ожидание не найдено");

    public async Task<ActionItemOutDto> AcceptAsync(Guid itemId, Guid actorId, CancellationToken ct)
    {
        var item = await GetItemEntityAsync(itemId, ct);
        if (item.Status == CustomerActionStatus.Accepted)
            throw new ConflictException("Ожидание уже принято");
        item.Status = CustomerActionStatus.Accepted;
        item.AcceptedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToItemOut(item, item.Artifacts);
    }

    private async Task<Dictionary<Guid, int>> OpenCountsAsync(List<Guid> projectIds, CancellationToken ct)
    {
        if (projectIds.Count == 0) return [];
        return await db.CustomerActionItems
            .Where(i => projectIds.Contains(i.ProjectId) && i.Status == CustomerActionStatus.Waiting)
            .GroupBy(i => i.ProjectId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    // ---------- Сторона Заказчика ----------
    public async Task<Project> AssertClientAccessAsync(User user, Guid projectId, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
                      ?? throw new NotFoundException("Проект не найден");
        if (user.ClientId is null || project.ClientId != user.ClientId)
            throw new ForbiddenException("Нет доступа к этому проекту");
        return project;
    }

    public async Task<List<PortalProjectDto>> PortalProjectsAsync(User user, CancellationToken ct)
    {
        if (user.ClientId is null) return [];
        var projects = await db.Projects.Where(p => p.ClientId == user.ClientId)
            .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        var counts = await OpenCountsAsync(projects.Select(p => p.Id).ToList(), ct);
        return projects.Select(p =>
        {
            var open = counts.GetValueOrDefault(p.Id);
            return new PortalProjectDto(p.Id, p.Code, p.Name, p.Stage, p.Status, null, open, open > 0);
        }).ToList();
    }

    public async Task<PortalProjectDetailDto> PortalProjectDetailAsync(User user, Guid projectId, CancellationToken ct)
    {
        var project = await AssertClientAccessAsync(user, projectId, ct);
        var items = await ListForProjectAsync(projectId, ct);
        var openN = items.Count(i => i.Status == CustomerActionStatus.Waiting);
        return new PortalProjectDetailDto(project.Id, project.Code, project.Name, project.Stage, project.Status, openN > 0, items);
    }

    public async Task<ActionItemOutDto> ProvideAsync(User user, Guid itemId, string? note, CancellationToken ct)
    {
        var item = await GetItemEntityAsync(itemId, ct);
        await AssertClientAccessAsync(user, item.ProjectId, ct);
        if (item.Status == CustomerActionStatus.Accepted)
            throw new ConflictException("Ожидание уже принято — изменения недоступны");
        item.Status = CustomerActionStatus.Provided;
        item.ProvidedAt = DateTimeOffset.UtcNow;
        item.ProvidedNote = note;
        await db.SaveChangesAsync(ct);

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == item.ProjectId, ct);
        if (project is not null)
            await notifier.NotifyAsync(project.ManagerId, "customer_action_provided",
                "Заказчик предоставил данные", $"{item.Title} (проект {project.Code})", $"/projects/{project.Id}", ct);
        return ToItemOut(item, item.Artifacts);
    }

    // ---------- Артефакты ----------
    public async Task<Artifact> GetArtifactEntityAsync(Guid artifactId, CancellationToken ct)
        => await db.Artifacts.FirstOrDefaultAsync(a => a.Id == artifactId, ct)
           ?? throw new NotFoundException("Файл не найден");

    public Task<byte[]> ReadArtifactAsync(Artifact artifact, CancellationToken ct)
        => storage.ReadAsync(artifact.StoragePath, ct);

    public async Task<ArtifactOutDto> SaveArtifactAsync(
        Guid projectId, Guid? itemId, string filename, string? contentType, byte[] data, Guid? uploadedBy, CancellationToken ct)
    {
        var reference = await storage.SaveAsync(projectId, filename, data, ct);
        var artifact = new Artifact
        {
            ProjectId = projectId, ActionItemId = itemId, Filename = filename, ContentType = contentType,
            SizeBytes = data.Length, StoragePath = reference, UploadedBy = uploadedBy,
        };
        db.Artifacts.Add(artifact);
        await db.SaveChangesAsync(ct);
        return ToArtifactOut(artifact);
    }

    // ---------- Мапперы ----------
    private static ArtifactOutDto ToArtifactOut(Artifact a)
        => new(a.Id, a.Filename, a.ContentType, a.SizeBytes, a.CreatedAt, a.UploadedBy);

    private static ActionItemOutDto ToItemOut(CustomerActionItem i, IEnumerable<Artifact> artifacts)
        => new(i.Id, i.ProjectId, i.Stage, i.Title, i.Description, i.ResponsibleName, i.ResponsibleEmail,
            i.ResponsibleUserId, i.DueDate, i.Status, i.ProvidedAt, i.AcceptedAt, i.ProvidedNote, i.CreatedAt,
            artifacts.Select(ToArtifactOut).ToList());
}

using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Задачи проекта (иерархия этап→задача). Порт services/task_service.py.
/// В отличие от Python задача проверяется на принадлежность проекту (закрывает IDOR;
/// фронт всегда шлёт совпадающие id — прозрачно).</summary>
public class TaskService(VehaDbContext db)
{
    public async Task<List<TaskOutDto>> ListForProjectAsync(Guid projectId, CancellationToken ct)
        => (await db.ProjectTasks
                .Where(t => t.ProjectId == projectId)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync(ct))
            .Select(ToOut).ToList();

    public async Task<TaskOutDto> CreateAsync(Guid projectId, TaskCreateDto dto, CancellationToken ct)
    {
        if (dto.ParentId is not null)
        {
            var parent = await db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == dto.ParentId, ct);
            if (parent is null || parent.ProjectId != projectId)
                throw new DomainValidationException("Родительская задача не найдена в этом проекте");
        }

        var task = new ProjectTask
        {
            ProjectId = projectId,
            ParentId = dto.ParentId,
            Name = dto.Name,
            Stage = dto.Stage,
            PlannedHours = dto.PlannedHours,
            AssigneeId = dto.AssigneeId,
            DueDate = dto.DueDate,
        };
        db.ProjectTasks.Add(task);
        await db.SaveChangesAsync(ct);
        return ToOut(task);
    }

    public async Task<TaskOutDto> UpdateAsync(Guid projectId, Guid taskId, TaskUpdateDto dto, CancellationToken ct)
    {
        var task = await LoadAsync(projectId, taskId, ct);
        if (dto.Name is not null) task.Name = dto.Name;
        if (dto.Stage is not null) task.Stage = dto.Stage;
        if (dto.PlannedHours is not null) task.PlannedHours = dto.PlannedHours.Value;
        if (dto.AssigneeId is not null) task.AssigneeId = dto.AssigneeId;
        if (dto.Status is not null) task.Status = dto.Status.Value;
        if (dto.DueDate is not null) task.DueDate = dto.DueDate;
        await db.SaveChangesAsync(ct);
        return ToOut(task);
    }

    public async Task DeleteAsync(Guid projectId, Guid taskId, CancellationToken ct)
    {
        var task = await LoadAsync(projectId, taskId, ct);
        task.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task<ProjectTask> LoadAsync(Guid projectId, Guid taskId, CancellationToken ct)
        => await db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, ct)
           ?? throw new NotFoundException("Задача не найдена");

    private static TaskOutDto ToOut(ProjectTask t)
        => new(t.Id, t.ProjectId, t.ParentId, t.Name, t.Stage, t.PlannedHours, t.AssigneeId, t.Status, t.DueDate);
}

using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Tests.Support;
using TaskStatus = Veha.Domain.Enums.TaskStatus;

namespace Veha.Tests.Services;

/// <summary>Интеграционные тесты TaskService: CRUD, валидация родителя, скоуп к проекту.</summary>
public class TaskServiceTests
{
    private static async Task<Guid> SeedProject(SqliteTestDb db)
    {
        await using var ctx = db.NewContext();
        var p = new Project
        {
            Code = $"PRJ-2002-{Guid.NewGuid().ToString()[..3]}", Name = "P", ClientId = Guid.NewGuid(),
            Type = ProjectType.Implementation, ManagerId = Guid.NewGuid(), Stage = Stage.Presale, Status = ProjectStatus.Active,
        };
        ctx.Projects.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Create_list_update_delete_roundtrip()
    {
        using var db = new SqliteTestDb();
        var pid = await SeedProject(db);

        Guid taskId;
        await using (var ctx = db.NewContext())
        {
            var t = await new TaskService(ctx).CreateAsync(pid,
                new TaskCreateDto { Name = "Этап 1", PlannedHours = 40m, Stage = Stage.Design }, default);
            taskId = t.Id;
            Assert.Equal(TaskStatus.Open, t.Status);
            Assert.Equal(pid, t.ProjectId);
        }

        await using (var ctx = db.NewContext())
        {
            var updated = await new TaskService(ctx).UpdateAsync(pid, taskId,
                new TaskUpdateDto { Status = TaskStatus.InProgress, PlannedHours = 60m }, default);
            Assert.Equal(TaskStatus.InProgress, updated.Status);
            Assert.Equal(60m, updated.PlannedHours);
        }

        await using (var ctx = db.NewContext())
        {
            await new TaskService(ctx).DeleteAsync(pid, taskId, default);
            Assert.Empty(await new TaskService(ctx).ListForProjectAsync(pid, default));
        }
    }

    [Fact]
    public async Task Create_with_parent_from_other_project_is_invalid()
    {
        using var db = new SqliteTestDb();
        var pidA = await SeedProject(db);
        var pidB = await SeedProject(db);

        Guid parentInB;
        await using (var ctx = db.NewContext())
            parentInB = (await new TaskService(ctx).CreateAsync(pidB, new TaskCreateDto { Name = "B-parent" }, default)).Id;

        await using (var ctx = db.NewContext())
            await Assert.ThrowsAsync<DomainValidationException>(() => new TaskService(ctx).CreateAsync(
                pidA, new TaskCreateDto { Name = "child", ParentId = parentInB }, default));
    }

    [Fact]
    public async Task Update_task_scoped_to_project_hides_cross_project_task()
    {
        using var db = new SqliteTestDb();
        var pidA = await SeedProject(db);
        var pidB = await SeedProject(db);

        Guid taskInB;
        await using (var ctx = db.NewContext())
            taskInB = (await new TaskService(ctx).CreateAsync(pidB, new TaskCreateDto { Name = "B-task" }, default)).Id;

        // Задача из проекта B недоступна через проект A (закрытый IDOR).
        await using (var ctx = db.NewContext())
            await Assert.ThrowsAsync<NotFoundException>(() => new TaskService(ctx).UpdateAsync(
                pidA, taskInB, new TaskUpdateDto { Name = "hack" }, default));
    }
}

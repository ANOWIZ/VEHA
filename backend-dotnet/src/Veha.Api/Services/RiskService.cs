using Microsoft.EntityFrameworkCore;
using Veha.Api.Dtos;
using Veha.Domain.Common;
using Veha.Domain.Entities;
using Veha.Domain.Enums;
using Veha.Domain.Rules;
using Veha.Infrastructure;

namespace Veha.Api.Services;

/// <summary>Реестр рисков: CRUD с аудитом, пересчёт score, портфель (матрица 3×3).
/// Порт services/risk_service.py. Score = probability×impact денормализован.</summary>
public class RiskService(
    VehaDbContext db, AuditService audit, NotificationService notifier, ProjectAccessService access)
{
    private static readonly RiskStatus[] Active = [RiskStatus.Open, RiskStatus.Mitigating];
    private static readonly string[] InternalRoles = ["admin", "director", "pm", "presale", "engineer", "finance"];

    public async Task<List<RiskOutDto>> ListForProjectAsync(Guid projectId, RiskStatus? status, CancellationToken ct)
    {
        var q = db.Risks.Where(r => r.ProjectId == projectId);
        if (status is not null) q = q.Where(r => r.Status == status.Value);
        return (await q.OrderByDescending(r => r.Score).ThenByDescending(r => r.CreatedAt).ToListAsync(ct))
            .Select(ToOut).ToList();
    }

    public async Task<Risk> GetAsync(Guid riskId, CancellationToken ct)
        => await db.Risks.FirstOrDefaultAsync(r => r.Id == riskId, ct)
           ?? throw new NotFoundException("Риск не найден");

    public async Task<RiskOutDto> CreateAsync(Guid projectId, RiskCreateDto dto, Guid actorId, CancellationToken ct)
    {
        var score = RiskRules.ComputeScore(dto.Probability, dto.Impact);
        var risk = new Risk
        {
            ProjectId = projectId, CreatedBy = actorId, Score = score,
            Title = dto.Title, Description = dto.Description, Category = dto.Category,
            Probability = dto.Probability, Impact = dto.Impact,
            ResponseStrategy = dto.ResponseStrategy, MitigationPlan = dto.MitigationPlan,
            OwnerId = dto.OwnerId, DueDate = dto.DueDate,
        };
        db.Risks.Add(risk);
        await audit.RecordAsync("Risk", risk.Id, AuditAction.Create, actorId,
            new { title = risk.Title, score, level = RiskRules.RiskLevel(score) }, ct);
        if (risk.OwnerId is not null && risk.OwnerId != actorId)
            await NotifyOwnerAsync(risk, ct);
        return ToOut(risk);
    }

    public async Task<RiskOutDto> UpdateAsync(Guid riskId, RiskUpdateDto dto, Guid actorId, CancellationToken ct)
    {
        var risk = await GetAsync(riskId, ct);
        var prevOwner = risk.OwnerId;
        var diff = new Dictionary<string, object>();
        void Track(string k, object? oldV, object? newV) => diff[k] = new { from = oldV, to = newV };

        if (dto.Title is not null && dto.Title != risk.Title) { Track("title", risk.Title, dto.Title); risk.Title = dto.Title; }
        if (dto.Description is not null && dto.Description != risk.Description) { Track("description", risk.Description, dto.Description); risk.Description = dto.Description; }
        if (dto.Category is not null && dto.Category.Value != risk.Category) { Track("category", risk.Category, dto.Category.Value); risk.Category = dto.Category.Value; }
        var probImpactChanged = false;
        if (dto.Probability is not null && dto.Probability.Value != risk.Probability) { Track("probability", risk.Probability, dto.Probability.Value); risk.Probability = dto.Probability.Value; probImpactChanged = true; }
        if (dto.Impact is not null && dto.Impact.Value != risk.Impact) { Track("impact", risk.Impact, dto.Impact.Value); risk.Impact = dto.Impact.Value; probImpactChanged = true; }
        var statusProvided = dto.Status is not null;
        if (dto.Status is not null && dto.Status.Value != risk.Status) { Track("status", risk.Status, dto.Status.Value); risk.Status = dto.Status.Value; }
        if (dto.ResponseStrategy is not null && dto.ResponseStrategy.Value != risk.ResponseStrategy) { Track("response_strategy", risk.ResponseStrategy, dto.ResponseStrategy.Value); risk.ResponseStrategy = dto.ResponseStrategy.Value; }
        if (dto.MitigationPlan is not null && dto.MitigationPlan != risk.MitigationPlan) { Track("mitigation_plan", risk.MitigationPlan, dto.MitigationPlan); risk.MitigationPlan = dto.MitigationPlan; }
        if (dto.OwnerId is not null && dto.OwnerId != risk.OwnerId) { Track("owner_id", risk.OwnerId, dto.OwnerId); risk.OwnerId = dto.OwnerId; }
        if (dto.DueDate is not null && dto.DueDate != risk.DueDate) { Track("due_date", risk.DueDate, dto.DueDate); risk.DueDate = dto.DueDate; }

        if (probImpactChanged)
        {
            var newScore = RiskRules.ComputeScore(risk.Probability, risk.Impact);
            if (newScore != risk.Score) { Track("score", risk.Score, newScore); risk.Score = newScore; }
        }

        if (statusProvided)
        {
            if (risk.Status == RiskStatus.Closed && risk.ClosedAt is null) risk.ClosedAt = DateTimeOffset.UtcNow;
            else if (risk.Status != RiskStatus.Closed) risk.ClosedAt = null;
        }

        if (diff.Count > 0)
            await audit.RecordAsync("Risk", risk.Id, AuditAction.Update, actorId, diff, ct);
        else
            await db.SaveChangesAsync(ct);

        if (risk.OwnerId is not null && risk.OwnerId != prevOwner && risk.OwnerId != actorId)
            await NotifyOwnerAsync(risk, ct);
        return ToOut(risk);
    }

    public async Task DeleteAsync(Guid riskId, Guid actorId, CancellationToken ct)
    {
        var risk = await GetAsync(riskId, ct);
        risk.DeletedAt = DateTimeOffset.UtcNow;
        await audit.RecordAsync("Risk", risk.Id, AuditAction.Delete, actorId, new { title = risk.Title }, ct);
    }

    private async Task NotifyOwnerAsync(Risk risk, CancellationToken ct)
    {
        if (risk.OwnerId is null) return;
        var owner = await db.Users.FirstOrDefaultAsync(u => u.Id == risk.OwnerId, ct);
        if (owner is null || !InternalRoles.Any(owner.Roles.Contains)) return;
        await notifier.NotifyAsync(risk.OwnerId.Value, "risk_assigned",
            "Вы назначены ответственным за риск", risk.Title, $"/projects/{risk.ProjectId}", ct);
    }

    // ---------- Портфель рисков (set-based) ----------
    public async Task<RiskPortfolioResponseDto> PortfolioAsync(User user, CancellationToken ct)
    {
        var projects = await access.AccessibleProjectsAsync(user, null, 500, ct);
        var byId = projects.ToDictionary(p => p.Id);
        var ids = byId.Keys.ToList();
        if (ids.Count == 0) return EmptyPortfolio();

        var rows = await db.Risks
            .Where(r => ids.Contains(r.ProjectId) && Active.Contains(r.Status))
            .Select(r => new { r.ProjectId, r.Probability, r.Impact, r.Score, r.Category })
            .ToListAsync(ct);

        var matrix = new Dictionary<(int, int), int>();
        var byCategory = new Dictionary<RiskCategory, int>();
        var perProject = new Dictionary<Guid, (int Active, int High, int Top)>();
        var totalHigh = 0;
        foreach (var r in rows)
        {
            matrix[(r.Probability, r.Impact)] = matrix.GetValueOrDefault((r.Probability, r.Impact)) + 1;
            byCategory[r.Category] = byCategory.GetValueOrDefault(r.Category) + 1;
            var agg = perProject.TryGetValue(r.ProjectId, out var e) ? e : (Active: 0, High: 0, Top: 0);
            agg.Active++;
            agg.Top = Math.Max(agg.Top, r.Score);
            if (r.Score >= RiskRules.HighThreshold) { agg.High++; totalHigh++; }
            perProject[r.ProjectId] = agg;
        }

        var matrixCells = (from p in Enumerable.Range(1, 3)
                           from i in Enumerable.Range(1, 3)
                           select new RiskMatrixCellDto(p, i, p * i, RiskRules.RiskLevel(p * i), matrix.GetValueOrDefault((p, i)))).ToList();

        var projectRows = perProject
            .Where(kv => byId.ContainsKey(kv.Key))
            .Select(kv =>
            {
                var p = byId[kv.Key];
                return new ProjectRiskRowDto(p.Id, p.Code, p.Name, p.Stage, p.Status, p.ManagerId,
                    kv.Value.Active, kv.Value.High, kv.Value.Top);
            })
            .OrderByDescending(r => r.TopScore).ThenByDescending(r => r.ActiveCount)
            .ToList();

        var categoryCounts = byCategory
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new RiskCategoryCountDto(kv.Key, kv.Value)).ToList();

        return new RiskPortfolioResponseDto(
            projectRows.Count, rows.Count, totalHigh, matrixCells, categoryCounts, projectRows);
    }

    private static RiskPortfolioResponseDto EmptyPortfolio()
    {
        var cells = (from p in Enumerable.Range(1, 3)
                     from i in Enumerable.Range(1, 3)
                     select new RiskMatrixCellDto(p, i, p * i, RiskRules.RiskLevel(p * i), 0)).ToList();
        return new RiskPortfolioResponseDto(0, 0, 0, cells, [], []);
    }

    private static RiskOutDto ToOut(Risk r) => new(
        r.Id, r.ProjectId, r.Title, r.Description, r.Category, r.Probability, r.Impact, r.Score,
        r.Status, r.ResponseStrategy, r.MitigationPlan, r.OwnerId, r.DueDate, r.ClosedAt,
        r.CreatedBy, r.CreatedAt, r.UpdatedAt, RiskRules.RiskLevel(r.Score));
}

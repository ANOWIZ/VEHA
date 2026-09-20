using Veha.Domain.Enums;
using TaskStatus = Veha.Domain.Enums.TaskStatus;

namespace Veha.Domain.Entities;

/// <summary>Проект внедрения. Код автогенерируется (PRJ-YYYY-NNN).</summary>
public class Project : BaseEntity
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid ClientId { get; set; }
    public ProjectType Type { get; set; }
    public Guid ManagerId { get; set; }
    public Guid? CuratorId { get; set; }
    public Stage Stage { get; set; } = Stage.Presale;
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateOnly? PlannedStart { get; set; }
    public DateOnly? PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }
    public decimal BudgetRevenue { get; set; }
    public string? ContractRef { get; set; }

    public List<ProjectMember> Members { get; set; } = [];
    public List<ProjectTask> Tasks { get; set; } = [];
    public List<Milestone> Milestones { get; set; } = [];
    public List<ProjectStageTransition> StageTransitions { get; set; } = [];
}

/// <summary>История переходов по стадиям (вперёд на 1 / откат на 1 с причиной).</summary>
public class ProjectStageTransition : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Stage? FromStage { get; set; }
    public Stage ToStage { get; set; }
    public string? Reason { get; set; }
    public Guid? CreatedBy { get; set; }
}

/// <summary>Участник проекта с проектной ролью и плановой ставкой продажи.</summary>
public class ProjectMember : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid UserId { get; set; }
    public ProjectMemberRole Role { get; set; }
    public decimal BillRate { get; set; }
    public DateOnly? PeriodFrom { get; set; }
    public DateOnly? PeriodTo { get; set; }
}

/// <summary>Задача проекта (иерархия этап→задача). Единица списания трудозатрат.</summary>
public class ProjectTask : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public Stage? Stage { get; set; }
    public decimal PlannedHours { get; set; }
    public Guid? AssigneeId { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Open;
    public DateOnly? DueDate { get; set; }
}

/// <summary>Веха проекта (в т.ч. платёжная).</summary>
public class Milestone : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Name { get; set; } = "";
    public DateOnly MilestoneDate { get; set; }
    public bool IsPayment { get; set; }
    public decimal Amount { get; set; }
}

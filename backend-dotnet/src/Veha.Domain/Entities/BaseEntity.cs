namespace Veha.Domain.Entities;

/// <summary>База доменных сущностей: UUID PK + временные метки + soft delete.
/// Соответствует DomainBase из Python-версии.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

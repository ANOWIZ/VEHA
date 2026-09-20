namespace Veha.Domain.Entities;

/// <summary>Пользователь (синхронизируется из Keycloak/AD). Роли — список строк
/// (как realm roles). Ставка себестоимости версионируется по датам — UserCostRate.</summary>
public class User : BaseEntity
{
    public string? KeycloakId { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? Grade { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Контакт Заказчика: ссылка на клиента (для роли client).</summary>
    public Guid? ClientId { get; set; }

    public List<string> Roles { get; set; } = [];

    public List<UserCostRate> CostRates { get; set; } = [];
}

/// <summary>Ставка себестоимости (руб/час), действующая с даты. Себестоимость
/// записи берётся по ставке, актуальной на дату трудозатраты.</summary>
public class UserCostRate : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public decimal CostRate { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
}

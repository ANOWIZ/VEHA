namespace Veha.Domain.Entities;

/// <summary>Заказчик (клиент). Признак КИИ (is_kii) важен для требований безопасности.</summary>
public class Client : BaseEntity
{
    public string Name { get; set; } = "";
    public string? Inn { get; set; }
    public string? Industry { get; set; }
    /// <summary>Контакты — произвольный JSON-объект (телефон/email/лицо).</summary>
    public Dictionary<string, string> Contacts { get; set; } = [];
    public bool IsKii { get; set; }  // признак критической информационной инфраструктуры
}

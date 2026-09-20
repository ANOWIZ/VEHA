using System.ComponentModel.DataAnnotations;

namespace Veha.Api.Dtos;

// Порт schemas/client.py. ИНН — 10 цифр (юрлицо) или 12 (ИП/физлицо).
// Ответ ClientOut = ClientBase + id, БЕЗ таймстемпов (created_at/updated_at не отдаются).

/// <summary>Тело создания клиента (ClientCreate).</summary>
public class ClientCreateDto
{
    [Required, StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = "";

    [StringLength(12)]
    [RegularExpression("^([0-9]{10}|[0-9]{12})$", ErrorMessage = "ИНН должен содержать 10 или 12 цифр")]
    public string? Inn { get; set; }

    public string? Industry { get; set; }

    public Dictionary<string, string> Contacts { get; set; } = new();

    public bool IsKii { get; set; }
}

/// <summary>Частичное обновление клиента (ClientUpdate) — все поля опциональны.</summary>
public class ClientUpdateDto
{
    [StringLength(255, MinimumLength = 1)]
    public string? Name { get; set; }

    [StringLength(12)]
    [RegularExpression("^([0-9]{10}|[0-9]{12})$", ErrorMessage = "ИНН должен содержать 10 или 12 цифр")]
    public string? Inn { get; set; }

    public string? Industry { get; set; }

    public Dictionary<string, string>? Contacts { get; set; }

    public bool? IsKii { get; set; }
}

/// <summary>Клиент в ответе (ClientOut): id + поля ClientBase.</summary>
public record ClientDto(
    Guid Id,
    string Name,
    string? Inn,
    string? Industry,
    Dictionary<string, string> Contacts,
    bool IsKii);

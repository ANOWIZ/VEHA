using System.Text.Json;
using System.Text.Json.Serialization;
using Veha.Domain.Common;

namespace Veha.Api;

/// <summary>Разбор enum из query-строки в snake_case ("on_hold" → OnHold). Стандартный
/// model-binding .NET понимает только имена C#/числа; используем ту же snake_case-схему,
/// что и тело запроса, чтобы контракт совпадал с Python.</summary>
public static class EnumQuery
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static T? Parse<T>(string? value) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>($"\"{value}\"", Opts);
        }
        catch (JsonException)
        {
            throw new DomainValidationException($"Недопустимое значение параметра: {value}");
        }
    }
}

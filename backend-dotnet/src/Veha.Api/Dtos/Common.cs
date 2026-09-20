namespace Veha.Api.Dtos;

/// <summary>Страница серверной пагинации: {items,total,limit,offset}.
/// Порт schemas/common.py Page[T] — контракт списков совпадает с Python.</summary>
public record Page<T>(IReadOnlyList<T> Items, int Total, int Limit, int Offset);

/// <summary>Простое сообщение: {message} (порт schemas/common.py Message).</summary>
public record MessageDto(string Message);

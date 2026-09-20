namespace Veha.Api.Dtos;

// Порт schemas/notification.py.

public record NotificationOutDto(
    Guid Id, string Kind, string Title, string? Body, string? Link, bool IsRead, DateTimeOffset CreatedAt);

public record UnreadCountDto(int Count);

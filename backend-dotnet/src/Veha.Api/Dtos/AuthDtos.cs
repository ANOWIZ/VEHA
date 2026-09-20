using System.ComponentModel.DataAnnotations;

namespace Veha.Api.Dtos;

// Порт schemas/user.py (DevLoginRequest/Response).

public class DevLoginRequestDto
{
    [Required, MinLength(1)] public string Username { get; set; } = "";
    public List<string> Roles { get; set; } = ["engineer"];
    public string? Email { get; set; }
    public string? FullName { get; set; }
}

public record DevLoginResponseDto(string AccessToken, string TokenType = "Bearer");

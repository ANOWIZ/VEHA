using Veha.Domain.Common;

namespace Veha.Api.Errors;

/// <summary>Единый формат ошибок: {"error": {"code","message","details"}}.
/// Доменные исключения мапятся на свои HTTP-коды, прочее — 500.</summary>
public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (DomainException ex)
        {
            await Write(ctx, ex.StatusCode, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Необработанная ошибка");
            await Write(ctx, 500, "internal", "Внутренняя ошибка сервера");
        }
    }

    private static Task Write(HttpContext ctx, int status, string code, string message)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsJsonAsync(new
        {
            error = new { code, message, details = Array.Empty<object>() },
        });
    }
}

namespace Veha.Domain.Common;

/// <summary>Базовое доменное исключение с машинным кодом ошибки.</summary>
public abstract class DomainException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
    public abstract int StatusCode { get; }
}

/// <summary>Ошибка валидации бизнес-правил (422, как ValidationError в Python).</summary>
public sealed class DomainValidationException(string message)
    : DomainException(message, "validation_error")
{
    public override int StatusCode => 422;
}

/// <summary>Конфликт состояния (409): например, повторное утверждение.</summary>
public sealed class ConflictException(string message)
    : DomainException(message, "conflict")
{
    public override int StatusCode => 409;
}

/// <summary>Сущность не найдена (404).</summary>
public sealed class NotFoundException(string message)
    : DomainException(message, "not_found")
{
    public override int StatusCode => 404;
}

/// <summary>Недостаточно прав (403).</summary>
public sealed class ForbiddenException(string message)
    : DomainException(message, "forbidden")
{
    public override int StatusCode => 403;
}

/// <summary>Требуется аутентификация (401).</summary>
public sealed class UnauthenticatedException(string message)
    : DomainException(message, "unauthenticated")
{
    public override int StatusCode => 401;
}

/// <summary>Проект закрыт/отменён — изменение недопустимо (409).</summary>
public sealed class ProjectClosedException(string message)
    : DomainException(message, "project_closed")
{
    public override int StatusCode => 409;
}

/// <summary>Недопустимый переход по стадиям (409).</summary>
public sealed class InvalidStageTransitionException(string message)
    : DomainException(message, "invalid_stage_transition")
{
    public override int StatusCode => 409;
}

/// <summary>Некорректные часы трудозатрат (422).</summary>
public sealed class TimesheetInvalidHoursException(string message)
    : DomainException(message, "timesheet_invalid_hours")
{
    public override int StatusCode => 422;
}

/// <summary>Превышен суточный лимит часов (422).</summary>
public sealed class TimesheetDailyLimitException(string message)
    : DomainException(message, "timesheet_daily_limit_exceeded")
{
    public override int StatusCode => 422;
}

/// <summary>Запись отправлена/утверждена — редактирование запрещено (409).</summary>
public sealed class TimesheetLockedException(string message)
    : DomainException(message, "timesheet_locked")
{
    public override int StatusCode => 409;
}

/// <summary>Утверждать может только РП/куратор проекта (403).</summary>
public sealed class NotApproverException(string message)
    : DomainException(message, "not_approver")
{
    public override int StatusCode => 403;
}

/// <summary>Расчёт (ТКП) не в статусе «черновик» — правка запрещена (409).</summary>
public sealed class QuoteLockedException(string message)
    : DomainException(message, "quote_locked")
{
    public override int StatusCode => 409;
}

/// <summary>Не задан курс валюты в снимке расчёта (422).</summary>
public sealed class RateNotFoundException(string currency)
    : DomainException($"Не задан курс для валюты {currency}", "currency_rate_not_found")
{
    public override int StatusCode => 422;
}

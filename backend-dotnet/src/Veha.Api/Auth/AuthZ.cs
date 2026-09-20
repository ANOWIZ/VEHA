using System.Security.Claims;

namespace Veha.Api.Auth;

/// <summary>Ролевые помощники (порт can_see_financials / FINANCIAL_ROLES из core/deps.py).
/// Финансовые поля (cost_rate, маржа, bill_rate) видят только admin/director/finance/pm.</summary>
public static class AuthZ
{
    public static readonly string[] FinancialRoles = ["admin", "director", "finance", "pm"];

    // Закупочные/прайсовые цены вендора видят те, кому они нужны по работе (вкл. presale).
    public static readonly string[] PriceRoles = ["admin", "presale", "pm", "finance", "director"];

    public static bool CanSeeFinancials(ClaimsPrincipal user) => FinancialRoles.Any(user.IsInRole);

    public static bool CanSeePrices(ClaimsPrincipal user) => PriceRoles.Any(user.IsInRole);
}

using System.Security.Claims;
using AggStudentDiscounts.Domain.Entities;

namespace AggStudentDiscounts.Api.Services;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        // JwtBearer по умолчанию отображает "sub" в ClaimTypes.NameIdentifier
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    public static bool IsModerator(this ClaimsPrincipal principal) => principal.IsInRole(Roles.Moderator);
}

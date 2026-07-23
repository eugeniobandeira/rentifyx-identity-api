using System.Diagnostics.CodeAnalysis;
using RentifyxIdentity.Api.Constants;
using RentifyxIdentity.Domain.Constants;

namespace RentifyxIdentity.Api.Extensions;

[ExcludeFromCodeCoverage]
internal static class CookieExtensions
{
    public static void AppendRefreshTokenCookie(
        this HttpContext httpContext,
        string refreshToken)
    {
        CookieOptions options = new()
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = CookieConstants.AuthPath,
            Expires = DateTimeOffset.UtcNow.AddDays(TokenPolicyConstants.RefreshTokenDays)
        };

        httpContext.Response.Cookies.Append(CookieConstants.RefreshToken, refreshToken, options);
    }

    public static void DeleteRefreshTokenCookie(this HttpContext httpContext)
        => httpContext.Response.Cookies.Delete(
            CookieConstants.RefreshToken,
            new CookieOptions { Path = CookieConstants.AuthPath });

    public static string? GetRefreshTokenCookie(this HttpContext httpContext)
        => httpContext.Request.Cookies[CookieConstants.RefreshToken];
}

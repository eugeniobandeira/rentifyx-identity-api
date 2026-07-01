namespace RentifyxIdentity.Api.Middlewares;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private static readonly string[] _docPrefixes = ["/scalar", "/openapi"];

    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? string.Empty;
        bool isDocPath = Array.Exists(_docPrefixes, p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        string csp = isDocPath
            ? "default-src 'self' 'unsafe-inline' 'unsafe-eval' data: blob:; connect-src 'self' ws: wss:"
            : "default-src 'self'";

        context.Response.Headers["Content-Security-Policy"] = csp;
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        await next(context);
    }
}

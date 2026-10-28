using Microsoft.AspNetCore.Diagnostics;

namespace CaudalesPlatform.Api.Shared;

/// <summary>Maps domain exceptions (AppException) to HTTP responses.</summary>
public class AppExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not AppException ex) return false; // let non-domain exceptions through

        context.Response.StatusCode = ex.StatusCode;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
        return true;
    }
}

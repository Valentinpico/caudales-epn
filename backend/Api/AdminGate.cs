namespace CaudalesPlatform.Api.Api;

/// <summary>
/// Decides which requests need X-Admin-Password. Routing matches paths and methods without
/// regard to case, so the gate must too: otherwise "POST /API/..." or "post /api/..." reaches
/// the endpoint without the password.
/// </summary>
public static class AdminGate
{
    public static bool RequiresPassword(string method, string path)
    {
        var isRead = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
        return !isRead
               && path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
               && !path.Equals("/api/admin/verify", StringComparison.OrdinalIgnoreCase)
               && !ChatEndpoints.IsPublic(path);
    }
}

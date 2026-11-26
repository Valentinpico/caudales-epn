using CaudalesPlatform.Api.Api;

namespace CaudalesPlatform.Tests;

public class AdminGateTests
{
    [Theory]
    [InlineData("POST", "/api/documents")]
    [InlineData("DELETE", "/api/documents/3")]
    [InlineData("POST", "/api/chat/reindex")]
    [InlineData("POST", "/API/chat/reindex")]
    [InlineData("post", "/api/chat/reindex")]
    [InlineData("delete", "/Api/Documents/3")]
    [InlineData("PATCH", "/api/x")]
    public void Mutations_under_api_need_the_password_whatever_the_case(string method, string path) =>
        Assert.True(AdminGate.RequiresPassword(method, path));

    [Theory]
    [InlineData("GET", "/api/documents")]
    [InlineData("get", "/API/documents")]
    [InlineData("HEAD", "/api/stations")]
    [InlineData("OPTIONS", "/api/documents")]
    [InlineData("POST", "/api/admin/verify")]
    [InlineData("POST", "/api/chat/ask")]
    [InlineData("POST", "/API/Chat/Upload")]
    [InlineData("DELETE", "/api/chat/session/abcdefgh12")]
    [InlineData("POST", "/files/x")]
    public void Reads_login_and_public_chat_routes_stay_open(string method, string path) =>
        Assert.False(AdminGate.RequiresPassword(method, path));
}

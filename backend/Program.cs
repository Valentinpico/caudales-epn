using System.Text.Json;
using System.Text.Json.Serialization;
using CaudalesPlatform.Api.Api;
using CaudalesPlatform.Api.Application;
using CaudalesPlatform.Api.Infrastructure;
using CaudalesPlatform.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(cfg["Cors:Origin"] ?? "http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(cfg.GetConnectionString("Postgres")));
builder.Services.AddScoped<SeriesService>();
builder.Services.AddScoped<KnowledgeGraphService>();
builder.Services.AddScoped<RepositoryService>();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();

var dataRoot = Path.Combine(app.Environment.ContentRootPath, "data");
var rawDir = cfg["RawData:Dir"] ?? Path.Combine(app.Environment.ContentRootPath, "info");

// Note: EnsureCreated, no migration history. Switch to EF Migrations when the schema
// starts changing in production.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DataSeeder.SeedAsync(db, rawDir, dataRoot, app.Logger);
    await scope.ServiceProvider.GetRequiredService<KnowledgeGraphService>().RebuildAsync();
}

app.UseCors();

Directory.CreateDirectory(dataRoot);
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(dataRoot), RequestPath = "/files" });

// Admin gate: every mutation under /api requires X-Admin-Password. Reads stay public.
var adminPassword = cfg["Admin:Password"] ?? throw new InvalidOperationException("Admin:Password is required.");
app.Use(async (ctx, next) =>
{
    var isMutation = ctx.Request.Method is "POST" or "PUT" or "DELETE" or "PATCH";
    var path = ctx.Request.Path.Value ?? "";
    if (isMutation && path.StartsWith("/api") && path != "/api/admin/verify"
        && ctx.Request.Headers["X-Admin-Password"] != adminPassword)
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsJsonAsync(new { error = "Se requiere contraseña de administrador." });
        return;
    }
    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/admin/verify", (AdminLogin body) =>
    body.Password == adminPassword ? Results.Ok(new { ok = true }) : Results.Unauthorized());
app.MapApi(dataRoot);

app.Run();

record AdminLogin(string Password);

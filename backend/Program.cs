using System.Text.Json;
using System.Text.Json.Serialization;
using CaudalesPlatform.Api.Api;
using System.Threading.RateLimiting;
using CaudalesPlatform.Api.Application;
using CaudalesPlatform.Api.Application.Rag;
using CaudalesPlatform.Api.Infrastructure;
using CaudalesPlatform.Api.Infrastructure.Rag;
using CaudalesPlatform.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Npgsql;

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

// Document assistant (RAG). Provider clients are singletons, created on first use.
var rag = cfg.GetSection("Rag").Get<RagOptions>() ?? new RagOptions();
rag.OpenAiApiKey ??= cfg["OPENAI_API_KEY"];
if (cfg["Rag:Region"] is null && cfg["AWS_REGION"] is { Length: > 0 } awsRegion) rag.Region = awsRegion;
builder.Services.AddSingleton(rag);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(NpgsqlDataSource.Create(cfg.GetConnectionString("Postgres")!));
builder.Services.AddSingleton<IChunkStore, PgVectorChunkStore>();
builder.Services.AddSingleton(_ => RagProviders.Create(rag,
    () => RagProviders.BedrockClient(rag), () => RagProviders.OpenAiHttpClient(rag)));
builder.Services.AddSingleton(sp => sp.GetRequiredService<RagModels>().Embedder);
builder.Services.AddSingleton(sp => sp.GetRequiredService<RagModels>().Chat);
builder.Services.AddSingleton<CorpusService>();
builder.Services.AddSingleton<SessionFileService>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddHostedService<RagBackgroundService>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = (ctx, ct) => new ValueTask(ctx.HttpContext.Response.WriteAsJsonAsync(
        new { error = "Demasiadas consultas seguidas. Espere un minuto e inténtelo de nuevo." }, ct));
    o.AddPolicy(ChatEndpoints.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = rag.RateLimitPerMinute, Window = TimeSpan.FromMinutes(1) }));
});
// Behind Caddy the client IP arrives in X-Forwarded-For; the rate limit partitions on it.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();

var dataRoot = Path.Combine(app.Environment.ContentRootPath, "data");
var rawDir = cfg["RawData:Dir"] ?? Path.Combine(app.Environment.ContentRootPath, "info");

// Note: EnsureCreated, no migration history. Switch to EF Migrations when the schema
// starts changing in production.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    // ponytail: EnsureCreated never alters an existing table; columns added later go here.
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE observations ADD COLUMN IF NOT EXISTS \"Origin\" text");
    await DataSeeder.SeedAsync(db, rawDir, dataRoot, app.Logger);
    await scope.ServiceProvider.GetRequiredService<KnowledgeGraphService>().RebuildAsync();
}

try { await app.Services.GetRequiredService<IChunkStore>().EnsureSchemaAsync(CancellationToken.None); }
catch (Exception e) { app.Logger.LogError(e, "No se pudo preparar pgvector; el asistente no estará disponible"); }

app.UseCors();
app.UseRateLimiter();

Directory.CreateDirectory(dataRoot);
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(dataRoot), RequestPath = "/files" });

// Admin gate: every mutation under /api requires X-Admin-Password. Reads stay public.
var adminPassword = cfg["Admin:Password"] ?? throw new InvalidOperationException("Admin:Password is required.");
app.Use(async (ctx, next) =>
{
    var isMutation = ctx.Request.Method is "POST" or "PUT" or "DELETE" or "PATCH";
    var path = ctx.Request.Path.Value ?? "";
    if (isMutation && path.StartsWith("/api") && path != "/api/admin/verify" && !ChatEndpoints.IsPublic(path)
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
app.MapChat();

app.Run();

record AdminLogin(string Password);

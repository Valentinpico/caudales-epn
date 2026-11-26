using CaudalesPlatform.Api.Application.Rag;

namespace CaudalesPlatform.Api.Infrastructure.Rag;

/// <summary>
/// Indexes the corpus once at startup (no-op when the fingerprint matches) and then purges
/// expired session files every few minutes. Failures are logged; the API keeps serving.
/// </summary>
public sealed class RagBackgroundService(
    IServiceProvider services, RagOptions options, ILogger<RagBackgroundService> logger) : BackgroundService
{
    public static readonly TimeSpan PurgeInterval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (options.IndexOnStartup)
        {
            try
            {
                var result = await services.GetRequiredService<CorpusService>().IndexAsync(force: false, ct);
                logger.LogInformation("Corpus RAG {State}: {Chunks} fragmentos ({Model})",
                    result.Reindexed ? "reindexado" : "sin cambios", result.Chunks, result.Model);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "No se pudo indexar el corpus RAG");
            }
        }

        using var timer = new PeriodicTimer(PurgeInterval);
        do
        {
            try
            {
                var purged = await services.GetRequiredService<SessionFileService>().PurgeExpiredAsync(ct);
                if (purged > 0) logger.LogInformation("Purgados {Count} fragmentos de sesión vencidos", purged);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Falló la purga de archivos de sesión");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}

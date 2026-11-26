using System.Globalization;
using System.Text;
using CaudalesPlatform.Api.Application.Rag;
using Npgsql;

namespace CaudalesPlatform.Api.Infrastructure.Rag;

/// <summary>
/// pgvector storage with plain SQL. Vectors travel as text literals cast to ::vector, which
/// avoids a type-mapping plugin. The corpus is a few hundred chunks, so search is an exact
/// cosine scan; add an HNSW index if it grows into the tens of thousands.
/// </summary>
public sealed class PgVectorChunkStore(NpgsqlDataSource dataSource) : IChunkStore
{
    private static readonly string Schema = $"""
        CREATE EXTENSION IF NOT EXISTS vector;
        CREATE TABLE IF NOT EXISTS rag_chunks (
            id bigserial PRIMARY KEY,
            doc_id text NOT NULL,
            title text NOT NULL,
            section text NOT NULL,
            url text,
            chunk_index int NOT NULL,
            content text NOT NULL,
            embedding vector({RagOptions.Dimensions}) NOT NULL,
            model text NOT NULL);
        CREATE TABLE IF NOT EXISTS rag_session_chunks (
            id bigserial PRIMARY KEY,
            session_id text NOT NULL,
            doc_id text NOT NULL,
            title text NOT NULL,
            section text NOT NULL,
            chunk_index int NOT NULL,
            content text NOT NULL,
            embedding vector({RagOptions.Dimensions}) NOT NULL,
            model text NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now());
        CREATE INDEX IF NOT EXISTS ix_rag_session_chunks_session ON rag_session_chunks (session_id);
        CREATE INDEX IF NOT EXISTS ix_rag_session_chunks_created ON rag_session_chunks (created_at);
        CREATE TABLE IF NOT EXISTS rag_index_state (
            id int PRIMARY KEY CHECK (id = 1),
            model text NOT NULL,
            fingerprint text NOT NULL,
            indexed_at timestamptz NOT NULL);
        """;

    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand(Schema);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IndexState> GetStateAsync(CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand("""
            SELECT (SELECT count(*) FROM rag_chunks), s.model, s.fingerprint, s.indexed_at
            FROM (SELECT 1) one LEFT JOIN rag_index_state s ON s.id = 1
            """);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return new IndexState((int)r.GetInt64(0),
            r.IsDBNull(1) ? null : r.GetString(1),
            r.IsDBNull(2) ? null : r.GetString(2),
            r.IsDBNull(3) ? null : r.GetFieldValue<DateTimeOffset>(3));
    }

    public async Task ReplaceCorpusAsync(IReadOnlyList<ChunkRecord> chunks, string model, string fingerprint, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using (var del = new NpgsqlCommand("DELETE FROM rag_chunks", conn, tx))
            await del.ExecuteNonQueryAsync(ct);

        foreach (var c in chunks)
        {
            await using var ins = new NpgsqlCommand("""
                INSERT INTO rag_chunks (doc_id, title, section, url, chunk_index, content, embedding, model)
                VALUES ($1, $2, $3, $4, $5, $6, $7::vector, $8)
                """, conn, tx);
            ins.Parameters.Add(new() { Value = c.DocId });
            ins.Parameters.Add(new() { Value = c.Title });
            ins.Parameters.Add(new() { Value = c.Section });
            ins.Parameters.Add(new() { Value = (object?)c.Url ?? DBNull.Value });
            ins.Parameters.Add(new() { Value = c.Index });
            ins.Parameters.Add(new() { Value = c.Content });
            ins.Parameters.Add(new() { Value = Literal(c.Embedding) });
            ins.Parameters.Add(new() { Value = model });
            await ins.ExecuteNonQueryAsync(ct);
        }

        await using (var state = new NpgsqlCommand("""
            INSERT INTO rag_index_state (id, model, fingerprint, indexed_at) VALUES (1, $1, $2, now())
            ON CONFLICT (id) DO UPDATE SET model = EXCLUDED.model, fingerprint = EXCLUDED.fingerprint, indexed_at = now()
            """, conn, tx))
        {
            state.Parameters.Add(new() { Value = model });
            state.Parameters.Add(new() { Value = fingerprint });
            await state.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task<List<ScoredChunk>> SearchAsync(float[] query, string model, string? sessionId, int k, CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand("""
            SELECT doc_id, title, section, url, content, 1 - (embedding <=> $1::vector) AS score, false
            FROM rag_chunks WHERE model = $2
            UNION ALL
            SELECT doc_id, title, section, NULL, content, 1 - (embedding <=> $1::vector), true
            FROM rag_session_chunks WHERE session_id = $3 AND model = $2
            ORDER BY score DESC LIMIT $4
            """);
        cmd.Parameters.Add(new() { Value = Literal(query) });
        cmd.Parameters.Add(new() { Value = model });
        cmd.Parameters.Add(new() { Value = (object?)sessionId ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text });
        cmd.Parameters.Add(new() { Value = k });

        var hits = new List<ScoredChunk>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            hits.Add(new ScoredChunk(r.GetString(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4), r.GetDouble(5), r.GetBoolean(6)));
        return hits;
    }

    public async Task AddSessionChunksAsync(string sessionId, IReadOnlyList<ChunkRecord> chunks, string model, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        foreach (var c in chunks)
        {
            await using var ins = new NpgsqlCommand("""
                INSERT INTO rag_session_chunks (session_id, doc_id, title, section, chunk_index, content, embedding, model)
                VALUES ($1, $2, $3, $4, $5, $6, $7::vector, $8)
                """, conn, tx);
            ins.Parameters.Add(new() { Value = sessionId });
            ins.Parameters.Add(new() { Value = c.DocId });
            ins.Parameters.Add(new() { Value = c.Title });
            ins.Parameters.Add(new() { Value = c.Section });
            ins.Parameters.Add(new() { Value = c.Index });
            ins.Parameters.Add(new() { Value = c.Content });
            ins.Parameters.Add(new() { Value = Literal(c.Embedding) });
            ins.Parameters.Add(new() { Value = model });
            await ins.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task<int> CountSessionChunksAsync(string sessionId, CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand("SELECT count(*)::int FROM rag_session_chunks WHERE session_id = $1");
        cmd.Parameters.Add(new() { Value = sessionId });
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<int> DeleteSessionAsync(string sessionId, CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand("DELETE FROM rag_session_chunks WHERE session_id = $1");
        cmd.Parameters.Add(new() { Value = sessionId });
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> PurgeSessionsOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        await using var cmd = dataSource.CreateCommand("DELETE FROM rag_session_chunks WHERE created_at < $1");
        cmd.Parameters.Add(new() { Value = cutoff.UtcDateTime, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.TimestampTz });
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>pgvector text form: [0.1,0.2,…] with invariant decimals.</summary>
    public static string Literal(float[] v)
    {
        var sb = new StringBuilder(v.Length * 10).Append('[');
        for (var i = 0; i < v.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(v[i].ToString("R", CultureInfo.InvariantCulture));
        }
        return sb.Append(']').ToString();
    }
}

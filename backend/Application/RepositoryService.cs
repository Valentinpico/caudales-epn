using CaudalesPlatform.Api.Domain.Entities;
using CaudalesPlatform.Api.Infrastructure;
using CaudalesPlatform.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace CaudalesPlatform.Api.Application;

public record DocumentInput(string Title, string Category, string? Objective, string? Description, string? Authors);
public record UploadedFile(string FileName, Stream Content);

/// <summary>Digital repository: project documents with their files under data/documentos.</summary>
public class RepositoryService(AppDbContext db, KnowledgeGraphService graph)
{
    public static readonly string[] Categories = ["propuesta", "datos", "informe", "protocolo", "codigo", "articulo"];
    private static readonly string[] Objectives = ["OE1", "OE2", "OE3", "OE4"];
    private static readonly string[] Extensions =
        [".pdf", ".docx", ".xlsx", ".csv", ".md", ".txt", ".ipynb", ".r", ".py", ".zip", ".png", ".jpg"];

    public async Task<List<Document>> ListAsync(string? category, string? objective, string? search)
    {
        var q = db.Documents.AsNoTracking();
        if (category is not null) q = q.Where(d => d.Category == category);
        if (objective is not null) q = q.Where(d => d.Objective == objective);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(d => d.Title.ToLower().Contains(s) || (d.Description ?? "").ToLower().Contains(s));
        }
        return await q.OrderByDescending(d => d.CreatedAt).ToListAsync();
    }

    public async Task<Document> CreateAsync(DocumentInput input, UploadedFile? file, string dataRoot)
    {
        Validate(input, file);
        var doc = new Document
        {
            Title = input.Title.Trim(), Category = input.Category, Objective = input.Objective,
            Description = input.Description, Authors = input.Authors,
        };
        if (file is not null) doc.FilePath = await SaveFileAsync(file, dataRoot);

        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        await graph.RebuildAsync();
        return doc;
    }

    public async Task DeleteAsync(int id, string dataRoot)
    {
        var doc = await db.Documents.FindAsync(id) ?? throw new NotFoundError($"Documento {id} no encontrado.");
        if (doc.FilePath is not null) File.Delete(Path.Combine(dataRoot, doc.FilePath));
        db.Documents.Remove(doc);
        await db.SaveChangesAsync();
        await graph.RebuildAsync();
    }

    private static void Validate(DocumentInput input, UploadedFile? file)
    {
        if (string.IsNullOrWhiteSpace(input.Title)) throw new ValidationError("El título es obligatorio.");
        if (!Categories.Contains(input.Category))
            throw new ValidationError($"Categoría inválida. Use: {string.Join(", ", Categories)}.");
        if (input.Objective is not null && !Objectives.Contains(input.Objective))
            throw new ValidationError("El objetivo debe ser OE1, OE2, OE3 u OE4.");
        if (file is not null && !Extensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            throw new ValidationError("Tipo de archivo no permitido.");
    }

    private static async Task<string> SaveFileAsync(UploadedFile file, string dataRoot)
    {
        // Random prefix avoids collisions; GetFileName strips any client-sent path.
        var relative = Path.Combine("documentos", $"{Guid.NewGuid():N}-{Path.GetFileName(file.FileName)}");
        var full = Path.Combine(dataRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await file.Content.CopyToAsync(fs);
        return relative;
    }
}

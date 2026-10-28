import { useRef, useState } from "react";
import { api } from "../../shared/api";
import { useAdmin } from "../../shared/admin";
import { useApi } from "../../shared/useApi";
import { ErrorNote, Loading } from "../../shared/Status";

const CATEGORIES: Record<string, string> = {
  propuesta: "Propuesta",
  datos: "Datos",
  informe: "Informe",
  protocolo: "Protocolo",
  codigo: "Código",
  articulo: "Artículo",
};
const OBJECTIVES = ["OE1", "OE2", "OE3", "OE4"];

export function RepositoryPage() {
  const { isAdmin } = useAdmin();
  const [category, setCategory] = useState("");
  const [objective, setObjective] = useState("");
  const [search, setSearch] = useState("");
  const docs = useApi(() => api.documents({ category, objective, search }), [category, objective, search]);

  return (
    <div className="space-y-8">
      <header className="max-w-3xl space-y-3">
        <h1 className="type-display text-4xl">Repositorio digital</h1>
        <p className="max-w-[68ch] text-muted">
          Documentos del proyecto organizados por tipo y por objetivo específico. Cada documento se enlaza en el grafo de
          conocimiento con el objetivo que respalda.
        </p>
      </header>

      <div className="flex flex-wrap gap-3">
        <input className="field max-w-xs" placeholder="Buscar en título o descripción" aria-label="Buscar documento"
          value={search} onChange={(e) => setSearch(e.target.value)} />
        <select className="field w-auto" aria-label="Tipo" value={category} onChange={(e) => setCategory(e.target.value)}>
          <option value="">Todos los tipos</option>
          {Object.entries(CATEGORIES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
        <select className="field w-auto" aria-label="Objetivo" value={objective} onChange={(e) => setObjective(e.target.value)}>
          <option value="">Todos los objetivos</option>
          {OBJECTIVES.map((o) => <option key={o}>{o}</option>)}
        </select>
      </div>

      <div className="grid gap-8 lg:grid-cols-[1fr_340px]">
        <div>
          {docs.error && <ErrorNote message={docs.error} />}
          {docs.loading && <Loading what="documentos" />}
          {docs.data?.length === 0 && (
            <p className="panel p-6 text-muted">
              No hay documentos con estos filtros.{isAdmin ? " Cargue uno con el formulario." : ""}
            </p>
          )}
          <ul className="divide-y divide-grid border-y border-grid">
            {docs.data?.map((d) => (
              <li key={d.id} className="grid gap-x-6 gap-y-1 py-4 sm:grid-cols-[7rem_1fr_auto]">
                <div className="flex gap-2 text-xs sm:flex-col sm:gap-1">
                  <span className="font-semibold text-river">{CATEGORIES[d.category] ?? d.category}</span>
                  {d.objective && <span className="type-condensed text-muted">{d.objective}</span>}
                </div>
                <div className="min-w-0">
                  <p className="font-semibold">{d.title}</p>
                  {d.description && <p className="max-w-[70ch] text-sm text-muted">{d.description}</p>}
                  {d.authors && <p className="mt-1 text-xs text-muted">{d.authors}</p>}
                </div>
                <div className="flex items-start gap-2">
                  {d.filePath && (
                    <a className="btn-quiet" href={api.fileUrl(d.filePath)} target="_blank" rel="noreferrer">
                      Abrir
                    </a>
                  )}
                  {isAdmin && (
                    <button
                      className="btn-quiet hover:border-rust hover:text-rust"
                      onClick={async () => {
                        if (!confirm(`¿Eliminar "${d.title}"? Se borra también el archivo.`)) return;
                        await api.deleteDocument(d.id);
                        docs.reload();
                      }}
                    >
                      Eliminar
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </div>

        {isAdmin ? (
          <UploadForm onUploaded={docs.reload} />
        ) : (
          <p className="text-sm text-muted">Para cargar documentos, ingrese como administrador desde la barra superior.</p>
        )}
      </div>
    </div>
  );
}

function UploadForm({ onUploaded }: { onUploaded: () => void }) {
  const form = useRef<HTMLFormElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  return (
    <form
      ref={form}
      className="panel space-y-3 self-start p-5"
      onSubmit={async (e) => {
        e.preventDefault();
        setSaving(true);
        setError(null);
        try {
          await api.createDocument(new FormData(e.currentTarget));
          form.current?.reset();
          onUploaded();
        } catch (err) {
          setError((err as Error).message);
        } finally {
          setSaving(false);
        }
      }}
    >
      <h2 className="font-semibold">Cargar documento</h2>
      <label className="block text-sm">
        Título
        <input name="title" required className="field mt-1" />
      </label>
      <div className="grid grid-cols-2 gap-3">
        <label className="block text-sm">
          Tipo
          <select name="category" required className="field mt-1">
            {Object.entries(CATEGORIES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </select>
        </label>
        <label className="block text-sm">
          Objetivo
          <select name="objective" className="field mt-1">
            <option value="">Ninguno</option>
            {OBJECTIVES.map((o) => <option key={o}>{o}</option>)}
          </select>
        </label>
      </div>
      <label className="block text-sm">
        Autores
        <input name="authors" className="field mt-1" />
      </label>
      <label className="block text-sm">
        Descripción
        <textarea name="description" rows={3} className="field mt-1" />
      </label>
      <label className="block text-sm">
        Archivo
        <input name="file" type="file" className="mt-1 block w-full text-sm" />
      </label>
      {error && <p role="alert" className="text-sm text-rust">{error}</p>}
      <button className="btn-primary w-full justify-center" disabled={saving}>
        {saving ? "Cargando…" : "Cargar documento"}
      </button>
    </form>
  );
}

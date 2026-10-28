import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { api, type GraphNode } from "../../shared/api";
import { useApi } from "../../shared/useApi";
import { ErrorNote, Loading } from "../../shared/Status";
import { computeLayout } from "./graphLayout";

const KINDS: Record<string, { label: string; color: string }> = {
  Project: { label: "Proyecto", color: "#14232E" },
  Objective: { label: "Objetivos", color: "#1F6F8B" },
  Activity: { label: "Actividades", color: "#6FA8BA" },
  Method: { label: "Métodos", color: "#6B5B95" },
  Variable: { label: "Variables", color: "#2E8A6B" },
  Dataset: { label: "Conjuntos de datos", color: "#A8742A" },
  Station: { label: "Estaciones", color: "#A9BEC4" },
  Document: { label: "Documentos", color: "#A3412F" },
  Researcher: { label: "Investigadores", color: "#7A6A58" },
  Institution: { label: "Instituciones", color: "#4A5560" },
  SDG: { label: "ODS", color: "#8AA33C" },
};
const LABELED = new Set(["Project", "Objective", "Method", "Variable", "Dataset", "Institution"]);
const W = 1100;
const H = 720;

const relationLabel = (r: string) => r.toLowerCase().replace(/_/g, " ");

export function GraphPage() {
  const graph = useApi(api.graph, []);
  const [hidden, setHidden] = useState<Set<string>>(new Set(["Station"]));
  const [selected, setSelected] = useState<string>("project:caudales");
  const [hover, setHover] = useState<string | null>(null);
  const detail = useApi(() => api.neighborhood(selected), [selected]);

  const view = useMemo(() => {
    if (!graph.data) return null;
    const nodes = graph.data.nodes.filter((n) => !hidden.has(n.kind));
    const ids = new Set(nodes.map((n) => n.id));
    const edges = graph.data.edges.filter((e) => ids.has(e.sourceId) && ids.has(e.targetId));
    const degree = new Map<string, number>();
    edges.forEach((e) => [e.sourceId, e.targetId].forEach((id) => degree.set(id, (degree.get(id) ?? 0) + 1)));
    return { nodes, edges, degree, pos: computeLayout(nodes, edges, W, H) };
  }, [graph.data, hidden]);

  const counts = useMemo(() => {
    const c = new Map<string, number>();
    graph.data?.nodes.forEach((n) => c.set(n.kind, (c.get(n.kind) ?? 0) + 1));
    return c;
  }, [graph.data]);

  const focus = hover ?? selected;
  const adjacent = useMemo(() => {
    const s = new Set<string>([focus]);
    view?.edges.forEach((e) => {
      if (e.sourceId === focus) s.add(e.targetId);
      if (e.targetId === focus) s.add(e.sourceId);
    });
    return s;
  }, [view, focus]);

  const toggle = (kind: string) =>
    setHidden((h) => {
      const n = new Set(h);
      if (n.has(kind)) n.delete(kind);
      else n.add(kind);
      return n;
    });

  return (
    <div className="space-y-6">
      <header className="max-w-3xl space-y-3">
        <h1 className="type-display text-4xl">Grafo de conocimiento</h1>
        <p className="max-w-[68ch] text-muted">
          Relaciones entre los objetivos del proyecto, sus actividades y métodos, los datos del INAMHI, los documentos del
          repositorio y el equipo. El grafo se guarda en la base de datos y se regenera cada vez que cambia el repositorio.
        </p>
      </header>

      <div className="flex flex-wrap gap-2" role="group" aria-label="Tipos de nodo visibles">
        {Object.entries(KINDS).map(([k, v]) =>
          counts.get(k) ? (
            <button
              key={k}
              aria-pressed={!hidden.has(k)}
              onClick={() => toggle(k)}
              className={`flex items-center gap-2 rounded-sm border px-2.5 py-1 text-sm ${
                hidden.has(k) ? "border-grid text-muted" : "border-ink/30 bg-sheet"
              }`}
            >
              <span className="h-2.5 w-2.5 rounded-full" style={{ background: hidden.has(k) ? "transparent" : v.color, boxShadow: `inset 0 0 0 1.5px ${v.color}` }} />
              {v.label} <span className="num text-muted">{counts.get(k)}</span>
            </button>
          ) : null
        )}
      </div>

      {graph.error && <ErrorNote message={graph.error} />}
      {graph.loading && <Loading what="el grafo" />}

      {view && (
        <div className="grid gap-6 xl:grid-cols-[1fr_340px]">
          <svg viewBox={`0 0 ${W} ${H}`} className="panel w-full" role="img" aria-label={`Grafo con ${view.nodes.length} nodos`}>
            {view.edges.map((e) => {
              const a = view.pos.get(e.sourceId)!;
              const b = view.pos.get(e.targetId)!;
              const on = adjacent.has(e.sourceId) && adjacent.has(e.targetId) && (e.sourceId === focus || e.targetId === focus);
              return (
                <line key={e.id} x1={a.x} y1={a.y} x2={b.x} y2={b.y} stroke={on ? "#14232E" : "#D3DCDB"} strokeWidth={on ? 1.4 : 0.8} />
              );
            })}
            {view.nodes.map((n) => {
              const p = view.pos.get(n.id)!;
              const r = 4 + Math.min(10, Math.sqrt(view.degree.get(n.id) ?? 0) * 1.6);
              const dim = !adjacent.has(n.id);
              const showLabel = LABELED.has(n.kind) || adjacent.has(n.id);
              return (
                <g
                  key={n.id}
                  transform={`translate(${p.x},${p.y})`}
                  opacity={dim ? 0.5 : 1}
                  className="cursor-pointer"
                  onMouseEnter={() => setHover(n.id)}
                  onMouseLeave={() => setHover(null)}
                  onClick={() => setSelected(n.id)}
                >
                  <circle r={r} fill={KINDS[n.kind]?.color ?? "#5B6B72"} stroke={n.id === selected ? "#A8742A" : "#fff"} strokeWidth={n.id === selected ? 3 : 1.2} />
                  {showLabel && (
                    <text y={-r - 4} textAnchor="middle" fontSize={11} fill="#14232E" paintOrder="stroke" stroke="#fff" strokeWidth={3}>
                      {n.label.length > 34 ? `${n.label.slice(0, 33)}…` : n.label}
                    </text>
                  )}
                </g>
              );
            })}
          </svg>

          <aside className="panel space-y-4 self-start p-5" aria-live="polite">
            {detail.data ? <NodeDetail node={detail.data.node} detail={detail.data} onSelect={setSelected} /> : <Loading what="el nodo" />}
          </aside>
        </div>
      )}
    </div>
  );
}

function NodeDetail({
  node,
  detail,
  onSelect,
}: {
  node: GraphNode;
  detail: { neighbors: GraphNode[]; edges: { id: number; sourceId: string; targetId: string; relation: string }[] };
  onSelect: (id: string) => void;
}) {
  const byId = new Map(detail.neighbors.map((n) => [n.id, n]));
  return (
    <>
      <div className="space-y-1">
        <p className="flex items-center gap-2 text-xs text-muted">
          <span className="h-2.5 w-2.5 rounded-full" style={{ background: KINDS[node.kind]?.color }} />
          {KINDS[node.kind]?.label ?? node.kind}
        </p>
        <h2 className="text-lg font-semibold leading-snug">{node.label}</h2>
        {node.description && <p className="text-sm text-muted">{node.description}</p>}
        {node.kind === "Station" && (
          <Link className="text-sm font-medium text-river underline underline-offset-4" to={`/estaciones/${node.id.slice(8)}`}>
            Ver la serie de la estación
          </Link>
        )}
      </div>
      <div>
        <h3 className="mb-1 text-sm font-semibold">{detail.edges.length} relaciones</h3>
        <ul className="max-h-[440px] divide-y divide-grid overflow-y-auto text-sm">
          {detail.edges.map((e) => {
            const out = e.sourceId === node.id;
            const other = byId.get(out ? e.targetId : e.sourceId);
            if (!other) return null;
            return (
              <li key={e.id}>
                <button className="w-full py-1.5 text-left hover:text-river" onClick={() => onSelect(other.id)}>
                  <span className="text-xs text-muted">{out ? relationLabel(e.relation) : `${relationLabel(e.relation)} (entrante)`}</span>
                  <span className="block">{other.label}</span>
                </button>
              </li>
            );
          })}
        </ul>
      </div>
    </>
  );
}

import { Link } from "react-router-dom";
import { api, type GraphNode } from "../../shared/api";
import { useApi } from "../../shared/useApi";
import { ErrorNote, Loading } from "../../shared/Status";

const PROJECT = "project:caudales";
const ROLE_ORDER = ["DIRIGE", "CODIRIGE", "COLABORA"];

export function ProjectPage() {
  const graph = useApi(api.graph, []);
  if (graph.error) return <ErrorNote message={graph.error} />;
  if (!graph.data) return <Loading what="la información del proyecto" />;

  const { nodes, edges } = graph.data;
  const byId = new Map(nodes.map((n) => [n.id, n]));
  const targets = (source: string, relation: string) =>
    edges
      .filter((e) => e.sourceId === source && e.relation === relation)
      .map((e) => byId.get(e.targetId))
      .filter((n): n is GraphNode => !!n);

  const project = byId.get(PROJECT);
  const objectives = targets(PROJECT, "TIENE_OBJETIVO").sort((a, b) => a.id.localeCompare(b.id));
  const lines = targets(PROJECT, "EN_LINEA");
  const sdgs = targets(PROJECT, "CONTRIBUYE_A");
  const team = edges
    .filter((e) => e.targetId === PROJECT && ROLE_ORDER.includes(e.relation))
    .sort((a, b) => ROLE_ORDER.indexOf(a.relation) - ROLE_ORDER.indexOf(b.relation))
    .map((e) => ({ person: byId.get(e.sourceId)!, institution: targets(e.sourceId, "PERTENECE_A")[0] }));

  return (
    <div className="space-y-14">
      <section className="max-w-4xl space-y-4">
        <p className="text-sm font-medium uppercase tracking-wide text-river">Proyecto semilla · Investigación aplicada</p>
        <h1 className="type-display text-4xl sm:text-5xl">{project?.label}</h1>
        <p className="max-w-[68ch] text-lg text-muted">{project?.description}</p>
      </section>

      <section className="space-y-4" aria-labelledby="lines-title">
        <h2 id="lines-title" className="text-xl font-semibold">Líneas de investigación</h2>
        <ul className="grid gap-px border border-grid bg-grid sm:grid-cols-2">
          {lines.map((line) => (
            <li key={line.id} className="bg-sheet p-5">
              <p className="type-condensed text-sm font-bold text-river">{line.description}</p>
              <p className="mt-1 font-semibold">{line.label}</p>
              <p className="mt-1 text-sm text-muted">{targets(line.id, "DEFINIDA_POR")[0]?.label}</p>
            </li>
          ))}
        </ul>
      </section>

      <section className="space-y-4" aria-labelledby="team-title">
        <h2 id="team-title" className="text-xl font-semibold">Equipo</h2>
        <ul className="divide-y divide-grid border-y border-grid">
          {team.map(({ person, institution }) => (
            <li key={person.id} className="grid gap-1 py-4 sm:grid-cols-[1fr_1.2fr_1fr] sm:gap-6">
              <span className="font-semibold">{person.label}</span>
              <span className="text-muted">{person.description}</span>
              <span className="text-sm text-muted">{institution?.label}</span>
            </li>
          ))}
        </ul>
      </section>

      <section className="grid gap-10 border-t border-grid pt-10 lg:grid-cols-[1.4fr_1fr]">
        <div className="space-y-3">
          <h2 className="text-xl font-semibold">Objetivos específicos</h2>
          <ol className="divide-y divide-grid border-y border-grid">
            {objectives.map((obj) => {
              const [code, title] = obj.label.split(": ");
              return (
                <li key={obj.id} className="grid grid-cols-[3.5rem_1fr] gap-3 py-4">
                  <span className="type-condensed text-lg font-bold text-river">{code}</span>
                  <div>
                    <p className="font-semibold">{title}</p>
                    <p className="text-sm text-muted">{obj.description}</p>
                  </div>
                </li>
              );
            })}
          </ol>
        </div>
        <div className="space-y-3">
          <h2 className="text-xl font-semibold">Objetivos de Desarrollo Sostenible</h2>
          <ul className="space-y-2">
            {sdgs.map((s) => (
              <li key={s.id} className="panel px-4 py-3 text-sm">{s.label}</li>
            ))}
          </ul>
          <Link to="/grafo" className="inline-block text-sm font-medium text-river underline underline-offset-4">
            Ver estas relaciones en el grafo de conocimiento
          </Link>
        </div>
      </section>
    </div>
  );
}

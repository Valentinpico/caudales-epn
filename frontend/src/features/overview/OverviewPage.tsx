import { useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api, type Variable } from "../../shared/api";
import { formatInt, variableLabel } from "../../shared/format";
import { useApi } from "../../shared/useApi";
import { ErrorNote, Loading } from "../../shared/Status";
import { VariableToggle } from "../../shared/VariableToggle";
import { CoverageCarpet } from "./CoverageCarpet";
import { projectProfile } from "./projectProfile";
import { Objectives, ResearchLines, Team } from "./ProjectSections";

export function OverviewPage() {
  const [variable, setVariable] = useState<Variable>("precipitation");
  const navigate = useNavigate();
  const overview = useApi(api.overview, []);
  const graph = useApi(api.graph, []);
  const stations = useApi(() => api.stations(variable), [variable]);
  const coverage = useApi(() => api.coverage(variable), [variable]);

  const o = overview.data;
  const firstYear = o?.first ? +o.first.slice(0, 4) : 1960;
  const lastYear = o?.last ? +o.last.slice(0, 4) : 2023;
  const profile = useMemo(() => graph.data && projectProfile(graph.data.nodes, graph.data.edges), [graph.data]);

  return (
    <div className="space-y-16">
      <section>
        <div className="max-w-4xl space-y-5">
          <p className="type-condensed text-sm font-bold uppercase tracking-wider text-river">
            Proyecto semilla · EPN, Departamento de Matemática
          </p>
          <h1 className="type-display text-4xl sm:text-5xl">
            Regionalización de caudales en Ecuador mediante análisis de datos funcionales
          </h1>
          <p className="max-w-[64ch] text-lg text-muted">
            Plataforma del proyecto: series hidrometeorológicas del INAMHI depuradas y trazables, repositorio de
            documentos y grafo de conocimiento que enlaza objetivos, métodos, datos y equipo.
          </p>
          <div className="flex flex-wrap gap-2">
            <Link to="/estaciones" className="btn-primary">
              Explorar estaciones
            </Link>
            <a href="#equipo" className="btn-quiet">
              Conocer al equipo
            </a>
          </div>
        </div>
        {graph.error && <ErrorNote message={graph.error} />}
      </section>

      {o && (
        <section aria-label="Cifras de la plataforma" className="grid grid-cols-2 gap-px bg-deep/20 sm:grid-cols-3 lg:grid-cols-6">
          {[
            [o.stations, "estaciones"],
            [`${firstYear}–${lastYear}`, "período de registro"],
            [formatInt(o.observations), "registros diarios"],
            [formatInt(o.issues), "hallazgos de calidad"],
            [profile?.lines.length ?? "–", "líneas de investigación"],
            [profile?.team.length ?? "–", "investigadores"],
          ].map(([value, label]) => (
            <div key={label} className="bg-deep px-5 py-6 text-white">
              <p className="type-display text-3xl num">{value}</p>
              <p className="mt-1 text-sm text-white/75">{label}</p>
            </div>
          ))}
        </section>
      )}
      {overview.error && <ErrorNote message={overview.error} />}

      {profile ? (
        <>
          <ResearchLines lines={profile.lines} />
          <Team team={profile.team} />
        </>
      ) : (
        !graph.error && <Loading what="la información del proyecto" />
      )}

      <section className="space-y-4 border-t border-grid pt-10" aria-labelledby="coverage-title">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 id="coverage-title" className="text-xl font-semibold">
            Cobertura de {variableLabel[variable].toLowerCase()} por estación y año
          </h2>
          <VariableToggle value={variable} onChange={setVariable} />
        </div>
        {coverage.error && <ErrorNote message={coverage.error} />}
        {stations.data && coverage.data ? (
          <CoverageCarpet
            coverage={coverage.data}
            stations={stations.data}
            firstYear={firstYear}
            lastYear={lastYear}
            onSelect={(code) => navigate(`/estaciones/${code}?variable=${variable}`)}
          />
        ) : (
          !coverage.error && <Loading what="la matriz de cobertura" />
        )}
      </section>

      {profile && <Objectives objectives={profile.objectives} sdgs={profile.sdgs} />}

      {o && (
        <section className="grid gap-px overflow-hidden border border-grid bg-grid sm:grid-cols-3">
          {[
            { to: "/estaciones", title: "Estaciones", text: `${o.stations} estaciones con series diarias, completitud y exportación CSV.` },
            { to: "/repositorio", title: "Repositorio", text: `${o.documents} documentos del proyecto: propuesta, datos, protocolos e informes.` },
            { to: "/grafo", title: "Grafo de conocimiento", text: `${o.graphNodes} nodos y ${o.graphEdges} relaciones entre objetivos, métodos, datos y equipo.` },
          ].map((c) => (
            <Link key={c.to} to={c.to} className="group bg-sheet p-5 hover:bg-paper">
              <p className="font-semibold group-hover:text-river">{c.title}</p>
              <p className="mt-1 text-sm text-muted num">{c.text}</p>
            </Link>
          ))}
        </section>
      )}
    </div>
  );
}

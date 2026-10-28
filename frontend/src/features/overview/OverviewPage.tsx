import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api, type Variable } from "../../shared/api";
import { formatInt, variableLabel } from "../../shared/format";
import { useApi } from "../../shared/useApi";
import { ErrorNote, Loading } from "../../shared/Status";
import { VariableToggle } from "../../shared/VariableToggle";
import { CoverageCarpet } from "./CoverageCarpet";

export function OverviewPage() {
  const [variable, setVariable] = useState<Variable>("precipitation");
  const navigate = useNavigate();
  const overview = useApi(api.overview, []);
  const stations = useApi(() => api.stations(variable), [variable]);
  const coverage = useApi(() => api.coverage(variable), [variable]);
  const project = useApi(() => api.neighborhood("project:caudales"), []);

  const o = overview.data;
  const firstYear = o?.first ? +o.first.slice(0, 4) : 1960;
  const lastYear = o?.last ? +o.last.slice(0, 4) : 2023;
  const objectives = (project.data?.neighbors ?? [])
    .filter((n) => n.kind === "Objective")
    .sort((a, b) => a.id.localeCompare(b.id));

  return (
    <div className="space-y-14">
      <section className="max-w-4xl space-y-4">
        <h1 className="type-display text-4xl sm:text-5xl">
          Series hidrometeorológicas de la red INAMHI, {firstYear} a {lastYear}
        </h1>
        {overview.error && <ErrorNote message={overview.error} />}
        {o && (
          <p className="max-w-[68ch] text-lg text-muted num">
            {formatInt(o.observations)} registros diarios de precipitación y temperatura de {o.stations} estaciones,
            depurados con {formatInt(o.issues)} hallazgos de control de calidad. Son el insumo del proyecto de
            regionalización de caudales mediante análisis de datos funcionales.
          </p>
        )}
      </section>

      <section className="space-y-4" aria-labelledby="coverage-title">
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

      <section className="grid gap-10 border-t border-grid pt-10 lg:grid-cols-[1fr_1.4fr]">
        <div className="space-y-3">
          <h2 className="text-xl font-semibold">Objetivos del proyecto</h2>
          <p className="max-w-[52ch] text-muted">
            Metodología para la regionalización de caudales en Ecuador basada en el análisis de datos funcionales y la
            homogeneización de variables hidrometeorológicas. Cada objetivo está enlazado en el grafo con sus
            actividades, métodos y documentos.
          </p>
          <Link to="/grafo" className="inline-block text-sm font-medium text-river underline underline-offset-4">
            Explorar el grafo de conocimiento
          </Link>
        </div>
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
      </section>

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

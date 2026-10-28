import { useState } from "react";
import { Link } from "react-router-dom";
import { api, type Variable } from "../../shared/api";
import { formatInt, variableLabel } from "../../shared/format";
import { useApi } from "../../shared/useApi";
import { ErrorNote, Loading } from "../../shared/Status";
import { issueKindLabel, issueKindRule } from "./issueKinds";

export function QualityPage() {
  const summary = useApi(api.qualitySummary, []);
  const [filter, setFilter] = useState<{ variable?: Variable; kind?: string }>({});
  const issues = useApi(() => api.issues(filter), [filter.variable, filter.kind]);

  const kinds = Object.keys(issueKindLabel);
  const count = (v: Variable, k: string) => summary.data?.find((s) => s.variable === v && s.kind === k)?.count ?? 0;

  return (
    <div className="space-y-10">
      <header className="max-w-3xl space-y-3">
        <h1 className="type-display text-4xl">Calidad de datos</h1>
        <p className="max-w-[68ch] text-muted">
          Al cargar los archivos crudos del INAMHI, cada fila pasa por las reglas siguientes. Ningún valor se corrige en
          silencio: todo lo descartado o marcado queda registrado con la línea del archivo original, para que la
          homogeneización y la imputación posteriores partan de una base trazable.
        </p>
      </header>

      {summary.error && <ErrorNote message={summary.error} />}
      {summary.loading && <Loading what="el resumen" />}
      {summary.data && (
        <div className="panel overflow-x-auto">
          <table className="num w-full min-w-[640px] text-left text-sm">
            <thead className="border-b border-grid text-xs text-muted">
              <tr>
                <th className="px-4 py-2 font-normal">Regla</th>
                <th className="px-4 py-2 font-normal">Qué se verifica y qué se hace</th>
                <th className="px-4 py-2 text-right font-normal">Precipitación</th>
                <th className="px-4 py-2 text-right font-normal">Temperatura</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-grid">
              {kinds.map((k) => (
                <tr key={k}>
                  <td className="whitespace-nowrap px-4 py-3 font-semibold">{issueKindLabel[k]}</td>
                  <td className="max-w-[60ch] px-4 py-3 text-muted">{issueKindRule[k]}</td>
                  {(["precipitation", "temperature"] as Variable[]).map((v) => (
                    <td key={v} className="px-4 py-3 text-right">
                      <button
                        className="underline-offset-4 hover:text-river hover:underline disabled:text-muted disabled:no-underline"
                        disabled={count(v, k) === 0}
                        onClick={() => setFilter({ variable: v, kind: k })}
                      >
                        {formatInt(count(v, k))}
                      </button>
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <section className="space-y-3">
        <div className="flex flex-wrap items-center gap-3">
          <h2 className="text-xl font-semibold">Hallazgos</h2>
          <select
            className="field w-auto"
            aria-label="Variable"
            value={filter.variable ?? ""}
            onChange={(e) => setFilter({ ...filter, variable: (e.target.value || undefined) as Variable | undefined })}
          >
            <option value="">Todas las variables</option>
            <option value="precipitation">{variableLabel.precipitation}</option>
            <option value="temperature">{variableLabel.temperature}</option>
          </select>
          <select
            className="field w-auto"
            aria-label="Regla"
            value={filter.kind ?? ""}
            onChange={(e) => setFilter({ ...filter, kind: e.target.value || undefined })}
          >
            <option value="">Todas las reglas</option>
            {kinds.map((k) => (
              <option key={k} value={k}>
                {issueKindLabel[k]}
              </option>
            ))}
          </select>
          <span className="text-xs text-muted">Se muestran hasta 1 000 resultados.</span>
        </div>
        {issues.error && <ErrorNote message={issues.error} />}
        {issues.data && (
          <div className="panel max-h-[60vh] overflow-auto">
            <table className="num w-full min-w-[640px] text-left text-sm">
              <thead className="sticky top-0 bg-sheet text-xs text-muted">
                <tr>
                  <th className="px-4 py-2 font-normal">Estación</th>
                  <th className="px-4 py-2 font-normal">Variable</th>
                  <th className="px-4 py-2 font-normal">Fecha</th>
                  <th className="px-4 py-2 font-normal">Regla</th>
                  <th className="px-4 py-2 font-normal">Detalle</th>
                  <th className="px-4 py-2 text-right font-normal">Línea del archivo</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-grid">
                {issues.data.map((i) => (
                  <tr key={i.id}>
                    <td className="px-4 py-1.5">
                      <Link className="type-condensed font-semibold text-river hover:underline" to={`/estaciones/${i.stationCode}?variable=${i.variable}`}>
                        {i.stationCode}
                      </Link>
                    </td>
                    <td className="px-4">{variableLabel[i.variable]}</td>
                    <td className="px-4">
                      {i.year}-{String(i.month).padStart(2, "0")}
                      {i.day ? `-${String(i.day).padStart(2, "0")}` : ""}
                    </td>
                    <td className="px-4">{issueKindLabel[i.kind] ?? i.kind}</td>
                    <td className="px-4 text-muted">{i.detail}</td>
                    <td className="px-4 text-right text-muted">{i.sourceLine}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

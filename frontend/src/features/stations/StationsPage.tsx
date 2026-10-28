import { useMemo, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { api, type StationSummary, type Variable } from "../../shared/api";
import { formatDec, formatInt, formatPct, variableLabel, year } from "../../shared/format";
import { useApi } from "../../shared/useApi";
import { ErrorNote, Loading } from "../../shared/Status";
import { VariableToggle } from "../../shared/VariableToggle";
import { SeriesChart } from "./SeriesChart";
import { CompletenessGrid } from "./CompletenessGrid";
import { issueKindLabel } from "../quality/issueKinds";

export function StationsPage() {
  const { code } = useParams();
  const [params, setParams] = useSearchParams();
  const variable = (params.get("variable") as Variable) ?? "precipitation";
  const [search, setSearch] = useState("");
  const all = useApi(() => api.stations(), []);

  const list = useMemo(() => {
    const s = search.trim().toLowerCase();
    return (all.data ?? [])
      .filter((x) => x.variable === variable)
      .filter((x) => !s || x.code.toLowerCase().includes(s) || x.name.toLowerCase().includes(s));
  }, [all.data, variable, search]);

  const selected = all.data?.filter((s) => s.code === code) ?? [];

  return (
    <div className="grid gap-8 lg:grid-cols-[360px_1fr]">
      <aside className="space-y-3 lg:sticky lg:top-4 lg:self-start">
        <h1 className="type-display text-3xl">Estaciones</h1>
        <VariableToggle value={variable} onChange={(v) => setParams({ variable: v })} />
        <input
          className="field"
          placeholder="Buscar por código o nombre"
          aria-label="Buscar estación"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        {all.error && <ErrorNote message={all.error} />}
        {all.loading && <Loading what="estaciones" />}
        {all.data && (
          <p className="text-xs text-muted">
            {list.length} estaciones con {variableLabel[variable].toLowerCase()}. La barra indica la completitud dentro del periodo de registro.
          </p>
        )}
        <ul className="panel max-h-[70vh] divide-y divide-grid overflow-y-auto">
          {list.map((s) => (
            <li key={s.code}>
              <Link
                to={`/estaciones/${s.code}?variable=${variable}`}
                aria-current={s.code === code ? "page" : undefined}
                className={`grid grid-cols-[4.2rem_1fr] gap-x-2 px-3 py-2 text-sm hover:bg-paper ${s.code === code ? "bg-paper" : ""}`}
              >
                <span className="type-condensed font-semibold">{s.code}</span>
                <span className="truncate">{s.name}</span>
                <span className="num text-xs text-muted">
                  {year(s.first)}–{year(s.last)}
                </span>
                <span className="flex items-center gap-2">
                  <span className="h-1.5 flex-1 bg-grid">
                    <span className="block h-full bg-river" style={{ width: `${s.completeness * 100}%` }} />
                  </span>
                  <span className="num w-12 text-right text-xs text-muted">{formatPct(s.completeness)}</span>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </aside>

      <section>
        {code ? (
          selected.length > 0 ? (
            <StationDetail key={`${code}-${variable}`} summaries={selected} variable={variable} />
          ) : (
            all.data && <ErrorNote message={`La estación ${code} no existe`} />
          )
        ) : (
          <div className="panel p-8 text-muted">
            <p className="text-lg text-ink">Seleccione una estación de la lista.</p>
            <p className="mt-1 max-w-[60ch] text-sm">
              Verá su serie mensual o diaria, la completitud mes a mes, los hallazgos de control de calidad y podrá
              descargar los datos en CSV.
            </p>
          </div>
        )}
      </section>
    </div>
  );
}

function StationDetail({ summaries, variable }: { summaries: StationSummary[]; variable: Variable }) {
  const [, setParams] = useSearchParams();
  const navigate = useNavigate();
  const [monthly, setMonthly] = useState(true);
  const s = summaries.find((x) => x.variable === variable);
  const available = summaries.map((x) => x.variable);

  const series = useApi(() => (s ? api.series(s.code, variable, monthly ? "monthly" : "daily") : Promise.resolve([])), [monthly]);
  const months = useApi(() => (s ? api.completeness(s.code, variable) : Promise.resolve([])), []);
  const issues = useApi(() => (s ? api.issues({ station: s.code, variable }) : Promise.resolve([])), []);
  const station = summaries[0];

  return (
    <article className="space-y-8">
      <header className="space-y-3">
        <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
          <h2 className="type-display text-4xl">{station.name}</h2>
          <span className="type-condensed text-2xl font-semibold text-river">{station.code}</span>
        </div>
        <p className="text-sm text-muted">
          {station.latitude !== null
            ? `Lat. ${formatDec(station.latitude)}, long. ${formatDec(station.longitude!)}, ${formatInt(station.altitude ?? 0)} m s. n. m.${station.province ? `, ${station.province}` : ""}`
            : "Coordenadas pendientes: los archivos crudos del INAMHI solo incluyen código y nombre de la estación."}
        </p>
        <VariableToggle
          value={variable}
          options={available}
          onChange={(v) => {
            setParams({ variable: v });
            navigate(`/estaciones/${station.code}?variable=${v}`, { replace: true });
          }}
        />
      </header>

      {!s ? (
        <p className="text-muted">Esta estación no registra {variableLabel[variable].toLowerCase()}.</p>
      ) : (
        <>
          <dl className="grid grid-cols-2 gap-px overflow-hidden border border-grid bg-grid text-sm sm:grid-cols-4">
            {[
              ["Periodo", `${s.first} a ${s.last}`],
              ["Días con dato", formatInt(s.observations)],
              ["Completitud", formatPct(s.completeness)],
              ["Hallazgos de calidad", `${formatInt(s.issues)} (${formatInt(s.suspect)} sospechosos)`],
            ].map(([k, v]) => (
              <div key={k} className="bg-sheet px-4 py-3">
                <dt className="text-xs text-muted">{k}</dt>
                <dd className="num mt-0.5 font-semibold">{v}</dd>
              </div>
            ))}
          </dl>

          <div className="panel space-y-3 p-5">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h3 className="font-semibold">Serie de {variableLabel[variable].toLowerCase()}</h3>
              <div className="flex gap-2">
                <div role="radiogroup" aria-label="Resolución" className="inline-flex rounded-sm border border-grid p-0.5 text-sm">
                  {[
                    [true, "Mensual"],
                    [false, "Diaria"],
                  ].map(([m, label]) => (
                    <button
                      key={String(m)}
                      role="radio"
                      aria-checked={monthly === m}
                      onClick={() => setMonthly(m as boolean)}
                      className={`rounded-sm px-3 py-1 ${monthly === m ? "bg-ink text-white" : "text-muted hover:text-ink"}`}
                    >
                      {label as string}
                    </button>
                  ))}
                </div>
                <a className="btn-quiet" href={api.exportUrl(s.code, variable)} download>
                  Descargar CSV
                </a>
              </div>
            </div>
            {series.error && <ErrorNote message={series.error} />}
            {series.data ? <SeriesChart points={series.data} variable={variable} monthly={monthly} /> : <Loading what="la serie" />}
          </div>

          <div className="grid gap-6 xl:grid-cols-[auto_1fr]">
            <div className="panel space-y-3 p-5">
              <h3 className="font-semibold">Completitud mensual</h3>
              <p className="max-w-[40ch] text-xs text-muted">Tono más oscuro, más días con dato en el mes. Gris: mes sin registros.</p>
              {months.data ? (
                <CompletenessGrid months={months.data} firstYear={+year(s.first)} lastYear={+year(s.last)} />
              ) : (
                <Loading what="la completitud" />
              )}
            </div>
            <div className="panel space-y-3 p-5">
              <h3 className="font-semibold">Hallazgos de control de calidad</h3>
              {issues.data && issues.data.length === 0 && (
                <p className="text-sm text-muted">Sin hallazgos para esta estación y variable.</p>
              )}
              {issues.data && issues.data.length > 0 && (
                <div className="max-h-[520px] overflow-y-auto">
                <table className="num w-full text-left text-sm">
                  <thead className="sticky top-0 bg-sheet text-xs text-muted">
                    <tr>
                      <th className="py-1 font-normal">Fecha</th>
                      <th className="font-normal">Tipo</th>
                      <th className="font-normal">Detalle</th>
                      <th className="text-right font-normal">Línea</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-grid">
                    {issues.data.map((i) => (
                      <tr key={i.id}>
                        <td className="whitespace-nowrap py-1.5 pr-3">
                          {i.year}-{String(i.month).padStart(2, "0")}
                          {i.day ? `-${String(i.day).padStart(2, "0")}` : ""}
                        </td>
                        <td className="whitespace-nowrap pr-3">{issueKindLabel[i.kind] ?? i.kind}</td>
                        <td className="pr-3 text-muted">{i.detail}</td>
                        <td className="text-right text-muted">{i.sourceLine}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                </div>
              )}
            </div>
          </div>
        </>
      )}
    </article>
  );
}

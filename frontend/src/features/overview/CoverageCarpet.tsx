import { useMemo, useState } from "react";
import type { StationSummary, YearCoverage } from "../../shared/api";
import { coverageColor, coverageLegend, daysInYear, formatPct } from "../../shared/format";

interface Props {
  coverage: YearCoverage[];
  stations: StationSummary[];
  firstYear: number;
  lastYear: number;
  onSelect: (code: string) => void;
}

const CELL_W = 10;
const CELL_H = 6;

/**
 * Network coverage matrix: one row per station (ordered by start of record),
 * one column per year, tone = share of days with data.
 */
export function CoverageCarpet({ coverage, stations, firstYear, lastYear, onSelect }: Props) {
  const [hover, setHover] = useState<{ code: string; year: number } | null>(null);

  const { rows, days } = useMemo(() => {
    const days = new Map(coverage.map((c) => [`${c.code}|${c.year}`, c.days]));
    const rows = [...stations].sort((a, b) => a.first.localeCompare(b.first) || a.code.localeCompare(b.code));
    return { rows, days };
  }, [coverage, stations]);

  const years = lastYear - firstYear + 1;
  const width = years * CELL_W;
  const height = rows.length * CELL_H;

  const locate = (e: React.MouseEvent<SVGSVGElement>) => {
    const box = e.currentTarget.getBoundingClientRect();
    const col = Math.floor(((e.clientX - box.left) / box.width) * years);
    const row = Math.floor(((e.clientY - box.top) / box.height) * rows.length);
    const station = rows[row];
    return station && col >= 0 && col < years ? { code: station.code, year: firstYear + col } : null;
  };

  const hovered = hover && rows.find((r) => r.code === hover.code);
  const hoveredDays = hover ? days.get(`${hover.code}|${hover.year}`) ?? 0 : 0;

  return (
    <figure className="space-y-3">
      <div className="flex min-h-[2.5rem] flex-wrap items-end justify-between gap-3 text-sm">
        <p aria-live="polite" className="num">
          {hovered ? (
            <>
              <strong className="type-condensed font-semibold">{hovered.code}</strong> {hovered.name}, {hover!.year}:{" "}
              {hoveredDays} de {daysInYear(hover!.year)} días con dato (
              {formatPct(hoveredDays / daysInYear(hover!.year))})
            </>
          ) : (
            <span className="text-muted">Pase el cursor sobre una celda para ver la estación y el año; haga clic para abrir la serie.</span>
          )}
        </p>
        <div className="flex items-center gap-2 text-xs text-muted">
          <span>Sin dato</span>
          <span className="h-3 w-4 border border-grid" />
          {coverageLegend.map((c) => (
            <span key={c} className="h-3 w-4" style={{ background: c }} />
          ))}
          <span>Año completo</span>
        </div>
      </div>

      <svg
        viewBox={`0 0 ${width} ${height}`}
        preserveAspectRatio="none"
        className="block h-[440px] w-full cursor-crosshair bg-sheet ring-1 ring-grid"
        role="img"
        aria-label={`Cobertura anual de ${rows.length} estaciones entre ${firstYear} y ${lastYear}`}
        onMouseMove={(e) => setHover(locate(e))}
        onMouseLeave={() => setHover(null)}
        onClick={(e) => {
          const cell = locate(e);
          if (cell) onSelect(cell.code);
        }}
      >
        {Array.from({ length: Math.floor(years / 10) + 1 }, (_, i) => {
          const x = (Math.ceil(firstYear / 10) * 10 - firstYear + i * 10) * CELL_W;
          return <line key={i} x1={x} x2={x} y1={0} y2={height} stroke="#D3DCDB" strokeWidth={0.6} vectorEffect="non-scaling-stroke" />;
        })}
        {rows.map((s, r) =>
          Array.from({ length: years }, (_, c) => {
            const y = firstYear + c;
            const d = days.get(`${s.code}|${y}`);
            if (!d) return null;
            return (
              <rect
                key={`${s.code}-${y}`}
                x={c * CELL_W}
                y={r * CELL_H}
                width={CELL_W - 0.6}
                height={CELL_H - 0.8}
                fill={coverageColor(d / daysInYear(y))}
              />
            );
          })
        )}
        {hover && (
          <rect
            x={(hover.year - firstYear) * CELL_W - 0.5}
            y={rows.findIndex((r) => r.code === hover.code) * CELL_H - 0.5}
            width={CELL_W + 0.4}
            height={CELL_H + 0.2}
            fill="none"
            stroke="#A8742A"
            strokeWidth={1.5}
            vectorEffect="non-scaling-stroke"
          />
        )}
      </svg>

      <div className="relative h-4 text-xs text-muted num" aria-hidden>
        {Array.from({ length: Math.floor(years / 10) + 1 }, (_, i) => {
          const yr = Math.ceil(firstYear / 10) * 10 + i * 10;
          if (yr > lastYear) return null;
          return (
            <span key={yr} className="absolute -translate-x-1/2" style={{ left: `${((yr - firstYear) / years) * 100}%` }}>
              {yr}
            </span>
          );
        })}
      </div>
      <figcaption className="text-xs text-muted">
        Cada fila es una estación, ordenada por el inicio de su registro. Cada columna es un año.
      </figcaption>
    </figure>
  );
}

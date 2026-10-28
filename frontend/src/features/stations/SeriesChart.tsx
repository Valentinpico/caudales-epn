import { useMemo, useState } from "react";
import type { SeriesPoint, Variable } from "../../shared/api";
import { formatDec, niceTicks, unit } from "../../shared/format";

const W = 900;
const H = 280;
const PAD = { l: 44, r: 12, t: 12, b: 26 };

const color: Record<Variable, string> = { precipitation: "#1F6F8B", temperature: "#A8742A" };

/**
 * Hand-drawn SVG chart: precipitation as columns (it accumulates), temperature as a
 * line. Suspect values are ringed. Gaps in the record stay as gaps.
 */
export function SeriesChart({ points, variable, monthly }: { points: SeriesPoint[]; variable: Variable; monthly: boolean }) {
  const [hover, setHover] = useState<number | null>(null);

  const scale = useMemo(() => {
    if (points.length === 0) return null;
    const t = points.map((p) => Date.parse(p.date));
    const t0 = t[0];
    const t1 = t[t.length - 1] + (monthly ? 30 : 1) * 864e5;
    const values = points.map((p) => p.value);
    const yMin = variable === "precipitation" ? 0 : Math.floor(Math.min(...values));
    const yTicks = niceTicks(yMin, Math.max(...values));
    const yMax = yTicks[yTicks.length - 1];
    const x = (ms: number) => PAD.l + ((ms - t0) / (t1 - t0)) * (W - PAD.l - PAD.r);
    const y = (v: number) => H - PAD.b - ((v - yTicks[0]) / (yMax - yTicks[0] || 1)) * (H - PAD.t - PAD.b);
    const y0 = new Date(t0).getUTCFullYear();
    const y1 = new Date(t1).getUTCFullYear();
    const step = Math.max(1, Math.ceil((y1 - y0) / 8));
    const xTicks = [];
    for (let yr = Math.ceil(y0 / step) * step; yr <= y1; yr += step) xTicks.push(yr);
    return { t, x, y, yTicks, xTicks, barW: Math.max(0.6, ((W - PAD.l - PAD.r) * (monthly ? 30 : 1) * 864e5) / (t1 - t0) - 0.4) };
  }, [points, variable, monthly]);

  if (!scale) return <p className="py-16 text-center text-sm text-muted">La estación no tiene registros de esta variable.</p>;
  const { t, x, y, yTicks, xTicks, barW } = scale;

  // Break the line where consecutive records are more than one step apart.
  const maxGap = (monthly ? 32 : 1.5) * 864e5;
  const path = points
    .map((p, i) => `${i > 0 && t[i] - t[i - 1] <= maxGap ? "L" : "M"}${x(t[i]).toFixed(1)},${y(p.value).toFixed(1)}`)
    .join("");

  const onMove = (e: React.MouseEvent<SVGSVGElement>) => {
    const box = e.currentTarget.getBoundingClientRect();
    const px = ((e.clientX - box.left) / box.width) * W;
    let lo = 0;
    let hi = t.length - 1;
    while (lo < hi) {
      const mid = (lo + hi) >> 1;
      if (x(t[mid]) < px) lo = mid + 1;
      else hi = mid;
    }
    setHover(lo);
  };

  const h = hover !== null ? points[hover] : null;

  return (
    <div>
      <p className="num h-6 text-sm" aria-live="polite">
        {h ? (
          <>
            <strong>{monthly ? h.date.slice(0, 7) : h.date}</strong>: {formatDec(h.value)} {unit[variable]}
            {monthly && <span className="text-muted"> ({h.days} días con dato)</span>}
            {h.suspect && <span className="text-ochre">, valor sospechoso</span>}
          </>
        ) : (
          <span className="text-muted">
            {monthly ? (variable === "precipitation" ? "Acumulado mensual" : "Media mensual") : "Valores diarios"} en {unit[variable]}
          </span>
        )}
      </p>
      <svg viewBox={`0 0 ${W} ${H}`} className="w-full" onMouseMove={onMove} onMouseLeave={() => setHover(null)} role="img"
        aria-label={`Serie de ${points.length} valores`}>
        {yTicks.map((v) => (
          <g key={v}>
            <line x1={PAD.l} x2={W - PAD.r} y1={y(v)} y2={y(v)} stroke="#D3DCDB" strokeWidth={0.7} />
            <text x={PAD.l - 6} y={y(v) + 3.5} textAnchor="end" fontSize={10.5} fill="#5B6B72" className="num">
              {formatDec(v)}
            </text>
          </g>
        ))}
        {xTicks.map((yr) => (
          <text key={yr} x={x(Date.UTC(yr, 0, 1))} y={H - 8} textAnchor="middle" fontSize={10.5} fill="#5B6B72" className="num">
            {yr}
          </text>
        ))}
        {variable === "precipitation" ? (
          points.map((p, i) => (
            <rect key={p.date} x={x(t[i])} y={y(p.value)} width={barW} height={Math.max(0, y(0) - y(p.value))}
              fill={p.suspect ? "#A8742A" : color.precipitation} />
          ))
        ) : (
          <path d={path} fill="none" stroke={color.temperature} strokeWidth={monthly ? 1.4 : 0.8} />
        )}
        {variable === "temperature" &&
          points.map((p, i) =>
            p.suspect ? <circle key={p.date} cx={x(t[i])} cy={y(p.value)} r={3} fill="none" stroke="#A3412F" strokeWidth={1.2} /> : null
          )}
        {h && hover !== null && (
          <line x1={x(t[hover])} x2={x(t[hover])} y1={PAD.t} y2={H - PAD.b} stroke="#14232E" strokeWidth={0.7} strokeDasharray="3 3" />
        )}
      </svg>
    </div>
  );
}

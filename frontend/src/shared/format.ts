import type { Variable } from "./api";

const intFmt = new Intl.NumberFormat("es-EC");
const decFmt = new Intl.NumberFormat("es-EC", { maximumFractionDigits: 1 });

export const formatInt = (n: number) => intFmt.format(n);
export const formatDec = (n: number) => decFmt.format(n);
export const formatPct = (ratio: number) => `${decFmt.format(ratio * 100)} %`;
export const year = (isoDate: string) => isoDate.slice(0, 4);

export const variableLabel: Record<Variable, string> = {
  precipitation: "Precipitación",
  temperature: "Temperatura",
};

export const unit: Record<Variable, string> = { precipitation: "mm", temperature: "°C" };

export const monthNames = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

/** Sequential scale for coverage ratios: empty → pale water → deep water. */
const COVERAGE_STEPS = ["#E3ECEB", "#B9D3D8", "#7FAFBE", "#3F829C", "#1F6F8B", "#0B3A53"];

export function coverageColor(ratio: number): string {
  if (ratio <= 0) return "transparent";
  const i = Math.min(COVERAGE_STEPS.length - 1, Math.floor(ratio * (COVERAGE_STEPS.length - 1) + 0.5));
  return COVERAGE_STEPS[Math.max(1, i)];
}

export const coverageLegend = COVERAGE_STEPS.slice(1);

export const daysInYear = (y: number) => ((y % 4 === 0 && y % 100 !== 0) || y % 400 === 0 ? 366 : 365);

/** "Nice" axis ticks covering [min, max]. */
export function niceTicks(min: number, max: number, count = 4): number[] {
  if (max <= min) return [min];
  const raw = (max - min) / count;
  const mag = 10 ** Math.floor(Math.log10(raw));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * mag).find((s) => s >= raw)!;
  const ticks: number[] = [];
  for (let t = Math.floor(min / step) * step; t <= max + step / 2; t += step) ticks.push(+t.toFixed(10));
  return ticks;
}

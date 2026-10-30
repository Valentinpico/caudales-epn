import { DISCHARGE_SERIES, type Variable } from "./api";
import { dischargeSeriesLabel, isDischarge } from "./format";

const ALL: Variable[] = ["precipitation", "temperature", ...DISCHARGE_SERIES];

const families = [
  { key: "precipitation", label: "Precipitación", swatch: "bg-river" },
  { key: "temperature", label: "Temperatura", swatch: "bg-ochre" },
  { key: "discharge", label: "Caudal", swatch: "bg-[#2E8A6B]" },
] as const;

const familyOf = (v: Variable) => (isDischarge(v) ? "discharge" : v);

const segment = (active: boolean) =>
  `flex items-center gap-2 rounded-sm px-3 py-1 text-sm ${active ? "bg-ink text-white" : "text-muted hover:text-ink"}`;

/** Variable picker: precipitation, temperature or streamflow, plus the streamflow series stage. */
export function VariableToggle({
  value,
  onChange,
  options = ALL,
}: {
  value: Variable;
  onChange: (v: Variable) => void;
  options?: Variable[];
}) {
  const discharge = options.filter(isDischarge);
  const available = families.filter((f) => options.some((v) => familyOf(v) === f.key));
  // Switching to streamflow lands on the final series when the station has it.
  const enter = (key: string) =>
    key === "discharge" ? (discharge.includes("dischargeFinal") ? "dischargeFinal" : discharge[0]) : (key as Variable);

  return (
    <div className="flex flex-wrap gap-2">
      <div role="radiogroup" aria-label="Variable" className="inline-flex rounded-sm border border-grid bg-sheet p-0.5">
        {available.map((f) => (
          <button
            key={f.key}
            role="radio"
            aria-checked={familyOf(value) === f.key}
            onClick={() => familyOf(value) !== f.key && onChange(enter(f.key))}
            className={segment(familyOf(value) === f.key)}
          >
            <span className={`h-2 w-2 rounded-full ${f.swatch}`} aria-hidden />
            {f.label}
          </button>
        ))}
      </div>
      {isDischarge(value) && discharge.length > 0 && (
        <div role="radiogroup" aria-label="Serie de caudal" className="inline-flex flex-wrap rounded-sm border border-grid bg-sheet p-0.5">
          {discharge.map((v) => (
            <button key={v} role="radio" aria-checked={value === v} onClick={() => onChange(v)} className={segment(value === v)}>
              {dischargeSeriesLabel[v]}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

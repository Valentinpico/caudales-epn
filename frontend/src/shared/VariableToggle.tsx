import type { Variable } from "./api";
import { variableLabel } from "./format";

const swatch: Record<Variable, string> = { precipitation: "bg-river", temperature: "bg-ochre" };

export function VariableToggle({
  value,
  onChange,
  options = ["precipitation", "temperature"],
}: {
  value: Variable;
  onChange: (v: Variable) => void;
  options?: Variable[];
}) {
  return (
    <div role="radiogroup" aria-label="Variable" className="inline-flex rounded-sm border border-grid bg-sheet p-0.5">
      {options.map((v) => (
        <button
          key={v}
          role="radio"
          aria-checked={value === v}
          onClick={() => onChange(v)}
          className={`flex items-center gap-2 rounded-sm px-3 py-1 text-sm ${
            value === v ? "bg-ink text-white" : "text-muted hover:text-ink"
          }`}
        >
          <span className={`h-2 w-2 rounded-full ${swatch[v]}`} aria-hidden />
          {variableLabel[v]}
        </button>
      ))}
    </div>
  );
}

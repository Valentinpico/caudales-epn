import type { MonthCoverage } from "../../shared/api";
import { coverageColor, monthNames } from "../../shared/format";

/** Year × month matrix of days with data, the station-level view of gaps. */
export function CompletenessGrid({ months, firstYear, lastYear }: { months: MonthCoverage[]; firstYear: number; lastYear: number }) {
  const byKey = new Map(months.map((m) => [`${m.year}-${m.month}`, m]));
  const years = Array.from({ length: lastYear - firstYear + 1 }, (_, i) => firstYear + i);

  return (
    <div className="overflow-x-auto">
      <table className="num border-separate border-spacing-[1.5px] text-[10px] leading-none">
        <thead>
          <tr>
            <th className="w-10" />
            {monthNames.map((m) => (
              <th key={m} className="w-6 pb-1 font-normal text-muted">
                {m}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {years.map((y) => (
            <tr key={y}>
              <th className="pr-1.5 text-right font-normal text-muted">{y % 5 === 0 ? y : ""}</th>
              {monthNames.map((_, i) => {
                const m = byKey.get(`${y}-${i + 1}`);
                const ratio = m ? m.days / m.daysInMonth : 0;
                return (
                  <td
                    key={i}
                    title={m ? `${y}-${String(i + 1).padStart(2, "0")}: ${m.days} de ${m.daysInMonth} días` : `${y}-${i + 1}: sin datos`}
                    className="h-2 w-6"
                    style={{ background: m ? coverageColor(ratio) : "#EEF2F1" }}
                  />
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

import type { GraphNode } from "../../shared/api";
import type { Member, ProjectProfile } from "./projectProfile";

function SectionTitle({ id, eyebrow, title }: { id: string; eyebrow: string; title: string }) {
  return (
    <div className="space-y-1">
      <p className="type-condensed text-sm font-bold uppercase tracking-wider text-river">{eyebrow}</p>
      <h2 id={id} className="type-display text-2xl sm:text-3xl">
        {title}
      </h2>
    </div>
  );
}

export function ResearchLines({ lines }: { lines: ProjectProfile["lines"] }) {
  return (
    <section className="space-y-5" aria-labelledby="lines-title">
      <SectionTitle id="lines-title" eyebrow="Líneas de investigación" title="Dónde se inscribe el proyecto" />
      <ul className="grid gap-px border border-grid bg-grid sm:grid-cols-2 lg:grid-cols-4">
        {lines.map((l) => (
          <li key={l.id} className="flex flex-col gap-2 bg-sheet p-5">
            <span className="type-condensed text-sm font-bold text-river">{l.code}</span>
            <span className="font-semibold leading-snug">{l.name}</span>
            <span className="mt-auto text-sm text-muted">{l.unit}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}

// First name + first surname: names follow "Name Name Surname Surname".
const initials = (name: string) => {
  const w = name.split(" ");
  return w[0][0] + (w.at(-2) ?? "")[0];
};

function MemberCard({ m }: { m: Member }) {
  return (
    <li className="grid grid-cols-[4.5rem_1fr] overflow-hidden border border-grid bg-sheet">
      <span
        aria-hidden
        className={`type-display flex items-center justify-center text-2xl ${m.leads ? "bg-deep text-white" : "bg-paper text-river"}`}
      >
        {initials(m.name)}
      </span>
      <div className="space-y-1 p-4">
        <p className="font-semibold leading-snug">{m.name}</p>
        <p className="type-condensed text-sm font-bold uppercase tracking-wide text-river">{m.role}</p>
        <p className="text-sm text-muted">{m.degree}</p>
        <p className="text-sm text-muted">{m.institution}</p>
      </div>
    </li>
  );
}

export function Team({ team }: { team: Member[] }) {
  const groups = [
    { title: "Dirección", members: team.filter((m) => m.leads) },
    { title: "Colaboradores", members: team.filter((m) => !m.leads) },
  ];
  return (
    <section id="equipo" className="space-y-5" aria-labelledby="team-title">
      <SectionTitle id="team-title" eyebrow="Equipo" title="Investigadores del proyecto" />
      {groups.map((g) => (
        <div key={g.title} className="space-y-2">
          <h3 className="text-sm font-semibold text-muted">{g.title}</h3>
          <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {g.members.map((m) => (
              <MemberCard key={m.id} m={m} />
            ))}
          </ul>
        </div>
      ))}
    </section>
  );
}

export function Objectives({ objectives, sdgs }: { objectives: GraphNode[]; sdgs: GraphNode[] }) {
  return (
    <section className="grid gap-10 lg:grid-cols-[1.5fr_1fr]" aria-labelledby="objectives-title">
      <div className="space-y-5">
        <SectionTitle id="objectives-title" eyebrow="Objetivos" title="Qué busca el proyecto" />
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
      </div>
      <div className="space-y-5">
        <SectionTitle id="sdg-title" eyebrow="Agenda 2030" title="Objetivos de Desarrollo Sostenible" />
        <ul className="space-y-2">
          {sdgs.map((s) => (
            <li key={s.id} className="panel px-4 py-3 text-sm">
              {s.label}
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}

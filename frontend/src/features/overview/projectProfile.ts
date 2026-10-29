import type { GraphEdge, GraphNode } from "../../shared/api";

export const PROJECT_ID = "project:caudales";
const ROLES = ["DIRIGE", "CODIRIGE", "COLABORA"] as const;

export interface Member {
  id: string;
  name: string;
  role: string; // first sentence of the description, e.g. "Directora"
  degree: string; // the rest, e.g. "Doctora en Ciencias."
  institution?: string;
  leads: boolean;
}

export interface ProjectProfile {
  project?: GraphNode;
  lines: { id: string; code: string; name: string; unit?: string }[];
  team: Member[];
  objectives: GraphNode[];
  sdgs: GraphNode[];
}

/** Project, research lines, team, objectives and SDGs, read from the knowledge graph. */
export function projectProfile(nodes: GraphNode[], edges: GraphEdge[]): ProjectProfile {
  const byId = new Map(nodes.map((n) => [n.id, n]));
  const targets = (source: string, relation: string) =>
    edges
      .filter((e) => e.sourceId === source && e.relation === relation)
      .map((e) => byId.get(e.targetId))
      .filter((n): n is GraphNode => !!n);

  const team = edges
    .filter((e) => e.targetId === PROJECT_ID && (ROLES as readonly string[]).includes(e.relation) && byId.has(e.sourceId))
    .sort((a, b) => ROLES.indexOf(a.relation as never) - ROLES.indexOf(b.relation as never))
    .map((e) => {
      const person = byId.get(e.sourceId)!;
      const [role, ...rest] = (person.description ?? "").split(". ");
      return {
        id: person.id,
        name: person.label,
        role,
        degree: rest.join(". "),
        institution: targets(person.id, "PERTENECE_A")[0]?.label,
        leads: e.relation !== "COLABORA",
      };
    });

  return {
    project: byId.get(PROJECT_ID),
    lines: targets(PROJECT_ID, "EN_LINEA").map((l) => ({
      id: l.id,
      code: l.description ?? "",
      name: l.label,
      unit: targets(l.id, "DEFINIDA_POR")[0]?.label,
    })),
    team,
    objectives: targets(PROJECT_ID, "TIENE_OBJETIVO").sort((a, b) => a.id.localeCompare(b.id)),
    sdgs: targets(PROJECT_ID, "CONTRIBUYE_A"),
  };
}

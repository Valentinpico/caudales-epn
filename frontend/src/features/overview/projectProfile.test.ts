import { describe, expect, it } from "vitest";
import type { GraphEdge, GraphNode } from "../../shared/api";
import { projectProfile } from "./projectProfile";

const node = (id: string, kind: string, label: string, description: string | null = null): GraphNode => ({
  id,
  kind,
  label,
  description,
});
let n = 0;
const edge = (sourceId: string, targetId: string, relation: string): GraphEdge => ({ id: ++n, sourceId, targetId, relation });

describe("projectProfile", () => {
  const nodes = [
    node("project:caudales", "Project", "Caudales"),
    node("researcher:b", "Researcher", "B Colab", "Colaborador. PhD en Hidrología."),
    node("researcher:a", "Researcher", "A Dir", "Directora. Doctora en Ciencias."),
    node("institution:dm", "Institution", "EPN, DM"),
    node("line:X-1", "ResearchLine", "Hidrología", "X-1"),
    node("objective:OE2", "Objective", "OE2: Dos"),
    node("objective:OE1", "Objective", "OE1: Uno"),
  ];
  const edges = [
    edge("researcher:b", "project:caudales", "COLABORA"),
    edge("researcher:a", "project:caudales", "DIRIGE"),
    edge("researcher:a", "institution:dm", "PERTENECE_A"),
    edge("project:caudales", "line:X-1", "EN_LINEA"),
    edge("line:X-1", "institution:dm", "DEFINIDA_POR"),
    edge("project:caudales", "objective:OE2", "TIENE_OBJETIVO"),
    edge("project:caudales", "objective:OE1", "TIENE_OBJETIVO"),
  ];

  it("orders the team by role and splits role from degree", () => {
    const { team } = projectProfile(nodes, edges);
    expect(team.map((m) => [m.name, m.role, m.degree, m.leads, m.institution])).toEqual([
      ["A Dir", "Directora", "Doctora en Ciencias.", true, "EPN, DM"],
      ["B Colab", "Colaborador", "PhD en Hidrología.", false, undefined],
    ]);
  });

  it("reads research lines with their unit and sorts objectives", () => {
    const p = projectProfile(nodes, edges);
    expect(p.lines).toEqual([{ id: "line:X-1", code: "X-1", name: "Hidrología", unit: "EPN, DM" }]);
    expect(p.objectives.map((o) => o.id)).toEqual(["objective:OE1", "objective:OE2"]);
  });
});

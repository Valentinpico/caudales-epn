import { describe, expect, it } from "vitest";
import type { GraphEdge, GraphNode } from "../../shared/api";
import { computeLayout } from "./graphLayout";

const node = (id: string): GraphNode => ({ id, kind: "Method", label: id, description: null });
const nodes = [node("a"), node("b"), node("c")];
const edges: GraphEdge[] = [{ id: 1, sourceId: "a", targetId: "b", relation: "USA" }];

describe("computeLayout", () => {
  it("places every node inside the bounds", () => {
    const m = computeLayout(nodes, edges, 1000, 560);
    expect(m.size).toBe(3);
    for (const p of m.values()) {
      expect(p.x).toBeGreaterThanOrEqual(0);
      expect(p.x).toBeLessThanOrEqual(1000);
      expect(p.y).toBeGreaterThanOrEqual(0);
      expect(p.y).toBeLessThanOrEqual(560);
    }
  });

  it("returns an empty map for an empty graph", () => expect(computeLayout([], [], 1000, 560).size).toBe(0));

  it("is deterministic", () => {
    expect(computeLayout(nodes, edges, 1000, 560).get("c")).toEqual(computeLayout(nodes, edges, 1000, 560).get("c"));
  });

  it("keeps linked nodes closer than unlinked ones", () => {
    const m = computeLayout(nodes, edges, 1000, 560);
    const d = (x: string, y: string) => Math.hypot(m.get(x)!.x - m.get(y)!.x, m.get(x)!.y - m.get(y)!.y);
    expect(d("a", "b")).toBeLessThan(d("a", "c"));
  });
});

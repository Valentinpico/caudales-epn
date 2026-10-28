import type { GraphEdge, GraphNode } from "../../shared/api";

export interface Pt {
  x: number;
  y: number;
}

/**
 * Deterministic force-directed layout (simplified Fruchterman-Reingold).
 * Returns one position per node inside [0, width] x [0, height].
 */
export function computeLayout(
  nodes: GraphNode[],
  edges: GraphEdge[],
  width: number,
  height: number
): Map<string, Pt> {
  const n = nodes.length;
  if (n === 0) return new Map();

  const cx = width / 2;
  const cy = height / 2;
  // Deterministic initial placement on an ellipse (no randomness).
  const pos: Pt[] = nodes.map((_, i) => {
    const a = (2 * Math.PI * i) / n;
    return { x: cx + Math.cos(a) * width * 0.35, y: cy + Math.sin(a) * height * 0.35 };
  });

  const index = new Map(nodes.map((nd, i) => [nd.id, i] as const));
  const links = edges
    .map((e) => [index.get(e.sourceId), index.get(e.targetId)] as const)
    .filter((l): l is readonly [number, number] => l[0] !== undefined && l[1] !== undefined);

  const k = Math.sqrt((width * height) / n) * 0.5; // ideal distance (fills the area)
  const iterations = 250;
  const marginX = width * 0.1;
  const marginY = height * 0.07;

  for (let it = 0; it < iterations; it++) {
    const disp: Pt[] = pos.map(() => ({ x: 0, y: 0 }));

    for (let i = 0; i < n; i++) {
      for (let j = i + 1; j < n; j++) {
        const dx = pos[i].x - pos[j].x;
        const dy = pos[i].y - pos[j].y;
        const dist = Math.hypot(dx, dy) || 0.01;
        const rep = (k * k) / dist;
        disp[i].x += (dx / dist) * rep;
        disp[i].y += (dy / dist) * rep;
        disp[j].x -= (dx / dist) * rep;
        disp[j].y -= (dy / dist) * rep;
      }
    }

    for (const [a, b] of links) {
      const dx = pos[a].x - pos[b].x;
      const dy = pos[a].y - pos[b].y;
      const dist = Math.hypot(dx, dy) || 0.01;
      const att = (dist * dist) / k;
      disp[a].x -= (dx / dist) * att;
      disp[a].y -= (dy / dist) * att;
      disp[b].x += (dx / dist) * att;
      disp[b].y += (dy / dist) * att;
    }

    const temp = Math.min(width, height) * 0.04 * (1 - it / iterations);
    for (let i = 0; i < n; i++) {
      disp[i].x += (cx - pos[i].x) * 0.02;
      disp[i].y += (cy - pos[i].y) * 0.02;
      const dl = Math.hypot(disp[i].x, disp[i].y) || 0.01;
      pos[i].x += (disp[i].x / dl) * Math.min(dl, temp);
      pos[i].y += (disp[i].y / dl) * Math.min(dl, temp);
      pos[i].x = Math.max(marginX, Math.min(width - marginX, pos[i].x));
      pos[i].y = Math.max(marginY, Math.min(height - marginY, pos[i].y));
    }
  }

  return new Map(nodes.map((nd, i) => [nd.id, pos[i]] as const));
}

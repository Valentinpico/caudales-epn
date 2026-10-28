export const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:8081";

export type Variable = "precipitation" | "temperature";

export interface StationSummary {
  code: string;
  name: string;
  variable: Variable;
  observations: number;
  first: string;
  last: string;
  completeness: number;
  suspect: number;
  issues: number;
  latitude: number | null;
  longitude: number | null;
  altitude: number | null;
  province: string | null;
}

export interface SeriesPoint {
  date: string;
  value: number;
  days: number;
  suspect: boolean;
}

export interface MonthCoverage {
  year: number;
  month: number;
  days: number;
  daysInMonth: number;
}

export interface YearCoverage {
  code: string;
  year: number;
  days: number;
}

export interface QualityIssue {
  id: number;
  stationCode: string;
  variable: Variable;
  sourceLine: number;
  year: number;
  month: number;
  day: number | null;
  kind: string;
  detail: string;
}

export interface IssueCount {
  variable: Variable;
  kind: string;
  count: number;
}

export interface Overview {
  stations: number;
  observations: number;
  issues: number;
  documents: number;
  graphNodes: number;
  graphEdges: number;
  first: string | null;
  last: string | null;
}

export interface DocumentItem {
  id: number;
  title: string;
  category: string;
  objective: string | null;
  description: string | null;
  authors: string | null;
  filePath: string | null;
  createdAt: string;
}

export interface GraphNode {
  id: string;
  kind: string;
  label: string;
  description: string | null;
}

export interface GraphEdge {
  id: number;
  sourceId: string;
  targetId: string;
  relation: string;
}

export interface Neighborhood {
  node: GraphNode;
  neighbors: GraphNode[];
  edges: GraphEdge[];
}

let adminPassword: string | null = null;
export const setAdminPassword = (p: string | null) => (adminPassword = p);

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (adminPassword) headers.set("X-Admin-Password", adminPassword);
  const res = await fetch(`${API_URL}${path}`, { ...init, headers });
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new Error(body?.error ?? `Error ${res.status} al consultar ${path}`);
  }
  return res.status === 204 ? (undefined as T) : res.json();
}

const qs = (params: Record<string, string | undefined | null>) => {
  const p = new URLSearchParams();
  Object.entries(params).forEach(([k, v]) => v && p.set(k, v));
  const s = p.toString();
  return s ? `?${s}` : "";
};

export const api = {
  overview: () => request<Overview>("/api/overview"),
  stations: (variable?: Variable) => request<StationSummary[]>(`/api/stations${qs({ variable })}`),
  coverage: (variable: Variable) => request<YearCoverage[]>(`/api/coverage${qs({ variable })}`),
  series: (code: string, variable: Variable, resolution: "daily" | "monthly") =>
    request<SeriesPoint[]>(`/api/stations/${code}/series${qs({ variable, resolution })}`),
  completeness: (code: string, variable: Variable) =>
    request<MonthCoverage[]>(`/api/stations/${code}/completeness${qs({ variable })}`),
  exportUrl: (code: string, variable: Variable) => `${API_URL}/api/stations/${code}/export${qs({ variable })}`,
  qualitySummary: () => request<IssueCount[]>("/api/quality/summary"),
  issues: (filter: { station?: string; variable?: Variable; kind?: string }) =>
    request<QualityIssue[]>(`/api/quality/issues${qs(filter)}`),
  documents: (filter: { category?: string; objective?: string; search?: string }) =>
    request<DocumentItem[]>(`/api/documents${qs(filter)}`),
  createDocument: (form: FormData) => request<DocumentItem>("/api/documents", { method: "POST", body: form }),
  deleteDocument: (id: number) => request<void>(`/api/documents/${id}`, { method: "DELETE" }),
  fileUrl: (path: string) => `${API_URL}/files/${path}`,
  graph: () => request<{ nodes: GraphNode[]; edges: GraphEdge[] }>("/api/graph"),
  neighborhood: (id: string) => request<Neighborhood>(`/api/graph/nodes/${encodeURIComponent(id)}`),
  verifyAdmin: async (password: string) => {
    const res = await fetch(`${API_URL}/api/admin/verify`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ password }),
    });
    return res.ok;
  },
};

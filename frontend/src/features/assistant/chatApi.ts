import { API_URL } from "../../shared/api";

export type AnswerKind = "rag" | "abstained";

export interface ChatSource {
  n: number;
  docId: string;
  title: string;
  section: string;
  /** Frontend route (/grafo), file under /files/..., or null (report without a page). */
  url: string | null;
  score: number;
  fromSession: boolean;
}

export interface ChatTurn {
  role: "user" | "assistant";
  content: string;
}

export interface SessionUpload {
  sessionId: string;
  docId: string;
  title: string;
  chunks: number;
  expiresAt: string;
}

export interface ChatHealth {
  provider: string;
  embeddingModel: string;
  chatModel: string;
  chunks: number;
  indexedAt: string | null;
  indexing: boolean;
  sessionTtlMinutes?: number;
  lastError?: string | null;
}

export type ChatEvent =
  | { type: "sources"; kind: AnswerKind; sources: ChatSource[] }
  | { type: "token"; text: string }
  | { type: "done"; kind: AnswerKind; answer: string; sources: ChatSource[]; topScore: number }
  | { type: "error"; message: string };

export const RATE_LIMIT_MESSAGE = "Demasiadas consultas seguidas. Espere un minuto e inténtelo de nuevo.";
export const INTERRUPTED_MESSAGE = "La respuesta se interrumpió antes de completarse. Inténtelo de nuevo.";

/**
 * Incremental parser for a text/event-stream body. The network may split an event (or a
 * CRLF pair) anywhere, so whatever has no closing blank line yet stays in the buffer.
 */
export function createSseParser() {
  let buffer = "";
  return (chunk: string): ChatEvent[] => {
    buffer += chunk;
    // Normalize CRLF/CR, but keep a trailing "\r": its "\n" may arrive in the next chunk.
    buffer = buffer.replace(/\r\n|\r(?!$)/g, "\n");
    const events: ChatEvent[] = [];
    let end = buffer.indexOf("\n\n");
    while (end >= 0) {
      const parsed = parseEvent(buffer.slice(0, end));
      if (parsed) events.push(parsed);
      buffer = buffer.slice(end + 2);
      end = buffer.indexOf("\n\n");
    }
    return events;
  };
}

function parseEvent(raw: string): ChatEvent | null {
  let name = "message";
  const data: string[] = [];
  for (const line of raw.split("\n")) {
    if (line.startsWith(":")) continue;
    const colon = line.indexOf(":");
    const field = colon < 0 ? line : line.slice(0, colon);
    const value = colon < 0 ? "" : line.slice(colon + 1).replace(/^ /, "");
    if (field === "event") name = value;
    else if (field === "data") data.push(value);
  }
  if (data.length === 0) return null;
  let payload: Record<string, unknown>;
  try {
    payload = JSON.parse(data.join("\n"));
  } catch {
    return null;
  }
  switch (name) {
    case "sources":
      return { type: "sources", kind: payload.kind as AnswerKind, sources: (payload.sources as ChatSource[]) ?? [] };
    case "token":
      return typeof payload.text === "string" ? { type: "token", text: payload.text } : null;
    case "done":
      return {
        type: "done",
        kind: payload.kind as AnswerKind,
        answer: String(payload.answer ?? ""),
        sources: (payload.sources as ChatSource[]) ?? [],
        topScore: Number(payload.topScore ?? 0),
      };
    case "error":
      return { type: "error", message: String(payload.message ?? INTERRUPTED_MESSAGE) };
    default:
      return null;
  }
}

async function errorMessage(res: Response, fallback: string) {
  if (res.status === 429) {
    const body = await res.json().catch(() => null);
    return body?.error ?? RATE_LIMIT_MESSAGE;
  }
  const text = await res.text().catch(() => "");
  try {
    return JSON.parse(text).error ?? fallback;
  } catch {
    return text.trim() || fallback;
  }
}

export const chatApi = {
  /** POST + SSE: EventSource only does GET, so the body is read as a stream. */
  async ask(
    body: { question: string; sessionId?: string; history: ChatTurn[] },
    onEvent: (e: ChatEvent) => void,
    signal?: AbortSignal
  ) {
    const res = await fetch(`${API_URL}/api/chat/ask`, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
      body: JSON.stringify(body),
      signal,
    });
    if (!res.ok) throw new Error(await errorMessage(res, `Error ${res.status} al consultar el asistente`));
    if (!res.body) throw new Error(INTERRUPTED_MESSAGE);

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    const parse = createSseParser();
    let finished = false;
    for (;;) {
      const { value, done } = await reader.read();
      const events = parse(done ? decoder.decode() + "\n\n" : decoder.decode(value, { stream: true }));
      for (const e of events) {
        if (e.type === "done" || e.type === "error") finished = true;
        onEvent(e);
      }
      if (done) break;
    }
    if (!finished) throw new Error(INTERRUPTED_MESSAGE);
  },

  async upload(sessionId: string, file: File): Promise<SessionUpload> {
    const form = new FormData();
    form.append("sessionId", sessionId);
    form.append("file", file);
    const res = await fetch(`${API_URL}/api/chat/upload`, { method: "POST", body: form });
    if (!res.ok) throw new Error(await errorMessage(res, "No se pudo cargar el archivo"));
    return res.json();
  },

  async deleteSession(sessionId: string): Promise<number> {
    const res = await fetch(`${API_URL}/api/chat/session/${encodeURIComponent(sessionId)}`, { method: "DELETE" });
    if (!res.ok) throw new Error(await errorMessage(res, "No se pudo quitar el archivo"));
    return (await res.json()).deletedChunks;
  },

  async health(): Promise<ChatHealth> {
    const res = await fetch(`${API_URL}/api/chat/health`);
    if (!res.ok) throw new Error(await errorMessage(res, "No se pudo consultar el estado del índice documental."));
    return res.json();
  },
};

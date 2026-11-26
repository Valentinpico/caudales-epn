import { useSyncExternalStore } from "react";
import { chatApi, type AnswerKind, type ChatSource, type ChatTurn, type SessionUpload } from "./chatApi";

export interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
  kind?: AnswerKind;
  sources?: ChatSource[];
  status?: "streaming" | "done" | "stopped" | "error";
  error?: string;
}

export interface ChatState {
  sessionId: string;
  messages: ChatMessage[];
  upload: SessionUpload | null;
  uploading: boolean;
  uploadError: string | null;
}

const STORAGE_KEY = "caudales.asistente";
const HISTORY_TURNS = 6;

export const newSessionId = () =>
  typeof crypto !== "undefined" && "randomUUID" in crypto
    ? crypto.randomUUID().replace(/-/g, "")
    : Array.from({ length: 32 }, () => Math.floor(Math.random() * 16).toString(16)).join("");

let seq = 0;
const nextId = () => `${Date.now().toString(36)}-${(seq++).toString(36)}`;

function load(storage: Storage | null): ChatState {
  const fresh: ChatState = { sessionId: newSessionId(), messages: [], upload: null, uploading: false, uploadError: null };
  try {
    const saved = JSON.parse(storage?.getItem(STORAGE_KEY) ?? "null") as Partial<ChatState> | null;
    if (!saved?.sessionId) return fresh;
    return {
      ...fresh,
      sessionId: saved.sessionId,
      // A stream cut by a reload cannot resume: keep what arrived, marked as stopped.
      messages: (saved.messages ?? []).map((m) => (m.status === "streaming" ? { ...m, status: "stopped" } : m)),
      upload: saved.upload && Date.parse(saved.upload.expiresAt) > Date.now() ? saved.upload : null,
    };
  } catch {
    return fresh;
  }
}

/** Completed turns sent as context, so follow-up questions ("¿y el segundo?") resolve. */
function history(messages: ChatMessage[]): ChatTurn[] {
  return messages
    .filter((m) => m.role === "user" || m.status === "done")
    .map((m) => ({ role: m.role, content: m.content }))
    .slice(-HISTORY_TURNS);
}

export function createChatStore(storage: Storage | null = safeSessionStorage(), api = chatApi) {
  let state = load(storage);
  let controller: AbortController | null = null;
  const listeners = new Set<() => void>();

  const set = (patch: Partial<ChatState>) => {
    state = { ...state, ...patch };
    listeners.forEach((l) => l());
    if (!state.messages.some((m) => m.status === "streaming")) persist();
  };
  const persist = () => {
    try {
      const { sessionId, messages, upload } = state;
      storage?.setItem(STORAGE_KEY, JSON.stringify({ sessionId, messages, upload }));
    } catch {
      /* storage full or blocked: the conversation just won't survive a reload */
    }
  };
  const patchMessage = (id: string, patch: (m: ChatMessage) => Partial<ChatMessage>) =>
    set({ messages: state.messages.map((m) => (m.id === id ? { ...m, ...patch(m) } : m)) });

  const isStreaming = () => state.messages.some((m) => m.status === "streaming");

  return {
    getState: () => state,
    subscribe(listener: () => void) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    isStreaming,

    async ask(question: string) {
      const text = question.trim();
      if (!text || isStreaming()) return;
      const turns = history(state.messages);
      const id = nextId();
      set({
        messages: [
          ...state.messages,
          { id: nextId(), role: "user", content: text },
          { id, role: "assistant", content: "", status: "streaming" },
        ],
      });
      controller = new AbortController();
      try {
        await api.ask(
          { question: text, sessionId: state.sessionId, history: turns },
          (e) => {
            if (e.type === "sources") patchMessage(id, () => ({ kind: e.kind, sources: e.sources }));
            else if (e.type === "token") patchMessage(id, (m) => ({ content: m.content + e.text }));
            else if (e.type === "done")
              patchMessage(id, () => ({ kind: e.kind, sources: e.sources, content: e.answer, status: "done" }));
            else patchMessage(id, () => ({ status: "error", error: e.message }));
          },
          controller.signal
        );
      } catch (err) {
        const aborted = (err as Error)?.name === "AbortError";
        patchMessage(id, (m) =>
          m.status !== "streaming"
            ? {}
            : aborted
              ? { status: "stopped" }
              : { status: "error", error: err instanceof Error ? err.message : String(err) }
        );
      } finally {
        controller = null;
      }
    },

    stop() {
      controller?.abort();
    },

    /** Clears the transcript; an attached file stays indexed for this session until removed. */
    reset() {
      controller?.abort();
      set({ messages: [] });
    },

    /** One file per session: a new one replaces the previous (the server deletes by session). */
    async attach(file: File) {
      set({ uploading: true, uploadError: null });
      try {
        if (state.upload) await api.deleteSession(state.sessionId);
        set({ upload: await api.upload(state.sessionId, file) });
      } catch (err) {
        set({ uploadError: err instanceof Error ? err.message : String(err) });
      } finally {
        set({ uploading: false });
      }
    },

    async detach() {
      set({ uploading: true, uploadError: null });
      try {
        await api.deleteSession(state.sessionId);
        set({ upload: null });
      } catch (err) {
        set({ uploadError: err instanceof Error ? err.message : String(err) });
      } finally {
        set({ uploading: false });
      }
    },

    /** Drops a file whose TTL passed: the server already purged it. */
    expire() {
      if (state.upload && Date.parse(state.upload.expiresAt) <= Date.now()) set({ upload: null });
    },
  };
}

function safeSessionStorage(): Storage | null {
  try {
    return typeof window !== "undefined" ? window.sessionStorage : null;
  } catch {
    return null;
  }
}

export type ChatStore = ReturnType<typeof createChatStore>;

let shared: ChatStore | null = null;
const sharedStore = () => (shared ??= createChatStore());

/** Module-level store: the conversation survives navigating away from the page and back. */
export function useChat(store: ChatStore = sharedStore()) {
  const state = useSyncExternalStore(store.subscribe, store.getState);
  return { ...state, streaming: state.messages.some((m) => m.status === "streaming"), store };
}

import { describe, expect, it, vi } from "vitest";
import type { ChatEvent, chatApi } from "./chatApi";
import { createChatStore } from "./chatStore";

class MemoryStorage {
  data = new Map<string, string>();
  getItem = (k: string) => this.data.get(k) ?? null;
  setItem = (k: string, v: string) => void this.data.set(k, v);
  removeItem = (k: string) => void this.data.delete(k);
  clear = () => this.data.clear();
  key = () => null;
  get length() {
    return this.data.size;
  }
}

const source = { n: 1, docId: "propuesta", title: "Propuesta", section: "Objetivos", url: "/files/x.pdf", score: 0.7, fromSession: false };

function fakeApi(script: (onEvent: (e: ChatEvent) => void, signal?: AbortSignal) => Promise<void>) {
  return {
    ask: vi.fn((_body: Parameters<typeof chatApi.ask>[0], onEvent: (e: ChatEvent) => void, signal?: AbortSignal) => script(onEvent, signal)),
    upload: vi.fn(async (sessionId: string, file: File) => ({ sessionId, docId: "sesion:1", title: file.name.replace(/\.\w+$/, ""), chunks: 3, expiresAt: new Date(Date.now() + 3_600_000).toISOString() })),
    deleteSession: vi.fn(async () => 3),
    health: vi.fn(),
  } as unknown as typeof chatApi & { ask: ReturnType<typeof vi.fn>; upload: ReturnType<typeof vi.fn>; deleteSession: ReturnType<typeof vi.fn> };
}

describe("chat store", () => {
  it("streams tokens into the assistant message and settles on done", async () => {
    const snapshots: string[] = [];
    const api = fakeApi(async (emit) => {
      emit({ type: "sources", kind: "rag", sources: [source] });
      emit({ type: "token", text: "Son " });
      emit({ type: "token", text: "tres [1]." });
      emit({ type: "done", kind: "rag", answer: "Son tres [1].", sources: [source], topScore: 0.7 });
    });
    const store = createChatStore(new MemoryStorage(), api);
    store.subscribe(() => snapshots.push(store.getState().messages.at(-1)?.content ?? ""));
    await store.ask("  ¿Cuántos objetivos?  ");

    const [user, answer] = store.getState().messages;
    expect(user).toMatchObject({ role: "user", content: "¿Cuántos objetivos?" });
    expect(answer).toMatchObject({ role: "assistant", content: "Son tres [1].", kind: "rag", status: "done", sources: [source] });
    expect(snapshots).toContain("Son ");
  });

  it("sends the session id and the last completed turns as history", async () => {
    const api = fakeApi(async (emit) => emit({ type: "done", kind: "rag", answer: "r", sources: [], topScore: 1 }));
    const store = createChatStore(new MemoryStorage(), api);
    for (let i = 0; i < 4; i++) await store.ask(`p${i}`);
    const body = api.ask.mock.calls[3][0];
    expect(body.sessionId).toBe(store.getState().sessionId);
    expect(body.sessionId).toMatch(/^[A-Za-z0-9_-]{8,64}$/);
    expect(body.history).toHaveLength(6);
    expect(body.history[0]).toEqual({ role: "user", content: "p0" });
    expect(body.history.at(-1)).toEqual({ role: "assistant", content: "r" });
  });

  it("marks abstained answers", async () => {
    const api = fakeApi(async (emit) => emit({ type: "done", kind: "abstained", answer: "No consta.", sources: [], topScore: 0.1 }));
    const store = createChatStore(new MemoryStorage(), api);
    await store.ask("¿Capital de Francia?");
    expect(store.getState().messages[1]).toMatchObject({ kind: "abstained", status: "done" });
  });

  it("settles on abstained without sources when done overrides a rag sources event", async () => {
    const api = fakeApi(async (emit) => {
      emit({ type: "sources", kind: "rag", sources: [source] });
      emit({ type: "token", text: "No consta." });
      emit({ type: "done", kind: "abstained", answer: "No consta.", sources: [], topScore: 0.5 });
    });
    const store = createChatStore(new MemoryStorage(), api);
    await store.ask("¿Quién ganó el mundial?");
    expect(store.getState().messages[1]).toMatchObject({ kind: "abstained", status: "done", content: "No consta.", sources: [] });
  });

  it("turns request failures and error events into an error message", async () => {
    const store = createChatStore(new MemoryStorage(), fakeApi(async () => { throw new Error("Demasiadas consultas"); }));
    await store.ask("a");
    expect(store.getState().messages[1]).toMatchObject({ status: "error", error: "Demasiadas consultas" });

    const store2 = createChatStore(new MemoryStorage(), fakeApi(async (emit) => emit({ type: "error", message: "Falló" })));
    await store2.ask("a");
    expect(store2.getState().messages[1]).toMatchObject({ status: "error", error: "Falló" });
  });

  it("excludes failed turns from the history", async () => {
    let fail = true;
    const api = fakeApi(async (emit) => {
      if (fail) throw new Error("x");
      emit({ type: "done", kind: "rag", answer: "ok", sources: [], topScore: 1 });
    });
    const store = createChatStore(new MemoryStorage(), api);
    await store.ask("uno");
    fail = false;
    await store.ask("dos");
    expect(api.ask.mock.calls[1][0].history).toEqual([{ role: "user", content: "uno" }]);
  });

  it("stop() aborts the stream and keeps the partial answer", async () => {
    const api = fakeApi(
      (emit, signal) =>
        new Promise<void>((_, reject) => {
          emit({ type: "token", text: "Parcial" });
          signal?.addEventListener("abort", () => reject(new DOMException("aborted", "AbortError")));
        })
    );
    const store = createChatStore(new MemoryStorage(), api);
    const pending = store.ask("q");
    expect(store.isStreaming()).toBe(true);
    store.stop();
    await pending;
    expect(store.getState().messages[1]).toMatchObject({ content: "Parcial", status: "stopped" });
  });

  it("persists the conversation and restores it, keeping the session id", async () => {
    const storage = new MemoryStorage();
    const api = fakeApi(async (emit) => emit({ type: "done", kind: "rag", answer: "r", sources: [], topScore: 1 }));
    const first = createChatStore(storage, api);
    await first.ask("q");
    const second = createChatStore(storage, api);
    expect(second.getState().sessionId).toBe(first.getState().sessionId);
    expect(second.getState().messages).toHaveLength(2);
  });

  it("reset() clears the transcript but keeps the attached file", async () => {
    const api = fakeApi(async (emit) => emit({ type: "done", kind: "rag", answer: "r", sources: [], topScore: 1 }));
    const store = createChatStore(new MemoryStorage(), api);
    await store.attach(new File(["# Nota"], "nota.md"));
    await store.ask("q");
    store.reset();
    expect(store.getState().messages).toEqual([]);
    expect(store.getState().upload?.title).toBe("nota");
  });

  it("attach replaces a previous file and detach deletes the session chunks", async () => {
    const api = fakeApi(async () => {});
    const store = createChatStore(new MemoryStorage(), api);
    await store.attach(new File(["a"], "a.md"));
    expect(api.deleteSession).not.toHaveBeenCalled();
    await store.attach(new File(["b"], "b.txt"));
    expect(api.deleteSession).toHaveBeenCalledTimes(1);
    expect(store.getState().upload?.title).toBe("b");
    await store.detach();
    expect(api.deleteSession).toHaveBeenCalledTimes(2);
    expect(store.getState().upload).toBeNull();
  });

  it("reports upload errors without dropping the current file", async () => {
    const api = fakeApi(async () => {});
    const store = createChatStore(new MemoryStorage(), api);
    api.upload.mockRejectedValueOnce(new Error("Solo se aceptan archivos PDF"));
    await store.attach(new File(["x"], "x.md"));
    expect(store.getState()).toMatchObject({ upload: null, uploading: false, uploadError: "Solo se aceptan archivos PDF" });
  });
});

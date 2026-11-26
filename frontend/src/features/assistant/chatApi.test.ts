import { afterEach, describe, expect, it, vi } from "vitest";
import { chatApi, createSseParser, INTERRUPTED_MESSAGE, type ChatEvent } from "./chatApi";

const sse = (event: string, data: unknown) => `event: ${event}\ndata: ${JSON.stringify(data)}\n\n`;

function streamResponse(chunks: string[], init: ResponseInit = {}) {
  const enc = new TextEncoder();
  return new Response(
    new ReadableStream({
      start(c) {
        chunks.forEach((ch) => c.enqueue(enc.encode(ch)));
        c.close();
      },
    }),
    { status: 200, headers: { "Content-Type": "text/event-stream" }, ...init }
  );
}

afterEach(() => vi.unstubAllGlobals());

describe("createSseParser", () => {
  it("reassembles events split mid-line and mid-separator", () => {
    const raw = sse("token", { text: "Hola" }) + sse("token", { text: " mundo" });
    const parse = createSseParser();
    const events: ChatEvent[] = [];
    for (const piece of [raw.slice(0, 7), raw.slice(7, 30), raw.slice(30, 41), raw.slice(41)]) events.push(...parse(piece));
    expect(events).toEqual([
      { type: "token", text: "Hola" },
      { type: "token", text: " mundo" },
    ]);
  });

  it("returns nothing until the blank line arrives", () => {
    const parse = createSseParser();
    expect(parse('event: token\ndata: {"text":"a"}\n')).toEqual([]);
    expect(parse("\n")).toEqual([{ type: "token", text: "a" }]);
  });

  it("handles CRLF line endings even when the pair is split across chunks", () => {
    const parse = createSseParser();
    expect(parse('event: token\r\ndata: {"text":"x"}\r')).toEqual([]);
    expect(parse("\n\r\n")).toEqual([{ type: "token", text: "x" }]);
  });

  it("parses sources, done and error payloads and skips comments and unknown events", () => {
    const parse = createSseParser();
    const source = { n: 1, docId: "d", title: "T", section: "S", url: "/grafo", score: 0.8, fromSession: false };
    const events = parse(
      ": keep-alive\n\n" +
        sse("sources", { kind: "rag", sources: [source] }) +
        sse("ping", {}) +
        sse("done", { kind: "rag", answer: "R [1]", sources: [source], topScore: 0.8 }) +
        sse("error", { message: "falló" })
    );
    expect(events).toEqual([
      { type: "sources", kind: "rag", sources: [source] },
      { type: "done", kind: "rag", answer: "R [1]", sources: [source], topScore: 0.8 },
      { type: "error", message: "falló" },
    ]);
  });

  it("does not split multi-byte characters across network chunks", async () => {
    const bytes = new TextEncoder().encode(sse("token", { text: "ñandú" }) + sse("done", { kind: "rag", answer: "ñandú", sources: [], topScore: 1 }));
    const cut = bytes.indexOf(0xc3) + 1; // inside the two-byte "ñ"
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(new ReadableStream({ start(c) { c.enqueue(bytes.slice(0, cut)); c.enqueue(bytes.slice(cut)); c.close(); } })))
    );
    const seen: ChatEvent[] = [];
    await chatApi.ask({ question: "q", history: [] }, (e) => seen.push(e));
    expect(seen[0]).toEqual({ type: "token", text: "ñandú" });
  });
});

describe("chatApi.ask", () => {
  it("posts the question and history as JSON", async () => {
    const fetchMock = vi.fn(async () => streamResponse([sse("done", { kind: "rag", answer: "", sources: [], topScore: 0 })]));
    vi.stubGlobal("fetch", fetchMock);
    await chatApi.ask({ question: "¿Qué?", sessionId: "abcdefgh", history: [{ role: "user", content: "a" }] }, () => {});
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toMatch(/\/api\/chat\/ask$/);
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body as string)).toEqual({ question: "¿Qué?", sessionId: "abcdefgh", history: [{ role: "user", content: "a" }] });
  });

  it("uses the server message on 429", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ error: "Espere un minuto." }), { status: 429 })));
    await expect(chatApi.ask({ question: "q", history: [] }, () => {})).rejects.toThrow("Espere un minuto.");
  });

  it("uses a plain-text 400 body as the message", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("La pregunta es demasiado larga.", { status: 400 })));
    await expect(chatApi.ask({ question: "q", history: [] }, () => {})).rejects.toThrow("La pregunta es demasiado larga.");
  });

  it("fails when the stream closes without done or error", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => streamResponse([sse("token", { text: "a" })])));
    await expect(chatApi.ask({ question: "q", history: [] }, () => {})).rejects.toThrow(INTERRUPTED_MESSAGE);
  });
});

import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from "react";
import { useApi } from "../../shared/useApi";
import { formatInt } from "../../shared/format";
import { AssistantMessage } from "./Answer";
import { chatApi, type SessionUpload } from "./chatApi";
import { useChat } from "./chatStore";

const STARTERS = [
  { topic: "Objetivos", question: "¿Cuáles son los objetivos general y específicos del proyecto?" },
  { topic: "Equipo", question: "¿Quiénes integran el equipo de investigación y qué rol cumple cada uno?" },
  { topic: "Métodos", question: "¿Cómo se usan FDA, Climatol y B-splines en el proyecto?" },
  { topic: "Estaciones y series", question: "¿Qué estaciones y series hidrometeorológicas se analizan?" },
  { topic: "Calidad de datos", question: "¿Qué problemas de calidad se detectaron en las series del INAMHI?" },
];

const ACCEPT = ".pdf,.md,.txt,application/pdf,text/markdown,text/plain";
const MAX_BYTES = 10 * 1024 * 1024;

const time = (iso: string) => new Date(iso).toLocaleTimeString("es-EC", { hour: "2-digit", minute: "2-digit", hour12: false });

/** Pages and a river line crossing them: the corpus the assistant reads from. */
function CorpusMark() {
  return (
    <svg viewBox="0 0 120 64" className="h-16 w-32 text-river" aria-hidden>
      {[0, 1, 2].map((i) => (
        <g key={i} transform={`translate(${14 + i * 30} ${10 - i * 3})`}>
          <rect width="26" height="34" rx="1" fill="#fff" stroke="#D3DCDB" />
          <path d={`M5 9 H21 M5 14 H19 M5 19 H${16 + i * 2}`} stroke="#A9BEC4" strokeWidth="1.4" />
        </g>
      ))}
      <path d="M2 54 H22 C30 54 32 30 40 30 C50 30 56 50 118 52" fill="none" stroke="currentColor" strokeWidth="2.2" />
      <rect x="37" y="23" width="9" height="9" rx="1" fill="#0B3A53" />
      <text x="41.5" y="30" textAnchor="middle" fontSize="7" fontWeight="700" fill="#fff">
        1
      </text>
    </svg>
  );
}

function UploadChip({ upload, busy, onRemove }: { upload: SessionUpload; busy: boolean; onRemove: () => void }) {
  return (
    <div className="flex items-center gap-2 rounded-sm border border-ochre/40 bg-ochre/10 py-1 pl-2.5 pr-1 text-sm">
      <svg viewBox="0 0 12 14" className="h-3.5 w-3 shrink-0 text-ochre" aria-hidden>
        <path d="M1 1 H8 L11 4 V13 H1 Z M8 1 V4 H11" fill="none" stroke="currentColor" strokeWidth="1.3" />
      </svg>
      <span className="min-w-0">
        <span className="block truncate font-semibold">{upload.title}</span>
        <span className="block text-xs text-muted num">
          {upload.chunks} {upload.chunks === 1 ? "fragmento" : "fragmentos"} · disponible hasta las {time(upload.expiresAt)}
        </span>
      </span>
      <button
        type="button"
        onClick={onRemove}
        disabled={busy}
        aria-label={`Quitar el archivo ${upload.title}`}
        className="ml-auto rounded-sm px-2 py-1 text-muted hover:bg-sheet hover:text-rust disabled:opacity-50"
      >
        <svg viewBox="0 0 10 10" className="h-2.5 w-2.5" aria-hidden>
          <path d="M1 1 L9 9 M9 1 L1 9" stroke="currentColor" strokeWidth="1.6" />
        </svg>
      </button>
    </div>
  );
}

function IndexStatus() {
  const health = useApi(chatApi.health, []);
  const h = health.data;
  if (health.error) return <p className="text-sm text-rust">{health.error}</p>;
  if (!h) return <div className="h-12 animate-pulse rounded-sm bg-grid/50" aria-label="Cargando estado del índice" />;
  return (
    <dl className="grid grid-cols-2 gap-px border border-grid bg-grid text-sm">
      <div className="bg-sheet px-3 py-2">
        <dt className="text-xs text-muted">Fragmentos indexados</dt>
        <dd className="type-display text-xl num">{formatInt(h.chunks)}</dd>
      </div>
      <div className="bg-sheet px-3 py-2">
        <dt className="text-xs text-muted">Índice</dt>
        <dd className="font-semibold">
          {h.indexing ? "Actualizando…" : h.indexedAt ? new Date(h.indexedAt).toLocaleDateString("es-EC") : "Sin datos"}
        </dd>
      </div>
    </dl>
  );
}

export function AssistantPage() {
  const { messages, upload, uploading, uploadError, streaming, store } = useChat();
  const [draft, setDraft] = useState("");
  const [fileError, setFileError] = useState<string | null>(null);
  const input = useRef<HTMLTextAreaElement>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const scroller = useRef<HTMLDivElement>(null);
  const pinned = useRef(true);

  useEffect(() => input.current?.focus(), []);

  // Follow the stream only while the reader is at the bottom; scrolling up stops it.
  useEffect(() => {
    const el = scroller.current;
    if (el && pinned.current) el.scrollTop = el.scrollHeight;
  }, [messages]);

  useEffect(() => {
    if (!upload) return;
    const ms = Date.parse(upload.expiresAt) - Date.now();
    const t = setTimeout(() => store.expire(), Math.max(0, Math.min(ms, 2 ** 31 - 1)));
    return () => clearTimeout(t);
  }, [upload, store]);

  const send = (question: string) => {
    if (!question.trim() || streaming) return;
    pinned.current = true;
    setDraft("");
    void store.ask(question);
    input.current?.focus();
  };

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    send(draft);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault();
      send(draft);
    }
  };

  const onFile = (file: File | undefined) => {
    setFileError(null);
    if (!file) return;
    if (!/\.(pdf|md|txt)$/i.test(file.name)) return setFileError("Solo se aceptan archivos PDF, Markdown (.md) o texto (.txt).");
    if (file.size > MAX_BYTES) return setFileError("El archivo supera el tamaño máximo de 10 MB.");
    void store.attach(file);
  };

  const newConversation = () => {
    store.reset();
    setDraft("");
    input.current?.focus();
  };

  const last = messages.at(-1);
  const status = streaming
    ? "El asistente está redactando la respuesta."
    : last?.role === "assistant"
      ? last.status === "error"
        ? `Error: ${last.error}`
        : last.kind === "abstained"
          ? "Respuesta sin respaldo documental."
          : "Respuesta lista."
      : "";

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="max-w-4xl space-y-3">
          <p className="type-condensed text-sm font-bold uppercase tracking-wider text-river">Asistente documental</p>
          <h1 className="type-display text-4xl">Pregunte a los documentos del proyecto</h1>
          <p className="max-w-[68ch] text-muted">
            Responde solo con lo que consta en la propuesta, el repositorio, el grafo de conocimiento y el catálogo de
            estaciones, y cita cada afirmación. Si no encuentra respaldo, lo dice.
          </p>
        </div>
        <button type="button" className="btn-quiet" onClick={newConversation} disabled={!messages.length && !streaming}>
          Nueva conversación
        </button>
      </header>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_19rem]">
        <section aria-label="Conversación" className="panel flex h-[max(30rem,calc(100dvh-20rem))] flex-col">
          <div
            ref={scroller}
            onScroll={(e) => {
              const el = e.currentTarget;
              pinned.current = el.scrollHeight - el.scrollTop - el.clientHeight < 48;
            }}
            className="flex-1 overflow-y-auto px-4 py-5 sm:px-6"
          >
            {messages.length === 0 ? (
              <div className="mx-auto flex max-w-2xl flex-col items-start gap-5 py-6">
                <CorpusMark />
                <div className="space-y-1">
                  <h2 className="text-xl font-semibold">¿Por dónde empezar?</h2>
                  <p className="text-sm text-muted">Elija una pregunta o escriba la suya.</p>
                </div>
                <ul className="grid w-full gap-px border border-grid bg-grid sm:grid-cols-2">
                  {STARTERS.map((s) => (
                    <li key={s.topic} className="bg-sheet last:sm:col-span-2">
                      <button
                        type="button"
                        onClick={() => send(s.question)}
                        className="group h-full w-full p-4 text-left hover:bg-paper"
                      >
                        <span className="type-condensed block text-xs font-bold uppercase tracking-wider text-river">
                          {s.topic}
                        </span>
                        <span className="mt-1 block leading-snug group-hover:text-river">{s.question}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            ) : (
              <ol role="log" aria-live="polite" aria-busy={streaming} aria-label="Mensajes" className="mx-auto max-w-3xl space-y-5">
                {messages.map((m) =>
                  m.role === "user" ? (
                    <li key={m.id} className="flex justify-end">
                      <p className="max-w-[85%] whitespace-pre-wrap rounded-sm bg-deep px-4 py-2.5 text-white">{m.content}</p>
                    </li>
                  ) : (
                    <li key={m.id}>
                      <AssistantMessage message={m} />
                    </li>
                  )
                )}
              </ol>
            )}
          </div>

          <form onSubmit={onSubmit} className="space-y-2 border-t border-grid bg-paper/60 p-3 sm:p-4">
            {upload && <UploadChip upload={upload} busy={uploading} onRemove={() => void store.detach()} />}
            {(fileError || uploadError) && (
              <p role="alert" className="text-sm text-rust">
                {fileError ?? uploadError}
              </p>
            )}
            <div className="flex items-end gap-2">
              <input
                ref={fileInput}
                type="file"
                accept={ACCEPT}
                className="hidden"
                tabIndex={-1}
                aria-hidden
                onChange={(e) => {
                  onFile(e.target.files?.[0]);
                  e.target.value = "";
                }}
              />
              <button
                type="button"
                onClick={() => fileInput.current?.click()}
                disabled={uploading}
                title="Adjuntar archivo propio (PDF, .md o .txt, hasta 10 MB)"
                aria-label="Adjuntar archivo propio"
                className="btn-quiet h-11 shrink-0 px-3"
              >
                {uploading ? (
                  <span className="h-4 w-4 animate-spin rounded-full border-2 border-river border-t-transparent" aria-hidden />
                ) : (
                  <svg viewBox="0 0 16 16" className="h-4 w-4" aria-hidden>
                    <path
                      d="M10.5 4.5 L5 10 A1.8 1.8 0 0 0 7.5 12.5 L13 7 A3.2 3.2 0 0 0 8.5 2.5 L3 8 A4.4 4.4 0 0 0 9.2 14.2 L13.5 10"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="1.4"
                      strokeLinecap="round"
                    />
                  </svg>
                )}
                <span className="sr-only">{uploading ? "Procesando archivo…" : ""}</span>
              </button>
              <label htmlFor="asistente-pregunta" className="sr-only">
                Pregunta
              </label>
              <textarea
                id="asistente-pregunta"
                ref={input}
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                onKeyDown={onKeyDown}
                rows={1}
                maxLength={1000}
                placeholder="Escriba su pregunta sobre el proyecto…"
                aria-describedby="asistente-ayuda"
                className="field max-h-40 min-h-11 flex-1 resize-none py-2.5 text-[15px] [field-sizing:content]"
              />
              {streaming ? (
                <button type="button" onClick={() => store.stop()} className="btn-quiet h-11 shrink-0 border-rust/40 text-rust">
                  <span className="h-2.5 w-2.5 bg-rust" aria-hidden />
                  Detener
                </button>
              ) : (
                <button className="btn-primary h-11 shrink-0" disabled={!draft.trim()}>
                  Enviar
                </button>
              )}
            </div>
            <p id="asistente-ayuda" className="text-xs text-muted">
              Enter envía · Shift + Enter agrega una línea
            </p>
          </form>
        </section>

        <aside className="space-y-6 text-sm">
          <section className="space-y-2" aria-labelledby="archivo-propio">
            <h2 id="archivo-propio" className="type-condensed text-xs font-bold uppercase tracking-wider text-river">
              Archivo propio
            </h2>
            <p className="text-muted">
              Puede adjuntar un PDF, Markdown o texto (hasta 10 MB) para consultarlo junto con los documentos del
              proyecto.
            </p>
            <p className="border-l-2 border-ochre bg-sheet px-3 py-2">
              El archivo es <strong>temporal</strong>: solo lo usa esta sesión, se elimina al vencer o al quitarlo y{" "}
              <strong>no se agrega al repositorio</strong>. Un archivo nuevo reemplaza al anterior.
            </p>
          </section>
          <section className="space-y-2" aria-labelledby="indice">
            <h2 id="indice" className="type-condensed text-xs font-bold uppercase tracking-wider text-river">
              Índice documental
            </h2>
            <IndexStatus />
          </section>
          <section className="space-y-2" aria-labelledby="como-leer">
            <h2 id="como-leer" className="type-condensed text-xs font-bold uppercase tracking-wider text-river">
              Cómo leer las respuestas
            </h2>
            <ul className="space-y-2 text-muted">
              <li className="flex gap-2">
                <span className="type-condensed inline-flex h-[1.15rem] min-w-[1.15rem] items-center justify-center rounded-sm bg-deep text-[11px] font-bold text-white">
                  2
                </span>
                Cada cifra remite a una fuente; púlsela para verla en la lista.
              </li>
              <li className="flex gap-2">
                <span className="mt-1.5 h-0.5 w-[1.15rem] shrink-0 bg-ochre" aria-hidden />
                Borde ocre: no hubo respaldo documental suficiente.
              </li>
            </ul>
          </section>
        </aside>
      </div>

      <p className="sr-only" aria-live="polite">
        {status}
      </p>
    </div>
  );
}

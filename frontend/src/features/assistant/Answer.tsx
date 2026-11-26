import { useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../../shared/api";
import { citedNumbers, parseBlocks, parseInline } from "./answerText";
import type { ChatSource } from "./chatApi";
import type { ChatMessage } from "./chatStore";

const FILE_PREFIX = "/files/";

function SourceLink({ source }: { source: ChatSource }) {
  const { url, title } = source;
  if (!url) return <span className="font-semibold">{title}</span>;
  if (url.startsWith(FILE_PREFIX) || /^https?:/.test(url))
    return (
      <a
        href={url.startsWith(FILE_PREFIX) ? api.fileUrl(url.slice(FILE_PREFIX.length)) : url}
        target="_blank"
        rel="noopener noreferrer"
        className="font-semibold text-river underline decoration-river/30 underline-offset-2 hover:decoration-river"
      >
        {title}
      </a>
    );
  return (
    <Link to={url} className="font-semibold text-river underline decoration-river/30 underline-offset-2 hover:decoration-river">
      {title}
    </Link>
  );
}

function Rich({ text, onCite, sources }: { text: string; onCite: (n: number) => void; sources: ChatSource[] }) {
  return (
    <>
      {parseInline(text).map((t, i) => {
        if (t.kind === "text") return <span key={i}>{t.text}</span>;
        if (t.kind === "bold")
          return (
            <strong key={i} className="font-semibold">
              {t.text}
            </strong>
          );
        const source = sources.find((s) => s.n === t.n);
        return (
          <button
            key={i}
            type="button"
            onClick={() => onCite(t.n)}
            disabled={!source}
            title={source ? `${source.title} · ${source.section}` : "Fuente no disponible"}
            aria-label={source ? `Fuente ${t.n}: ${source.title}` : `Cita ${t.n}`}
            className="type-condensed mx-0.5 inline-flex h-[1.15rem] min-w-[1.15rem] -translate-y-px items-center justify-center rounded-sm bg-deep px-1 align-middle text-[11px] font-bold leading-none text-white num transition-colors hover:bg-river disabled:bg-grid disabled:text-muted"
          >
            {t.n}
          </button>
        );
      })}
    </>
  );
}

function Body({ content, sources, onCite }: { content: string; sources: ChatSource[]; onCite: (n: number) => void }) {
  return (
    <div className="space-y-3 leading-relaxed">
      {parseBlocks(content).map((b, i) =>
        b.kind === "paragraph" ? (
          <p key={i}>
            <Rich text={b.text} sources={sources} onCite={onCite} />
          </p>
        ) : b.ordered ? (
          <ol key={i} className="list-decimal space-y-1.5 pl-5 marker:text-river">
            {b.items.map((item, j) => (
              <li key={j} className="pl-1">
                <Rich text={item} sources={sources} onCite={onCite} />
              </li>
            ))}
          </ol>
        ) : (
          <ul key={i} className="list-[square] space-y-1.5 pl-5 marker:text-river">
            {b.items.map((item, j) => (
              <li key={j} className="pl-1">
                <Rich text={item} sources={sources} onCite={onCite} />
              </li>
            ))}
          </ul>
        )
      )}
    </div>
  );
}

function Sources({ id, sources, cited, active }: { id: string; sources: ChatSource[]; cited: Set<number>; active: number | null }) {
  return (
    <details open className="group mt-5 border-t border-grid pt-3" id={`${id}-fuentes`}>
      <summary className="type-condensed flex cursor-pointer list-none items-center gap-2 text-xs font-bold uppercase tracking-wider text-muted hover:text-ink">
        <span aria-hidden className="inline-block transition-transform group-open:rotate-90">
          ›
        </span>
        {sources.length} {sources.length === 1 ? "fuente consultada" : "fuentes consultadas"}
      </summary>
      <ol className="mt-2 divide-y divide-grid">
        {sources.map((s) => (
          <li
            key={s.n}
            id={`${id}-s${s.n}`}
            tabIndex={-1}
            className={`grid grid-cols-[1.75rem_1fr_auto] items-baseline gap-2 py-2 text-sm transition-colors focus:outline-none ${
              active === s.n ? "bg-river/10" : ""
            }`}
          >
            <span
              className={`type-condensed text-center text-xs font-bold num ${cited.has(s.n) ? "text-river" : "text-muted"}`}
            >
              [{s.n}]
            </span>
            <span className="min-w-0">
              <SourceLink source={s} />
              {s.fromSession && (
                <span className="type-condensed ml-2 rounded-sm bg-ochre/15 px-1.5 py-0.5 text-[11px] font-bold uppercase tracking-wide text-ochre">
                  Su archivo
                </span>
              )}
              <span className="block text-xs text-muted">{s.section}</span>
            </span>
            <span className="text-xs text-muted num" title="Similitud con la pregunta">
              {Math.round(s.score * 100)} %
            </span>
          </li>
        ))}
      </ol>
    </details>
  );
}

export function AssistantMessage({ message }: { message: ChatMessage }) {
  const [active, setActive] = useState<number | null>(null);
  const sources = message.sources ?? [];
  const streaming = message.status === "streaming";
  const abstained = message.kind === "abstained";

  const onCite = (n: number) => {
    setActive(n);
    const details = document.getElementById(`${message.id}-fuentes`) as HTMLDetailsElement | null;
    if (details) details.open = true;
    const item = document.getElementById(`${message.id}-s${n}`);
    item?.scrollIntoView({ block: "nearest", behavior: "smooth" });
    item?.focus({ preventScroll: true });
  };

  return (
    <article
      aria-label={abstained ? "Respuesta sin respaldo documental" : "Respuesta del asistente"}
      className={`panel border-l-[3px] px-5 py-4 ${abstained ? "border-l-ochre bg-[#FBF7EF]" : "border-l-river"}`}
    >
      {abstained && (
        <p className="type-condensed mb-2 flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-ochre">
          <svg viewBox="0 0 16 16" className="h-3.5 w-3.5" aria-hidden>
            <path d="M2 12 H14" stroke="currentColor" strokeWidth="1.6" strokeDasharray="2 2" fill="none" />
            <path d="M2 9 C5 9 6 4 8 4 C10 4 11 9 14 9" stroke="currentColor" strokeWidth="1.6" fill="none" />
          </svg>
          Sin respaldo documental
        </p>
      )}

      {message.content ? (
        <Body content={message.content} sources={sources} onCite={onCite} />
      ) : (
        streaming && (
          <p className="flex items-center gap-2 text-sm text-muted">
            <span className="inline-flex gap-1" aria-hidden>
              {[0, 1, 2].map((i) => (
                <span key={i} className="h-1.5 w-1.5 animate-pulse rounded-full bg-river" style={{ animationDelay: `${i * 160}ms` }} />
              ))}
            </span>
            {sources.length ? `Redactando a partir de ${sources.length} fragmentos…` : "Buscando en los documentos del proyecto…"}
          </p>
        )
      )}
      {streaming && message.content && <span aria-hidden className="ml-0.5 inline-block h-4 w-[2px] animate-pulse bg-river align-middle" />}

      {abstained && !streaming && (
        <p className="mt-3 text-sm text-muted">
          Ningún documento del proyecto alcanzó la similitud mínima para responder con seguridad. Reformule la pregunta o
          adjunte un archivo propio.
        </p>
      )}

      {message.status === "stopped" && (
        <p className="mt-3 text-sm text-muted">Respuesta detenida antes de completarse.</p>
      )}
      {message.status === "error" && (
        <p role="alert" className="mt-3 border-l-2 border-rust bg-rust/5 px-3 py-2 text-sm text-rust">
          {message.error}
        </p>
      )}

      {sources.length > 0 && !streaming && (
        <Sources id={message.id} sources={sources} cited={new Set(citedNumbers(message.content))} active={active} />
      )}
    </article>
  );
}

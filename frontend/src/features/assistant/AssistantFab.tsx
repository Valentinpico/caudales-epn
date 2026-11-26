import { Link, useLocation } from "react-router-dom";

/** Floating shortcut to the assistant on every page except its own. */
export function AssistantFab() {
  const { pathname } = useLocation();
  if (pathname.startsWith("/asistente")) return null;
  return (
    <Link
      to="/asistente"
      aria-label="Abrir el asistente documental"
      className="fixed bottom-5 right-5 z-40 flex items-center gap-2.5 rounded-sm bg-deep py-2.5 pl-3 pr-4 text-sm font-semibold text-white shadow-[0_6px_20px_-6px_rgba(11,58,83,0.6)] transition hover:-translate-y-0.5 hover:bg-river"
    >
      <svg viewBox="0 0 28 22" className="h-5 w-6" aria-hidden>
        <path d="M2 2 H26 V16 H11 L6 20 V16 H2 Z" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
        <path d="M6 12 H9 C11 12 11.5 5.5 13.5 5.5 C15.5 5.5 16.5 11 22 11.5" fill="none" stroke="#A9D3E0" strokeWidth="1.6" />
      </svg>
      <span className="hidden sm:inline">Asistente</span>
    </Link>
  );
}

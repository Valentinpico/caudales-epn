import { useRef, useState } from "react";
import { BrowserRouter, NavLink, Route, Routes } from "react-router-dom";
import { AdminProvider, useAdmin } from "./shared/admin";
import { OverviewPage } from "./features/overview/OverviewPage";
import { StationsPage } from "./features/stations/StationsPage";
import { QualityPage } from "./features/quality/QualityPage";
import { RepositoryPage } from "./features/repository/RepositoryPage";
import { GraphPage } from "./features/graph/GraphPage";

const NAV = [
  { to: "/", label: "Resumen" },
  { to: "/estaciones", label: "Estaciones" },
  { to: "/calidad", label: "Calidad de datos" },
  { to: "/repositorio", label: "Repositorio" },
  { to: "/grafo", label: "Grafo de conocimiento" },
];

function Mark() {
  // A hydrograph: fast rise, slow recession.
  return (
    <svg viewBox="0 0 32 20" className="h-5 w-8" aria-hidden>
      <path d="M1 18 H6 C9 18 10 3 13 3 C16 3 18 14 31 17" fill="none" stroke="currentColor" strokeWidth="2.2" />
    </svg>
  );
}

function AdminButton() {
  const { isAdmin, login, logout } = useAdmin();
  const dialog = useRef<HTMLDialogElement>(null);
  const [error, setError] = useState<string | null>(null);

  if (isAdmin)
    return (
      <button className="btn-quiet" onClick={logout}>
        Salir de administración
      </button>
    );

  return (
    <>
      <button className="btn-quiet" onClick={() => dialog.current?.showModal()}>
        Administrar
      </button>
      <dialog ref={dialog} className="panel w-80 p-5 backdrop:bg-ink/40">
        <form
          method="dialog"
          onSubmit={async (e) => {
            e.preventDefault();
            const password = new FormData(e.currentTarget).get("password") as string;
            if (await login(password)) dialog.current?.close();
            else setError("Contraseña incorrecta.");
          }}
          className="space-y-3"
        >
          <h2 className="text-lg font-semibold">Ingresar como administrador</h2>
          <p className="text-sm text-muted">Necesario para cargar o eliminar documentos del repositorio.</p>
          <input name="password" type="password" autoFocus required className="field" aria-label="Contraseña" />
          {error && <p className="text-sm text-rust">{error}</p>}
          <div className="flex justify-end gap-2">
            <button type="button" className="btn-quiet" onClick={() => dialog.current?.close()}>
              Cancelar
            </button>
            <button className="btn-primary">Ingresar</button>
          </div>
        </form>
      </dialog>
    </>
  );
}

function Shell() {
  return (
    <div className="min-h-screen">
      <header className="border-b border-grid bg-sheet">
        <div className="mx-auto flex max-w-[1320px] flex-wrap items-center gap-x-8 gap-y-2 px-4 py-3 sm:px-6">
          <NavLink to="/" className="flex items-center gap-2 text-river">
            <Mark />
            <span className="type-display text-xl text-ink">Caudales</span>
          </NavLink>
          <nav className="order-3 -mx-1 flex w-full gap-1 overflow-x-auto text-sm lg:order-none lg:w-auto">
            {NAV.map((n) => (
              <NavLink
                key={n.to}
                to={n.to}
                end={n.to === "/"}
                className={({ isActive }) =>
                  `whitespace-nowrap rounded-sm px-2.5 py-1.5 ${
                    isActive ? "bg-paper font-semibold text-ink" : "text-muted hover:text-ink"
                  }`
                }
              >
                {n.label}
              </NavLink>
            ))}
          </nav>
          <div className="ml-auto">
            <AdminButton />
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-[1320px] px-4 pb-20 pt-8 sm:px-6">
        <Routes>
          <Route path="/" element={<OverviewPage />} />
          <Route path="/estaciones" element={<StationsPage />} />
          <Route path="/estaciones/:code" element={<StationsPage />} />
          <Route path="/calidad" element={<QualityPage />} />
          <Route path="/repositorio" element={<RepositoryPage />} />
          <Route path="/grafo" element={<GraphPage />} />
        </Routes>
      </main>
      <footer className="border-t border-grid">
        <p className="mx-auto max-w-[1320px] px-4 py-6 text-xs text-muted sm:px-6">
          Escuela Politécnica Nacional, Departamento de Matemática. Datos crudos del Instituto Nacional de
          Meteorología e Hidrología (INAMHI).
        </p>
      </footer>
    </div>
  );
}

export function App() {
  return (
    <AdminProvider>
      <BrowserRouter>
        <Shell />
      </BrowserRouter>
    </AdminProvider>
  );
}

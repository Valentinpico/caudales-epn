export function Loading({ what }: { what: string }) {
  return <p className="py-10 text-sm text-muted">Cargando {what}…</p>;
}

export function ErrorNote({ message }: { message: string }) {
  return (
    <p role="alert" className="border-l-2 border-rust bg-sheet px-3 py-2 text-sm text-rust">
      {message}. Verifique que la API esté en ejecución y vuelva a intentarlo.
    </p>
  );
}

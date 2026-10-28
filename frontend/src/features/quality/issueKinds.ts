export const issueKindLabel: Record<string, string> = {
  DuplicateMonth: "Mes duplicado",
  InvalidMonth: "Mes inválido",
  InvalidDay: "Día inexistente",
  Sentinel: "Valor centinela",
  OutOfRange: "Fuera de rango",
  Unparseable: "No numérico",
};

/** What each rule checks and what the load does about it. */
export const issueKindRule: Record<string, string> = {
  DuplicateMonth: "La misma estación y mes aparece más de una vez. Se conserva la primera fila y se descartan las repeticiones.",
  InvalidMonth: "El número de mes no está entre 1 y 12. La fila completa se descarta.",
  InvalidDay: "Hay un valor en un día que no existe en ese mes, por ejemplo 30 de febrero. El valor se descarta.",
  Sentinel: "La precipitación igual o mayor a 999,9 mm es un código de dato faltante. Se trata como vacío.",
  OutOfRange: "Precipitación sobre 300 mm diarios o temperatura fuera de 0,1 a 40 °C. El valor se conserva marcado como sospechoso para revisión.",
  Unparseable: "La celda contiene texto en lugar de un número. El valor se descarta.",
};

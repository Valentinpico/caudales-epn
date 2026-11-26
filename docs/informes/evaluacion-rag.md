# Evaluación del asistente documental (Producto 3)

> Evaluación de punta a punta del asistente de consulta documental del proyecto PISF-25-03:
> recuperación, generación, citas y abstención, medidas sobre la API real
> (`POST /api/chat/ask`) con el índice construido a partir de la documentación del proyecto.

## 1. Qué se midió y por qué

El asistente responde en tres pasos: (1) recupera los fragmentos más parecidos a la pregunta
en pgvector, (2) aplica un umbral mínimo de similitud (si ningún fragmento lo supera, se
abstiene sin llamar al modelo) y (3) genera una respuesta citada a partir de esos fragmentos;
si el modelo concluye que el contexto no respalda una respuesta, contesta con una marca
convenida (`[[SIN_RESPALDO]]`) y la respuesta se convierte en abstención.

A diferencia de una evaluación de solo recuperación, aquí se mide lo que recibe el usuario:

- si la respuesta contiene los datos que la documentación sí trae;
- si el asistente se abstiene cuando la pregunta no tiene respaldo, y solo entonces;
- si cada cita `[n]` apunta a una fuente que realmente se mostró;
- cuánto tarda la respuesta completa.

**Configuración de la corrida final:** proveedor `bedrock` (us-east-1), embeddings
`cohere.embed-multilingual-v3` (1024 dimensiones), generación
`us.anthropic.claude-haiku-4-5-20251001-v1:0`, 195 fragmentos indexados (postulación del
proyecto, informes técnico y mensual del Producto 1 y el catálogo proyectado desde la base de
datos), umbral `RAG__MINSCORE = 0.35`, `TopK = 8`, como máximo 3 fragmentos por sección.

## 2. Conjunto de preguntas

`scripts/rag_eval/preguntas.json` contiene **32 preguntas** en español, redactadas como las
haría un integrante del equipo o un evaluador, no copiadas del texto:

| Grupo | n | Contenido |
|---|---|---|
| En corpus | 21 | objetivo general y específicos, director y colaboradores externos, FDA, Bootstrap y umbrales de quiebre, Climatol, B-splines, clustering funcional, estaciones y período, variables, reglas y hallazgos de calidad, observaciones cargadas, repositorio, grafo, presupuesto, cronograma, ODS, tecnologías del Producto 1, publicaciones esperadas |
| Fuera de corpus | 6 | cuatro ajenas al proyecto (capital de Francia, Mundial 2022, receta, fotosíntesis) y dos cercanas al dominio pero sin respaldo (caudal medio del río Guayas, número de estudiantes de la EPN) |
| Seguimiento | 5 | tres que dependen del turno anterior (codirector, prueba estadística del paquete, origen de los datos) y dos fuera de tema formuladas como seguimiento de una pregunta válida, una larga y una corta |

Cada pregunta declara el tipo esperado (`rag` o `abstained`) y, si corresponde, los **hechos
clave** que la respuesta debe contener, como grupos de alternativas (por ejemplo
`["2232", "2 232", "2.232"]`). Las preguntas de seguimiento envían como historial la pregunta
previa y la **respuesta real** que obtuvo en la misma corrida.

## 3. Método

`scripts/rag_eval/run_eval.py` (solo biblioteca estándar de Python) envía cada pregunta a la
API, lee el flujo SSE (`sources`, `token`, `done`) y registra tipo de respuesta, puntaje
máximo, latencia total, latencia hasta el primer fragmento de texto, fuentes y respuesta. Las
consultas se espacian 3,5 s para no saturar la cuota de Bedrock; durante la corrida se elevó el
límite por IP con `RAG__RATELIMITPERMINUTE=60`.

```bash
cd backend && RAG__PROVIDER=bedrock RAG__RATELIMITPERMINUTE=60 dotnet run   # API local
python3 scripts/rag_eval/run_eval.py --api http://localhost:5000            # tras GET /api/chat/health con indexing=false
```

**Calificación.** Una respuesta es *correcta* si contiene todos los grupos de hechos clave,
*parcial* si contiene algunos e *incorrecta* si ninguno; una pregunta en corpus que recibe
abstención cuenta como *abstención indebida*. La calificación automática se revisó a mano
respuesta por respuesta en la corrida final para descartar coincidencias accidentales de
texto. Una cita es válida si su número existe en la lista de fuentes devuelta.

**Métricas de abstención.** Precisión = abstenciones correctas / abstenciones emitidas;
exhaustividad (recall) = abstenciones correctas / preguntas que debían abstenerse.

## 4. Resultados (corrida final)

Resultado completo: `scripts/rag_eval/resultados/corrida-3.json`.

| Métrica | Valor |
|---|---|
| Preguntas | 32 (24 respondibles, 8 sin respaldo) |
| Exactitud en preguntas respondibles | **24/24 correctas** (0 parciales, 0 incorrectas, 0 abstenciones indebidas) |
| Precisión de la abstención | **1,00** (8 de 8 abstenciones emitidas eran correctas) |
| Exhaustividad de la abstención | **1,00** (8 de 8 preguntas sin respaldo se abstuvieron) |
| Validez de citas | **24/24** respuestas con todas sus citas válidas |
| Latencia total, todas | mediana **1,61 s**, p95 **2,77 s** |
| Latencia total, respuestas con fuentes | mediana 1,87 s, p95 2,78 s |
| Latencia hasta el primer texto, respuestas con fuentes | mediana 0,89 s, máximo 1,96 s |
| Abstención por umbral (sin llamar al modelo) | 0,34–0,37 s |

### Distribución del puntaje máximo de similitud

| Grupo | n | mín. | mediana | máx. |
|---|---|---|---|---|
| En corpus (sin seguimiento) | 21 | 0,507 | 0,638 | 0,722 |
| Fuera de corpus (sin seguimiento) | 6 | 0,338 | 0,436 | 0,581 |

Umbral elegido: **0,35**. Las distribuciones se solapan: la pregunta cercana sin respaldo
«caudal medio anual del río Guayas» obtiene 0,581, por encima de cinco preguntas válidas. Ningún
umbral separa los dos grupos por sí solo, por eso la abstención tiene dos capas:

| Capa | Abstenciones | Preguntas |
|---|---|---|
| Umbral sobre la pregunta sola | 2 de 8 | o01 (0,338), f04 (0,349) |
| Marca del modelo `[[SIN_RESPALDO]]` | 6 de 8 | o02, o03, o04, o05, o06, f05 |

El umbral funciona como filtro barato de lo claramente ajeno (no consume generación); la
decisión fina la toma el modelo con el contexto a la vista. Con estos datos, un umbral de 0,45
seguiría sin abstenciones indebidas (el mínimo en corpus es 0,507) y ahorraría la generación
en 4 de las 6 preguntas fuera de corpus, pero el margen es de apenas 0,06 sobre un conjunto
pequeño; se mantiene 0,35 hasta contar con más preguntas reales.

### Resultados por pregunta

| id | tema | esperado | obtenido | puntaje | latencia (s) | calificación |
|---|---|---|---|---|---|---|
| c01 | objetivo general | rag | rag | 0,585 | 2,49 | correcta |
| c02 | objetivos específicos | rag | rag | 0,648 | 2,62 | correcta |
| c03 | director | rag | rag | 0,507 | 1,35 | correcta |
| c04 | colaboradores externos | rag | rag | 0,638 | 1,54 | correcta |
| c05 | FDA | rag | rag | 0,656 | 2,56 | correcta |
| c06 | umbrales Bootstrap | rag | rag | 0,597 | 1,94 | correcta |
| c07 | Climatol | rag | rag | 0,696 | 1,27 | correcta |
| c08 | B-splines | rag | rag | 0,607 | 1,70 | correcta |
| c09 | clustering funcional | rag | rag | 0,677 | 1,63 | correcta |
| c10 | estaciones y período | rag | rag | 0,695 | 2,35 | correcta |
| c11 | variables | rag | rag | 0,627 | 1,97 | correcta |
| c12 | reglas de calidad | rag | rag | 0,650 | 3,02 | correcta |
| c13 | hallazgos de calidad | rag | rag | 0,646 | 1,59 | correcta |
| c14 | observaciones diarias | rag | rag | 0,638 | 1,22 | correcta |
| c15 | repositorio | rag | rag | 0,720 | 2,21 | correcta |
| c16 | grafo | rag | rag | 0,686 | 1,24 | correcta |
| c17 | presupuesto | rag | rag | 0,551 | 1,07 | correcta |
| c18 | cronograma de caudales | rag | rag | 0,623 | 1,16 | correcta |
| c19 | ODS | rag | rag | 0,722 | 2,79 | correcta |
| c20 | tecnologías Producto 1 | rag | rag | 0,585 | 2,75 | correcta |
| c21 | publicaciones | rag | rag | 0,624 | 2,29 | correcta |
| o01 | capital de Francia | abstención | abstención (umbral) | 0,338 | 0,34 | correcta |
| o02 | Mundial 2022 | abstención | abstención (modelo) | 0,354 | 0,99 | correcta |
| o03 | receta | abstención | abstención (modelo) | 0,486 | 0,99 | correcta |
| o04 | fotosíntesis | abstención | abstención (modelo) | 0,429 | 1,73 | correcta |
| o05 | caudal del río Guayas | abstención | abstención (modelo) | 0,581 | 1,39 | correcta |
| o06 | estudiantes de la EPN | abstención | abstención (modelo) | 0,444 | 1,41 | correcta |
| f01 | ¿y el codirector? | rag | rag | 0,537 | 2,02 | correcta |
| f02 | ¿y qué prueba usa? | rag | rag | 0,674 | 1,25 | correcta |
| f03 | ¿y de qué archivos? | rag | rag | 0,715 | 1,79 | correcta |
| f04 | seguimiento fuera de tema (largo) | abstención | abstención (umbral) | 0,349 | 0,37 | correcta |
| f05 | seguimiento fuera de tema (corto) | abstención | abstención (modelo) | 0,716 | 0,84 | correcta |

En f05 el puntaje corresponde a la consulta ampliada con el turno anterior: al ser una pregunta
corta se le permite superar el umbral por esa vía, y es el modelo quien detecta que no hay
respaldo.

## 5. Fallos encontrados y cambios realizados

**Antes de la evaluación — abstención tras una pregunta válida.** Una pregunta fuera de tema
hecha después de una pregunta del proyecto no se marcaba como abstención: la consulta
embebida antepone la pregunta previa, lo que elevaba el puntaje por encima del umbral; el
modelo declinaba en prosa, pero la respuesta seguía marcada como `rag` y mostraba fuentes. Se
corrigió en dos capas: (a) el umbral se evalúa sobre el embedding de la pregunta sola (ambas
consultas se embeben en una única llamada) y la consulta ampliada solo ordena el contexto, salvo
en preguntas cortas de hasta cuatro palabras, que sí pueden superar el umbral con ella; (b) el
modelo responde `[[SIN_RESPALDO]]` cuando el contexto no lo respalda, el servicio retiene los
primeros fragmentos del flujo hasta saber si empiezan con esa marca y, si es así, emite una
abstención sin fuentes. Los casos f04 y f05 de este conjunto cubren ambas vías.

**Corrida 1 (`corrida-1.json`, TopK 6) — 22 correctas, 1 parcial, 1 abstención indebida;
precisión de abstención 0,89.** La pregunta c18 («¿En qué meses del cronograma se levantan
las series de caudal?») recibió abstención del modelo con puntaje 0,62. La causa estaba en la
recuperación: los seis fragmentos devueltos eran listas de estaciones de caudal del catálogo
(una misma sección partida en varios fragmentos casi idénticos), y la actividad del cronograma
(«Levantamiento de datos de caudal y detección de quiebres: año 1, meses 7–8») quedaba fuera. El
modelo hizo lo correcto con el contexto que recibió. La pregunta c05 (usos del FDA) fue parcial:
omitió el suavizamiento con bases de Fourier del OE1.

**Corrida 2 (`corrida-2.json`) — diversificación con 2 fragmentos por sección.** Se solicita a
la base el triple de candidatos y se conservan como máximo dos fragmentos por sección. c18 pasó
a correcta y la precisión de abstención a 1,00, pero c12 (reglas de calidad) quedó parcial: la
subsección 3.3 del informe técnico ocupa cuatro fragmentos y la tabla de reglas quedó fuera.

**Corrida 3 (`corrida-3.json`, final) — 3 fragmentos por sección y TopK 8.** Con un tope de tres
fragmentos por sección y ocho fragmentos en el contexto (unos 8 000 caracteres, dentro del límite
de 12 000), c12, c18 y c05 quedan correctas y no aparece ninguna regresión.

**Objetivo general (sección 5.1).** La sección 5.1 de la postulación aparece en las tres corridas,
aunque en el sexto lugar (0,534): el catálogo y el resumen del proyecto la superan. La respuesta
fue correcta en todos los casos, por lo que no se modificó el troceado de esa sección.

## 6. Limitaciones

- **Tamaño del conjunto.** 32 preguntas permiten detectar fallos evidentes, no estimar tasas
  con precisión: una exactitud de 24/24 es compatible con una tasa real de error de varios
  puntos porcentuales.
- **Una sola corrida por configuración.** La generación no es determinista; el puntaje de
  similitud sí lo es para un modelo de embeddings fijo. Las respuestas pueden variar entre
  corridas, especialmente en las abstenciones que decide el modelo.
- **Calificación por hechos clave.** La coincidencia de texto verifica que el dato aparezca,
  no que la redacción sea completa ni que no haya afirmaciones adicionales erróneas; por eso se
  revisaron las respuestas a mano. No se usó un modelo evaluador.
- **Ajuste con el mismo conjunto.** Los cambios de recuperación (tope por sección y TopK) se
  decidieron con fallos de este mismo conjunto. Están justificados por la causa observada
  (secciones largas que acaparan el contexto), pero conviene validarlos con preguntas nuevas.
- **Abstención dependiente del modelo.** Seis de las ocho abstenciones las decidió el modelo con
  la marca convenida. Si cambia el modelo de generación, hay que repetir la evaluación; un modelo
  que no respete la marca declinaría en prosa con fuentes visibles.
- **Corpus en evolución.** El catálogo se proyecta desde la base de datos e incluye ya series
  de caudal que los informes del Producto 1 describen como pendientes; algunas respuestas
  (por ejemplo, c10) combinan ambas fuentes. Las preguntas y los hechos esperados deben revisarse
  cuando se incorporen nuevos informes.
- **Latencia local.** Las latencias se midieron desde un equipo local contra Bedrock en
  us-east-1 y no incluyen la red del servidor de producción ni concurrencia.

## 7. Archivos

| Archivo | Contenido |
|---|---|
| `scripts/rag_eval/preguntas.json` | conjunto de 32 preguntas con tipo esperado y hechos clave |
| `scripts/rag_eval/run_eval.py` | ejecutor de la evaluación contra la API |
| `scripts/rag_eval/resultados/corrida-1.json` | corrida inicial (TopK 6, sin diversificación) |
| `scripts/rag_eval/resultados/corrida-2.json` | diversificación con 2 fragmentos por sección |
| `scripts/rag_eval/resultados/corrida-3.json` | corrida final (TopK 8, 3 fragmentos por sección) |

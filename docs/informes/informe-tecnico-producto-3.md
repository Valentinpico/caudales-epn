<!-- rag: exclude -->
# Sistema de Consulta Inteligente basado en RAG para Documentación, Metodologías, Datos y Resultados del Proyecto

**Metodología para la regionalización de caudales en Ecuador basada en el Análisis de Datos Funcionales y la homogeneización de variables hidrometeorológicas**

> Informe técnico — Producto 3 (PISF-25-03). Informe técnico de implementación de un sistema
> de consulta inteligente basado en RAG para documentación, metodologías, datos y resultados
> del proyecto. Los campos `[COMPLETAR: ...]` son lo único pendiente.

| | |
|---|---|
| **Código del proyecto** | PISF-25-03 |
| **Directora del proyecto** | Adriana Uquillas Andrade |
| **Codirector del proyecto** | Miguel Alfonso Flores Sánchez |
| **Servidor Público 3** | Kenig Valentín Pico Ponce |
| **Unidad** | Escuela Politécnica Nacional, Departamento de Matemática |
| **Período** | 01 al 31 de diciembre de 2026 |
| **Fecha de entrega** | [COMPLETAR: fecha] |

---

## Tabla de contenido

1. Introducción
2. Objetivos
3. Metodología e implementación
4. Resultados
5. Evaluación del sistema
6. Limitaciones y trabajo futuro
7. Conclusiones
8. Bibliografía
9. Anexos

---

## 1. Introducción

El presente informe describe el diseño e implementación de un **sistema de consulta
inteligente** (el asistente de la plataforma) incorporado a la plataforma web del proyecto *Metodología para la
regionalización de caudales en Ecuador basada en el Análisis de Datos Funcionales y la
homogeneización de variables hidrometeorológicas* (PISF-25-03), construido sobre la técnica
de **generación aumentada por recuperación** (*Retrieval-Augmented Generation*, RAG). El
sistema cubre cuatro ámbitos del proyecto: la **documentación** (informes, postulación y
repositorio digital), las **metodologías** (los métodos del grafo de conocimiento y los
descritos en la postulación), los **datos** (el catálogo de estaciones y series) y los
**resultados** (los del Producto 1 y la evaluación del propio sistema).

El Producto 1 dejó la información del proyecto organizada pero repartida en varios lugares:
la postulación describe los objetivos, la metodología, el cronograma y el presupuesto; los
informes técnico y mensual documentan la plataforma; el grafo de conocimiento relaciona
objetivos, actividades, métodos, equipo e instituciones; el repositorio digital cataloga los
documentos; y el módulo de series resume las estaciones y la completitud de cada registro.
Una pregunta concreta —«¿qué umbrales de quiebre usa la homogeneización?», «¿en qué meses se
levantan las series de caudal?», «¿quién codirige el proyecto?»— exigía saber de antemano en
qué documento o en qué vista buscar.

El Producto 3 resuelve esa dispersión con una interfaz de **pregunta en lenguaje natural**. El
asistente no responde de memoria: recupera los fragmentos pertinentes del corpus del proyecto,
se los entrega al modelo de lenguaje como único contexto y exige que cada afirmación **cite el
fragmento** del que proviene, con su documento y su sección. Cuando la pregunta no tiene
respaldo en ese corpus, el asistente **se abstiene de forma explícita** en lugar de improvisar,
que es la falla característica —y la más costosa en un proyecto científico— de un modelo de
lenguaje sin anclaje documental.

El diseño se apoya en cuatro decisiones que atraviesan todo el producto:

1. **El proveedor de modelos es un detalle de configuración.** El servicio de consulta depende
   de puertos (`IEmbedder`, `IChatModel`, `IChunkStore`) y no de un proveedor concreto. Por
   omisión usa **Amazon Bedrock** —embeddings multilingües de Cohere y generación con Claude
   Haiku 4.5— sobre la misma cuenta de AWS en la que ya se publicó la plataforma; **OpenAI
   directo** queda como alternativa que se activa con una variable de entorno.
2. **La abstención es una funcionalidad, no un error.** Se decide en dos capas: un umbral de
   similitud que descarta lo claramente ajeno **sin llamar al modelo de lenguaje**, y una marca
   convenida con la que el propio modelo declara que el contexto no respalda una respuesta.
3. **Ninguna infraestructura nueva.** La base vectorial es la misma instancia de PostgreSQL del
   Producto 1, ahora con la extensión `pgvector`, y el catálogo de la base de datos se proyecta
   como texto para que el asistente responda con los mismos datos que muestra la plataforma.
4. **Todo lo que sube un visitante es temporal.** Los archivos propios se indexan con alcance
   de sesión, solo se recuperan dentro de esa sesión y se eliminan al vencer su tiempo de vida;
   nunca se incorporan al corpus permanente ni al repositorio.

## 2. Objetivos

### 2.1. Objetivo general

Implementar un sistema de consulta inteligente basado en RAG que permita interrogar en lenguaje
natural la documentación, las metodologías, los datos y los resultados del proyecto PISF-25-03, con respuestas ancladas en el corpus del proyecto,
fuentes citadas de forma verificable y abstención explícita cuando la pregunta no tiene
respaldo documental.

### 2.2. Objetivos específicos

1. Construir una canalización de ingestión que convierta la documentación del proyecto —la
   postulación (disponible solo en PDF), los informes del Producto 1 y el catálogo de la base
   de datos— en fragmentos indexados con representaciones vectoriales.
2. Implementar la búsqueda semántica sobre `pgvector`, reutilizando la instancia de PostgreSQL
   del Producto 1.
3. Integrar Amazon Bedrock como proveedor por omisión de embeddings y de generación, y mantener
   OpenAI directo como alternativa configurable sin cambios de código.
4. Garantizar el anclaje documental mediante un umbral de similitud, una instrucción de sistema
   que prohíbe completar con conocimiento externo, una marca de no respaldo y la cita numerada
   de cada fragmento usado.
5. Entregar la respuesta en **flujo continuo**, para que la espera percibida no dependa de la
   longitud total de la respuesta.
6. Permitir la consulta **temporal** de archivos propios, aislada por sesión, con límites de
   tamaño, vencimiento automático y borrado a demanda.
7. Proteger la superficie pública y acotar el costo: límite de consultas por IP, puerta de
   administración, aislamiento de sesiones y defensa ante instrucciones incrustadas en los
   documentos.
8. Evaluar el sistema de punta a punta con un conjunto de preguntas de referencia y medir
   exactitud, abstención, validez de citas y latencia.

## 3. Metodología e implementación

### 3.1 Arquitectura de la solución

El asistente se integró **dentro de la misma plataforma** del Producto 1, sin servicios
adicionales en el despliegue. La solución añade a la arquitectura existente las piezas de la
Tabla 1.

| Componente | Rol | Ubicación |
|---|---|---|
| Extensión `pgvector` | Almacenamiento de vectores y búsqueda por similitud del coseno | Imagen `pgvector/pgvector:pg16` (PostgreSQL 16) |
| Capa de aplicación RAG | Corpus, fragmentación, recuperación, instrucción y sesiones | `backend/Application/Rag` |
| Adaptadores de infraestructura | Bedrock, OpenAI, almacén `pgvector` y tareas en segundo plano | `backend/Infrastructure/Rag` |
| Endpoints y puerta de administración | Rutas `/api/chat`, límite por IP y control de mutaciones | `backend/Api/ChatEndpoints.cs`, `backend/Api/AdminGate.cs` |
| Interfaz de consulta | Página `/asistente`, botón flotante y transcripción con fuentes | `frontend/src/features/assistant` |
| Extractor de PDF | Conversión fuera de línea de la postulación a Markdown | `scripts/corpus_pdf` |
| Evaluación | Conjunto de preguntas, ejecutor y resultados | `scripts/rag_eval`, `docs/informes/evaluacion-rag.md` |

*Tabla 1. Componentes añadidos a la plataforma.*

La separación en capas sigue la del Producto 1: los *endpoints* validan la petición y delegan;
los servicios de aplicación (`ChatService`, `CorpusService`, `SessionFileService`) concentran
la lógica; el acceso a modelos y datos vive detrás de puertos. Las funciones que deciden el
comportamiento —fragmentación, umbral, diversificación, construcción de la instrucción y
detección de la marca de no respaldo— son **funciones puras** (`MarkdownChunker`,
`RagPrompt`), verificables sin red ni base de datos.

![Arquitectura del asistente documental: el navegador envía la pregunta por POST a la API .NET 9 y recibe la respuesta en flujo (SSE); la API, con sus endpoints, puerta de administración y servicios de consulta, corpus, sesiones e indexado, obtiene vectores y respuestas de Amazon Bedrock (u OpenAI como alternativa), consulta PostgreSQL con pgvector y lee el corpus Markdown.](../diagramas/arquitectura-asistente.svg)

*Figura 1. Arquitectura del asistente documental.*

El flujo de una consulta es el siguiente:

```
Pregunta del visitante (POST /api/chat/ask)
   ↓
Validación (pregunta no vacía, hasta 1 000 caracteres; identificador de sesión)
   ↓
Embedding de la pregunta (y de la pregunta ampliada con el turno anterior, en la misma llamada)
   ↓
Búsqueda por coseno en pgvector: corpus permanente + archivos de la sesión
   ↓
Diversificación (máx. 3 fragmentos por sección, 8 en total)
   ↓
Umbral sobre la pregunta sola ── nada lo supera ──→ abstención, SIN llamar al modelo
   │ hay fragmentos suficientes
   ↓
Evento «sources» con las fuentes numeradas
   ↓
Instrucción de sistema + fragmentos delimitados como datos → modelo en flujo continuo
   ↓
¿Empieza con [[SIN_RESPALDO]]? ── sí ──→ se corta la generación y se emite abstención
   │ no
   ↓
Eventos «token» y evento final «done» con la respuesta, el tipo y las fuentes
```

### 3.2 Corpus documental e ingestión

El corpus permanente tiene **dos orígenes** que llegan a la ingestión con la misma forma
(documento, título, enlace y Markdown):

1. **Archivos Markdown** de las raíces configuradas (`docs/informes` y `docs/corpus` por
   omisión): los informes técnico y mensual del Producto 1 y la postulación del proyecto.
2. **El catálogo de la base de datos, proyectado a Markdown** (`CatalogProjection`), en tres
   documentos:
   - **Catálogo del proyecto (grafo de conocimiento):** una sección por tipo de nodo
     —proyecto, objetivos específicos, actividades del cronograma, métodos, equipo,
     instituciones, líneas de investigación, ODS, variables y conjuntos de datos—, cada nodo
     con su descripción y sus relaciones redactadas en español («dirige», «codirige»,
     «aplica el método», «contribuye a»).
   - **Repositorio digital:** cada documento con su categoría, objetivo, autores, descripción
     y ruta del archivo.
   - **Estaciones y series hidrometeorológicas:** totales de estaciones y observaciones, un
     resumen por variable (precipitación, temperatura y las series de caudal cargadas) y, por
     estación, su período, número de observaciones y completitud.

Se proyecta el catálogo a texto en lugar de traducir la pregunta a SQL: el resto de la
canalización ya trabaja sobre texto, la respuesta queda citada como cualquier otra fuente y
una consulta generada por el modelo no sería auditable ni determinista. Cada documento
proyectado lleva un enlace a la vista correspondiente de la plataforma (`/grafo`,
`/repositorio`, `/estaciones`), que es el destino de la cita en la interfaz.

**La postulación solo existe en PDF.** Se convierte **fuera de línea** con
`scripts/corpus_pdf/extract_pdfs.py`, que usa `pypdf` (o `pdftotext` si aquel no está
disponible) y versiona el resultado en `docs/corpus/`. Así el corpus permanente del backend
solo indexa Markdown. El extractor:

- elimina membretes y números de página (líneas cortas repetidas en al menos la mitad de las
  páginas);
- promueve a encabezado solo las secciones numeradas que empiezan con mayúscula («3.2.
  Resumen del proyecto»), para que los fragmentos conserven una sección identificable y los
  valores del formulario («SEMILLA») no se confundan con títulos;
- **reemplaza números de cédula y correos electrónicos**, porque el asistente es público;
- añade un comentario `<!-- source: ... -->` con la ruta del PDF original, que la interfaz
  usa como enlace de la cita.

**Indexado idempotente por huella.** La ingestión calcula una huella SHA-256 del modelo de
embeddings y de todos los fragmentos (documento, enlace, sección y texto). Si coincide con la
almacenada en `rag_index_state`, el arranque no vuelve a indexar ni consume llamadas de
embeddings; si cambia el contenido o el modelo, se reemplaza el corpus completo en una sola
transacción. El indexado se ejecuta al iniciar la API (`RagBackgroundService`) y puede
forzarse con `POST /api/chat/reindex`, protegido por la contraseña de administración.

**Exclusión explícita.** Un archivo Markdown puede quedar fuera del corpus con el comentario
`<!-- rag: exclude -->`. Se aplica a los documentos que describen al propio asistente: la
evaluación lista preguntas fuera de tema («capital de Francia») y, indexada, elevaba esas
preguntas por encima del umbral (el corpus pasó de 195 a 213 fragmentos). El presente informe
y el informe mensual del Producto 3 llevan la misma marca.

En la corrida de evaluación el índice contenía **195 fragmentos** de seis documentos: la
postulación, los informes técnico y mensual del Producto 1 y los tres documentos del catálogo.

### 3.3 Fragmentación y representación vectorial

La fragmentación (`MarkdownChunker`) divide cada documento **por encabezados** y conserva la
ruta de títulos de cada fragmento («Metodología > Fase 2»), de modo que un trozo tomado del
medio de una sección sigue diciendo de dónde viene. Cuando una sección supera 1 200
caracteres, se corta en ventanas de unos 1 000 caracteres en límites de línea, con **150
caracteres de solapamiento**, para que una afirmación que cae en el corte no quede partida en
dos fragmentos que por separado no responden nada. Los bloques de código no se interpretan
como encabezados y el texto alternativo de las imágenes se conserva como prosa («Figura: …»),
porque a veces es el único lugar donde aparece un dato.

El texto que se envía al modelo de embeddings incluye el título del documento y la sección,
además del contenido del fragmento. Los vectores tienen **1 024 dimensiones** y se guardan en
una columna `vector(1024)`; la búsqueda calcula la similitud del coseno (`1 − distancia`), de
modo que la capa de aplicación trabaja siempre con «más alto es más parecido». Cada fragmento
guarda el modelo con el que se calculó y la búsqueda filtra por ese modelo: vectores de
modelos distintos nunca se comparan entre sí. El corpus tiene unos cientos de fragmentos, por
lo que la búsqueda es un recorrido exacto; un índice HNSW solo se justificaría con decenas de
miles.

### 3.4 Proveedores de modelos

| Rol | Bedrock (por omisión) | OpenAI directo (alternativa) |
|---|---|---|
| Embeddings | `cohere.embed-multilingual-v3`, 1 024 dimensiones, tipos de entrada distintos para documentos y consultas, lotes de 96 textos | `text-embedding-3-small`, reducido a 1 024 dimensiones, lotes de 512 textos |
| Generación | `us.anthropic.claude-haiku-4-5-20251001-v1:0` mediante la API Converse en flujo | `gpt-4o-mini` mediante *chat completions* en flujo |
| Credenciales | Cadena estándar de AWS: perfil local en desarrollo, rol de la instancia en EC2 | `OPENAI_API_KEY` |
| Activación | `RAG__PROVIDER=bedrock` | `RAG__PROVIDER=openai` |

*Tabla 2. Proveedores de modelos configurables.*

La elección por omisión responde a dos razones. Primero, el corpus está en español y Cohere
Embed Multilingual v3 distingue entre el texto que se indexa (`search_document`) y la pregunta
(`search_query`), lo que mejora la recuperación de preguntas cortas frente a fragmentos largos.
Segundo, la plataforma ya opera en AWS (us-east-1), de modo que Bedrock no añade otra cuenta,
otra facturación ni otra clave que custodiar: en EC2 las credenciales provienen del rol de la
instancia. La disponibilidad de ambos modelos en la cuenta se verificó antes de implementar.

Ambos proveedores comparten la temperatura (0,2), el máximo de 1 024 *tokens* por respuesta y
la dimensión de 1 024, de modo que la misma columna `pgvector` sirve para los dos. Cambiar de
proveedor cambia el modelo de embeddings y, por la huella descrita en 3.2, fuerza un indexado
completo en el siguiente arranque.

El cliente de Bedrock usa reintentos **estándar** con espera progresiva (hasta seis). Se
descartó el modo adaptativo después de observar que su limitador del lado del cliente,
compartido por todas las peticiones, seguía rechazando consultas durante minutos después de
que Bedrock se había recuperado de una ráfaga de llamadas de embeddings.

### 3.5 Recuperación

Ante una pregunta, el servicio solicita a la base el triple de candidatos del valor final
(`TopK = 8`, es decir, 24 candidatos) y los **diversifica**: conserva como máximo **tres
fragmentos por sección** de un mismo documento hasta completar ocho. La regla surgió de la
evaluación (sección 5): sin ella, una sección larga del catálogo —listas de estaciones
partidas en fragmentos casi idénticos— ocupaba todo el contexto y dejaba fuera la actividad del
cronograma que respondía la pregunta.

**Preguntas de seguimiento.** Para que una pregunta como «¿Y quién es el codirector?» tenga
sentido, se construye también una consulta ampliada que antepone la pregunta anterior del
usuario. Ambas se convierten en vectores **en una sola llamada** y sus resultados se
**intercalan** (uno de cada lista, sin repetir y respetando el tope por sección), en lugar de
reemplazar unos por otros: si el usuario cambia de tema, la consulta ampliada solo encontraría
el tema anterior y el contexto perdería los fragmentos que responden la pregunta actual. La
interfaz envía como historial los últimos seis turnos completos; el servidor los recorta a
800 caracteres cada uno.

### 3.6 Umbral y abstención en dos capas

**Primera capa: umbral sobre la pregunta sola.** Si ningún fragmento del corpus alcanza la
similitud mínima (`RAG__MINSCORE`, 0,35), la consulta se resuelve como abstención **sin llamar
al modelo de lenguaje**: cuesta una llamada de embeddings y ninguna de generación. El umbral
se evalúa sobre la pregunta sola, nunca sobre la consulta ampliada, para que el historial no
arrastre por encima del umbral una pregunta ajena hecha después de una válida. La única
excepción son las preguntas de hasta cuatro palabras («¿Y el codirector?»), demasiado cortas
para puntuar por sí mismas, que sí pueden superarlo con la consulta ampliada. Los fragmentos
de un archivo propio del usuario siempre cuentan, porque son el material que pidió consultar.

**Segunda capa: marca del modelo.** La instrucción de sistema indica que, si la pregunta no
tiene relación con el proyecto o el contexto no permite responderla, el modelo debe contestar
exactamente `[[SIN_RESPALDO]]`. El servicio **retiene los primeros fragmentos del flujo**
mientras todavía podrían ser el inicio de esa marca; si la completan, corta la generación —lo
que cierra la conexión con el proveedor y detiene la facturación— y emite una abstención sin
fuentes. Si divergen, libera lo retenido y continúa el flujo sin demora apreciable.

Las dos capas son necesarias porque las distribuciones de similitud de las preguntas dentro y
fuera del corpus **se solapan**: la pregunta «¿Cuál es el caudal medio anual del río
Guayas?», sin respaldo en la documentación, obtuvo 0,581, por encima de cinco preguntas
válidas. Ningún umbral fijo separa ambos grupos; el umbral filtra lo claramente ajeno a bajo
costo y la decisión fina la toma el modelo con el contexto a la vista.

| Tipo | Significado | Se muestra como |
|---|---|---|
| `rag` | Respuesta construida con los fragmentos recuperados, con citas | Respuesta con lista de fuentes |
| `abstained` | Sin respaldo suficiente (umbral o marca del modelo) | «Sin respaldo documental», borde ocre y sin fuentes |

*Tabla 3. Tipos de respuesta del asistente.*

### 3.7 Citas y respuesta en flujo continuo

La instrucción se arma con un mensaje de sistema fijo y un mensaje de usuario con los
fragmentos numerados `[1]…[n]` —cada uno con su título, su sección y una marca si proviene del
archivo del usuario— seguidos de la pregunta. El mensaje de sistema fija el rol (asistente del
proyecto PISF-25-03), el idioma (español neutro de Ecuador, trato de usted y sin voseo), la
brevedad, la obligación de **citar con el número del fragmento** al final de cada afirmación,
la prohibición de completar con conocimiento externo y la regla de presentar todos los valores
cuando el contexto trae varios. El contexto se limita a **12 000 caracteres**: los fragmentos
entran en orden de similitud y el que cruza el presupuesto se recorta en lugar de descartarse.

La respuesta se entrega con **eventos enviados por el servidor** (*Server-Sent Events*) en
`POST /api/chat/ask`:

| Evento | Contenido |
|---|---|
| `sources` | Tipo de respuesta y fuentes numeradas (documento, título, sección, enlace, similitud, origen) |
| `token` | Siguiente fragmento de texto de la respuesta |
| `done` | Respuesta completa, tipo, fuentes y similitud máxima |
| `error` | Mensaje genérico si la generación falla a mitad del flujo |

*Tabla 4. Eventos del flujo de respuesta.*

La ruta fija `Cache-Control: no-cache` y `X-Accel-Buffering: no` y desactiva el búfer de
respuesta, para que el proxy inverso entregue cada evento de inmediato. El cliente consume el
flujo con `fetch` —la pregunta viaja en el cuerpo de un `POST`, que `EventSource` no admite— y
reconstruye los eventos que la red parte a la mitad. Si el usuario pulsa «Detener» o cierra la
pestaña, la cancelación se propaga hasta el proveedor y no se registra como error.

En la interfaz, cada cita `[n]` se dibuja como una insignia que remite a la fuente de la lista;
cada fuente muestra el título del documento (enlazado a la vista o al PDF de origen), la
sección y el porcentaje de similitud. El texto de la respuesta se interpreta con un analizador
mínimo —párrafos, listas, negritas y citas— que **nunca produce HTML**, de modo que el
contenido generado no puede inyectar marcado en la página.

### 3.8 Consulta temporal de archivos propios

Un visitante puede adjuntar un archivo **PDF, Markdown o texto** de hasta **10 MB** para
consultarlo junto con la documentación del proyecto (`POST /api/chat/upload`). El archivo se
fragmenta y se convierte en vectores igual que el corpus, pero se guarda en una tabla aparte,
`rag_session_chunks`, con el identificador de sesión y la fecha de carga; **nunca** entra en
el corpus permanente ni en el repositorio digital.

- **Formatos y validación.** El texto de un PDF se extrae en el servidor con PdfPig; un PDF
  dañado o protegido se rechaza con un mensaje claro. Los `.md` y `.txt` deben ser UTF-8
  válido y sin bytes nulos: un binario renombrado como texto se rechaza **antes** de gastar
  llamadas de embeddings.
- **Límites.** 10 MB por archivo, comprobados tanto con `Content-Length` como en cuerpos
  fragmentados (sin esa cabecera), un máximo de 300 fragmentos por archivo y de **600
  fragmentos por sesión** (unos dos archivos extensos, `RAG__MAXCHUNKSPERSESSION`). La cuota de
  la sesión se comprueba antes de calcular embeddings; al superarla la API responde `413` con
  un mensaje que pide quitar el archivo actual antes de cargar otro.
- **Vencimiento.** El servicio en segundo plano elimina cada 10 minutos los fragmentos con más
  de **120 minutos** (`RAG__SESSIONTTLMINUTES`). La interfaz muestra la hora hasta la que el
  archivo estará disponible.
- **Borrado a demanda.** `DELETE /api/chat/session/{id}` elimina de inmediato lo que subió la
  sesión; la interfaz lo expone con el botón de quitar archivo, y un archivo nuevo reemplaza
  al anterior.

El panel de la interfaz declara la temporalidad **antes** de la acción: «El archivo es
temporal: solo lo usa esta sesión, se elimina al vencer o al quitarlo y no se agrega al
repositorio».

### 3.9 Seguridad y control de costos

La página del asistente es pública y cada respuesta tiene un costo por uso del proveedor, por
lo que la protección se trató como parte del producto:

- **Límite por IP.** Las rutas públicas que consumen el proveedor —consulta, carga y borrado
  de sesión— admiten **10 peticiones por minuto por dirección IP** (ventana fija,
  `RAG__RATELIMITPERMINUTE`) y responden `429` con un mensaje en español. Detrás de Caddy la
  IP real llega en `X-Forwarded-For`; la API solo acepta esa cabecera cuando proviene de las
  redes privadas del proxy (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16 y 127.0.0.0/8), de modo
  que un cliente no puede falsificarla para eludir el límite.
- **Puerta de administración.** Toda petición que no sea de lectura bajo `/api` exige la
  cabecera `X-Admin-Password`, salvo la verificación de la contraseña y las tres rutas públicas
  del asistente. La regla vive en `AdminGate` y compara rutas y métodos **sin distinguir
  mayúsculas**, igual que el enrutador: de lo contrario, «POST /API/chat/reindex» habría
  llegado al *endpoint* sin contraseña. El reindexado, que consume llamadas de embeddings,
  queda así reservado a la administración.
- **Aislamiento de sesiones.** El identificador de sesión es un UUID aleatorio de 32 caracteres
  hexadecimales generado en el navegador y guardado en `sessionStorage` (por pestaña); el servidor exige que
  tenga entre 8 y 64 caracteres alfanuméricos, `-` o `_`. La búsqueda solo une al corpus los
  fragmentos cuya sesión coincide con la de la petición: un archivo subido en una sesión no
  aparece en ninguna otra.
- **Protección ante instrucciones incrustadas.** Los documentos y, sobre todo, los archivos
  subidos son texto de terceros. La instrucción de sistema declara que el contenido dentro de
  `<documento>` es material de consulta y no instrucciones, y ordena ignorar cualquier orden
  que aparezca allí. Cada fragmento se envuelve en esas etiquetas y, antes, se elimina de su
  contenido, título y sección cualquier secuencia con forma de etiqueta `documento`, en
  cualquier combinación de mayúsculas, para que un fragmento no pueda cerrar su propia
  envoltura ni falsificar otra.
- **Control de costos.** La abstención por umbral no llama al modelo; la pregunta se limita a
  1 000 caracteres, el contexto a 12 000 caracteres, el historial a seis turnos de 800
  caracteres y la respuesta a 1 024 *tokens*; el indexado solo se repite cuando cambia la
  huella; y cortar el flujo (marca de no respaldo, «Detener» o desconexión) cierra la conexión
  con el proveedor.
- **Errores sin detalles internos.** `GET /api/chat/health` es público e informa proveedor,
  modelos, número de fragmentos, fecha del índice y parámetros de recuperación; ante una falla
  muestra un mensaje genérico y deja en los registros del servidor los mensajes del proveedor o
  de la base de datos, que podrían revelar identificadores de la cuenta o del servidor.
- **Proveedor mal configurado.** Si el proveedor no puede configurarse al arrancar (por
  ejemplo, `RAG__PROVIDER=openai` sin `OPENAI_API_KEY`, o credenciales de AWS que no se
  resuelven), el error queda en los registros y solo el asistente se deshabilita: toda ruta
  `/api/chat` responde `503` con «El asistente no está disponible en este momento.», que la
  interfaz muestra tal cual, mientras el resto de la plataforma sigue funcionando.

### 3.10 Pruebas

Toda la lógica del asistente se cubrió con pruebas automatizadas que no dependen de red ni de
servicios externos: los proveedores se ejercitan contra dobles y el almacén contra una
implementación en memoria. Al cierre de este informe:

| Suite | Total | Del asistente | Comando |
|---|---|---|---|
| Backend (xUnit) | **106** | 49 | `dotnet test backend/tests/CaudalesPlatform.Tests.csproj` |
| Frontend (Vitest) | **42** | 30 | `cd frontend && npx vitest run` |
| Extractor de PDF (unittest) | **5** | 5 | `python3 -m unittest scripts/corpus_pdf/test_extract_pdfs.py` |

*Tabla 5. Suites de prueba al cierre del Producto 3 (todas en verde).*

Lo que se prueba, y por qué:

- **Fragmentación e instrucción:** ruta de encabezados, cortes con solapamiento, texto
  alternativo de imágenes, contexto numerado y acotado, intercalado y diversificación por
  sección, historial alternado que termina en la pregunta y eliminación de etiquetas de
  envoltura en cualquier combinación de mayúsculas.
- **Abstención:** que **no se llame al modelo** cuando todo queda bajo el umbral —una prueba
  de costo, no solo de comportamiento—, que el historial no eleve el puntaje de una pregunta
  ajena, que una pregunta corta sí pueda apoyarse en el turno anterior y que la marca
  `[[SIN_RESPALDO]]` se detecte aunque llegue partida entre varios fragmentos del flujo.
- **Sesiones:** la carga va solo a la tabla de sesión, se rechazan identificadores,
  extensiones, tamaños y binarios disfrazados de texto, se aplica la cuota de fragmentos por
  sesión sin llamar al proveedor, y la purga y el borrado eliminan lo que corresponde.
- **Proveedores:** Bedrock es el proveedor por omisión y usa reintentos estándar; OpenAI exige
  clave; ida y vuelta del formato de Cohere; 1 024 dimensiones y orden de entrada en OpenAI;
  lectura del flujo y de los errores; un proveedor mal configurado deja el asistente en `503`
  sin afectar al resto de la API.
- **Indexado y catálogo:** se indexa una vez y no se repite hasta que cambia el contenido o el
  modelo; la proyección del grafo, el repositorio y las estaciones produce el Markdown
  esperado.
- **Puerta de administración:** combinaciones de métodos y rutas en mayúsculas y minúsculas.
- **Frontend:** interpretación de la respuesta (párrafos, listas, citas), lectura del flujo de
  eventos —incluidos eventos partidos por la red—, negrita sin cerrar al detener el flujo,
  mensajes de error del servidor (`413`, `429`, `503`) y estado de la conversación (abstención,
  historial, persistencia), la cancelación con «Detener» y la carga y el reemplazo del archivo
  propio.

### 3.11 Despliegue

El sistema se ejecuta con los mismos archivos de Docker Compose de desarrollo y de producción
del Producto 1, con tres cambios: la imagen de PostgreSQL pasa de `postgres:16-alpine` a
`pgvector/pgvector:pg16`; la API recibe `RAG__PROVIDER`, `AWS_REGION` y, si se usa la
alternativa, `OPENAI_API_KEY`; y la carpeta `docs/` se monta en solo lectura como corpus. Se
publicó el **26 de noviembre de 2026** en la instancia EC2 existente, en
https://100-49-251-85.sslip.io/asistente, con tres puntos atendidos:

1. **Rol IAM de la instancia.** La API obtiene credenciales de Bedrock del rol de la instancia.
   El rol `caudales-demo-bedrock` se crea ahora en `deploy/aws/create.sh`: permite
   `bedrock:InvokeModel` (embeddings) y `bedrock:InvokeModelWithResponseStream` (generación en
   flujo) solo sobre los dos modelos usados y el perfil de inferencia de Claude Haiku 4.5, se
   asocia a la instancia mediante un perfil de instancia y eleva a 2 el límite de saltos de los
   metadatos IMDSv2, necesario para que un contenedor alcance el servicio de credenciales. El
   acceso a los modelos está habilitado en la consola de Bedrock para us-east-1.
2. **Cambio de imagen de PostgreSQL.** La imagen `pgvector` se basa en Debian (glibc) y la
   anterior en Alpine (musl). El volumen de datos se conserva, pero el orden de las
   intercalaciones de texto puede diferir, por lo que se ejecutó
   `REINDEX DATABASE caudales;` antes de abrir la plataforma. La extensión `vector` y las
   tablas del asistente se crean solas al arrancar la API.
3. **Corpus e indexado inicial.** El *script* de despliegue (`deploy/aws/caudales`) envía con
   `rsync` la carpeta `docs/` —cuyo Markdown es el corpus—, excluyendo `docs/capturas` y los
   archivos `.docx`. El primer arranque con el corpus calcula los embeddings de unos doscientos
   fragmentos; los siguientes son instantáneos mientras no cambie la huella. La detención
   automática por inactividad del Producto 1 no afecta al índice, que persiste en la base.

Tras la publicación se verificó que la respuesta en flujo continuo llega de forma incremental a
través del *proxy* inverso.

### 3.12 Proceso de desarrollo

- Rama `producto-3` a partir de `fase-1`, con *commits* atómicos según *Conventional
  Commits* y un registro de decisiones (modelos verificados en la cuenta, umbral, riesgos de
  despliegue).
- Verificación previa de los modelos disponibles en Bedrock para la cuenta del proyecto antes
  de fijar los valores por omisión.
- Evaluación de punta a punta contra la API real, con corridas sucesivas que guiaron los
  cambios de recuperación (sección 5).
- Revisión de calidad y seguridad del conjunto antes del cierre, que produjo las correcciones
  de la puerta de administración, la confianza en `X-Forwarded-For`, el cierre del flujo de
  Bedrock al detener la generación, la validación de binarios y los mensajes del endpoint de
  salud.

## 4. Resultados

El resultado del producto es una página **«Asistente»** en el menú principal de la plataforma
(`/asistente`, cargada bajo demanda) y un **botón flotante** presente en todas las demás
páginas que lleva a ella. La página ofrece preguntas de ejemplo por tema (objetivos, equipo,
métodos, estaciones y series, calidad de datos), la transcripción de la conversación, el
panel del archivo propio y el estado del índice documental (fragmentos indexados y fecha del
índice). La conversación se conserva al recargar la pestaña y se reinicia con «Nueva
conversación».

### 4.1 Respuesta anclada en la documentación

Ante una pregunta cubierta por el corpus, el asistente responde en flujo continuo y marca cada
afirmación con el número del fragmento que la respalda. En el ejemplo, la consulta «¿Cómo se
usan FDA, Climatol y B-splines en el proyecto?» se responde con tres párrafos —uno por
método— que combinan el catálogo del grafo de conocimiento y la postulación del proyecto: el
suavizamiento e imputación con bases de Fourier, la homogeneización con Climatol y umbrales
calculados con Bootstrap, y la reconstrucción de hidrogramas con B-splines (año 1, meses
10–12).

![Página del asistente con la pregunta «¿Cómo se usan FDA, Climatol y B-splines en el proyecto?»: la respuesta en tres párrafos con citas numeradas, la lista desplegable de seis fuentes consultadas con título enlazado, sección y porcentaje de similitud, y a la derecha los paneles de archivo propio, índice documental (195 fragmentos) y guía de lectura.](../capturas/producto-3/asistente-respuesta.png)

*Figura 2. Respuesta anclada en la documentación, con citas y fuentes.*

La lista de fuentes muestra, para cada fragmento, el documento enlazado —el catálogo enlaza a
la vista del grafo y la postulación al PDF del repositorio—, la sección de procedencia y su
similitud con la pregunta (61 %, 58 %, 57 % en las primeras posiciones del ejemplo).

### 4.2 Abstención ante preguntas sin respaldo

Cuando la pregunta no tiene respaldo en la documentación, la respuesta lleva la insignia «Sin
respaldo documental», un borde ocre y ninguna fuente, y orienta al usuario sobre los temas que
sí puede consultar.

![Pregunta «¿Cuál es la capital de Francia?» respondida con la insignia «Sin respaldo documental», el mensaje de que no se encontró información en la documentación del proyecto, los temas que sí puede responder y la nota de que ningún documento alcanzó la similitud mínima.](../capturas/producto-3/asistente-abstencion.png)

*Figura 3. Abstención ante una pregunta ajena al proyecto.*

En este caso la abstención la decide el umbral: la similitud máxima fue 0,338, por debajo de
0,35, de modo que **no hubo llamada al modelo de lenguaje** y la respuesta tardó 0,34 s en la
evaluación. Las preguntas cercanas al dominio pero sin respaldo —por ejemplo, el caudal medio
del río Guayas— sí superan el umbral, llegan al modelo y este responde con la marca de no
respaldo, que la interfaz muestra de la misma forma.

### 4.3 Consulta temporal de un archivo propio

El usuario adjunta un archivo con el clip junto al cuadro de pregunta. Una vez indexado, el
archivo aparece sobre el cuadro con su número de fragmentos y la hora hasta la que estará
disponible, y sus fragmentos se citan como cualquier otra fuente, con la etiqueta «Su
archivo».

![Consulta sobre una nota de campo adjuntada («nota-campo-rio-pita», 2 fragmentos, disponible hasta las 09:34 p. m.): la respuesta da el caudal medido en el aforo del río Pita y la recomendación de la nota, citando el archivo propio en los dos primeros lugares (83 % y 73 %) y, a continuación, fragmentos del catálogo de estaciones de caudal.](../capturas/producto-3/asistente-archivo-propio.png)

*Figura 4. Consulta de un archivo propio con indexado temporal.*

El ejemplo usa una nota de campo de prueba sobre un aforo en el río Pita. La respuesta extrae
de ella el caudal medido y la recomendación, y la lista de fuentes la sitúa por delante del
corpus permanente por ser lo más similar a la pregunta; los fragmentos del catálogo de
estaciones de caudal completan el contexto.

## 5. Evaluación del sistema

La evaluación se documenta en detalle en `docs/informes/evaluacion-rag.md`. Mide lo que
recibe el usuario —no solo la recuperación—, contra la API real (`POST /api/chat/ask`) y el
índice construido con la documentación del proyecto.

**Configuración de la corrida final:** proveedor Bedrock (us-east-1), embeddings
`cohere.embed-multilingual-v3`, generación Claude Haiku 4.5, 195 fragmentos indexados, umbral
0,35, `TopK = 8` y como máximo 3 fragmentos por sección.

**Conjunto de preguntas.** `scripts/rag_eval/preguntas.json` contiene **32 preguntas** en
español, redactadas como las haría un integrante del equipo o un evaluador:

| Grupo | n | Contenido |
|---|---|---|
| En corpus | 21 | objetivos, equipo, métodos (FDA, Bootstrap, Climatol, B-splines, clustering funcional), estaciones, variables, calidad de datos, repositorio, grafo, presupuesto, cronograma, ODS, tecnologías y publicaciones |
| Fuera de corpus | 6 | cuatro ajenas al proyecto y dos cercanas al dominio sin respaldo documental |
| Seguimiento | 5 | tres que dependen del turno anterior y dos fuera de tema formuladas como seguimiento |

*Tabla 6. Conjunto de preguntas de evaluación.*

Cada pregunta declara el tipo esperado y los **hechos clave** que la respuesta debe contener;
las de seguimiento envían como historial la respuesta real obtenida en la misma corrida. La
calificación automática se revisó a mano respuesta por respuesta.

**Resultados de la corrida final** (`scripts/rag_eval/resultados/corrida-3.json`):

| Métrica | Valor |
|---|---|
| Exactitud en preguntas respondibles | **24/24 correctas** (0 parciales, 0 incorrectas, 0 abstenciones indebidas) |
| Precisión de la abstención | **1,00** (8 de 8 abstenciones emitidas eran correctas) |
| Exhaustividad de la abstención | **1,00** (8 de 8 preguntas sin respaldo se abstuvieron) |
| Validez de citas | **24/24** respuestas con todas sus citas válidas |
| Latencia total, todas las preguntas | mediana **1,61 s**, p95 **2,77 s** |
| Latencia total, respuestas con fuentes | mediana 1,87 s, p95 2,78 s |
| Latencia hasta el primer texto, respuestas con fuentes | mediana 0,89 s, máximo 1,96 s |
| Abstención por umbral (sin llamar al modelo) | 0,34–0,37 s |

*Tabla 7. Resultados de la evaluación de punta a punta.*

| Grupo | n | Similitud mínima | Mediana | Máxima |
|---|---|---|---|---|
| En corpus (sin seguimiento) | 21 | 0,507 | 0,638 | 0,722 |
| Fuera de corpus (sin seguimiento) | 6 | 0,338 | 0,436 | 0,581 |

*Tabla 8. Distribución de la similitud máxima por grupo.*

De las ocho abstenciones, **dos** las decidió el umbral (0,338 y 0,349) y **seis** la marca del
modelo. Con estos datos, un umbral de 0,45 seguiría sin abstenciones indebidas y ahorraría la
generación en cuatro de las seis preguntas fuera de corpus, pero el margen sobre la pregunta
válida más baja es de apenas 0,06 en un conjunto pequeño; se mantiene 0,35 hasta contar con
más preguntas reales.

**Fallos encontrados y cambios.** La evaluación guio tres correcciones:

1. **Abstención tras una pregunta válida.** Una pregunta ajena hecha después de una del
   proyecto heredaba el puntaje de la anterior a través de la consulta ampliada y se
   respondía con fuentes visibles. Se corrigió con las dos capas de la sección 3.6: umbral
   sobre la pregunta sola y marca del modelo.
2. **Corrida 1** (sin diversificación, `TopK = 6`): 22 correctas, 1 parcial y 1 abstención
   indebida. La pregunta por los meses del cronograma en que se levantan las series de caudal
   recibió seis fragmentos casi idénticos de listas de estaciones y ninguno del cronograma.
3. **Corridas 2 y 3:** con dos fragmentos por sección se corrigió el cronograma, pero la tabla
   de reglas de calidad quedó fuera; con **tres fragmentos por sección y `TopK = 8`** (unos
   8 000 caracteres de contexto, dentro del límite de 12 000) todas las preguntas quedaron
   correctas sin regresiones.

**Lectura de los resultados.** 32 preguntas permiten detectar fallos evidentes, no estimar
tasas con precisión: 24 de 24 es compatible con una tasa real de error de varios puntos
porcentuales. Los ajustes de recuperación se decidieron con fallos de este mismo conjunto y
conviene validarlos con preguntas nuevas.

## 6. Limitaciones y trabajo futuro

- **Resultados de los Productos 2 en adelante.** El ámbito de «resultados» cubre hoy los del
  Producto 1 y la evaluación del sistema. Los resultados de suavizamiento, imputación y
  regionalización funcional se incorporarán al corpus a medida que se generen en el Producto 2;
  el mecanismo de proyección del catálogo (`CatalogProjection`) y la lectura de las carpetas
  `docs/informes` y `docs/corpus` ya permiten añadirlos sin cambios en la canalización.
- **Abstención dependiente del modelo.** Seis de las ocho abstenciones de la evaluación las
  decidió el modelo con la marca convenida. Si se cambia el modelo de generación —incluido el
  paso a OpenAI— debe repetirse la evaluación: un modelo que no respete la marca declinaría en
  prosa con las fuentes visibles.
- **Alternativa OpenAI sin prueba en vivo.** La ruta OpenAI está implementada y cubierta con
  pruebas contra dobles, pero no se ejecutó contra el servicio real por no disponer de una
  clave del proyecto durante el período.
- **Conjunto de evaluación pequeño y una corrida por configuración.** La generación no es
  determinista; conviene ampliar el conjunto con preguntas reales de los usuarios y repetir la
  evaluación tras cada ampliación del corpus.
- **Corpus en evolución.** El catálogo se proyecta desde la base y ya incluye series de caudal
  que los informes del Producto 1 describen como pendientes; algunas respuestas combinan ambas
  fuentes. Los nuevos informes del proyecto deben incorporarse a `docs/informes` o
  `docs/corpus` y revisarse los hechos esperados de la evaluación.
- **Límite por IP en memoria.** Con varias réplicas de la API cada una llevaría su propio
  conteo, y varios usuarios detrás de una misma salida a internet comparten cupo.
- **Borrado por identificador de sesión.** Quien conozca un identificador de sesión puede
  borrar sus archivos. Es el mismo nivel de confianza que la carga y la consulta con ese
  identificador, y lo que se pierde son datos temporales que el vencimiento eliminaría de todos
  modos.
- **Consumo no medido por respuesta.** No se registra el uso de *tokens* por consulta; el
  control de costo se apoya en los límites de la sección 3.9. Registrar ese consumo permitiría
  estimar el gasto mensual con uso real.
- **Búsqueda solo semántica.** No hay coincidencia por palabras clave; una búsqueda híbrida
  podría mejorar las preguntas por códigos de estación o cifras exactas.

## 7. Conclusiones

- Se incorporó a la plataforma del proyecto un sistema de consulta inteligente que responde en
  lenguaje natural sobre la documentación, las metodologías, los datos y los resultados del
  Producto 1 (postulación, informes, grafo de conocimiento, repositorio y catálogo de
  estaciones y series), **citando el fragmento
  de procedencia** de cada afirmación y sin infraestructura adicional: la base vectorial es la
  misma instancia de PostgreSQL, ahora con `pgvector`.
- El anclaje documental se sostiene en mecanismos independientes y verificables: un umbral que
  abstiene sin gastar una llamada al modelo, una marca con la que el modelo declara la falta de
  respaldo, una instrucción que prohíbe completar con conocimiento externo y citas numeradas
  que la interfaz enlaza a su fuente. La evaluación de punta a punta obtuvo 24 de 24 respuestas
  correctas, precisión y exhaustividad de abstención de 1,00 y todas las citas válidas, con una
  mediana de 1,61 s por respuesta.
- La separación por puertos deja el proveedor como una decisión de configuración: Amazon
  Bedrock, sobre la cuenta en la que ya opera la plataforma, es el valor por omisión, y OpenAI
  directo se activa con una variable de entorno.
- La consulta de archivos propios se resolvió sin comprometer el corpus ni el repositorio:
  alcance por sesión, vencimiento automático a los 120 minutos, borrado a demanda, límites de
  tamaño y validación del contenido.
- La superficie pública quedó acotada en abuso y costo: límite por IP con una cabecera de
  reenvío que no se puede falsificar, puerta de administración insensible a mayúsculas,
  aislamiento de sesiones y defensa ante instrucciones incrustadas en los documentos.
- La calidad se respalda con 153 pruebas automatizadas en verde (106 del backend, 42 del
  frontend y 5 del extractor) y con una evaluación reproducible cuyos fallos guiaron los
  ajustes de recuperación.

## 8. Bibliografía

- Escuela Politécnica Nacional. (2025). *Postulación del proyecto: Metodología para la
  regionalización de caudales en Ecuador basada en el Análisis de Datos Funcionales y la
  homogeneización de variables hidrometeorológicas*. Vicerrectorado de Investigación,
  Innovación y Vinculación.
- Lewis, P., Perez, E., Piktus, A., Petroni, F., Karpukhin, V., Goyal, N., et al. (2020).
  Retrieval-Augmented Generation for Knowledge-Intensive NLP Tasks. *Advances in Neural
  Information Processing Systems*, 33.
- Amazon Web Services. *Amazon Bedrock User Guide*. https://docs.aws.amazon.com/bedrock/
- Cohere. *Embed Multilingual v3*. https://docs.cohere.com/
- pgvector contributors. *pgvector: open-source vector similarity search for Postgres*.
  https://github.com/pgvector/pgvector
- Pico Ponce, K. V. (2026). *Informe técnico de implementación de un aplicativo web para la
  gestión de series hidrometeorológicas y documentación del proyecto, con repositorio digital
  y grafo de conocimiento soportado en base de datos* (Producto 1, PISF-25-03). Escuela
  Politécnica Nacional.

## 9. Anexos

### Anexo 1 — Diagramas

- **Figura 1, arquitectura del asistente:** `docs/diagramas/arquitectura-asistente.svg`.

### Anexo 2 — Endpoints del asistente

| Método | Ruta | Acceso | Descripción |
|--------|------|--------|-------------|
| GET | `/api/chat/health` | Pública | Proveedor, modelos, fragmentos indexados, fecha del índice y parámetros |
| POST | `/api/chat/ask` | Pública, 10/min por IP | Pregunta con historial y sesión; respuesta en flujo SSE |
| POST | `/api/chat/upload` | Pública, 10/min por IP | Carga temporal de un archivo propio (`multipart/form-data`: `file`, `sessionId`) |
| DELETE | `/api/chat/session/{sessionId}` | Pública, 10/min por IP | Borrado inmediato de los archivos de la sesión |
| POST | `/api/chat/reindex?force=` | Administración | Reindexado del corpus permanente |

*Tabla 9. Endpoints del asistente documental.*

### Anexo 3 — Variables de configuración

| Variable | Por omisión | Función |
|---|---|---|
| `RAG__PROVIDER` | `bedrock` | Proveedor activo: `bedrock` u `openai` |
| `AWS_REGION` | `us-east-1` | Región de Bedrock |
| `RAG__BEDROCKEMBEDDINGMODEL` | `cohere.embed-multilingual-v3` | Modelo de embeddings en Bedrock |
| `RAG__BEDROCKCHATMODEL` | `us.anthropic.claude-haiku-4-5-20251001-v1:0` | Modelo de generación en Bedrock |
| `OPENAI_API_KEY` | (vacía) | Clave de OpenAI, obligatoria con `RAG__PROVIDER=openai` |
| `RAG__OPENAIEMBEDDINGMODEL` | `text-embedding-3-small` | Modelo de embeddings en OpenAI |
| `RAG__OPENAICHATMODEL` | `gpt-4o-mini` | Modelo de generación en OpenAI |
| `RAG__CORPUSROOTS` | `docs/informes,docs/corpus` | Raíces del corpus Markdown |
| `RAG__INDEXONSTARTUP` | `true` | Indexa al arrancar (sin costo si la huella no cambió) |
| `RAG__TOPK` | `8` | Fragmentos en el contexto |
| `RAG__MINSCORE` | `0.35` | Umbral de similitud para responder |
| `RAG__MAXCONTEXTCHARS` | `12000` | Presupuesto de contexto por consulta |
| `RAG__MAXTOKENS` | `1024` | Máximo de *tokens* por respuesta |
| `RAG__MAXQUESTIONCHARS` | `1000` | Longitud máxima de la pregunta |
| `RAG__RATELIMITPERMINUTE` | `10` | Peticiones públicas por minuto y por IP |
| `RAG__SESSIONTTLMINUTES` | `120` | Vida de un archivo propio |
| `RAG__MAXUPLOADBYTES` | `10485760` | Tamaño máximo por archivo (10 MB) |
| `RAG__MAXCHUNKSPERUPLOAD` | `300` | Fragmentos máximos por archivo |
| `RAG__MAXCHUNKSPERSESSION` | `600` | Fragmentos máximos por sesión, sumando sus cargas |

*Tabla 10. Variables de configuración del asistente.*

### Anexo 4 — Ejecución y pruebas

```bash
# Levantar el sistema con el asistente (credenciales de AWS en ~/.aws, perfil por omisión)
cp env.example .env          # definir ADMIN_PASSWORD; RAG__PROVIDER=bedrock
docker compose up --build    # aplicativo en http://localhost:5174, página /asistente

# Pruebas
dotnet test backend/tests/CaudalesPlatform.Tests.csproj           # 106 pruebas
cd frontend && npx vitest run                                     # 42 pruebas
python3 -m unittest scripts/corpus_pdf/test_extract_pdfs.py       # 5 pruebas

# Extracción de la postulación a Markdown (fuera de línea)
python3 scripts/corpus_pdf/extract_pdfs.py

# Reindexado forzado del corpus (administración)
curl -X POST "http://localhost:8081/api/chat/reindex" -H "X-Admin-Password: <contraseña>"

# Evaluación de punta a punta (consume llamadas del proveedor)
python3 scripts/rag_eval/run_eval.py --api http://localhost:5000
```

### Anexo 5 — Repositorio y despliegue

- **Código fuente (GitHub, rama producto-3):**
  https://github.com/Valentinpico/caudales-epn/tree/producto-3 
- **Plataforma desplegada:** https://100-49-251-85.sslip.io/ (asistente en https://100-49-251-85.sslip.io/asistente, publicado el 26 de noviembre de 2026)

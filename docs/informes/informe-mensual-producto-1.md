# INFORME MENSUAL DE PRODUCTOS ENTREGADOS

> Documento corto (formato administrativo). El detalle técnico está en
> `informe-tecnico-producto-1.md`. Campos `[COMPLETAR: ...]` pendientes.

| Campo | Valor |
|-------|-------|
| **NOMBRE DEL PRODUCTO** | Informe técnico de implementación de un aplicativo web para la gestión de series hidrometeorológicas y documentación del proyecto, con repositorio digital y grafo de conocimiento soportado en base de datos. |
| **CÓDIGO PROYECTO** | [COMPLETAR: código del proyecto] |
| **NOMBRE DE PROYECTO** | Metodología para la regionalización de caudales en Ecuador basada en el Análisis de Datos Funcionales y la homogeneización de variables hidrometeorológicas |
| **NOMBRE DEL PROFESIONAL** | Valentin Pico — Ingeniero de Software |
| **PERÍODO** | 01 al 31 de octubre de 2026 |
| **FECHA DE PRESENTACIÓN** | [COMPLETAR: fecha de entrega] |

---

## 1. OBJETIVO

Diseñar e implementar un aplicativo web para la gestión de las series
hidrometeorológicas y de la documentación del proyecto, que integre un repositorio
digital y un grafo de conocimiento soportado en base de datos, garantizando la
organización, trazabilidad y control de calidad de los registros diarios del INAMHI que
sirven de insumo para el suavizamiento funcional, la imputación y la homogeneización de
series previstos en el proyecto.

## 2. ACTIVIDADES REALIZADAS

Durante el período se desarrollaron las siguientes actividades:

1. Análisis de la postulación del proyecto y de los archivos crudos del INAMHI
   (precipitación y temperatura, formato ancho año × mes × día) para definir el alcance
   del aplicativo y el modelo de datos.
2. Definición de la arquitectura de la plataforma (API .NET 9 por capas, frontend React
   por funcionalidades) y configuración del entorno en contenedores (PostgreSQL 16, API y
   frontend) con Docker Compose.
3. Diseño e implementación del modelo de datos: estaciones, observaciones diarias,
   hallazgos de calidad, documentos del repositorio y nodos y relaciones del grafo de
   conocimiento, con Entity Framework Core sobre PostgreSQL.
4. Desarrollo de la **carga masiva de las series del INAMHI**: 2 011 282 registros
   diarios de 106 estaciones (1960–2023), con trazabilidad de cada valor hasta la línea
   del archivo original.
5. Implementación del **control de calidad automático**: meses duplicados, meses y días
   inválidos, valores ilegibles, códigos de dato faltante (999,9) y valores fuera de
   rango, que se conservan marcados como sospechosos. Se registraron 2 232 hallazgos.
6. Desarrollo de las vistas de **series hidrometeorológicas**: matriz de cobertura
   estaciones × años, serie diaria y mensual por estación, matriz de completitud mes a
   mes y exportación en CSV.
7. Implementación del **repositorio digital** de documentos del proyecto, clasificados por
   tipo y por objetivo específico (OE1–OE4), con carga, descarga y eliminación.
8. Implementación del **grafo de conocimiento soportado en base de datos** que relaciona
   el proyecto, sus objetivos, actividades y métodos, los conjuntos de datos, estaciones y
   variables, los documentos, el equipo, las instituciones, las líneas de investigación y
   los ODS (154 nodos y 419 relaciones), con visualización interactiva.
9. Desarrollo de la **página de inicio** del proyecto, con sus líneas de investigación,
   el equipo, la cobertura de la red, los objetivos específicos y los ODS, obtenidos del
   grafo de conocimiento.
10. Implementación del modo administración (control de acceso por contraseña, lectura
   pública por defecto) y de pruebas automatizadas para backend y frontend (43 pruebas).
11. Elaboración del informe técnico del producto y control de versiones (Git).

## 3. PRODUCTOS ALCANZADOS

### Producto 1 — Aplicativo web para la gestión de series hidrometeorológicas y documentación del proyecto, con repositorio digital y grafo de conocimiento

El producto entregado es una plataforma web que centraliza en una sola base de datos las
series diarias de precipitación y temperatura del INAMHI, la documentación del proyecto y
un **grafo de conocimiento soportado en base de datos**. La plataforma permite consultar
la cobertura y completitud de cada estación, revisar sus series diarias y mensuales,
auditar las inconsistencias detectadas en los datos crudos y descargar las series para su
análisis. El **repositorio digital** organiza los documentos por objetivo específico y el
grafo relaciona de forma trazable objetivos, actividades, métodos, datos, documentos y
actores del proyecto. Se ejecuta íntegramente en contenedores reproducibles (Docker
Compose) y opera en modo lectura, con un modo de administración protegido por contraseña.
La plataforma sienta la base para incorporar las series de caudal, las series
homogeneizadas y los resultados de la regionalización en las fases siguientes.

**Resultados alcanzados:**

1. Aplicativo web de gestión de series hidrometeorológicas con 2 011 282 registros
   diarios de 106 estaciones del INAMHI (1960–2023).
2. Control de calidad automático con 2 232 hallazgos documentados, sin alterar los
   valores originales.
3. Repositorio digital de documentos del proyecto vinculado a los objetivos OE1–OE4.
4. Grafo de conocimiento soportado en base de datos (PostgreSQL) con 154 nodos y 419
   relaciones.
5. Infraestructura reproducible en contenedores (Docker Compose) y pruebas
   automatizadas.
6. Documentación técnica del producto (informe técnico detallado, modelo de datos,
   arquitectura y ejecución).

### Evidencias

![Página de inicio con el proyecto, sus líneas de investigación, el equipo y la matriz de cobertura de la red.](../capturas/producto-1/inicio-proyecto-equipo.png)

![Serie mensual de precipitación de una estación con exportación CSV.](../capturas/producto-1/estacion-serie.png)

![Página de calidad de datos con reglas, conteos y hallazgos.](../capturas/producto-1/calidad-datos.png)

![Repositorio digital de documentos del proyecto.](../capturas/producto-1/repositorio.png)

![Grafo de conocimiento interactivo.](../capturas/producto-1/grafo-conocimiento.png)

## 4. REFERENCIAS

- Escuela Politécnica Nacional. (2025). *Postulación del proyecto: Metodología para la
  regionalización de caudales en Ecuador basada en el Análisis de Datos Funcionales y la
  homogeneización de variables hidrometeorológicas*. Vicerrectorado de Investigación,
  Innovación y Vinculación.
- Instituto Nacional de Meteorología e Hidrología (INAMHI). *Registros diarios de
  precipitación y temperatura de la red nacional de estaciones, 1960–2023*.

## 5. CONCLUSIONES

El análisis desarrollado a partir del informe permite concluir lo siguiente:

- La plataforma centraliza los registros diarios del INAMHI, la documentación y el grafo
  de conocimiento del proyecto en una sola base de datos, con trazabilidad desde cada
  valor hasta el archivo original.
- El control de calidad automático documenta las inconsistencias de los datos crudos sin
  modificarlos, lo que provee una base auditable para el suavizamiento, la imputación y
  la homogeneización.
- La arquitectura por capas, la ejecución en contenedores y las pruebas automatizadas
  garantizan reproducibilidad y mantenibilidad.

En conjunto, este producto establece la **base tecnológica del proyecto**: una plataforma
reproducible y trazable que consolida las series hidrometeorológicas y la documentación
en un repositorio digital y un grafo de conocimiento navegables, sobre la cual se
incorporarán los resultados de las siguientes fases.

## 6. ACEPTACIÓN

Firma Valentin Pico en calidad de profesional contratado del Proyecto
[COMPLETAR: código del proyecto] y la directora del Proyecto Adriana Uquillas Andrade, en
calidad de directora del Proyecto [COMPLETAR: código del proyecto], para constancia de la
recepción de este producto y su aprobación a entera satisfacción.

| Elaborado por: | Aprobado por: |
|----------------|---------------|
| [COMPLETAR: firma electrónica] | |
| **Nombre:** Valentin Pico | **Nombre:** Adriana Uquillas Andrade |
| Ingeniero de Software — Profesional contratado | Directora del Proyecto |

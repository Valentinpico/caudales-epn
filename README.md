# Plataforma Caudales — EPN

Aplicativo web para la **gestión de series hidrometeorológicas** y la **documentación del proyecto**
"Metodología para la regionalización de caudales en Ecuador basada en el Análisis de Datos
Funcionales y la homogeneización de variables hidrometeorológicas" (Escuela Politécnica Nacional,
Departamento de Matemática). Integra un **repositorio digital** y un **grafo de conocimiento**
soportado en base de datos.

## Stack

| Capa | Tecnología |
|------|-----------|
| Backend | .NET 9 (C#), ASP.NET Core Minimal API, Entity Framework Core |
| Base de datos | PostgreSQL 16 (series, repositorio y grafo) |
| Frontend | React + TypeScript + Vite + Tailwind CSS |
| Pruebas | xUnit (backend) · Vitest (frontend) |
| Orquestación | Docker Compose |

## Cómo correr

1. Copiar los archivos crudos del INAMHI en `info/`:
   `INAMHI_precipitacion_CRUDA.csv` e `INAMHI_temperatura_CRUDA.csv`.
2. `cp env.example .env` y definir `ADMIN_PASSWORD`.
3. `docker compose up --build`

| Servicio | URL |
|----------|-----|
| Aplicativo | http://localhost:5174 |
| API | http://localhost:8081 |
| PostgreSQL | localhost:5434 |

En el primer arranque la API carga ~2 millones de registros diarios (COPY binario, menos de un
minuto) y registra los hallazgos de control de calidad. Los arranques siguientes no recargan.

### Coordenadas de estaciones (opcional)

Los archivos crudos solo traen código y nombre. Para agregar ubicación, crear
`data/estaciones.csv` con el encabezado `codigo,latitud,longitud,altitud,provincia` y reiniciar
el backend.

## Pruebas

```bash
cd backend/tests && dotnet test
cd frontend && npm test
```

## Estructura

```
backend/   API .NET 9: Api → Application → Infrastructure, Domain/Entities, tests/
frontend/  React + TS por features: overview, stations, quality, repository, graph
data/      archivos del repositorio digital (servidos en /files)
info/      datos crudos del INAMHI (no versionados)
```

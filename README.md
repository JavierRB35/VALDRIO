# VALDRIO

Aplicación Windows local para cálculo y gestión de costes, en construcción guiada por especificación v0.4 y evidencia verificable.

## Estado actual

- **T00: CERRADA** — alcance confirmado; D01/D02 aceptadas.
- **T01: CERRADA** — solución .NET 10, CI Windows y shell WPF.
- **T01A: CERRADA** — interfaz internacional y preferencias independientes.
- **T02: CERRADA** — decimal canónico, unidades/IDs, PDEC-1, BalancedRound y proyección conciliada.
- **T02A: EN CURSO** — subentrega 1 completada: Money, CurrencyDefinition, catálogo fixture y minor units congelados.
- CI T02A.1: **39/39 tests**, Runner monetario, publish autocontenido y WPF smoke.
- JPY se proyecta con quantum 1; KWD con 0,001. No existe un 0,01 fijo.
- D04-D07 siguen siendo propuestas económicas.
- Siguiente subentrega: **T02A.2 — fechas canónicas, IClock y zonas**.

## Evidencia

- [Estado del proyecto](ESTADO_PROYECTO.md)
- [Decisiones D01-D12](docs/decisiones/DECISIONES_PENDIENTES.md)
- [T02A](docs/tareas/T02A.md)
- [Evidencia T02A subentrega 1](docs/evidencias/T02A_SUB1_2026-10-04.md)
- [T02](docs/tareas/T02.md)
- [Evidencia T02](docs/evidencias/T02_2026-10-04.md)
- [Fuentes y huellas](docs/fuentes/FUENTES_DISPONIBLES.md)

## Límites actuales

Todavía no se declara implementado:

- catálogo monetario completo de producto;
- fechas canónicas/IClock/horas ambiguas de T02A.2;
- FX/RateSet/RevalueStudy de T02A.3;
- SQLite productivo;
- importación contable;
- motor económico de repartos posterior.

La internacionalización y la precisión decimal permanecen desacopladas del cálculo y de las preferencias de presentación.

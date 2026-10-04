# VALDRIO

Aplicación Windows local para cálculo y gestión de costes, en construcción guiada por especificación v0.4 y evidencia verificable.

## Estado actual

- **T00: CERRADA** — alcance confirmado; D01/D02 aceptadas.
- **T01: CERRADA** — solución .NET 10, CI Windows y shell WPF.
- **T01A: CERRADA** — interfaz internacional y preferencias independientes.
- **T02: CERRADA** — decimal canónico, unidades/IDs, PDEC-1, BalancedRound y proyección conciliada.
- CI T02: **32/32 tests**, corpus **5/5**, guard sin `double/float`, publish autocontenido y WPF smoke.
- D04-D07 siguen siendo propuestas económicas.
- Siguiente tarea: **T02A — Money, monedas, fechas canónicas y tipos congelados**.

## Evidencia

- [Estado del proyecto](ESTADO_PROYECTO.md)
- [Decisiones D01-D12](docs/decisiones/DECISIONES_PENDIENTES.md)
- [T00](docs/tareas/T00.md)
- [T01](docs/tareas/T01.md)
- [T01A](docs/tareas/T01A.md)
- [T02](docs/tareas/T02.md)
- [Evidencia T02](docs/evidencias/T02_2026-10-04.md)
- [Fuentes y huellas](docs/fuentes/FUENTES_DISPONIBLES.md)

## Límites actuales

Todavía no se declara implementado:

- FX/RateSet y catálogo monetario completo;
- contratos temporales completos de T02A;
- SQLite productivo;
- importación contable;
- motor económico de repartos posterior.

La internacionalización y la precisión decimal ya forman parte de los fundamentos y deben conservarse en tareas posteriores.

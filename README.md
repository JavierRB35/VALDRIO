# VALDRIO

Aplicación Windows local para cálculo y gestión de costes, en construcción guiada por especificación v0.4 y evidencia verificable.

## Estado actual

- **T00: CERRADA** — alcance confirmado; D01/D02 aceptadas.
- **T01: CERRADA** — solución .NET 10, CI Windows y shell WPF.
- **T01A: CERRADA** — interfaz internacional y preferencias independientes.
- **T02: CERRADA** — decimal canónico, PDEC-1, BalancedRound y proyección conciliada.
- **T02A: CERRADA** — Money/metadatos, fechas canónicas, RateSet/FX y RevalueStudy verificados.
- CI de cierre T02A: **82/82 tests**, 39/39 referencias, Runner C33–C37/I14, guard sin red, publish y WPF smoke.
- Fecha de negocio, instante UTC y zona de presentación están separados.
- D04-D07 siguen siendo propuestas económicas.
- R07 de fuentes canónicas: **CERRADA** mediante PR #9.

## Evidencia

- [Estado del proyecto](ESTADO_PROYECTO.md)
- [Decisiones D01-D12](docs/decisiones/DECISIONES_PENDIENTES.md)
- [T02A](docs/tareas/T02A.md)
- [Evidencia T02A subentrega 1](docs/evidencias/T02A_SUB1_2026-10-04.md)
- [Evidencia T02A subentrega 2](docs/evidencias/T02A_SUB2_2026-10-04.md)
- [Evidencia T02A subentrega 3](docs/evidencias/T02A_SUB3_2026-10-05.md)
- [Evidencia R07](docs/evidencias/R07_2026-10-05.md)
- [T02](docs/tareas/T02.md)
- [Fuentes y huellas](docs/fuentes/FUENTES_DISPONIBLES.md)

## Límites actuales

Todavía no se declara implementado:

- catálogo monetario completo de producto;
- SQLite productivo;
- importación contable;
- motor económico de repartos posterior.

La internacionalización, precisión decimal, moneda y tiempo permanecen explícitos y desacoplados.

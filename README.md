# VALDRIO

Aplicación Windows local para cálculo y gestión de costes, en construcción guiada por especificación v0.4 y evidencia verificable.

## Estado actual

- **T00: CERRADA** — alcance confirmado; D01/D02 aceptadas.
- **T01: CERRADA** — solución .NET 10, CI Windows y shell WPF.
- **T01A: CERRADA** — interfaz inglés/español, región y zona independientes, preferencias por usuario, edición con cultura capturada, pseudotraducción/RTL y evidencia visual.
- CI T01A: **16/16 tests**, Runner, publish win-x64 autocontenido y smoke WPF.
- Las propuestas D04-D07 siguen siendo propuestas.
- La siguiente tarea por dependencias es **T02 — Unidades, decimal, política y redondeo**.

## Evidencia

- [Estado del proyecto](ESTADO_PROYECTO.md)
- [T00](docs/tareas/T00.md)
- [Aceptación T00](docs/evidencias/T00_ACEPTACION_2026-10-04.md)
- [Decisiones D01-D12](docs/decisiones/DECISIONES_PENDIENTES.md)
- [T01](docs/tareas/T01.md)
- [T01A](docs/tareas/T01A.md)
- [Evidencia T01A](docs/evidencias/T01A_2026-10-04.md)
- [Fuentes y huellas](docs/fuentes/FUENTES_DISPONIBLES.md)

## Límite actual

Todavía no se declara implementado:

- motor económico;
- Money/FX;
- SQLite productivo;
- importación contable.

La presentación internacional ya está en los fundamentos y las tareas posteriores deben conservarla.

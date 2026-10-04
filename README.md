# VALDRIO

Aplicación Windows local para cálculo y gestión de costes, en construcción guiada por especificación v0.4 y evidencia verificable.

## Estado actual

- El paquete de desarrollo v0.4 está disponible, íntegro y reconciliado.
- **T00**: pendiente únicamente de revisión del propietario del flujo y de las decisiones identificadas.
- **T01**: implementada y revalidada técnicamente contra su ficha real; cierre formal pendiente de T00.
- CI Windows: build verde, **4/4 tests**, Runner ejecutado, publish `win-x64` autocontenido y smoke WPF verde.
- Las propuestas económicas siguen siendo propuestas; no se han convertido en reglas definitivas.
- **T01A aún no se ha iniciado**.

## Evidencia

- [Estado del proyecto](ESTADO_PROYECTO.md)
- [T00](docs/tareas/T00.md)
- [Revisión de usuario T00](docs/revision/T00_REVISION_USUARIO.md)
- [Evidencia del paquete v0.4](docs/evidencias/T00_PAQUETE_2026-10-04.md)
- [Decisiones D01-D12](docs/decisiones/DECISIONES_PENDIENTES.md)
- [T01 reconciliada](docs/tareas/T01.md)
- [Evidencia de revalidación T01](docs/evidencias/T01_REVALIDACION_PAQUETE_2026-10-04.md)
- [Fuentes y huellas](docs/fuentes/FUENTES_DISPONIBLES.md)

## Límite actual

No se declara implementado todavía:

- motor económico;
- persistencia SQLite productiva;
- importación contable;
- Money/FX;
- preferencias dinámicas de idioma/región/zona;
- T01A o tareas posteriores.

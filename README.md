# VALDRIO

Aplicación Windows local para cálculo y gestión de costes, en construcción guiada por especificación v0.4 y evidencia verificable.

## Estado actual

- **T00: CERRADA** — alcance confirmado; D01/D02 aceptadas.
- **T01: CERRADA** — solución .NET 10, CI Windows y shell WPF.
- **T01A: CERRADA** — interfaz internacional y preferencias independientes.
- **T02: CERRADA** — decimal canónico, PDEC-1, BalancedRound y proyección conciliada.
- **T02A: CERRADA** — Money/metadatos, fechas canónicas, RateSet/FX y contrato de orquestación de RevalueStudy verificados; integración SQLite pendiente.
- **T03: CERRADA** — archivo SQLite real, bloqueo OS, unidades/auditoría, copias/restauración y flujo WPF. **132/132 tests**, 24 checks WPF de archivos; cierre integrado con CI final aprobada.
- Correcciones A01–A06 completadas: **114/114 tests**, 39/39 referencias, 12 comprobaciones reales del botón FX, publish y WPF smoke.
- JSON canónico versionado, huellas FX v2, cuantización exacta e inmutabilidad preparados para persistencia.
- Fecha de negocio, instante UTC y zona de presentación están separados.
- D04-D07 siguen siendo propuestas económicas.
- R07 de fuentes canónicas: **CERRADA** mediante PR #9.

## Evidencia

- [Cierre de auditoría A01–A06](docs/evidencias/AUDITORIA_CIERRE_2026-10-05.md)
- [Evidencia T03](docs/evidencias/T03_2026-10-05.md)
- [Directrices de la siguiente fase T04](docs/directrices/T04_SIGUIENTE_FASE.md)
- [Contrato técnico del archivo](docs/adr/ADR-0002-archivo-empresa.md)

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
- importación/cálculo sobre el archivo de empresa SQLite;
- importación contable;
- motor económico de repartos posterior.

La internacionalización, precisión decimal, moneda y tiempo permanecen explícitos y desacoplados.

## Probar el archivo de empresa

En Windows, ejecutar Valdrio.Desktop y usar Crear archivo con nombre, moneda sugerida y una ruta nueva `.valdrio`. Guardar una unidad, crear copia, cerrar y volver a abrir. Restaurar pide un destino nuevo. El archivo activo se conserva si falla otra apertura. No utilizar una carpeta de red o con sincronización activa.

Para reproducir los ejemplos sintéticos de CI: `Valdrio.Desktop.exe --capture-dir=<carpeta-de-evidencia>`; genera `company-file/interaction.json`, ejemplos SQLite y capturas. El artifact Windows es de desarrollo, todavía sin instalador/cifrado comercial ni motor económico completo.

# Cierre de auditoría VALDRIO — 2026-10-06

## Perímetro y procedencia

Base remota `audit/t06-import-hardening-20261006@8e193c9d5e909f7c9be34532f2dfdbe331e45f16`. Las 260 fuentes de partida se reconstruyeron y verificaron por SHA de blob. El cambio conserva esa historia, incluidos N01–N05 y la sincronización anterior con T05. La antigua referencia de `main` es `99c9dec289cb749094f6036c6b3f17bb3841e855`.

Este bloque corrige los defectos identificados en la auditoría más reciente y completa la entrega técnica T06.1 cuando pasa la aceptación Windows del HEAD final. No certifica ausencia absoluta de defectos, aprobación D03 ni implementación de T06.2–T06.6.

## Hallazgos corregidos

| ID | Cambio | Regresión |
|---|---|---|
| E01 | Guardas explícitas de Worksheet/Stylesheet en la fixture de prueba; se mantienen Nullable y TreatWarningsAsErrors | Build de solución completa, sin avisos ni errores |
| E02 | Create/Save/Publish/Clone de modelos, Create/Change de centros y ChangeUnitState aplican resultado confirmado antes de releer; aviso y botón de refresco; revisión confirmada visible | Ocho operaciones reales contra SQLite con fallo de lectura posterior; recuperación sin repetir la escritura; edición pendiente/revisión original conservadas; conciliación de selección limpia |
| E03 | Selección de sección efectiva y condiciones numéricas exactas antes de clasificar fecha; AM/PM y A/P son tokens horarios | Quince libros válidos según Open XML, positivos/negativos/cero, condiciones/fallback, precisión, escapes y controles; formato regional ambiguo bloqueado |
| E04 | Fecha ISO preservada como DateIso; fecha serial/ISO y caché de fórmula no son identidad de cuenta | Ocho combinaciones de tipo/caché/política numérica y conservación lexical |
| E05 | TryGetPartById y coordenadas limitadas al rango XLSX; duplicados y estilos inexistentes rechazados explícitamente | Relación ausente, coordenada desbordada/fuera de hoja, celda/fila duplicada y estilo inexistente devuelven IMP_XLSX_INVALID |
| E06 | Serial de fecha exige fracción lexical exactamente cero antes de convertir días | Fracciones bajo la precisión de Decimal, underflow, cero exacto, sistemas 1900/1904 y diagnóstico de fecha inválida mapeada |
| E07 | CreateStudy nace abierto y UpdateStudyContext no cambia estado; ChangeStudyState conserva su contrato | Cambio de operación tras cierre legítimo y creación manipulada a cerrado rechazados como FILE_INVALID_DATA |

Las regresiones previas N01–N05 se mantienen. Ninguna expectativa existente se elimina ni se rebaja. La inspección de staging no escribe recursos ni revisión/auditoría del estudio.

## Mejoras incorporadas

- Límites configurables de entrada, expansión ZIP, partes XML, filas, columnas, texto, shared strings y entradas del paquete; rechazo controlado y cancelación en recorridos. Los valores y formatos soportados constan en [T06](../tareas/T06.md).
- ReadWorkspace obtiene centros/unidades/modelos de SQLite en una sola transacción y una sola validación completa; las validaciones de integridad se mantienen. GetCell usa un índice por columna y la inspección calcula columnas mapeadas una sola vez.
- La UI muestra el fallo de guardar preferencias en el idioma seleccionado y permite reintentar, conservando el cambio aplicado en memoria y los demás suscriptores.
- Las fichas de estado separan la cronología histórica del estado vigente. T05 está integrada; la entrega técnica T06.1 tiene una puerta de aceptación explícita.
- La aceptación exige repo limpio, HEAD estable y manifest v2 con hashes recursivos de outputs, TRX y binarios. La integración se hará únicamente con checks del HEAD final aprobado.

## Verificación local del árbol funcional

SDK .NET **10.0.401**. Solución completa en Release con EnableWindowsTargeting, Nullable y avisos como errores. **0 avisos, 0 errores**. Suite completa **271/271**, **0 fallos y 0 omitidas**, incluidos ViewModels de producción contra SQLite real y los libros de regresión validados según Office2010/Open XML.

TRX `closure-final-local.trx`, SHA-256 `6c70054d9dbf8d38bcd5dc404e83834ba590c08a9ea457aa03a97f3111376ad6`. La prueba de WPF aquí es de compilación; la interacción nativa se valida en Windows. Las 39 referencias matemáticas Python/Fraction pasan y las fuentes canónicas e históricas permanecen sin cambios.

## Aceptación Windows e integración

Pendiente de ejecutar sobre el commit remoto que contiene este árbol. El run anterior 37429542751 certifica exclusivamente la base anterior `b36cc2f…`; no se usa como evidencia del bloque nuevo. Se registrarán el run aprobado, SourceCommit, identidad del árbol y hashes de receipt/build/TRX antes del cierre.

## Trabajo posterior

D03 pendiente de muestra real aprobada. PrepareImport completo, conciliación/comparativos, Debe/Haber, confirmación/idempotencia/rollback v4, recursos, FX y WPF/runner T06 siguen pendientes. La siguiente entrega es T06.2 según sus directrices y no forma parte de este cierre técnico.

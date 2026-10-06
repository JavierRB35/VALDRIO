# Cierre de auditoría VALDRIO — 2026-10-06

## Perímetro y procedencia

Base remota `audit/t06-import-hardening-20261006@8e193c9d5e909f7c9be34532f2dfdbe331e45f16`. Las 260 fuentes de partida se reconstruyeron y verificaron por SHA de blob. El cambio conserva esa historia, incluidos N01–N05 y la sincronización anterior con T05. La antigua referencia de `main` es `99c9dec289cb749094f6036c6b3f17bb3841e855`.

Este bloque corrige los defectos identificados en la auditoría más reciente y constituye el cierre técnico de T06.1. Su integración se condiciona a la aceptación Windows aprobada del HEAD final de la PR #17. No certifica ausencia absoluta de defectos, aprobación D03 ni implementación de T06.2–T06.6.

## Hallazgos corregidos

| ID | Cambio | Regresión |
|---|---|---|
| E01 | Guardas explícitas de Worksheet/Stylesheet en la fixture de prueba; se mantienen Nullable y TreatWarningsAsErrors | Build de solución completa, sin avisos ni errores |
| E02 | Create/Save/Publish/Clone de modelos, Create/Change de centros y ChangeUnitState aplican resultado confirmado antes de releer; aviso y botón de refresco; revisión confirmada visible | Ocho operaciones reales contra SQLite con fallo de lectura posterior; recuperación sin repetir la escritura; edición pendiente/revisión original conservadas; conciliación de selección limpia |
| E03 | Selección de sección efectiva y condiciones numéricas exactas antes de clasificar fecha; AM/PM y A/P son tokens horarios; mmm/mmmm/mmmmm conservan el significado de mes; tokens de calendario no soportados se bloquean | Veinticuatro libros válidos según Open XML, positivos/negativos/cero, condiciones/fallback, precisión, escapes, meses y controles General/exponente; formato regional ambiguo y tres calendarios no soportados bloqueados |
| E04 | Fecha ISO preservada como DateIso; fecha serial/ISO y caché de fórmula no son identidad de cuenta | Ocho combinaciones de tipo/caché/política numérica y conservación lexical |
| E05 | TryGetPartById y coordenadas limitadas al rango XLSX; duplicados y estilos inexistentes rechazados explícitamente | Relación ausente, coordenada desbordada/fuera de hoja, celda/fila duplicada y estilo inexistente devuelven IMP_XLSX_INVALID |
| E06 | Serial de fecha exige fracción lexical exactamente cero antes de convertir días | Fracciones bajo la precisión de Decimal, underflow, cero exacto, sistemas 1900/1904 y diagnóstico de fecha inválida mapeada |
| E07 | CreateStudy nace abierto y UpdateStudyContext no cambia estado; ChangeStudyState conserva su contrato | Cambio de operación tras cierre legítimo y creación manipulada a cerrado rechazados como FILE_INVALID_DATA |

Las regresiones previas N01–N05 se mantienen. Ninguna expectativa existente se elimina ni se rebaja. La inspección de staging no escribe recursos ni revisión/auditoría del estudio.

## Mejoras incorporadas

- Límites configurables de entrada, expansión ZIP, partes XML, filas, columnas, texto, shared strings y entradas del paquete; rechazo controlado y cancelación en recorridos. El límite de entrada no puede superar int.MaxValue, capacidad de direccionamiento del MemoryStream utilizado; dos regresiones cubren configuraciones desbordadas. Los valores y formatos soportados constan en [T06](../tareas/T06.md).
- ReadWorkspace obtiene centros/unidades/modelos de SQLite en una sola transacción y una sola validación completa; las validaciones de integridad se mantienen. GetCell usa un índice por columna y la inspección calcula columnas mapeadas una sola vez.
- La UI muestra el fallo de guardar preferencias en el idioma seleccionado y permite reintentar, conservando el cambio aplicado en memoria y los demás suscriptores.
- Las fichas de estado separan la cronología histórica del estado vigente. T05 está integrada; la entrega técnica T06.1 tiene una puerta de aceptación explícita.
- La aceptación exige repo limpio, HEAD estable y manifest v2 con hashes recursivos de outputs, TRX y binarios. La integración se hará únicamente con checks del HEAD final aprobado.

## Verificación local del árbol funcional

SDK .NET **10.0.401**. Solución completa en Release con EnableWindowsTargeting, Nullable y avisos como errores. **0 avisos, 0 errores**. Suite completa **285/285**, **0 fallos y 0 omitidas**, incluidos ViewModels de producción contra SQLite real y los libros de regresión validados según Office2010/Open XML. Se conservan las 208 pruebas previas y se añaden 77 casos.

TRX `closure-final-285.trx`, SHA-256 `2897a957f43dd5480965af9276e029f02043b628939ee89a567015f7a9c56d06`. La prueba de WPF aquí es de compilación; la interacción nativa se valida en Windows. Las 39 referencias matemáticas Python/Fraction pasan y las fuentes canónicas e históricas permanecen sin cambios.

## Aceptación Windows e integración

La primera aceptación del bloque, [run 37441375663](https://github.com/JavierRB35/VALDRIO/actions/runs/37441375663), está **aprobada**. Certifica `66faa2a891db33082ec7eece401f21e5dc008d8a` mediante checkout `4a7ebe91ea860af7ecbb9ded7ae936645301e24b`, cuyos padres son `main@99c9dec…` y ese HEAD. Ambos árboles coinciden exactamente en `51453e9dbbc0628e759a3df34b22b1572609867b`.

Resultados de esa primera aceptación — 271/271 pruebas Windows; 73 checks WPF de estudios, 10 de recuperación, 53 de modelos, 24 de archivos y 12 FX; ocho imágenes de estudios; 71/71 pruebas en cada proceso UTC/Madrid. Los artifacts se descargaron y verificaron — SourceCommit, manifest v2, TRX y hashes de los once archivos incluidos en los manifests de modelos/estudios. La revisión visual del panel de modelos confirma el botón Actualizar y textos sin recorte.

- Build manifest SHA-256 `dfc7bc7396e47dc66ee0949978d720a6a0e7b380742c54764092bfab022bbc30`.
- TRX Windows SHA-256 `ae909d4390fd39c567cb39dcbdb1c99035c5fa2003f0c27b467b40c268ec7b0c`.
- ZIP del artifact SHA-256 `af7777f83bc733af2435fba07a5190c71ebbeccccb9fbf3634bcf2be115acacb`.

La segunda aceptación, [run 37442263939](https://github.com/JavierRB35/VALDRIO/actions/runs/37442263939), también está aprobada sobre `9f6c433921ecf78f8c2bf7ac89ebfccedeb140b9`. Incluye cuatro casos de literales entre comillas que terminan en `_`, `*` o una barra inversa; esos caracteres dentro de un literal no activan escape/relleno.

La revisión final añade los meses largos junto a tokens horarios, controles General/exponente, bloqueo explícito de calendarios no soportados y protección del límite de entrada. La suite local final pasa 285/285. Las dos aceptaciones anteriores se conservan como evidencia intermedia y **no sustituyen los checks del HEAD final**.

El recibo definitivo es `verification.json` del artifact `valdrio-wpf-visual-evidence` del run aprobado asociado al último HEAD de [PR #17](https://github.com/JavierRB35/VALDRIO/pull/17), y después del run de `main`. Los runs y hashes definitivos se registran también en la descripción de la PR sin cambiar el código después de verificarlo. El workflow incluye las fichas de estado/evidencia en sus paths para validar también el commit de cierre. Se integra mediante merge real con SHA esperado, sin rebase ni force-push.

## Trabajo posterior

D03 pendiente de muestra real aprobada. PrepareImport completo, conciliación/comparativos, Debe/Haber, confirmación/idempotencia/rollback v4, recursos, FX y WPF/runner T06 siguen pendientes. La siguiente entrega es T06.2 según sus directrices y no forma parte de este cierre técnico.

# VALDRIO — Importación con columnas Debe/Haber

Fecha 2026-10-06. Base main@933f854a9223cd70494c4c3d3a89712c72f60153. Rama `feature/import-debit-credit-20261006`.

## Recorrido disponible

En Estudios → Seleccionar columnas de importación…, abrir CSV/XLSX y declarar su estructura. Elegir **Debe menos Haber en dos columnas** y seleccionar cuenta, Debe y Haber. Validar todas las filas; la vista previa muestra ambos originales y la diferencia. Declarar moneda y un ejemplo comprobado, aceptar la convención para el periodo del estudio, preparar/revisar totales y confirmar.

Se conserva el modo de una columna de importe firmado. No hay selección por nombre, importes vacíos como cero ni interpretación por región. Preparar no escribe. La confirmación guarda el lote completo y ambas celdas de origen; cambiar configuración exige volver a validar y aprobar.

## Verificación

SDK 10.0.401. Release completo, WPF mediante EnableWindowsTargeting, sin errores ni avisos. Suite local 389/389, sin fallos ni omisiones, conservando las 348 pruebas anteriores y añadiendo 41 casos. TRX `debit-credit-full.trx`, SHA-256 `e13e2b534299f6df61a6aa109627c06c88948a9aef95c28cb5a3bf6710f43cd4`. La aceptación nativa del último HEAD y main se registra en la PR asociada; el resultado local no la sustituye y un HEAD nuevo exige su propia verificación.

`T06DebitCreditImportTests` comprueba originales y resta exacta, positivos/abonos/ceros, error intermedio y Missing, overflow, cancelación de operandos con precisión/magnitud excesivas, límite de la diferencia, sintaxis de negativos, aprobación de negocio/convención, FX ausente, duplicados, SQLite real, idempotencia, rollback/fallo/cancelación tras escrituras reales, snapshot/runner y backup. También prueba cambios de modo/columnas, cuatro formatos regionales, pseudo, fallo de refresco posterior al commit, segunda hoja XLSX y tipos incompatibles en Haber, con cobertura EN/ES.

La fixture genuina v4 nueva procede del run de main 37458418151 y conserva su SourceCommit, digest de artifact y SHA del archivo. El lector nuevo debe mantener bytes, lote canónico, snapshots v1/v2 y hashes anteriores; no se reconstruye con el mapping actual. No se cambian las fixtures previas ni las fuentes/casos canónicos v0.4.

Windows añade 22 comprobaciones con controles WPF, tres capturas y manifest de seis archivos en `import-debit-credit`. CSV importa 160, −7,50 y 0; XLSX añade 160 desde la hoja elegida y una fórmula cached, con ambos originales conservados. El estudio llega a revisión 2, cuatro recursos y neto 312,50. Se verifican historia, snapshots, lector independiente, cierre/reapertura, backup/restauración, cambios de interpretación y preservación del artifact anterior.

`Verify-Windows.ps1` exige estos checks/imágenes/manifests y ejecuta runner sobre la misma foto de WPF. Su recibo mantiene SourceCommit/build/TRX/hashes y añade DebitCreditChecks/Images/Manifest. La suite UTC/Madrid incorpora los 41 casos, 147 por proceso.

## Límites

Esta entrega confirma importes de periodo en moneda funcional, firmados o Debe menos Haber. Los cierres, acumulados/comparativos, agrupación, reemplazo, FX y coste completo siguen pendientes. D03 continúa pendiente de muestra productiva aprobada; la evidencia técnica usa fuentes sintéticas.

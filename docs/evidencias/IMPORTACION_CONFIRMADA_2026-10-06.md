# VALDRIO — Confirmación de la selección de columnas

Fecha 2026-10-06. Base remota main `776532e4a5d7b51c0ff3e1ade33b8a4826d9104c`. Rama `feature/import-confirmation-20261006`.

## Resultado

El selector CSV/XLSX conserva hoja/columnas explícitas y vista previa. Ahora prepara y confirma lotes de importes firmados de periodo en la moneda funcional del estudio. Los recursos y la evidencia se guardan atómicamente en formato v4 y quedan disponibles al reabrir, capturar o restaurar backup.

Recorrido en Estudios → Seleccionar columnas de importación…

1. Abrir o guardar un estudio y resolver sus ediciones pendientes.
2. Seleccionar CSV/XLSX, hoja, encabezados/datos y columnas de cuenta/importe.
3. Validar todas las filas, incluidos ceros, negativos y ceros iniciales del código.
4. Declarar moneda ISO y un ejemplo de cuenta/importe que se ha comprobado. Aceptar que son importes firmados para el periodo visible.
5. Preparar y revisar recuentos y totales por signo. Los bloqueos impiden confirmar.
6. Confirmar. Se muestra el lote guardado y revisión nueva; no puede duplicarse por segundo clic.
7. Cerrar el selector. El estudio muestra sus lotes y puede capturar un input v2 con los recursos reales.

La aplicación conserva cada celda extraída y su ubicación; no almacena el workbook byte a byte. No cambia país/moneda/signos por la región de pantalla. Un archivo modificado después del preview exige una nueva preparación.

## Alcance y decisiones

Admite PeriodAmount/SignedAmount, moneda funcional, Reject de duplicados y AppendBatch. La UI conserva ceros y selecciona todas las cuentas del archivo. Cierres, diferencias de acumulados, Debe/Haber, agrupación y sustitución de lotes siguen pendientes. El puente FX y particiones/ejecución de costes pertenecen a sus tareas siguientes.

D03 no se declara aceptada para una fuente productiva. No se ha recibido una muestra real del propietario. Las pruebas usan ejemplos sintéticos explícitamente comprobados; perfiles pendientes/sintéticos sin aprobación bloquean confirmación. La UI registra la declaración de interpretación por lote, sin inferirla por encabezado o tipo de cuenta. D04–D07 no cambian.

## Comprobaciones locales reales

SDK 10.0.401. Compilación Release completa, incluido WPF mediante EnableWindowsTargeting, 0 errores y 0 avisos. Suite completa 348/348, 0 fallidas, 0 omitidas. Se mantienen las 313 anteriores y se añaden 35 casos de confirmación.

TRX local `import-confirmation-full.trx`, SHA-256 `0b50bf859b71bc0629c1e414114b5e212decd9b9bdefc3c205164e9357dedd02`. Este receipt local no sustituye la aceptación nativa del HEAD remoto.

`T06ImportConfirmationTests` cubre preparación sin mutación/cancelación, v3 genuino y sus cinco snapshots, mapping v4 sin diferencias, guardado/lectura en conexión real SQLite, importes originales y lexical quoted, perfiles pendientes, modos no disponibles, FX ausente, fila inválida intermedia y precisión, duplicados de cuenta/lote, idempotencia, scope/revisión/estado, rollback real después del primer write y antes de commit con fallo/cancelación, schemas v1/v2 estrictos, runner real, contexto protegido, backup/restauración, triggers/FK, cultura, confirmación tras fallo de refresco/cancelación tardía y cambio externo del archivo. Excel confirma la segunda hoja y el valor numérico lexical almacenado sin double.

La ampliación necesaria de las expectativas de migración es explícita, ahora hay tres pasos 1→4 y un paso adicional en una copia que ya contenía el evento 1→2. Las comprobaciones de historia/fuentes anteriores no se eliminan ni se suprimen avisos EF.

## Aceptación Windows y procedencia

El flujo nativo anterior se conserva. Se añaden 18 comprobaciones de confirmación WPF y dos capturas en `import-confirmation`, con controles reales para selección/aprobación/preparación/confirmación, lectura independiente, snapshot v2, cierre/reapertura, backup/restauración y bloqueo de moneda con recursos. Las fuentes son sintéticas y separadas del archivo ya publicado por el recorrido T05.

`Verify-Windows.ps1` exige 348 pruebas sin omisiones, 13 comprobaciones anteriores del selector, 18 nuevas de confirmación, imágenes y manifest de cinco archivos SHA-256. Runner verifica la misma foto producida por WPF. El receipt añade ImportConfirmationChecks/Images/Manifest y mantiene hashes de build/TRX/manifest y SourceCommit.

El workflow incluye ahora la clase de confirmación en las dos ejecuciones UTC/Europe-Madrid. Las fuentes canónicas v0.4, corpus matemático y fixtures originales conservan sus hashes.

La PR asociada registra el run del último HEAD y el run de main después de integrar, con recibo/manifest/hashes contrastados. Solo tras esa aceptación se declara integrado este ámbito. No se atribuye a un HEAD nuevo un resultado de su predecesor.

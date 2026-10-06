# ADR-0007 — Columnas Debe/Haber con confirmación de periodo

Fecha 2026-10-06. Ampliación de T06 sobre main@933f854a9223cd70494c4c3d3a89712c72f60153. D03 general continúa pendiente de muestra real aprobada.

## Interpretación

El usuario elige entre una columna de importe firmado y dos columnas Debe/Haber. Cuenta, Debe y Haber deben ser columnas distintas y existentes, seleccionadas explícitamente por posición/encabezado. No se infiere la fórmula por nombres, país o plan contable.

Con `DebitMinusCredit`, importe de periodo = Debe − Haber. Por ejemplo, 180 − 20 = 160; 0 − 7,50 = −7,50; 0 − 0 = 0. La aprobación por lote muestra esta fórmula y conserva la referencia del ejemplo comprobado. Cambiar interpretación o columnas invalida preparado, mapeo y aprobación; idioma/región/pseudo/RTL conservan la elección.

Ambos operandos requieren un valor explícito. Missing, vacío, texto inválido, fecha, booleano, error o fórmula sin caché no son cero. La gramática de signos textuales se aplica a cada operando; no se toman valores absolutos. Los números XLSX conservan su lexical almacenado y se leen independientemente del formato visual.

La resta es decimal exacta y comprobada. Overflow bloquea la fila. Preparación valida escala y magnitud de cada operando y de la diferencia bajo la política congelada del archivo; dos originales inválidos no se aceptan por cancelarse en una diferencia cero.

## Datos e historia

El recurso conserva el importe de partida interpretado y su moneda fija declarada. Las celdas Debe y Haber, sus posiciones, lexical/tipos/fórmulas/cachés y el perfil permanecen por separado en el original del lote. No se inventa un segundo recurso para el Haber ni una conversión FX.

El perfil v1 ya define `DebitMinusCredit` y los campos Debit/Credit. Esta entrega implementa esa convención prevista, manteniendo la forma canónica de lotes v4 e inputs v2. No añade propiedades a los contratos persistidos ni reescribe mappings, migraciones o snapshots. Los valores nuevos no son interpretables por binarios anteriores que bloqueaban esta convención; estos fallan al validar, sin reinterpretarla como importe firmado.

La fixture nueva `t06-period-v4.json` proviene del archivo real de aceptación WPF de main@933f854a…, run 37458418151, artifact 11410377899. Acredita apertura del v4 anterior, lote firmado, hashes y snapshots v1/v2 idénticos con el nuevo lector. Las fixtures históricas existentes permanecen intactas.

Confirmación reutiliza revisión/scope/estado, recomputación en transacción, idempotencia, bloqueo de fuente/configuración repetidas, inmutabilidad y rollback. El snapshot y runner congelan los recursos reales de ambas convenciones. Una moneda extranjera sigue bloqueada sin puente FX.

## Alcance pendiente

ClosingBalance, diferencias de acumulados y su comparativo/corte de ejercicio, agrupación, reemplazo, FX y cálculo de costes conservan sus tareas pendientes. Un Debe/Haber de cierre o acumulado no se transforma en importe de periodo por elegir las columnas; la aprobación exige declarar que representa el periodo mostrado.

La evidencia de esta entrega está en [IMPORTACION_DEBE_HABER_2026-10-06.md](../evidencias/IMPORTACION_DEBE_HABER_2026-10-06.md). La PR asociada registra la aceptación del HEAD y main por SHA.

# ADR-0006 — Confirmación de importes de periodo y formato v4

Fecha 2026-10-06. Subentrega de T06. No declara T06/T06A completas ni acepta D03 para una fuente real sin ejemplo.

## Contrato de aplicación

`IImportWorkspace.PrepareImport` recibe empresa, estudio/revisión, ID, fuente capturada y perfil explícito. Lee en transacción sin escritura. Devuelve `PreparedImport` inmutable y reproducible, con contexto/política congelados, filas, diagnósticos localizados, recuentos y totales por signo/moneda. El perfil no depende de la presentación. Recalcular el mismo ID/origen/perfil/contexto produce la misma huella.

Solo `PeriodAmount` y `SignedAmount` con cuentas duplicadas rechazadas se confirman en esta entrega. Cero explícito se conserva; ausencia/importe inválido no crea Money. Escala y magnitud siguen la política del archivo. El contrato también permite excluir ceros explícitamente conservando todas las filas de origen. La UI importa todas las filas válidas y conserva ceros. No filtra cuentas 6/7 ni asigna rutas o partes ficticias.

Un perfil `PendingBusinessApproval` o `SyntheticFixture` no puede confirmarse. La UI exige una referencia de ejemplo comprobado y una aceptación expresa de importes firmados, periodo y moneda. Las fechas coinciden con las del estudio, visibles en ISO. Un original en otra moneda sigue identificado con su moneda y queda bloqueado por `IMP_FX_REQUIRED`; no utiliza el ejemplo de tipos de T02A. El catálogo funcional procede del estudio, nunca de la región.

Cerrar, acumulados, Debe/Haber, agrupación y reemplazo requieren sus contratos pendientes. Se bloquean las modalidades no disponibles, sin convertirlas en importe de periodo o inventar comparativos. Un importe 160 declarado de periodo sigue siendo 160.

## Confirmación y repetición

`ConfirmImport` contiene ámbito, revisión esperada, RequestId, preparado/huella, AppendBatch y actor. En la transacción vuelve a comprobar contexto/política/revisión/estado abierto y recalcula toda la preparación. Una fila bloqueante impide el lote completo. El objeto mutable de UI no es una fila autorizada del commit.

RequestId identifica el lote. Replay idéntico devuelve el mismo recibo, sin otra revisión; otro actor o preparado no se acepta. Una segunda solicitud con el mismo archivo y configuración económica se bloquea aunque cambien el ID/nombre del perfil, preparado o referencia de aprobación. No hay confirmación forzada ni DELETE global.

Se guardan lote, filas, recursos, enlaces, Study.Revision+1 y eventos Study/ImportBatch en una transacción. `ConfirmImport` tiene handler explícito en la cadena del estudio. Sus referencias y el evento de lote se verifican al reabrir, sin disfrazarlo como UpdateStudyContext. Company.Revision y el publicado seleccionado no cambian.

Tras commit la UI aplica el recibo antes de refrescar. Cancelación tardía o fallo de refresco conservan la confirmación y bloquean el segundo clic. Una lectura posterior puede reconciliar la revisión visible. La UI reabre el origen antes de confirmar y compara el hash de los bytes capturados; cambios externos exigen abrirlo y preparar de nuevo.

## Archivo v4

`ImportDbContext`, `V4Model`, migración `202610060004_ImportedResources` y snapshot propios. Los mappings/snapshots/migraciones v1/v2/v3 permanecen congelados. Upgrade migra una nueva copia y conserva eventos anteriores. Rutas admitidas 1→2→3→4, 2→3→4 y 3→4; el original no se abre para escritura. Abrir v3 directamente pide Upgrade.

Tablas ImportBatches, ImportedRows, Resources y ResourceSourceLinks. FK compuestas atan empresa/estudio/lote/fila/recurso. No hay cascadas de borrado. Actualizar o eliminar filas confirmadas se rechaza con triggers. Los importes se almacenan como TEXT decimal canónico. Reabrir valida columnas, datos canónicos, enlaces, recuentos, contexto y auditoría contra el lote completo.

Se guardan celdas originales, tipos, lexical, fórmula/caché, ubicación y perfil. FileHash acredita el archivo capturado. No se almacenan todos sus bytes ni se promete reconstruir un workbook exacto a partir de las celdas extraídas.

Los recursos en moneda funcional conservan Original=Functional; no se crea una aplicación FX externa. Sus UUID se derivan determinísticamente del ID del lote y número de registro. Abrir un nuevo lote Append puede añadir recursos de la misma cuenta provenientes de otra fuente; el efecto se muestra antes de confirmar. La fuente exacta repetida no se duplica inadvertidamente.

Con importaciones presentes, cambiar periodo, moneda funcional o modelo requiere otro estudio o su transformación correspondiente. Renombrar, cerrar/reabrir, consultar historia, capturar y backup/restauración siguen disponibles. Cambiar preferencias no toca el archivo ni una interpretación.

## Inputs, hashes y runner

`valdrio-study-input-v1` continúa contextual, con recursos vacíos y los hashes anteriores idénticos. `StudySnapshotReceipt.Input` despacha a DTOs tipados por esquema. Un estudio sin importaciones conserva v1; con importaciones usa `valdrio-study-input-v2` y `period-import-t06-v1`.

v2 congela los lotes confirmados hasta su revisión y sus recursos. Round-trip estricto rechaza campos borrados, desconocidos o incoherentes, incluidos originales/recursos/totales calculados. La reapertura compara los lotes del snapshot con el ledger a esa revisión. Un snapshot v1 antiguo no se regenera después de importar.

EconomicInputHash v2 incluye contexto económico aplicado y recursos con IDs, cuenta, importes/moneda/política y periodo. Procedencia, celdas, perfil y referencia de aprobación también forman parte de FullSnapshotHash. Los IDs de un lote nuevo son diferentes; no se promete igualdad económica de dos importaciones independientes con UUID distintos. Repetir una captura o cambiar nombre/preferencias no altera la economía congelada del mismo ledger.

Runner `--study-snapshot` verifica ambos esquemas sin DB/red y devuelve recuento y conciliaciones reales, con decimales invariantes. CI lo ejecuta sobre el receipt exportado por WPF. Recursos disponibles eliminan el diagnóstico ResourcesPending y añaden PartitionsPending. Políticas, actividad y consumos pendientes continúan bloqueando ReadyForCalculation. No existe todavía un cálculo de costes completo.

## Evidencia

[IMPORTACION_CONFIRMADA_2026-10-06.md](../evidencias/IMPORTACION_CONFIRMADA_2026-10-06.md) registra la suite y el protocolo nativo. La PR asociada conserva HEAD/merge, runs, receipt y hashes finales; CI de otro SHA no valida esta entrega.

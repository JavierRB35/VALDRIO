# ADR-0010 — Comparativos y sustitución identificada de importaciones

Fecha: 2026-10-07. Alcance: cierre funcional T06, moneda fuente igual a funcional.

## Decisión

El formato de empresa v6 conserva todas las filas, recursos, enlaces y eventos anteriores.
Una nueva importación puede indicar `ReplacesBatchId`; solo puede sustituir un lote vigente
del mismo estudio. La operación sigue siendo `ConfirmImport`, identificada por el recibo
inmutable y su relación de sustitución. Revisión, estado abierto, ámbito, idempotencia y
escritura transaccional se comprueban antes de confirmar. Los snapshots históricos no cambian.

`ActiveBatches` reproduce el ledger por revisión y retira solo el identificador sustituido.
Los inputs v4 contienen el historial completo, pero sus recursos y hash económico usan los
lotes vigentes. Los lectores de inputs v1/v2/v3 se conservan. El runner identifica los lotes
vigentes y sustituidos explícitamente. El historial de la interfaz también los distingue.

Se elimina la unicidad SQL de la huella de origen para permitir volver a una versión retirada.
La validación transaccional y la reapertura rechazan duplicados vigentes, incluso al reproducir
cada prefijo histórico. No se permite sustituir un lote por una copia idéntica del mismo lote.
No se eliminan recursos. T07 todavía no ha introducido particiones ni consumos que migrar.

## Diferencia de acumulados

Perfil v3 y preparado v3 congelan el documento anterior y su perfil independiente, la selección
de filas de ambos documentos, ejercicio común y referencia de ámbito comprobado. Cada fuente
se agrupa por código ordinal, sin usar descripciones, antes de restar. El anterior debe cerrar
el día previo al inicio del periodo del estudio; el actual cierra al final del estudio.
Ambos acumulados empiezan en el ejercicio declarado, de duración máxima de un año, que puede
ser distinto del año natural. No se cruza un cierre de ejercicio.

Los códigos seleccionados deben coincidir en ambos lados. Ausente no equivale a cero. Se
conservan ceros explícitos y signos, se validan operandos, agregados y diferencias con la
política decimal, y un error bloquea el lote completo. Los totales preparados corresponden
a las diferencias. La tabla comparativa muestra anterior, actual, diferencia y filas anteriores.

Las filas SQLite del lote corresponden al archivo actual. El documento anterior completo,
sus filas seleccionadas y sus aportaciones se conservan en el perfil y preparado canónicos del
lote, y en los snapshots; no se afirma que sean filas del archivo actual ni que se conserve
el workbook byte por byte. La UI vuelve a comprobar el SHA de ambos archivos antes del commit.

El perfil confirma explícitamente signos, moneda y comparabilidad. Cambiar esos datos revoca
la preparación/aprobación. No se usa una cotización de ejemplo ni se infiere un país o plan contable.
`PeriodAmount` conserva 160; `ClosingBalanceAsStudyAmount` conserva 160 con aprobación específica;
`CumulativeExpenseDifference` convierte 160 y 100 en 60. `ClosingBalance` sin esa aprobación
es staging de evidencia y continúa bloqueado para generar recursos de costes.

## Compatibilidad

Una nueva migración v6 modifica exclusivamente el índice de detección de duplicados. La
actualización opera sobre una copia y añade su evento de formato; la fuente queda intacta.
Se conservan mappings/migraciones v1–v5 y una fixture v5 genuina obtenida de un recorrido WPF
sintético anterior. Las propiedades nuevas nulas se omiten para preservar los JSON antiguos.

## Aceptación de negocio

El propietario aclara que su Excel es solo un ejemplo y que declara el cierre 31/10/2026 en
el formulario. Se comprueba técnicamente con esa fecha y 6005 = 358,35 EUR. Esto no acepta
una fuente productiva ni clasifica sus cuentas como costes. Cada futura importación real
requiere su propia selección, interpretación y aprobación. FX permanece en T06A.

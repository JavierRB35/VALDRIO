# Clasificación editable en T11 — 2026-10-09

**Rama:** feature/t11-reparto-secundario-cierre · PR #35, sin fusionar.

- La pantalla 6 ofrece «Composición y comportamiento del coste»: clasificación múltiple por cuenta, código y descripción, importe, estado y opción de revisar clasificaciones anteriores.
- Las partes Unclassified se muestran como **Fijo por defecto**, no como Fijo revisado; este supuesto se materializa como AssumedFixed cuando se cierra reparto primario. No se inventa driver/rango para esa hipótesis.
- Las cuentas no desglosadas se modifican masivamente como AssumedFixed, Variable (con unidad y rango explícitos y periodo del estudio) o MixedPending. Los componentes de cuentas desglosadas siguen en el editor detallado T07, sin aplanar la estructura.
- ClassifyBatch guarda todas las cuentas seleccionadas en una única transacción con revisión optimista y auditoría; no modifica importes, fuentes, rutas, asignaciones ni capas.
- Hasta que se cierre el reparto primario puede reclasificarse una cuenta, incluso varias veces y en varias sesiones. Cierre: confirma los valores fijos iniciales aún sin clasificar, bloquea mixtos pendientes y hace la edición de la clasificación de solo lectura. Reabrir reparto primario permite volver a corregir.
- AssumedFixed genera advertencia y no permite absorción definitiva sin revisar; no equivale a Fixed con contexto.
- No se altera schema SQL ni versiones canónicas v0.4; la nueva mutación se reproduce desde ResourceChanges.
- **Pendiente** de compilar .NET 10, pruebas C# y aceptación de WPF Windows. PR #35 continúa en borrador. GitHub Actions no se ejecutó; gasto 0 USD por estas operaciones.

## Garantías y pruebas adicionales de integración
Se añaden pruebas de mutación `ClassifyBatch` para actualización masiva, reclasificación posterior, conservación y rechazo de alteraciones de importes. La lectura de estudios antiguos conserva `Unclassified` hasta que el usuario confirma el cierre, evitando migraciones implícitas.
El cierre de clasificación se materializa antes del cierre del reparto en **dos transacciones auditadas consecutivas**, porque ya existen dos servicios de guardado independientes. Si falla el cierre del reparto, las suposiciones fijas quedan guardadas en la etapa aún abierta y el usuario puede modificarlas; no se proclama atomicidad entre ambas fases.

Las revisiones que cambian solamente el comportamiento sin cambiar importes/rutas no invalidan las asignaciones absolutas: `ResourceHistory.EconomicRevision` conserva la revisión de los datos monetarios o de destino. La revisión general del estudio, el recibo de cambios y la huella económica del snapshot siguen reflejando las clasificaciones nuevas.

## Continuación: fijo revisado
El selector masivo incorpora «Marcar fijo revisado» junto a «Marcar variable». Ambos exigen unidad de actividad, periodo del estudio y un rango explícito, almacenando `ResourceBehaviorContext`. «Volver a fijo por defecto» mantiene un supuesto sin verificar y retira el contexto, sin alterar importe, ruta, capa ni capacidad. La secuencia de revisiones queda incluida en la prueba de integración escrita, todavía sin ejecutar.

## Filtros y selección masiva
Búsqueda por código/descripción/estado, filtro de partidas modificadas o mixtas, selección de todas las visibles y limpieza de selección. Cambiar el filtro cancela la selección previa, evitando aplicar cambios a cuentas ocultas. Se conserva la transacción `ClassifyBatch` y la reapertura sin perder las modificaciones anteriores.

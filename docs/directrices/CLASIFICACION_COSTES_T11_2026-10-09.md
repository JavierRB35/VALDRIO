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

# PEND-UI-05 — Activar «Incluir» al introducir una asignación

Fecha: 2026-10-15. Estado: pendiente de implementación.

## Requisito del propietario

En las tablas de reparto, al rellenar el campo de asignación de una fila, activar automáticamente la casilla «Incluir» de esa misma fila, independientemente del tipo de asignación aplicado. El requisito afecta a los repartos primarios y secundarios donde existan ese campo y esa casilla.

## Criterios de aceptación

- Introducir un dato no vacío en el campo de asignación de una fila editable activa «Incluir» sin un clic adicional, tanto mediante escritura como mediante pegado.
- El comportamiento se aplica a todas las modalidades con entrada de asignación; no se limita a porcentajes.
- Se distingue la presencia de un dato de su valor: un cero explícito también activa «Incluir». Se conservan las validaciones de cada modalidad; marcar la fila no implica que el dato sea válido.
- Solo cambia la casilla de la fila editada, sin modificar las asignaciones o la inclusión de otras filas.
- El usuario puede desmarcar «Incluir» manualmente después de introducir el dato. La mera actualización de la pantalla no vuelve a marcarlo; una nueva introducción o edición de un dato no vacío aplica de nuevo la regla.
- Cargar o reabrir datos existentes no altera la selección guardada por este automatismo. Tampoco se modifica el estado de filas de solo lectura o bloqueadas.
- Esta petición no define una nueva regla para desmarcar al vaciar el campo; conservar el comportamiento existente en ese caso.
- Los totales y, cuando corresponda, el indicador de PEND-UI-04 reflejan la inclusión según las reglas existentes. Se conserva la navegación por teclado prevista en PEND-UI-03.

## Validación pendiente

Comprobar localmente ambos repartos y sus modalidades con escritura, pegado, cero explícito, entrada inválida, desmarcado manual, edición posterior y reapertura. Verificar que no cambia otra fila ni se omiten validaciones. La anotación no implementa todavía el automatismo. No ejecutar GitHub Actions.

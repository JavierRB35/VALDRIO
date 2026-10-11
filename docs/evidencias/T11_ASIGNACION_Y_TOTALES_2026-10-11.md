# Reparto de auxiliares y filas de totales — seguimiento

Fecha: 2026-10-11. Rama de trabajo: `codex/usability-project-nodes-import-20261011`. PR #37. No fusionado a `main`.

## Incidencia reproducida por el propietario

La aplicación compilaba correctamente, pero en el paso de reparto secundario el desplegable **Asignación de claves a nodos auxiliares** volvía a blanco tras elegir una clave publicada.

### Cambio realizado

- Antes, el ComboBox estaba enlazado a `SelectedKeyId` mediante `Mode=OneWay`. El handler ejecutaba una operación asíncrona y después forzaba `UpdateTarget()`; al refrescar, el control podía recuperar el valor anterior (nulo).
- Ahora la selección usa `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged` y cada fila guarda **selección en edición** y **selección realmente confirmada** por separado.
- El handler solo responde a cambios del usuario con foco/desplegable activo, preserva el valor visible mientras la aplicación guarda, no dispara guardados desde eventos de reconstrucción, y restaura la última clave confirmada si el commit no se completa.
- El método de guardado ahora devuelve `Task<bool>` para distinguir operación guardada de validación rechazada. Las comprobaciones existentes de clave secundaria publicada, permisos, fase cerrada, unicidad por auxiliar y compatibilidad de unidad siguen aplicándose.
- `MainWindow.MethodologySmoke.cs` añade prueba de interfaz real: seleccionar una clave desde el ComboBox, esperar al commit, confirmar selección persistida en memoria y en el proyecto SQLite.

## Autoasignación y claves disponibles

La validación del motor no admite cuotas asignadas al propio auxiliar cuando la clave tiene `SelfAllocationPolicy.Forbidden`. El editor de clave secundaria muestra ahora la opción ya existente en el ViewModel `Permitir destino al propio auxiliar`, con ayuda contextual. Esta opción no elimina la comprobación de un circuito sin salida al principal. Si aún no existe ninguna clave secundaria publicada, aparece una explicación visible junto a la tabla (en ES/EN).

Prueba funcional adicional: crear clave secundaria que se asigna parcialmente a sí misma y parcialmente a un principal; verificar que no se asigna sin autorización y sí con `SelfAllowed`. Verificar que una clave con salida única a sí misma sigue produciendo el diagnóstico específico, no un resultado económico inventado.

## Totales monetarios

Las filas de pie son **de solo lectura**, situadas debajo de cada DataGrid, por lo que permanecen visibles sin falsificar filas económicas, sin alterar edición, selección, ordenación ni resultados de cálculo.

- **Recursos, preparar cuentas**: por moneda original, convertido, ajustes, efectivo, pendiente y diferencia; sin sumar divisas distintas.
- **Recursos, partes y revisión**: importe firmado de las partes del recurso seleccionado, sin volver a sumar esos importes al total global.
- **Reparto primario**: composición (importe efectivo), cobertura de claves, hechos adjudicados, agrupación por centro/nodo y distribución absoluta editable. Los porcentajes fijo/variable no se suman como importes.
- **Reparto secundario**: columna de coste directo, columna recibida de auxiliares, coste final por nodo principal; contribuciones al terminal; detalle de explicación del importe publicado seleccionado; circulación bruta marcada **informativa** y no sumada al resultado terminal.
- Totalización desde valores decimales firmados y moneda funcional del estudio, con visualización según configuración regional, no desde texto ya formateado. Los importes negativos se mantienen como tales.

## Validación efectuada / pendiente

- **Hecho**: revisión estática de etiquetas XAML (balance de apertura/cierre), duplicados de nombres de controles y claves ES/EN. No aparecieron problemas en los archivos modificados.
- **Pendiente**: compilación y validación WPF Windows del nuevo conjunto de commits. La compilación correcta comunicada por el propietario corresponde a la versión anterior a esta corrección.
- **Pendiente**: ejecutar la prueba `RunMethodologyChecks` y la suite local, especialmente la selección real del ComboBox, prueba de fallo por clave incompatible, refresco, reapertura, totales con abonos, moneda múltiple original y cuadratura `SUM(PrincipalCosts.TotalAmount) = SUM(SecondaryResults.PostedAmount)`.
- **Pendiente visual**: footer alineado con columnas en anchos mínimos/DPI altos, desplazamiento horizontal, pantallas ES/EN, foco durante guardado y mensajes de error.
- **No ejecutado**: GitHub Actions, workflows de pago, merge o push a `main`.

## Restricción contable

Los movimientos brutos entre auxiliares no son costes terminales adicionales. La única reconciliación contable válida compara hechos terminales publicados contra la suma final por nodos principales. Las filas de total no escriben hechos, no publican resultados y no modifican el algoritmo de reparto.

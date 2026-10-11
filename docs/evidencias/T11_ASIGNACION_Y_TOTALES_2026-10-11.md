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

## Corrección crítica posterior — 11/10/2026, selección con ratón

**Causa exacta adicional localizada:** el code-behind ya contenía `SecondaryAssignmentDropDownClosed`, pero el ComboBox de `SecondaryNodeAssignmentsGrid` no estaba suscrito al evento `DropDownClosed`. El evento `SelectionChanged` rechazaba intencionadamente las selecciones mientras `IsDropDownOpen == true` (para evitar doble escritura), así que las selecciones habituales con ratón nunca llegaban a ejecutar `PersistSecondaryAssignment`. Cambiar solo el binding a `TwoWay` no resolvía ese defecto.

**Arreglo:** conectado `DropDownClosed` en el ComboBox, mantenido `SelectionChanged` para teclado, explicitados `DisplayMemberPath="Label"` y `SelectedValuePath="Value"`. Si el guardado falla por publicación, fase, autoasignación, unidad o restricciones de reglas, se restaura el último valor confirmado y aparece una explicación roja en esa misma fila. Se añadió el recurso de error en ES/EN.

**Foco al abrir fases:** `MainWindow.NavigateWorkflow` invoca `AllocationWorkspacePanel.FocusKeyLibrary()` al entrar/abrir Primario o Secundario sin cambios pendientes, después de actualizar la visibilidad. Se selecciona y enfoca la primera fila de la biblioteca si existe; si está vacía, se enfoca la tabla. Los guardados no ejecutan este salto de foco.

**Cobertura de regresión añadida:** el smoke WPF de metodología abre el desplegable, selecciona con él abierto, confirma que todavía no se ha guardado, lo cierra y espera el guardado; comprueba selección visible, la regla persistida en SQLite, lectura fresca del workspace y reapertura del proyecto. Además comprueba el foco en la primera fila al abrir cada fase.

**Estado:** revisión estática de XAML, nombres de handlers y claves de localización ES/EN completada sin incidencias. **Pendiente de compilar y ejecutar la prueba de integración WPF en Visual Studio Windows del usuario.** No fusionar ni dar por solucionado en ejecución hasta completar dichas pruebas.


## Política posterior — autoasignación siempre permitida (2026-10-11)

Tras la prueba visual el propietario confirma que **todas las autoprestaciones deben estar permitidas**. Esto sustituye la propuesta anterior de exigir autorización de autoasignación por versión.

Se ha eliminado el veto `GRAPH_SELF_NOT_ALLOWED` de `AllocationKeyRules.ResolveSecondary` y el bloqueo anticipado `KEY_SELF_ONLY`; el grafo secundario pasa siempre `AllowSelf=true` al solver, incluso con claves previamente guardadas como `SelfAllocationPolicy.Forbidden`. Las claves secundarias nuevas serializan `SelfAllocationPolicy.Allowed` automáticamente. Se retira del editor la casilla adicional de permiso.

**No se elimina la comprobación matemática:** claves cuyo coste nunca puede salir hacia nodos principales provocan `GRAPH_CLOSED_COMPONENT` durante la factorización. Las claves con salida parcial y autoasignación deben poder asignarse y resolverse. No se cambian ni fuerzan los datos históricos, ni se altera el contrato de entradas `ReciprocalInput` directamente invocadas con `AllowSelf=false`.

Cobertura de regresión: `T10PublishedRoutingIntegrationTests` usa una clave histórica `Forbidden` con autoasignación parcial y verifica conservación y un circuito cerrado; `T11MethodologyTests` prueba reconstitución de costes con clave `Forbidden`; el smoke WPF crea y aplica una clave secundaria con autoasignación y comprueba que las nuevas se guardan como `Allowed`.

Pendiente: compilación/pruebas .NET Windows en Visual Studio y reproducción con el archivo real del usuario. GitHub Actions no se ha ejecutado ni solicitado.

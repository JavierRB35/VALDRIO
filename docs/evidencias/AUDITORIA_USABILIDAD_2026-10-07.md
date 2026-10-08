# Auditoría de usabilidad del front — 7 de octubre de 2026

Alcance: navegación WPF, empresa, estructura, estudio, importación, ayudas,
estados pendientes y guardado. Se revisa el código y se comprueban los controles
reales con archivos sintéticos; no se modifican los archivos de trabajo del usuario.

Decisión del propietario: **un único avance guiado**. Se conserva el acceso a las
operaciones individuales y avanzadas. No cambian las reglas económicas ni el esquema.
T07 sigue pendiente de conversación. La directriz permanente está en
`docs/directrices/USABILIDAD.md`.

## Hallazgos y correcciones

| Hallazgo | Consecuencia anterior | Corrección |
| --- | --- | --- |
| Volver a archivo/columnas y continuar repetía lectura y validación | Se perdían inclusiones, exclusiones y asignaciones de tipos de cambio | El avance reutiliza la lectura y validación vigentes. Validar de nuevo sin cambios conserva la selección. Cambiar el origen o el mapeo sigue invalidándola. |
| «Solo revisar» usaba el destino de escritura | Con un estudio guardado se podía preparar/confirmar y se pedía resolver cambios pendientes | Revisión sin destino ni permiso de escritura; conserva el formulario y no pregunta por guardarlo. |
| Campos editables durante la preparación | Se podía cambiar el contexto mientras se preparaba un lote | Formularios deshabilitados durante la operación; la cancelación sigue disponible fuera de ellos. |
| Dos avances distintos en estructura | Uno saltaba de pantalla, otro solo seleccionaba una pestaña; un modelo publicado previo podía ocultar requisitos del borrador actual | Un único avance principal con texto de acción. Usa el estado del modelo actual y publica antes de llevar al estudio. |
| Retroceso ignoraba los pasos del modelo | «Anterior» abandonaba estructura | Retrocede primero dentro del modelo. |
| Cambios de centro bloqueaban publicación sin señalar el motivo | Guardar modelo no guardaba el centro y continuar parecía no responder | Se muestra el centro pendiente antes de ofrecer «Guardar centro y continuar». El centro se guarda explícitamente. |
| Se comprobaba cantidad de nodos/maestros, no su idoneidad | Solo auxiliares o maestros inactivos llevaban al lugar equivocado | Se exige centro/unidad activos y se indica el nodo principal requerido; se conserva el formulario incompleto. |
| Resultado de crear unidad fuera de la pantalla actual | Error o confirmación invisible | Estado visible y anunciado junto a la creación de unidades. |
| Atrás tras guardar forzaba de nuevo el último paso | Botón aparentemente inoperante | El guardado cambia de paso una sola vez; luego se permite inspeccionar. |
| Cambiar una revisión guardada conservaba el mensaje «guardada» | Confusión entre lote persistido y nuevas modificaciones | Mensaje explícito de revisión modificada, preparación invalidada; el lote guardado permanece en el estudio. |
| Ayuda del modelo del estudio indicaba un botón inexistente; ayuda de campos de pantallas anteriores permanecía visible | Instrucciones ajenas al contexto | Ayuda específica de selección del modelo publicado y limpieza al cambiar de paso. |
| Operación en curso y solo lectura se confundían con empresa ausente | Recomendación incorrecta para resolver el bloqueo | Estados diferenciados y avance de escritura deshabilitado con explicación en consulta. |

## Recorrido resultante

1. Crear o abrir empresa; el avance puede crear el archivo y pasar a estructura.
2. Crear/cargar modelo, completar centros y unidades, aplicar nodos y guardar/publicar.
   Un único botón principal cambia su texto según la acción. Los requisitos dirigen al
   formulario correspondiente. Las operaciones de mantenimiento siguen disponibles.
3. Completar y guardar el estudio; el avance muestra después su destino de importación.
4. Leer archivo, asignar columnas, seleccionar registros, indicar significado y moneda,
   revisar totales y guardar. Volver atrás conserva lo que siga siendo válido.

## Verificación

- Tres pruebas portátiles nuevas: revisión de estudio guardado limpio/con cambios
  sin escritura ni preguntas; requisitos de centro pendiente y maestros activos.
- Veinte regresiones nativas adicionales al recorrido previo de 19 comprobaciones.
  El avance y retroceso usan los mismos métodos que los botones del usuario, sin
  revelar automáticamente la pantalla antes de comprobar cada transición.
- Casos: empresa vacía, borrador, maestros incompletos, unidad inválida, solo auxiliar,
  nodo incompleto, centro pendiente, publicación, estudio, selección de todos menos uno,
  retroceso con tipos de cambio, revisión sin escritura, bloqueo durante preparación,
  guardado y reapertura, estado tras editar y ayuda contextual.
- Se mantiene la prueba 6005 = 280 + 78,35 = 358,35, agrupada por cuenta independientemente
  de su descripción, y los recorridos existentes de debe/haber, sustitución,
  acumulados, conversiones, instantáneas, recuperación, idiomas y teclado.
- `Verify-Windows.ps1` exige los 20 resultados en `guided-workflow/audit.json`, cuatro
  capturas y la aceptación previa completa. El recibo `verification.json` acredita
  el commit y los binarios concretos; este documento no sustituye ese recibo.

## Límite y siguiente evaluación

Las regresiones corrigen defectos reproducibles del front; no permiten atribuir a
ellos todos los problemas históricos de guardado observados en capturas. La prueba
con el propietario debe valorar si entiende las acciones sin explicaciones externas.
Las búsquedas/filtros para listados grandes y una lista de incidencias con acceso
directo a cada fila son mejoras futuras a comentar, fuera de esta corrección.

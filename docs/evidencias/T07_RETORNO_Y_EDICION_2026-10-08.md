# T07 — Retorno, selección de celda y destinos identificables — 2026-10-08

Continuación del commit 9a70979 en codex/usability-six-issues, PR #27.

## Hallazgos y cambios

- El avance inferior abría el editor opcional de destinos. Se retira ese avance
  cuando se revisan correcciones; destinos se abre con su botón explícito.
- Anterior ocultaba el formulario conservando un borrador que impedía editar la
  columna Correcciones. El retorno ahora consulta guardar/descartar/cancelar cuando
  hay cambios, cierra el editor y restablece la edición. Existe un botón en el editor
  y la barra fija identifica explícitamente «Cerrar destinos y volver a Correcciones»,
  accesible sin buscar un control fuera del área visible.
- La fila Pendiente de decidir procede de la materialización inicial de la cuenta
  importada, no de añadir un nuevo coste. Se explica visiblemente; abrir y cerrar el
  editor sin cambios no crea partes, eventos ni cambios en el archivo.
- El combo de pools mostraba nodo + descripción, que podía estar vacía. Ahora utiliza
  centro físico (código y nombre), nodo, papel, descripción o indicación de ausencia,
  unidad de actividad y funciones del modelo publicado. El texto admite varias líneas
  en la selección y en el desplegable. Los valores siguen siendo los IDs de los pools.
- Un clic en Correcciones abre el editor y selecciona todo el número, incluido cero.
  Escribir sustituye ese número; no añade dígitos al valor anterior. Se conserva la
  vinculación de selección de la cuenta, la región capturada, el guardado automático
  y Escape para cancelar. Las demás columnas permanecen de consulta.

No se modifican reglas económicas, originales, correcciones ya guardadas, conversiones,
particiones persistidas ni esquemas. Los borradores se descartan solo por elección
explícita. Las opciones avanzadas y la revaloración siguen disponibles.

## Validación local

- Build Release de la solución: 0 errores y 0 advertencias.
- Suite completa: 534 correctas, 0 fallos, 0 omitidas (artifacts/tests/return-full.trx).
- Tras completar textos/retorno fijo: 4 regresiones afectadas correctas
  (artifacts/tests/return-final-focused.trx). Cubren vuelta limpia, cancelar/descartar
  formulario, guardar destinos al volver y etiquetas congeladas de pools.
- WPF previo completo: 354 comprobaciones correctas, código de salida 0
  (artifacts/wpf-return-debug). Incluye evento de ratón, selección del cero, sustitución,
  guardado, retorno, nombres de pools, conservación C04 y reapertura.
- Aceptación final: 356 comprobaciones WPF correctas (44 T07), código de salida 0.
  artifacts/wpf-return-final incluye la barra fija de regreso y pool-choices-es.png,
  captura inspeccionada del desplegable real con nombres completos y ajuste de línea.
- 987 claves EN/ES coincidentes y sin duplicados; 58 archivos de specification
  conservan SHA-256 frente al commit 07ba978. git diff --check correcto.

Recibos/capturas/TRX permanecen en artifacts, ignorado por Git. Fixtures sintéticas;
no certifican una fuente productiva. Validación local, sin GitHub Actions (0 USD),
sin tocar main, sources sincronizados ni la copia original abierta en Visual Studio.

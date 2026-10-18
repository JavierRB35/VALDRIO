# T14 — claridad, cálculo automático y presentación

Fecha: 18/10/2026. Base: ff7df95; rama local fix/t14-claridad-funcionamiento-20261018.
Se conservan los cambios previos sin confirmar del menú T14 y sus traducciones.

## Inventario preservado
Tabla por nodo/unidad, normal y real editables, utilización, coste de referencia, estados,
selector de análisis con protección de cambios, importación, borrador, confirmación,
anulación, historial, fuentes independientes, capacidades avanzadas y cierre.
Asistente: calendario, histórico revisado y declaración justificada; previsualizar,
aplicar a la fila y cancelar. No se cambia el cálculo económico ni los datos de sources/.

## Cambios
- Las paradas se introducen por turno/recurso, día/recurso, período/recurso o como total.
  El programa calcula el total; no hay que multiplicarlo fuera de la aplicación.
- Ejemplo: 20 días × 2 turnos × 8 horas × 3 recursos = 960 horas;
  0,5 horas de parada por turno/recurso × 20 × 2 × 3 = 60 horas;
  disponibles 900; al 90 %, capacidad normal 810.
- Todos los datos siguen siendo modificables. Los resultados se actualizan al escribir;
  una entrada inválida elimina la propuesta obsoleta y deshabilita su aplicación.
- Porcentaje de entrada 0–100; el motor conserva su factor 0–1. No se inventan factores,
  días de trabajo, horas ni observaciones reales.
- Se recuperan los parámetros de calendarios guardados, incluidos los antiguos con
  paradas totales. Se conserva frecuencia, entrada y multiplicador como evidencia.
- Errores con campo y corrección, resultado desglosado, fuentes desplegadas cuando
  faltan al confirmar. Tarjeta blanca, resumen verde, etiquetas y ayudas ES/EN.
- Aplicar no guarda automáticamente: mantiene el borrador de la fila y la protección
  contra pérdida de cambios. La actividad real permanece independiente.

## Validación local
- Solución Release: 0 errores, 0 advertencias.
- 22/22 pruebas de T14 y catálogo de traducciones: pruebas.trx.
- 26/26 comprobaciones WPF: checks-wpf.json; escritura a través de bindings reales,
  foco, cálculo automático, rechazo de paradas excesivas y aplicación a la fila,
  español/inglés y ventanas de 1040 y 680 px.
- Nueva regresión: las cuatro frecuencias, actualización al cambiar días, invalidación,
  adopción, confirmación, recuperación del calendario y reapertura del archivo SQLite.
- Inspección visual de capturas grandes y estrechas. Las ventanas pequeñas conservan
  desplazamiento de campos y acciones fijas.
- El primer intento de pruebas en aislamiento no pudo conectar con testhost; la
  ejecución local fuera del aislamiento sí completó las 22 pruebas.
- GitHub Actions: no ejecutado; coste 0 USD.

Entrega compilada localmente en src/Valdrio.Desktop/bin/Release/net10.0-windows/.
No se ha sustituido ninguna instalación externa ni publicado/fusionado en GitHub.

## Integración autorizada
El propietario solicita fusionar el 18/10/2026. Se incorpora también el acceso previo de T14 al menú, conservado y comprobado con 18/18 controles WPF ES/EN (checks-menu.json). Main remoto comprobado: ff7df95. Los workflows continúan siendo exclusivamente manuales.


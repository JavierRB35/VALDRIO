# Desplazamiento exterior y acciones inferiores fijas — 2026-11-03

## Solicitud y base

El propietario solicita eliminar el scroll interior del área de trabajo en todas las pestañas de primario y secundario, extenderlo a las demás pantallas cuando sea posible y fusionar al terminar. Base: `main@f05e0bb`; rama de trabajo `fix/single-scroll-fixed-actions-20261103`. Árbol inicial limpio. Se aplican protocolo del proyecto, AGENTS.md, sistema visual V1, usabilidad y directriz T08-CUENTA-01; se consultó el issue #28. No se modifica identidad, descripción ni clasificación de cuentas.

## Cambios

- Tablas de primario/secundario: eliminadas alturas fijas y máximas; sin desplazamiento vertical interno. Las tablas crecen con sus filas en todas las pestañas y detalles. Se elimina el mínimo de altura ligado al viewport que dejaba espacios vacíos en claves.
- Mismo criterio en estructura, recursos, clasificación, resultados, catálogo/composición, actividad, tarifas, órdenes, importaciones y reutilización. Las tablas Pareto y los diagramas de resultados utilizan el desplazamiento vertical de la página; conservan el horizontal.
- Actividad, importación física y reutilización: un contenedor exterior reúne cabecera y contenido; acciones inferiores fuera de él. Importación de saldos: un único contenedor exterior para cabecera y pestañas, dejando navegación, confirmación, cancelación y estado operativo en el pie. Las explicaciones extensas ya no agotan el espacio en escalado 150 %.
- `FixedPageActions` traslada los controles reales de guardar/publicar al pie del shell cuando corresponde. Conserva comandos, eventos, nombres, contexto y habilitación de su ubicación original. Sigue la pestaña activa y restaura los controles al abandonarla. Se usa para claves, clasificación, análisis, edición de centros/nodos, publicación de estructura, recursos, catálogo/composición y aceptación de órdenes. Las barras inferiores que ya eran fijas se conservan.
- La rueda sigue desplazando la página tras enfocar una tabla de altura natural. Se conserva Shift+rueda horizontal y el comportamiento propio de desplegables y editores de texto.
- Se actualiza la directriz de usabilidad y la ayuda de repartos. Se restauran los recorridos recomendados que faltaban en la ayuda española/inglesa del informe auxiliar, detectados por las pruebas.

Sin cambios en cálculos, reglas contables, persistencia, esquema o dependencias. Se conservan los controles locales de selección, filtros, altas y edición junto a sus datos; las acciones de confirmación inferiores permanecen fijas. La antigua pantalla auxiliar, retirada del flujo normal en la entrega anterior, no se reactiva.

## Verificaciones ejecutadas

- Compilación Release de Desktop y dependencias: sin errores ni advertencias.
- Comparación de atributos funcionales XAML antes/después: sin pérdida de comandos, eventos, bindings, selecciones, estados, etiquetas accesibles o permisos.
- Recorrido WPF real posterior: **57 controles** satisfactorios de edición de coste, importación de catálogo, composición por ID, persistencia/reapertura, tarifas y áreas de trabajo; capturas de referencia anteriores y posteriores.
- Matriz WPF principal: **952 comprobaciones** satisfactorias en español/inglés y anchos 1536/1000. Recorre todas las pestañas de estructura, recursos, primario, secundario y resultados, además de análisis, catálogo/composición, actividad, tarifas, órdenes e importación física. Comprueba tablas de 60 filas (solo datos de presentación), rueda con foco dentro de la tabla, altura natural, posiciones fijas y ausencia de acciones huérfanas al cerrar.
- Matriz adicional final: **546 comprobaciones** satisfactorias de importación de saldos, reutilización, capacidades, actividad e importación física, incluyendo escala de presentación 150 %, espacio útil y ajuste horizontal de los botones.
- Regresión focalizada: 7 casos ejecutados; 5 pasaron inicialmente y 2 detectaron recorridos ausentes en la ayuda de auxiliares. Tras corregir los documentos, los 4 casos de ayuda pasan. No queda ningún fallo pendiente de esa selección.
- `scripts/verify_help.py`: 17 vistas lógicas y 18 XAML cubiertos.
- `git diff --check`: sin errores.

No se repite la batería contable completa: no hay cambios de negocio y las comprobaciones nuevas se concentran en disposición, interacción y conservación de controles.

## Evidencias locales

- `artifacts/single-scroll-before/`: referencia previa.
- `artifacts/single-scroll-after/`: recorrido funcional de 57 controles.
- `artifacts/single-scroll-tabs-v2/checks.json`: 952 comprobaciones y capturas por pestaña.
- `artifacts/single-scroll-extras-v3/checks.json`: 546 comprobaciones finales y capturas al 150 %.
- `tests/Valdrio.Tests/TestResults/single-scroll-regression.trx` y `single-scroll-help.trx`.
- Ejecución reproducible: `--single-scroll-checks-only`, opcional `--single-scroll-extras`, `--capture-dir=<directorio nuevo>` y ruta de archivo sintético origen. Se genera una copia desechable; no se alteran los datos del origen.

## Límites e integración

Las tablas extensas requieren más trabajo de presentación al crecer sin viewport virtualizado; la prueba de estrés de esta entrega usa 60 filas por tabla. Se mantienen los desplazamientos horizontales necesarios y los propios de campos de texto/desplegables. Las pruebas adicionales del asistente de importación son de disposición; no introducen nuevas reglas de importación.

Fusión local en main autorizada expresamente por el propietario. Se reutilizan las verificaciones válidas al integrar sin conflictos. Sin push, sin GitHub Actions y sin operaciones sobre fuentes sincronizadas.

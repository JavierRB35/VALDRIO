# Validación de entradas antes de operaciones de dominio

Base integrada: `c835b96` (incluye capacidad guiada). Validación local, sin GitHub Actions.

## Problema y alcance

Crear un período con la misma estructura dejaba el nombre vacío y enviaba esa entrada al constructor de dominio. `STUDY_NAME_REQUIRED` detenía el depurador antes de que la capa de presentación tratase el error.

Se revisaron los lanzamientos explícitos de excepciones bajo `src/`, con seguimiento de las rutas de entrada de la interfaz. Se han cambiado los casos de datos incompletos o mal formados identificados en esos flujos para devolver mensajes y conservar el formulario, antes de invocar operaciones estrictas o escribir datos.

| Área | Validaciones incorporadas |
| --- | --- |
| Empresas y unidades | Nombre, moneda, código, dimensión, escala y duplicados |
| Estudios y períodos | Nombre, fechas, orden temporal, estructura publicada, moneda y escenario |
| Modelos | Nombres y códigos, duplicados, selección de rol y unidad |
| Repartos | Entrada numérica parcial, selecciones, reglas incompletas, conflictos y límites |
| Recursos | Importes, destinos, exclusión, unidades, intervalos, ajustes, motivos y revalorización |
| Importación | Columnas y formatos, conversión, cabeceras, fechas y cantidades físicas, CSV de productos |
| Actividad y capacidad | Números durante la escritura, rangos, confirmación incompleta y longitud de evidencias |
| Composición y estimaciones | Catálogo, cantidades, factores, pérdidas y selección de receta |

La ausencia de una regla de reparto durante la evaluación de cobertura ahora devuelve un diagnóstico. Las API estrictas originales siguen disponibles. Los importadores físicos y de productos incorporan resultados de validación que la UI puede consumir sin lanzar excepciones por entradas esperables.

Se conservan los contratos de dominio, las comprobaciones de integridad, revisiones concurrentes, archivos dañados, límites aritméticos y errores de programación. No se ha añadido un manejador global que oculte fallos ni se ha desactivado la interrupción del depurador. Esta revisión no implica que cualquier excepción futura sea imposible.

## Pruebas

- Batería completa integrada: 807 pruebas superadas, 0 fallidas (`input-validation-integrated.trx`).
- Último ajuste de mensajes y regresiones de actividad: 40 pruebas superadas, 0 fallidas.
- Compilación WPF: 0 errores y 0 advertencias.
- Regresiones nuevas: observación de excepciones de primera oportunidad, conservación de los textos editados, ausencia de escrituras inválidas y reintento correcto tras corregir el nombre.
- Comprobación de claves de traducción añadidas: todas existen en el catálogo.

La primera ejecución visual alcanzó el guardado de recursos y se interrumpió al cerrar/reabrir por `ArgumentNullException` dentro de `DataGridAutomationPeer.RaiseAutomationCellSelectedEvent`. Ese resultado no constituye una validación completa del recorrido visual.

La repetición del 3 de noviembre completó las comprobaciones de empresas, modelos, estudios (incluido el nombre vacío del nuevo período), importación, recursos y repartos. No reprodujo la excepción de automatización de tabla. El recorrido terminó después con una aserción de la prueba visual sobre el indicador de selección de la barra lateral (`Visual shell: es sidebar ... step: 0`). Por tanto, se verificaron los flujos funcionales anteriores, pero la aceptación visual global sigue sin pasar; no se modificó esa aserción ni el diseño para ocultar el resultado.

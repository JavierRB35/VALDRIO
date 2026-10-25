# VALDRIO — realidad funcional actual

**Corte de código:** `928b26353f83af49d88829d10b1497c71d1409b3` · **Revisión documental:** 25/10/2026.

Este inventario describe la aplicación encontrada en el código de la entrega T18 autorizada para fusión. Las pruebas citadas en fichas y evidencias pertenecen a sus respectivos SHA; no se presentan como una nueva aceptación WPF del binario de este corte.

## Navegación del escritorio

La navegación lateral ofrece cinco grupos desplegables independientes, con la ubicación actual marcada. Los requisitos de cada operación siguen aplicándose.

| Grupo | Accesos |
| --- | --- |
| Proyecto | Proyecto; Estructura de costes. |
| Datos del análisis | Análisis y periodo; Importar saldos; Preparar cuentas. |
| Repartos de costes | Reparto primario; Reparto secundario. |
| Actividad y tarifas | Actividad y capacidades; Capacidad y servicios auxiliares; Tarifas de centros. |
| Productos y resultados | Productos y servicios; Órdenes estimadas; Resultados y trazabilidad. |
| Acceso separado | Preferencias. |

## Funciones descritas por pantalla

### Proyecto, estructura y análisis

- **Proyecto:** crear y abrir archivos `.valdrio`; abrir el ejemplo como copia; cerrar, copiar, restaurar o actualizar a otra copia; configurar unidades del proyecto.
- **Diseñar estructura:** mantener centros, nodos, pools y unidades en borradores versionados; validar y publicar una estructura para análisis.
- **Período y análisis:** seleccionar modelo y contexto del período, escenario y moneda; guardar, reabrir y consultar estados e instantáneas. El cierre de un análisis no equivale al cierre del reparto o a un cierre integral.

### Importación y preparación

- **Importar saldos:** leer CSV/XLSX, mapear cuentas y descripciones e importes (incluido Debe/Haber), revisar interpretación, agrupación y procedencia, y confirmar expresamente.
- **Preparar cuentas:** revisar códigos, descripciones e importes; editar correcciones en tabla; clasificar y repartir destinos; gestionar pendientes/exclusiones y aplicar reglas de ajuste reutilizables mediante vista previa y confirmación explícita.

### Repartos y resultados

- **Primario:** mantener claves y versiones, reglas, destinos y cobertura; explicar la distribución por cuenta y cerrar/reabrir bajo validaciones.
- **Secundario:** asignar auxiliares, revisar cobertura y viabilidad, calcular prestaciones recíprocas, inspeccionar liquidaciones y costes terminales por centro; cerrar/reabrir. La circulación bruta entre auxiliares no se suma de nuevo como coste final.
- **Flujos y ejecuciones (T12):** calcular y publicar ejecuciones persistidas; comparar la vigente con históricas, inspeccionar diagramas, contribuciones, conciliación y procedencia. Consultar un histórico no vuelve a ejecutar el cálculo.
- **Actividad y capacidades (T14):** mantener observaciones físicas versionadas por análisis, período, nodo/pool y unidad; registrar capacidad normal y actividad real con fuentes, borradores, confirmación e historial; importar actividad y usar una propuesta asistida de capacidad normal. No convierte automáticamente una propuesta en dato confirmado.
- **Capacidad y servicios auxiliares (T16):** definir tratamiento de capacidad auxiliar y servicios medidos por destino; identificar cobertura pendiente; mostrar respuestas y conciliaciones sin sumar circulación recíproca como coste adicional.
- **Tarifas de centros (T15):** consultar tarifas y variaciones de gestión a partir de ejecuciones compatibles y datos físicos confirmados. Las recetas alimentan cantidades estimadas en T18; no constituyen consumos reales T19.
- **Productos y servicios (T17/T18):** mantener catálogo y versiones de composiciones; fijar unidades, cantidades, conversión, base por unidad/lote/orden, exceso de entrada, rendimiento y semielaborados publicados; calcular necesidades físicas y un subtotal de referencia económica.

## Importación y estimación de T18

La ayuda de [Productos y servicios](../help/14-productos-servicios.md) contiene el procedimiento completo ES; la [versión inglesa](../help/en/14-productos-servicios.md) está incluida con la aplicación.

El archivo de catálogo admite CSV/TSV con **Referencia, Nombre, Unidad de medida y Coste unitario**. Las referencias existentes se actualizan por código: se conserva identidad, unidad y tipo, y se actualizan nombre y coste. Las referencias nuevas se agregan con el tipo seleccionado. Unidades incompatibles o cualquier fila inválida bloquean la aplicación parcial del fichero. El analista guarda expresamente el catálogo importado.

En **Productos y servicios**, para la cantidad previsualizada se multiplican las cantidades requeridas de materiales finales por sus costes unitarios de referencia y se muestra aparte la referencia importada del producto de salida. Semielaborados se expanden antes de sumar. Se muestran materiales sin precio y actividades indirectas no valoradas: el subtotal puede ser parcial, no se presenta como coste total y no se rellenan faltantes con cero. No se aplican automáticamente las tarifas de T15 ni se convierte moneda.

Los precios importados son datos de gestión en la moneda predeterminada del proyecto. No prueban una compra, consumo real ni valoración contable. T19 (consumos y desviaciones reales) y T32 (valoración contable) permanecen fuera de esta implementación.

## Órdenes estimadas e historial congelado

**Órdenes estimadas** valora los materiales finales y la actividad con las tarifas completas de un cálculo guardado y las observaciones vigentes al previsualizar. Exige receta publicada, referencia de orden y cantidad positiva. No añade el precio del producto terminado ni cuenta dos veces semielaborados. Cada lote iniciado requiere su preparación completa.

Aceptar crea una versión inmutable con receta, precios, cantidades, tarifas, fuentes, política de precisión, actor y fecha UTC. Un cambio posterior del catálogo o de la actividad no altera su coste. La revisión conserva referencia y producto y enlaza con la última versión. La comparación muestra cantidad, total, coste unitario y fuentes; sus diferencias no son consumos reales.

La falta de precio/tarifa, moneda incompatible, previsualización desactualizada, referencia duplicada o revisión concurrente bloquean la aceptación. También se exige análisis abierto y archivo editable. El formato 13 se obtiene actualizando una copia; la lectura de históricos no modifica recursos ni repartos. [Ayuda ES](../help/16-ordenes-estimadas.md) y [EN](../help/en/16-ordenes-estimadas.md).

## Pareto de ambos repartos

Barras por nodo en orden descendente de coste y línea de porcentaje acumulado. Primario utiliza los costes agregados por nodo; secundario, los costes finales de principales. La tabla permite consultar importes y porcentajes. Con abonos el acumulado puede superar 100 %; total no positivo implica porcentaje no disponible.

## Ayuda contextual y microayuda

La aplicación incluye 17 fichas de ayuda en español y 17 en inglés, identificadas por vista lógica en [`help-map.json`](../help/help-map.json), copiadas localmente junto al ejecutable. **Ayuda (F1)** abre la guía completa de la pantalla; la microayuda de campo se activa al recibir foco de teclado o puntero. La tecla **Tab** conserva el recorrido normal entre controles y no es un comando de ayuda. La ventana de ayuda no consulta la red ni modifica datos del análisis.

## Límites de alcance

- La aplicación no registra todavía consumos reales por producto/orden para compararlos con T17/T18.
- La estimación T18 no es una valoración NIC 2 ni un cálculo de inventario/producción en curso.
- El cálculo recíproco de auxiliares no implica selector ABC, TDABC ni otros métodos no presentes en la interfaz.
- El cierre de reparto primario/secundario no es el cierre integral del período.

## Evidencia y validación

Las fichas [T14](../tareas/T14.md) a [T18](../tareas/T18.md), los [registros de estado](../../ESTADO_PROYECTO.md) y las carpetas de [evidencias fechadas](../evidencias) documentan qué pruebas se ejecutaron y en qué SHA. Este corte documental se valida con `python scripts/verify_help.py`; no ejecuta GitHub Actions ni sustituye el smoke interactivo Windows sobre una publicación final.

La entrega T18 aporta 776 pruebas locales superadas y 25 comprobaciones WPF ES/EN; el menú y Pareto conservan su evidencia de integración previa. Véase [evidencia T18](../evidencias/t18-ordenes-20261025.md). La revisión documental no amplía esos resultados a toda la matriz DPI ni equivale a aceptación de negocio.

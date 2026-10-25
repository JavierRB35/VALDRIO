# VALDRIO — realidad funcional actual

**Corte de código:** `main@6885eca675cce04127b709a7d98cc35aea9ad697` · **Revisión documental:** 25/10/2026.

Este inventario describe la aplicación encontrada en el código de producción de `main`. Las pruebas citadas en fichas y evidencias pertenecen a sus respectivos SHA; no se presentan como una nueva aceptación WPF del binario de este corte.

## Navegación del escritorio

La navegación lateral agrupa el trabajo en Proyecto y estructura, Análisis y datos, y Repartos y cálculo. La tercera fase tiene subgrupos de asignación, capacidad y resultados. La aplicación exige los requisitos de cada operación (por ejemplo, proyecto, análisis, saldos confirmados o primario cerrado); el orden del menú no certifica que un cálculo esté completo.

| Fase | Accesos disponibles en el código |
| --- | --- |
| Proyecto y estructura | Proyecto; Diseñar estructura. |
| Análisis y datos | Período y análisis; Importar saldos; Preparar cuentas. |
| Repartos y cálculo — asignación | Reparto primario; Reparto secundario y cierre. |
| Repartos y cálculo — capacidad | Actividad y capacidades (T14); Capacidad y servicios auxiliares (T16); Tarifas de centros (T15). |
| Repartos y cálculo — resultados | Productos y servicios (T17/T18); Flujos y ejecuciones (T12). |
| Fuera de las fases | Preferencias de idioma, formato regional y zona horaria. |

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
- **Tarifas de centros (T15):** consultar tarifas y variaciones de gestión a partir de ejecuciones compatibles y datos físicos confirmados. Los consumos por producto u orden no se obtienen de recetas T17/T18.
- **Productos y servicios (T17/T18):** mantener catálogo y versiones de composiciones; fijar unidades, cantidades, conversión, base por unidad/lote/orden, exceso de entrada, rendimiento y semielaborados publicados; calcular necesidades físicas y un subtotal de referencia económica.

## Importación y estimación de T18

La ayuda de [Productos y servicios](../help/14-productos-servicios.md) contiene el procedimiento completo ES; la [versión inglesa](../help/en/14-productos-servicios.md) está incluida con la aplicación.

El archivo de catálogo admite CSV/TSV con **Referencia, Nombre, Unidad de medida y Coste unitario**. Las referencias existentes se actualizan por código: se conserva identidad, unidad y tipo, y se actualizan nombre y coste. Las referencias nuevas se agregan con el tipo seleccionado. Unidades incompatibles o cualquier fila inválida bloquean la aplicación parcial del fichero. El analista guarda expresamente el catálogo importado.

Para la cantidad previsualizada, T18 multiplica las cantidades requeridas de materiales finales por sus costes unitarios de referencia y muestra aparte la referencia importada del producto de salida. Semielaborados se expanden antes de sumar. Se muestran materiales sin precio y actividades indirectas no valoradas: el subtotal puede ser parcial, no se presenta como coste total y no se rellenan faltantes con cero. No se aplican automáticamente las tarifas de T15 ni se convierte moneda.

Los precios importados son datos de gestión en la moneda predeterminada del proyecto. No prueban una compra, consumo real ni valoración contable. T19 (consumos y desviaciones reales) y T32 (valoración contable) permanecen fuera de esta implementación.

## Ayuda contextual y microayuda

La aplicación incluye 16 fichas de ayuda en español y 16 en inglés, identificadas por vista lógica en [`help-map.json`](../help/help-map.json), copiadas localmente junto al ejecutable. **Ayuda (F1)** abre la guía completa de la pantalla; la microayuda de campo se activa al recibir foco de teclado o puntero. La tecla **Tab** conserva el recorrido normal entre controles y no es un comando de ayuda. La ventana de ayuda no consulta la red ni modifica datos del análisis.

## Límites de alcance

- La aplicación no registra todavía consumos reales por producto/orden para compararlos con T17/T18.
- La estimación T18 no es una valoración NIC 2 ni un cálculo de inventario/producción en curso.
- El cálculo recíproco de auxiliares no implica selector ABC, TDABC ni otros métodos no presentes en la interfaz.
- El cierre de reparto primario/secundario no es el cierre integral del período.

## Evidencia y validación

Las fichas [T14](../tareas/T14.md) a [T18](../tareas/T18.md), los [registros de estado](../../ESTADO_PROYECTO.md) y las carpetas de [evidencias fechadas](../evidencias) documentan qué pruebas se ejecutaron y en qué SHA. Este corte documental se valida con `python scripts/verify_help.py`; no ejecuta GitHub Actions ni sustituye el smoke interactivo Windows sobre una publicación final.

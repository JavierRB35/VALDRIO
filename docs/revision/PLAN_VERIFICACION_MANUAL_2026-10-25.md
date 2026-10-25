# Matriz de comprobación manual Windows — corte T18

**Código objetivo:** `928b26353f83af49d88829d10b1497c71d1409b3` · **Preparada:** 25/10/2026.

Esta lista traduce a pruebas interactivas los flujos que están en el código actual. **Estado inicial: no ejecutada en esta revisión.** Un resultado anterior solo cuenta si evidencia el mismo SHA, versión de binario, idioma, datos y escala. No modificar los proyectos sintéticos originales; usar copias de trabajo.

| ID | Prueba | Resultado que debe observarse | Estado |
| --- | --- | --- | --- |
| M01 | Iniciar VALDRIO, abrir/cerrar/crear una copia de proyecto | El archivo y su copia quedan diferenciados; cancelar una operación no cambia el original. | NO EJECUTADO |
| M02 | Navegar por los cinco grupos en ES y EN | Los grupos muestran Proyecto, Datos del análisis, Repartos de costes, Actividad y tarifas, Productos y resultados; cada acceso se habilita según sus datos previos. | NO EJECUTADO |
| M03 | Ayuda completa con F1 en cada una de las 17 vistas lógicas y en ES/EN | El título y el contenido corresponden a la pantalla o asistente actual; el texto se puede leer/desplazar; cambiar vista o idioma refresca la guía abierta. | NO EJECUTADO |
| M04 | Microayuda de campo con ratón y foco de teclado | Al entrar en un campo aparece su explicación. Tab mueve el foco al siguiente control y no abre/activa la ayuda por sí mismo. | NO EJECUTADO |
| M05 | Crear/publicar estructura y abrir análisis de período | Un borrador no se trata como publicado; el análisis conserva modelo y período propios; estudio cerrado aplica solo lectura donde corresponde. | NO EJECUTADO |
| M06 | Importar saldos CSV/XLSX con descripción, importe único y Debe/Haber | La vista previa no confirma datos; selección, interpretación, signos y agrupación son comprensibles; solo Confirmar persiste. | NO EJECUTADO |
| M07 | Preparar cuentas y aplicar regla reutilizable | Código y descripción siguen visibles; la regla se previsualiza, se aplica al análisis mediante confirmación y se puede deshacer en los estados permitidos. | NO EJECUTADO |
| M08 | Reparto primario con pendientes y cierre/reapertura | Cobertura y resultados concilian; se bloquea cierre con partes inválidas o pendientes; la reapertura conserva trazabilidad. | NO EJECUTADO |
| M09 | Reparto secundario con reciprocidad y diagnóstico no viable | Las prestaciones se liquidan una sola vez; un ciclo o cobertura insuficiente se explica; no se añade la circulación bruta al coste terminal. | NO EJECUTADO |
| M10 | T14 actividad y capacidad | Normal y real conservan fuentes y estados separados; cero observado no equivale a ausente; la importación física previsualiza y protege los cambios existentes. | NO EJECUTADO |
| M11 | T16 servicio auxiliar con tratamiento Pendiente y cálculo | Pendiente bloquea el cálculo hasta decidir; la cobertura parcial queda visible; la conciliación distingue coste propio, recibido y variación. | NO EJECUTADO |
| M12 | T15 tarifa de centro con datos confirmados y sin consumo por orden | Tarifa y variación explican sus fuentes; la falta de consumos T19 se muestra como información no disponible, no como cero o asignación estimada. | NO EJECUTADO |
| M13 | T17 composición con unidades, lote, rendimiento y semielaborado | Conversión solo entre dimensiones compatibles; lote iniciado y redondeo discreto se explican; publicación es inmutable; no hay doble suma del semielaborado expandido. | NO EJECUTADO |
| M14 | T18 importar productos y costes desde CSV/TSV | Se mapean Referencia, Nombre, Unidad de medida y Coste unitario; coma decimal y tabulador se interpretan según la región; unidad inexistente o coste negativo se rechazan sin aplicar cambios parciales. | NO EJECUTADO |
| M15 | T18 reimportar catálogo | Una referencia existente conserva identidad, unidad y tipo y actualiza nombre/coste; una referencia nueva se agrega con el tipo elegido; una unidad distinta para referencia existente rechaza el archivo completo. Guardar, cerrar y reabrir conserva datos y procedencia. | NO EJECUTADO |
| M16 | T18 estimar composición con coste completo e incompleto | Subtotal de materiales con precio, componentes sin precio, actividad indirecta y referencia del producto aparecen diferenciados. La referencia del producto no se resta del subtotal; los faltantes no se tratan como cero y el subtotal parcial no se llama coste total. | NO EJECUTADO |
| M17 | T12 publicar y abrir ejecución histórica | La ejecución vigente se identifica por revisión; consultar otra ejecución no recalcula ni sustituye resultados históricos. | NO EJECUTADO |
| M18 | Escala y teclado: 100 %, 125 %, 150 %, 200 %; teclado y lectura accesible | Controles, errores, tablas, diálogo de ayuda y foco continúan visibles y operables sin pérdida funcional. Anotar defectos con pantalla, idioma, tamaño y reproducción. | NO EJECUTADO |

| M19 | T18 aceptar una orden y revisar su historial | Materiales y actividad valorados, siete respuestas, datos congelados; revisar crea una versión nueva y comparar muestra las dos cantidades y costes sin registrar consumo real. | RECORRIDO WPF AUTOMATIZADO ES/EN SUPERADO; aceptación de usuario pendiente |
| M20 | T18 faltantes, concurrencia, solo lectura, análisis cerrado y formato 12→13 | Bloqueo explícito; reversión transaccional; copia original intacta y auditoría consistente. | PRUEBAS DE INTEGRACIÓN SUPERADAS; recorrido manual completo pendiente |
| M21 | Pareto primario y secundario | Barras descendentes, línea acumulada, valores exactos y estados de total no positivo; secundarios sin doble suma de circulación. | EVIDENCIA WPF PREVIA DEL MENÚ/PARETO; revisar con datos de negocio |

Los resultados automatizados están delimitados en [T18](../evidencias/t18-ordenes-20261025.md) y [menú/Pareto](../evidencias/menu-pareto-20261025.md). No convierten las filas M01–M18 en comprobaciones manuales ejecutadas.

## Registro de ejecución

Para cada pase, anotar SHA de código, versión de ejecutable, versión de Windows y DPI/escala, idioma y formato regional, proyecto de prueba (copia), ID de caso, resultado, incidencia, captura/log y responsable. No marcar un bloque completo por haber probado una sola pantalla o un único idioma.

Las pruebas de unidad/integración T17/T18 están en `tests/Valdrio.Tests/T17CompositionTests.cs` `T18CompositionCostTests.cs` y `T18FrozenEstimateTests.cs`; complementan, pero no reemplazan, esta comprobación manual WPF. Esta matriz no requiere GitHub Actions. Ejecutar CI solo con autorización expresa y respetando el límite económico del proyecto.

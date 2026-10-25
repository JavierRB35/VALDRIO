# Ayuda contextual: Resultados y trazabilidad
**ID Vista UI:** `View_CostRuns`  
**Última actualización:** `2026-10-25`
**Estado:** guía funcional mantenida para `928b263` (25/10/2026). Describe el código de esta pantalla; las pruebas visuales interactivas se registran por separado y solo aplican al SHA indicado en cada evidencia.

## 1. Objetivo y función financiera
Publicar y consultar ejecuciones persistidas del cálculo de costes y explicar flujos por centro, nodo, regla y recurso sin volver a calcular automáticamente al abrir un histórico.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Ejecución seleccionada | Lista de corridas | Según consulta | Distingue versión vigente e histórica. |
| Calcular, cancelar, actualizar | Acciones | Según estado | Controla creación de ejecución, cancelación y recarga. |
| Vista de gráfico | Primario/secundario | Opcional | Filtra la etapa de flujo a inspeccionar. |
| Zoom y etiquetas | Interacción de gráfico | Opcional | Ajusta legibilidad sin modificar resultados. |
| Selección de centro, nodo o arco | Tabla/gráfico | Opcional | Acota procedencias y conciliación. |
| Limpiar selección | Acción | Opcional | Vuelve a la visión global. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Coste efectivo y coste terminal | Totales del cálculo persistido | Comprueba conservación de importes. |
| Balance por centro | Primario, secundario neto, final | Separa imputación original del efecto neto posterior. |
| Balance por nodo | Rol, primario, secundario neto y final | Permite analizar auxiliares y principales. |
| Grafo de flujo | Etapas origen → primario → secundario → final | Visualización explicativa de rutas. |
| Detalle y reconciliación | Fuente, destino, regla e importes | Permite seguir importes de una ejecución publicada. |

## 4. Casos de uso, validaciones y errores frecuentes
- Una ejecución histórica no es vigente para el análisis actual.
- Abrir una ejecución no debe ejecutar de nuevo el solver ni sustituir su versión.
- No sumar importes de tránsito bruto como nuevos costes finales.
- Si faltan cierres o entradas coherentes, la creación de ejecución debe dar diagnóstico, no resultado ficticio.
- Un filtro/selección del gráfico no altera los saldos publicados.

### Referencias al código
- [`src/Valdrio.Desktop/CostFlowPanel.xaml`](../../src/Valdrio.Desktop/CostFlowPanel.xaml)
- [`src/Valdrio.Desktop/CostFlowViewModel.cs`](../../src/Valdrio.Desktop/CostFlowViewModel.cs)
- [`src/Valdrio.Application/CalculationRuns.cs`](../../src/Valdrio.Application/CalculationRuns.cs)

## Recorrido recomendado
1. Completa los repartos y guarda una ejecución económica antes de consultar sus resultados históricos.
2. Selecciona una ejecución concreta y recorre los flujos de costes por recurso, regla, nodo y centro para justificar los importes.
3. Compara el resultado con el análisis vigente sin confundir una ejecución guardada con un recálculo automático; consulta el origen de cada cifra.

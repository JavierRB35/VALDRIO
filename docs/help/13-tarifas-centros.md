# Ayuda contextual: Tarifas de centros
**ID Vista UI:** `View_PrincipalRates`  
**Última actualización:** `2026-11-03`
**Estado:** guía revisada sobre `2599188` y la auditoría contextual de 28/10/2026. Las evidencias de validación se registran por separado.

## 1. Objetivo y función financiera
Interpretar tarifas y variaciones de capacidad por centro principal, usando costes de una ejecución económica compatible y datos confirmados en Actividad y capacidades, de acuerdo con la política normalizada vigente. La vista es de análisis, no editor de horas.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Centro seleccionado | Tabla | Sí para detalle | Filtra las preguntas al centro y unidad concreta. |
| Solo incidencias | Booleano | No | Reduce filas visibles a casos que requieren atención. |
| Ver actividad / ver costes | Acciones | Opcional | Abren la fuente de las observaciones físicas o la ejecución T12. |
| Exportar | Acción | Opcional | Extrae reporte y procedencia JSON cuando hay resultados. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Coste y tarifa combinada | Suma de tarifas de los componentes, cada uno con su denominador | Indica coste por unidad si existen datos consistentes. |
| Componentes y comportamiento | Fijo, variable, recibido y fuente | Permite explicar por qué difiere la tarifa de otro centro. |
| Base y tarifa por componente | Actividad normal o real según política | Distingue denominadores económicos. |
| Variación de capacidad | Diferencia normalizado entre actividad normal y real | Muestra infra/sobreactividad sin llamarla ahorro realizado. |
| Tabla pregunta/respuesta | Siete preguntas de gestión | Explica costes aplicados, pendientes y conciliación solo con datos disponibles. |
| Proveniencia | Ejecución económica y versiones de Actividad y capacidades | Conserva rastreabilidad y comparabilidad. |

## 4. Casos de uso, validaciones y errores frecuentes
- Sin ejecución T12 vigente/compatible o capacidad confirmada no se debe mostrar tarifa ficticia.
- No comparar tarifas de centros con unidad/perímetro no comparables.
- El coste de actividades realizado no equivale a coste aplicado a órdenes si falta consumo real (no disponible en esta versión).
- La capacidad no utilizada no equivale a horas realizadas sin asignación.
- La tarifa normalizada de gestión no certifica por sí sola valoración de existencias según NIC 2.

### Cómo interpretar la tarifa y la variación
El **fijo propio sujeto a evaluación de capacidad** se divide por la capacidad normal. Los componentes variables y consumidos se dividen por la actividad real. Los costes de período quedan separados, fuera de la tarifa combinada. Por tanto, no dividas todo el coste por una única base ni vuelvas a normalizar un coste auxiliar ya evaluado.

Ejemplo: 1.000 de fijo propio, normal de 100 h y real de 80 h dan 10/h, 800 de coste de actividad realizada y 200 de variación de capacidad. Con importes positivos, una variación negativa indica sobreactividad, no ahorro cobrado. Sin consumo real por producto u orden, el coste aplicado y el realizado sin asignar siguen pendientes; una orden estimada no completa esa conciliación.

### Referencias al código
- [`src/Valdrio.Desktop/PrincipalRatesWindow.xaml`](../../src/Valdrio.Desktop/PrincipalRatesWindow.xaml)
- [`src/Valdrio.Desktop/PrincipalRatesEditor.cs`](../../src/Valdrio.Desktop/PrincipalRatesEditor.cs)
- [`src/Valdrio.Application/PrincipalRateReport.cs`](../../src/Valdrio.Application/PrincipalRateReport.cs)
- [`src/Valdrio.Calculation/PrincipalRates.cs`](../../src/Valdrio.Calculation/PrincipalRates.cs)

## Recorrido recomendado
1. Guarda una ejecución económica compatible con la revisión del análisis y pulsa Actualizar; esta pantalla obtiene la ejecución vigente sin un selector de históricos.
2. Verifica que el centro principal dispone de observaciones de actividad y capacidad confirmadas; los datos ausentes no equivalen a cero.
3. Consulta la tarifa y la explicación de las variaciones, su unidad y su procedencia; corrige los datos en las pantallas de origen, no en esta tabla de análisis.

Las tarifas se presentan automáticamente desde los costes cerrados vigentes y los datos físicos confirmados. Actualizar actividad no reabre repartos; la evaluación auxiliar fijada en el cierre conserva su trazabilidad.

## Lectura rápida de la tarifa (mejora T15)

La pantalla muestra una tabla de **composición de la tarifa** para el centro seleccionado: fijo propio, variable observado, costes recibidos/consumidos y gastos del período, cada uno con su importe, base y tarifa. La variación de capacidad se presenta por separado, no como un gasto adicional.

- **Fijo propio sujeto a capacidad:** se divide por actividad normal. La diferencia entre coste fijo y coste de actividad realizada queda identificada como variación de capacidad.
- **Variable observado:** se divide por actividad real.
- **Recibido o consumido:** se divide por actividad real porque su evaluación de capacidad no se repite en este centro. Si la procedencia muestra un fijo recibido sin evaluación acreditada, aparece un **aviso de revisión**, no un error automático ni una reclasificación.
- **Gasto de período:** se muestra fuera de la tarifa.

La opción **Requieren atención** incluye también centros con esa advertencia metodológica. Revisa el origen desde **Ver origen de costes** o la clasificación en **Preparar cuentas**; no se cambia ningún cálculo desde esta pantalla.

**Finalidad:** tarifa de gestión, no valoración contable validada de existencias. El cálculo inventariable está fuera de esta pantalla y necesita el perímetro y los controles específicos del módulo T32. No se ofrecen tarifas inventariables ficticias ni se presentan las órdenes estimadas como consumos reales.

## Ampliación: tarifas y costes unitarios (candidatura 2026-11-04)

La pantalla se denomina **Tarifas y costes unitarios**. La pestaña **Tarifas de centros** muestra los nodos generales; **Factores directos** muestra grupos homogéneos de MOD y grupos de aprovisionamiento configurados. Ambas consultan el **mismo cálculo** y las mismas fuentes congeladas; seleccionar una pestaña no modifica importes ni recalcula el reparto.

La finalidad del nodo se selecciona al diseñar la estructura. Los modelos históricos sin clasificación explícita permanecen en centros generales hasta que el usuario actualice su configuración en una versión editable. Los importes propios, recibidos, variables y de capacidad se siguen calculando conforme a CM08/CM09. La tarifa MOD no implica que sus horas se hayan aplicado a órdenes: eso requiere consumos reales T19. No se transforma una estimación de tiempo en actividad real ni se duplica la prestación auxiliar.

**Estado:** funcionalidad desarrollada en rama candidata; no considerar integrada o validada en Windows hasta disponer de prueba y aceptación de la versión.

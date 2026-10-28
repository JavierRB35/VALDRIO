# Ayuda contextual: Tarifas de centros
**ID Vista UI:** `View_PrincipalRates`  
**Última actualización:** `2026-10-28`
**Estado:** guía revisada sobre `2599188` y la auditoría contextual de 28/10/2026. Las evidencias de validación se registran por separado.

## 1. Objetivo y función financiera
Interpretar tarifas y variaciones de capacidad por centro principal, usando costes de una ejecución económica compatible y datos confirmados en Actividad y capacidades, de acuerdo con la política normalizada vigente. La vista es de análisis, no editor de horas.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Centro seleccionado | Tabla | Sí para detalle | Filtra las preguntas al centro y unidad concreta. |
| Solo incidencias | Booleano | No | Reduce filas visibles a casos que requieren atención. |
| Actualizar | Acción | Opcional | Reconstruye la lectura del estado vigente. |
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

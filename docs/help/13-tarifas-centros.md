# Ayuda contextual: Tarifas de centros
**ID Vista UI:** `View_PrincipalRates`  
**Última actualización:** `2026-10-21`  
**Estado:** borrador documental contrastado con código de `main@211af0d`; pendiente de prueba interactiva Windows.

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
| Coste y tarifa combinada | Componentes normalizados / base aprobada | Indica coste por unidad si existen datos consistentes. |
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

### Referencias al código
- [`src/Valdrio.Desktop/PrincipalRatesWindow.xaml`](../../src/Valdrio.Desktop/PrincipalRatesWindow.xaml)
- [`src/Valdrio.Desktop/PrincipalRatesEditor.cs`](../../src/Valdrio.Desktop/PrincipalRatesEditor.cs)
- [`src/Valdrio.Application/PrincipalRateReport.cs`](../../src/Valdrio.Application/PrincipalRateReport.cs)
- [`src/Valdrio.Calculation/PrincipalRates.cs`](../../src/Valdrio.Calculation/PrincipalRates.cs)

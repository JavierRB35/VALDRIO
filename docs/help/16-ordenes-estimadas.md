# Ayuda contextual: Órdenes estimadas
**ID Vista UI:** `View_Estimates`  
**Última actualización:** `2026-10-28`

## 1. Objetivo y función financiera
Valora una orden con una composición publicada, precios de materiales y tarifas de actividad de un cálculo económico guardado. Cada aceptación crea una versión independiente que conserva esos datos. Se consulta desde **Productos y resultados → Órdenes estimadas**.

Es una estimación de gestión: no consume materiales, no modifica repartos y no registra producción real ni valoración contable de existencias.

## 2. Guía de campos y entradas (Inputs)
| Campo | Significado |
| --- | --- |
| Referencia de orden | Identifica la orden dentro del análisis. |
| Receta publicada | Fija cantidades, bases y unidades. |
| Cantidad | Salida de la orden, con el separador decimal configurado. |
| Cálculo guardado | Origen económico de las tarifas. |

1. Publica la composición en **Productos y servicios**. Importa y guarda los precios de referencia de los materiales finales.
2. Guarda un cálculo económico completo y confirma las observaciones de actividad necesarias para sus tarifas.
3. Abre **Órdenes estimadas**, escribe una referencia única dentro del análisis y selecciona receta, cantidad y cálculo guardado.
4. Pulsa **Calcular estimación**. Revisa las siete respuestas y el desglose de cantidades, precios/tarifas, importes y fuentes.
5. Pulsa **Aceptar y congelar versión**. El análisis debe estar abierto y el archivo permitir escritura. La confirmación indica que la versión ya está guardada.

Un cambio de entrada invalida la previsualización. Si los precios, las observaciones o la última versión cambian antes de aceptar, hay que volver a calcular. Una orden sin aceptar no se guarda al salir; la aplicación permite conservar la edición o descartarla explícitamente.

## 3. Interpretación de resultados (Outputs)
Selecciona una versión y pulsa **Consultar versión congelada**. Sus valores no se sustituyen por precios, recetas ni observaciones actuales. Para revisar una orden, selecciona su última versión y pulsa **Preparar nueva versión**. La referencia y el producto se mantienen; puedes elegir otra versión de receta, cantidad o cálculo.

Elige dos versiones de la misma orden en los selectores del historial y pulsa **Comparar versiones**. Se muestran cantidades, totales, costes unitarios y fuentes de ambas, con la diferencia de coste total. Esa diferencia puede combinar cambios de volumen, receta, precios y tarifas; no es una desviación de consumo real.

## 4. Casos de uso, validaciones y errores frecuentes
Con 120 unidades, lote de 50, preparación de 2 h por lote a 30/h y proceso de 0,1 h por unidad a 20/h, se inician 3 lotes: preparación 180, proceso 240, total 420 y coste unitario 3,5. El último lote parcial requiere su preparación completa.

Las tarifas se multiplican con toda su precisión, antes del redondeo de importes. Los semielaborados se expanden y sus componentes finales se suman una sola vez. El precio de referencia del producto terminado no se añade otra vez al coste.

## 5. Datos ausentes y límites
- Un precio o tarifa ausente bloquea la aceptación. Un cero declarado es distinto de un dato ausente.
- Los precios de referencia y la moneda funcional del análisis deben coincidir; no se convierte divisa automáticamente.
- Se usan el cálculo económico elegido y las observaciones de actividad vigentes al previsualizar; ambos quedan identificados en la versión aceptada.
- Los archivos anteriores se actualizan a formato 13 en una copia, conservando el original.
- El registro de consumos reales y las desviaciones corresponden a T19. La valoración contable corresponde a T32.

### Si falta valoración
La vista puede conservar las necesidades físicas aunque falte un precio o una tarifa. El diagnóstico identifica el componente que debes completar. Es un cálculo físico con valoración incompleta: no permite aceptar ni sustituye el coste ausente por cero.

## Recorrido recomendado
1. Prepara una receta publicada, precios de materiales y un cálculo económico guardado con las tarifas de actividad necesarias.
2. Introduce referencia, cantidad, receta y cálculo; ejecuta la estimación y revisa desglose, fuentes y respuestas antes de aceptarla.
3. Acepta y congela la versión; consulta su historial o compara dos versiones de la misma orden, sin interpretar diferencias como consumos reales.

# Ayuda contextual: Productos y servicios: composiciones
**ID Vista UI:** `View_Compositions`  
**Última actualización:** `2026-10-21`  
**Estado:** borrador documental contrastado con código de `main@211af0d`; pendiente de prueba interactiva Windows.

## 1. Objetivo y función financiera
Construir catálogo físico de productos/servicios y versiones de composición con cantidades de materiales, semielaborados y actividad. Permite estimar necesidades físicas para una unidad, lote u orden de simulación; T17 no asigna precios ni calcula costes monetarios.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Catálogo: código, nombre, tipo y unidad | Texto/selector | Según artículo | Identifica materiales, productos, servicios y semielaborados. |
| Producto y versión | Selectores | Sí | Abren borrador o versión publicada inmutable. |
| Tamaño de lote | Cantidad | Según receta | Define unidades del lote que comparte preparación. |
| Nota | Texto | No | Explica hipótesis y alcance físico. |
| Líneas de composición | Tabla | Según receta | Seleccionan componente, cantidad, unidad, tipo y condiciones de consumo. |
| Conversión de unidad | Factor numérico | Si hay cambio de unidad | Indica unidades de destino por cada unidad introducida, solo entre dimensiones compatibles. |
| Exceso o rendimiento | Porcentaje | Según línea | Modifica necesidad física según política declarada; no equivale automáticamente a merma capitalizable. |
| Preparación por lote | Cantidad | Cuando procede | Los lotes parciales requieren preparación completa según D06. |
| Publicar / nueva versión | Acciones | Según estado | Publicación fija la receta; cambios posteriores requieren nueva versión. |
| Cantidad a previsualizar | Cantidad | Sí, si simulación | Escala el requerimiento físico a la unidad/lote/orden conceptual. |
| Guardar / previsualizar | Acciones | Según estado | Separa persistencia de borrador y cálculo físico informativo. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Estado de versión | Borrador/publicada + número | Permite auditar la hipótesis aplicable. |
| Necesidades físicas | Cantidad tras conversión, rendimiento y discreción | Sirve para preparar consumos/órdenes futuros, no para valorarlos monetariamente. |
| Preguntas de analista | Lotes, dependencias, mermas, versiones | Señalan datos faltantes y cambios. |
| Resumen expandido | Componentes finales sin doble cómputo del semielaborado | Permite revisar dependencias antes de calcular costes T18. |

## 4. Casos de uso, validaciones y errores frecuentes
- T17 no contiene precios, tasas, importes ni consumos reales T19.
- No mezclar unidades de dimensiones distintas ni factores implícitos.
- Las referencias a semielaborados han de apuntar a versiones publicadas; se impiden ciclos.
- Una receta publicada no se edita en sitio: crear nueva versión.
- No deducir materiales directos a partir de códigos contables 600/601.

### Referencias al código
- [`src/Valdrio.Desktop/CompositionWindow.xaml`](../../src/Valdrio.Desktop/CompositionWindow.xaml)
- [`src/Valdrio.Desktop/CompositionEditor.cs`](../../src/Valdrio.Desktop/CompositionEditor.cs)
- [`src/Valdrio.Calculation/CompositionCalculator.cs`](../../src/Valdrio.Calculation/CompositionCalculator.cs)

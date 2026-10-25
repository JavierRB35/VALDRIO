# Ayuda contextual: Productos, servicios y costes de referencia
**ID Vista UI:** `View_Compositions`  
**Última actualización:** `2026-10-25`
**Estado:** guía funcional de T17/T18 contrastada con `928b263`; el coste mostrado es una estimación de gestión, no una valoración contable.

## 1. Para qué sirve
Esta pantalla define qué componentes físicos necesita un producto o servicio y permite calcular un coste de gestión estimado usando costes unitarios de referencia. El cálculo sirve para analizar una composición o comparar su subtotal de componentes con el coste unitario importado del producto.

El importe de referencia es un dato de gestión: **no es consumo real, coste observado ni valoración contable de existencias o producción en curso**. El consumo real y sus desviaciones corresponden a T19; la valoración contable, a T32. Los costes unitarios usan la moneda predeterminada de la empresa.

## 2. Importar productos y costes unitarios
En la pestaña **Catálogo**, selecciona **Importar productos y costes unitarios** y elige un archivo CSV o TSV. El archivo debe incluir una fila de cabeceras y estas cuatro columnas:

| Columna | Qué introducir | Ejemplo |
| :--- | :--- | :--- |
| Referencia | Código estable que identifica el producto o material | `P-104` |
| Nombre | Descripción que verá el analista | `Tornillo inoxidable` |
| Unidad de medida | Código o símbolo existente en el catálogo de unidades de la empresa | `ud` |
| Coste unitario | Coste de una unidad, sin símbolo de moneda | `0,12` |

Se aceptan separadores CSV habituales (coma o punto y coma) y tabuladores. El separador decimal sigue la configuración regional; con coma decimal, el punto y coma suele ser el separador de columnas más cómodo. Las cabeceras se pueden escribir en español o inglés. La unidad debe coincidir con un código o símbolo de unidad ya configurado; la importación no crea ni convierte unidades. El coste debe ser cero o positivo.

El selector **Importar como** clasifica las referencias nuevas como producto, material o servicio. Para los componentes directos de una composición, elige **Material**; los productos y servicios representan resultados que se pueden comparar con el subtotal calculado. La importación no crea líneas de composición automáticamente.

Al importar otra vez, VALDRIO busca la referencia. Si existe, actualiza su nombre y coste de referencia, conserva su identidad y unidad y mantiene el tipo que ya tenía. Si es nueva, añade una fila con el tipo seleccionado. Si una referencia existente llega con otra unidad, se rechaza el archivo completo para que el coste no quede asociado a una unidad incompatible. Revisa el resultado y pulsa **Guardar catálogo** para persistirlo; cerrar o recargar antes de guardar descarta esos cambios.

El catálogo muestra el coste unitario y el nombre del fichero de origen con la fecha de importación. El registro de cambios de la empresa conserva la trazabilidad de las modificaciones. Los precios importados se expresan en la moneda predeterminada de la empresa; no se aplica conversión de divisa.

## 3. Crear y publicar una composición
1. Guarda o importa primero los productos y materiales del catálogo.
2. En **Composición**, selecciona un producto o servicio y abre una versión o crea un borrador.
3. Indica el tamaño del lote cuando una preparación compartida lo requiera. Añade cada componente en la tabla con cantidad, unidad de entrada, factor de conversión y aplicación por unidad, lote iniciado u orden.
4. Usa **Exceso de entrada** para expresar material adicional sobre la cantidad base. Usa **Rendimiento útil** para expresar qué proporción de la entrada produce cantidad útil. Son hipótesis físicas: no determinan por sí solas si una pérdida es normal ni cómo debe tratarse contablemente.
5. Para un componente semielaborado, selecciona una versión publicada del mismo modelo. La aplicación expande esa versión y suma sus componentes finales una sola vez, evitando contar también el semielaborado como un coste adicional.
6. Guarda el borrador para continuar editándolo. **Publicar** fija la versión; para cambiarla, crea una nueva versión.

Las conversiones son explícitas y solo se permiten entre unidades de la misma dimensión. El factor indica unidades del componente por unidad introducida; si ambas unidades son iguales, el factor debe ser 1. Las cantidades de unidades discretas se redondean hacia arriba después de convertir. Un lote parcial requiere la preparación completa del lote iniciado (por ejemplo, 120 unidades con un lote de 50 requieren 3 preparaciones).

## 4. Casos de uso e interpretación del coste estimado
Escribe la cantidad de salida y selecciona **Calcular necesidades físicas**. La tabla de resultados detalla los componentes, las cantidades base y ajustadas y la cantidad necesaria tras conversiones y redondeos. El resumen expande los semielaborados y muestra solo materiales y actividades finales.

La respuesta de coste muestra por separado:

- **Subtotal de componentes con precio:** suma de cantidad requerida × coste unitario de referencia para los materiales finales que tienen precio.
- **Faltan precios de referencia:** identifica los componentes físicos que no tienen coste unitario. El subtotal visible no los incluye y, por tanto, puede ser parcial.
- **Actividad indirecta no incluida:** las cantidades de actividad aparecen en el resultado físico, pero esta pantalla no aplica automáticamente tarifas T15 ni duplica importes de centros. El subtotal no incluye esas actividades.
- **Referencia importada del producto:** coste unitario de referencia del resultado multiplicado por la cantidad de salida. Se presenta como comparación separada; no se resta del subtotal ni se usa como coste de fabricación.

Ejemplo: si la receta requiere 3 kg de material a 4 €/kg y 2 horas de actividad, VALDRIO muestra 12 € como subtotal de componentes, avisa de que las 2 horas de actividad no están valoradas y, si se importó un coste unitario del producto, muestra su referencia aparte. No debe interpretarse el subtotal de 12 € como coste total mientras haya costes o actividades sin valorar.

## 5. Límites y validaciones
- Un coste de referencia describe un precio unitario cargado por el usuario. No representa necesariamente precio de compra, consumo, tarifa aprobada o coste contable.
- Las referencias de coste usan la moneda predeterminada de la empresa y no admiten conversión automática. Comprueba que los importes del fichero usan esa moneda.
- Si faltan precios de materiales o hay actividad indirecta, el cálculo no es un coste completo. VALDRIO lo señala y no convierte la ausencia en cero.
- Las mermas y rendimientos son parámetros de cantidad; el analista debe decidir su normalidad y tratamiento en su política económica.
- Los materiales indirectos pueden seguir en centros de coste. No incluyas el mismo consumo a la vez como material directo y como coste indirecto.
- No deduzcas materiales directos de los códigos contables 600/601.
- Las versiones publicadas no se modifican; crea una versión nueva para cambiar cantidades o componentes.

### Referencias al código
- [`src/Valdrio.Desktop/CompositionWindow.xaml`](../../src/Valdrio.Desktop/CompositionWindow.xaml)
- [`src/Valdrio.Desktop/CompositionEditor.cs`](../../src/Valdrio.Desktop/CompositionEditor.cs)
- [`src/Valdrio.Desktop/ProductCostImport.cs`](../../src/Valdrio.Desktop/ProductCostImport.cs)
- [`src/Valdrio.Calculation/CompositionCalculator.cs`](../../src/Valdrio.Calculation/CompositionCalculator.cs)
- [`src/Valdrio.Calculation/CompositionCostEstimator.cs`](../../src/Valdrio.Calculation/CompositionCostEstimator.cs)

## Órdenes estimadas completas
Para valorar también la actividad y conservar una versión aceptada, abre **Productos y resultados → Órdenes estimadas**. Usa una composición publicada y un cálculo económico guardado. El subtotal de esta pantalla sigue siendo una consulta de referencia. Consulta la [guía de órdenes estimadas](16-ordenes-estimadas.md).

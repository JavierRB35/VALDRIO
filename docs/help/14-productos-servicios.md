# Ayuda contextual: Productos, servicios y costes de referencia
**ID Vista UI:** `View_Compositions`  
**Última actualización:** `2026-11-18`
**Estado:** guía funcional de T17/T18 revisada con el comportamiento implementado en la rama de productos. El coste mostrado es una estimación de gestión, no una valoración contable.

## 1. Para qué sirve
Esta pantalla mantiene el catálogo de productos, servicios, semielaborados y materiales; define qué componentes físicos necesita cada resultado; y calcula necesidades y un subtotal de gestión con costes unitarios de referencia.

El importe de referencia **no es consumo real, coste observado ni valoración contable de existencias o producción en curso**. El consumo real y sus desviaciones corresponden a T19; la valoración contable, a T32. Los costes unitarios se expresan en la moneda predeterminada de la empresa y no se convierten a otra divisa.

## 2. Importar productos y costes unitarios
En **Catálogo**, selecciona **Importar productos y costes unitarios**. El archivo debe tener una fila de cabeceras y estas cuatro columnas, en cualquier orden:

| Columna | Qué introducir | Ejemplo |
| :--- | :--- | :--- |
| Referencia | Código estable como texto; se conservan mayúsculas, minúsculas y ceros iniciales | `0012` |
| Nombre | Descripción que verá el analista | `Tornillo inoxidable` |
| Unidad de medida | Código o símbolo ya existente en el catálogo de unidades | `ud` |
| Coste unitario | Coste de una unidad, sin símbolo de moneda; cero o positivo | `0,12` |

Formatos y límites reales:

- **CSV/TSV/TXT:** UTF-8; la primera línea determina el separador entre `;`, `,` o tabulador. Las comillas dobles protegen separadores dentro de un valor. Las comillas sin cerrar se rechazan.
- **XLSX:** se lee únicamente la **primera hoja**; las cabeceras deben estar en la **fila 1** y los datos comienzan en la **fila 2**. La celda de Referencia debe ser texto: un número como `0012` pierde los ceros iniciales y la fila se rechaza. No se evalúan fórmulas: se usa el último valor cacheado que Excel haya guardado. Una fórmula sin valor cacheado o una celda de error no aporta un valor utilizable y la fila se rechaza.
- El adaptador XLSX aplica límites técnicos de 32 MiB por archivo, 100.000 filas, 16.384 columnas, 262.144 caracteres por celda y 250.000 cadenas compartidas. Un archivo que los supere se rechaza.
- Las cabeceras admiten español o inglés y se comparan sin distinción de mayúsculas, acentos ni espacios. Una cabecera repetida o ambigua se rechaza.
- El coste sigue la configuración regional: en es-ES se escribe `0,12`; `12.50` también se interpreta como decimal cuando el punto no pueda ser separador de miles (tres cifras finales). No se admiten símbolos de moneda.

**Importar como** clasifica solo las referencias nuevas como producto, material o servicio. Para componentes directos elige **Material**. En una reimportación, la referencia existente conserva identidad, unidad y tipo; se actualizan nombre, coste y procedencia. Si la unidad no coincide, o hay referencias duplicadas, se rechaza el archivo completo. Revisa el resultado y pulsa **Guardar catálogo y cambios pendientes** para persistirlo de forma atómica; cerrar o recargar antes de guardar descarta esos cambios.

## 3. Editar la composición activa

1. Da de alta un producto, material o servicio con referencia, nombre, tipo y unidad; guárdalo. La unidad **Unidad (u)** está en las unidades predeterminadas y puede instalarse desde el botón de la pantalla en proyectos antiguos.
2. Puedes dejar el coste unitario vacío o escribir un coste manual con coma o punto decimal (`12,50` o `12.50`), sin separador de miles. Un valor inválido permanece en la celda y no se guarda. Si el coste manual es distinto de cero, al abrir la composición se explica la precedencia del importe calculado.
3. En **Composición**, selecciona el producto y pulsa **Abrir**. No hay selectores de borrador, número de versión ni botón de publicación en el recorrido habitual. Edita los componentes y pulsa **Guardar composición y catálogo**.
4. Los componentes admitidos son materiales **activos** del catálogo, semielaborados publicados de la misma estructura y actividades de **nodos principales activos**. Estos nodos pueden representar producción, mano de obra directa (MOD) u otros factores directos. Los nodos auxiliares no se ofrecen: sus costes llegan mediante el reparto secundario y no deben imputarse dos veces.
5. Una composición nueva no puede introducir nodos auxiliares, componentes inactivos ni semielaborados inactivos tampoco a través de un hijo semielaborado. Las definiciones históricas pueden abrirse y consultarse; si la referencia está inactiva, aparece en el selector marcada como inactiva, se abre con **Abrir** y la consulta es de solo lectura: no permite editar, guardar ni publicar. Sus componentes no seleccionables se identifican como tales.
6. En cada línea introduce base, cantidad, unidad de entrada, factor de conversión y merma o rendimiento.

### Bases, conversión y redondeo
- **Por unidad:** la cantidad se multiplica por las unidades de salida simuladas.
- **Por lote iniciado:** se multiplica por los lotes completos, redondeando al alza. Con 120 unidades y tamaño de lote 50 se inician 3 lotes; el último parcial consume la preparación completa.
- **Por orden:** la cantidad se aplica una sola vez en el cálculo. Esta pantalla no conoce la cantidad de una orden concreta, por lo que una línea con esta base impide actualizar automáticamente el coste unitario.
- La merma se aplica antes de la conversión y del redondeo. **Exceso de entrada** del 10 %: `10 × 1,10 = 11`. **Rendimiento útil** del 80 %: `10 / 0,80 = 12,5`. Después se aplican la base, el factor de conversión y, solo en unidades discretas, el redondeo al alza.
- Cero es un valor declarado y válido; una celda vacía significa dato ausente y no se transforma en cero.
- Las conversiones solo se permiten entre unidades de la misma dimensión. Si la unidad de entrada y la del componente son la misma, el factor debe ser `1`.

## 4. Coste manual, coste calculado y resultados parciales
Cuando se guarda una composición con todos los materiales finales valorados, sin actividades y sin bases por orden, VALDRIO calcula el coste unitario del resultado y actualiza su coste de referencia. El cálculo usa el tamaño de lote guardado, de modo que un cambio de lote puede cambiar el coste unitario si hay preparaciones por lote. El origen pasa a indicar composición y conserva la referencia interna de la definición utilizada.

Si falta un precio, hay actividad —incluida MOD u otros directos— sin tarifa en esta pantalla, o existe una base por orden en cualquier nivel expandido, el desglose es **parcial**:

- se conserva la referencia manual cuando existe;
- se retira el coste calculado por una composición anterior, para que un desglose incompleto no siga apareciendo como vigente;
- se muestra un aviso explicando qué debe revisarse;
- nunca se inventa un precio para un dato ausente.

La actividad se muestra en las necesidades físicas, pero esta pantalla no aplica tarifas T15. Para valorar nodos principales con tarifas y conservar una estimación de orden aceptada, continúa en **Órdenes estimadas** (T18).

## 5. Interpretación de la previsualización
Al calcular necesidades físicas:

- la tabla detalla cantidad base, cantidad ajustada por merma, cantidad convertida y cantidad requerida por componente;
- los semielaborados se expanden y sus componentes finales se suman una sola vez;
- el resumen muestra materiales y actividades finales;
- el **subtotal de componentes con precio** suma cantidad requerida × coste unitario de referencia de los materiales finales con precio;
- los componentes sin precio se enumeran por separado y no se incluyen en ese subtotal;
- la actividad sin tarifa se indica por separado y excluye del subtotal;
- la **referencia importada del producto** se muestra como comparación aparte: coste unitario × cantidad simulada. No se resta del subtotal ni se usa como coste de fabricación.

Ejemplo: 3 kg de material a 4 €/kg y 2 horas de actividad producen un subtotal de 12 € con aviso de actividad no valorada. Mientras falte precio o tarifa, 12 € no es un coste total.

### Límites de interpretación contable
- Un coste de referencia es un importe unitario proporcionado por el usuario. No es necesariamente precio de compra, consumo real, tarifa aprobada ni coste contable.
- Los materiales indirectos pueden seguir en centros de coste. No incluyas el mismo consumo a la vez como material directo y como coste indirecto.
- No deduzcas materiales directos de los códigos contables 600/601.
- Las mermas y rendimientos son parámetros de cantidad planificada; el analista decide su normalidad y su tratamiento en la política económica. Esta pantalla no aprueba esa política.

## 6. Compatibilidad histórica y validaciones
- No hay controles de versiones en la interfaz ordinaria. Guardar la composición activa crea una nueva definición publicada interna; las definiciones empleadas por estimaciones u órdenes aceptadas permanecen inmutables.
- Quitar una referencia guardada la **desactiva**, sin borrar su identidad ni sus relaciones. Puede reactivarse marcando **Activo**.
- Si una referencia inactiva conserva una composición guardada, puede **abrirse en modo consulta** para revisar su histórico: se muestran sus componentes y datos, pero no se puede editar, guardar ni publicar, no se crea una versión nueva ni un clon y no se ofrece como componente de otras composiciones. El selector de producto incluye estas referencias identificadas como inactivas. Los selectores de componentes nuevos siguen limitados a materiales activos y nodos principales activos.
- Descartar cambios recupera en estado limpio la versión guardada que estaba abierta y conserva el producto seleccionado. Si la composición abierta nunca llegó a guardarse (por ejemplo, una versión nueva sin guardar), no se recrea: la pantalla queda explícitamente sin composición cargada y se indica que hay que pulsar **Abrir**.
- El catálogo exige referencias únicas, nombre, unidad válida y coste no negativo. Las cantidades, factores y porcentajes se validan antes de guardar; el rendimiento debe estar entre 0 y 100 y el exceso de entrada no puede ser negativo.
- El tamaño de lote debe ser positivo; si la unidad de salida es discreta, debe ser entero.
- La operación con valores decimales usa `decimal`; los importes y cantidades se conservan con su precisión y se redondean solo donde la regla física lo indica.

### Referencias al código
- [`src/Valdrio.Desktop/CompositionWindow.xaml`](../../src/Valdrio.Desktop/CompositionWindow.xaml)
- [`src/Valdrio.Desktop/CompositionEditor.cs`](../../src/Valdrio.Desktop/CompositionEditor.cs)
- [`src/Valdrio.Desktop/ProductCostImport.cs`](../../src/Valdrio.Desktop/ProductCostImport.cs)
- [`src/Valdrio.Calculation/CompositionCalculator.cs`](../../src/Valdrio.Calculation/CompositionCalculator.cs)
- [`src/Valdrio.Calculation/CompositionCostEstimator.cs`](../../src/Valdrio.Calculation/CompositionCostEstimator.cs)

Para valorar también la actividad y conservar una versión aceptada, abre **Productos y resultados → Órdenes estimadas** y consulta su [guía](16-ordenes-estimadas.md).

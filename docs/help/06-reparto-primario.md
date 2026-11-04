# Ayuda contextual: Reparto primario
**ID Vista UI:** `View_PrimaryAllocation`  
**Última actualización:** `2026-10-28`
**Estado:** guía revisada sobre `2599188` y la auditoría contextual de 28/10/2026. Las evidencias de validación se registran por separado.

## 1. Objetivo y función financiera
Asignar costes preparados a nodos/centros mediante claves e inductores con cobertura verificable. Permite calcular, justificar y cerrar la primera imputación de costes a la estructura.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Biblioteca de claves y versión | Selector | Según asignación | Usa claves publicadas, con control de referencias históricas. |
| Nombre de clave | Texto | Sí al crear | Denomina la regla reutilizable. |
| Criterio/modo | Porcentaje, unidades o igualitario | Sí | Define cómo repartir el importe entre destinos incluidos. |
| Unidad inductora | Selector | Si se usan cantidades | Unidad de la magnitud empleada en la clave. |
| Destinos incluidos y valor | Casillas/cantidad o porcentaje | Sí | Determinan los pesos de distribución; revisar suma y cobertura. |
| Composición fijo/variable | Porcentajes | Según cuenta | Distingue comportamiento del coste para explicaciones y cálculo posteriores. |
| Regla: cuenta exacta o prefijo | Texto/selector | Opcional | Asocia conjuntos de cuentas a una clave guardada. |
| Regla: clave publicada | Selector | Sí, si regla | Indica el criterio que se aplicará a cuentas coincidentes. |
| Excepciones y asignación individual | Cuenta/parte/clave | Según cobertura | Resuelve casos no cubiertos por reglas generales. |
| Importes absolutos | Moneda | Según modalidad | Distribución explícita cuando se utiliza asignación monetaria. |
| Asignación masiva | Selección de filas y clave | Opcional | Aplica criterio a múltiples cuentas previo control de alcance. |
| Descripción visible de cuenta | Selector | Opcional | Ayuda a resolver procedencias sin cambiar identidad del código. |
| Calcular/cerrar/reabrir | Acciones | Según estado | Obliga a revisar resultados y requisitos previos a cierre. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Cobertura por cuenta | Asignado y pendiente | Muestra costes aún sin repartir. |
| KPIs de primario | Base efectiva, repartible, asignado, excluido, directo | Conciliación de la distribución inicial. |
| Detalle por cuenta | Destino, centro, driver, porcentaje e importe | Explica por qué cada recurso llega a un nodo. |
| Grafo primario | Relaciones de cuenta seleccionada | Visualiza el origen/destino sin presentar flujos ficticios. |
| Totales por nodo | Suma de importes distribuidos | Base que recibirá el reparto secundario. |

## 4. Casos de uso, validaciones y errores frecuentes
- Guardar una clave o regla no equivale a aplicarla o cerrar reparto.
- No cerrar si existen partes repartibles pendientes o claves inválidas.
- Una clave publicada referenciada históricamente no debe borrarse sin validar dependencias.
- Comprobar signos y redondeo con importes positivos y negativos.
- Cambiar composición o recursos puede exigir recalcular resultados.

- Para editar una clave, Tab y Mayús+Tab recorren los controles editables habilitados de líneas, pesos, clasificación y cobertura; se omiten las columnas informativas. Los diagnósticos contextuales aparecen en rojo; los avisos modales siguen existiendo.

### Elegir criterio y resolver coincidencias
- **Porcentajes:** los destinos incluidos deben sumar exactamente 100 %.
- **Unidades de inductor:** introduce cantidades no negativas de una misma magnitud; la suma debe ser positiva. Cada destino recibe su cantidad dividida por el total: 30 y 30 horas reparten 50 % y 50 %, no 30 % y 30 %.
- **Partes iguales:** cada destino incluido recibe la misma proporción.

Elige un inductor que represente el consumo o beneficio del recurso; la aplicación comprueba los pesos, pero no acredita que el criterio económico sea adecuado. Las cantidades de una clave no sustituyen las observaciones de actividad y capacidad.

Una asignación individual de clave prevalece sobre reglas automáticas. Entre reglas, prevalece cuenta exacta, después el prefijo más largo y después la mayor prioridad; un empate sin resolver produce diagnóstico. La composición fijo/variable divide el importe por comportamiento, no cambia el porcentaje destinado a cada nodo.

### Referencias al código
- [`src/Valdrio.Desktop/AllocationWorkspacePanel.xaml`](../../src/Valdrio.Desktop/AllocationWorkspacePanel.xaml)
- [`src/Valdrio.Desktop/AllocationWorkspaceViewModel.Primary.cs`](../../src/Valdrio.Desktop/AllocationWorkspaceViewModel.Primary.cs)
- [`src/Valdrio.Application/PrimaryAllocation.cs`](../../src/Valdrio.Application/PrimaryAllocation.cs)

### Gráfico de Pareto
El gráfico ordena todos los nodos que reciben coste en el primario de mayor a menor coste. Cada barra muestra el coste agregado del nodo; la línea representa el porcentaje acumulado sobre la suma de costes positivos. Puedes consultar la tabla de valores y desplazar horizontalmente el gráfico. Los abonos se muestran con su signo y quedan fuera del acumulado. Positivos más abonos concilian con el neto; si el neto es cero o negativo, no se calcula porcentaje. Sin resultados se indica que todavía no hay datos.

### Claves directas
**Generar claves directas a nodos** prepara claves de un único destino. Informa de las creadas, equivalentes existentes y omitidas; no las asigna automáticamente a cuentas. Revisa el destino y asigna la clave que corresponda.

## Recorrido recomendado
1. Crea o selecciona las claves de reparto y verifica el criterio y los destinos de cada cuenta o recurso.
2. Asigna las claves en las tablas, guarda los cambios y calcula solo cuando la cobertura y los porcentajes sean válidos.
3. Examina importes por nodo y el Pareto (barras de coste, línea de porcentaje acumulado); consulta el detalle y cierra el reparto cuando esté revisado.

Las tablas crecen con sus filas: utiliza el desplazamiento exterior de la página. Guardar claves y las acciones de cierre permanecen en la barra inferior; cambian con la pestaña activa. El desplazamiento horizontal permite consultar las columnas que no caben.

## Ampliación de presentación por finalidad — candidatura 2026-11-04

Las tablas de asignaciones y de totales por nodo ofrecen dos categorías independientes: **Centros generales/indirectos** y **Factores directos**. La selección utiliza la finalidad explícita del nodo, no su nombre, papel principal/auxiliar ni el tipo de recurso. Los nodos antiguos sin finalidad explícita se muestran provisionalmente entre los generales. Los **totales conciliados** siguen incluyendo ambas categorías y proceden de los mismos hechos primarios, sin otro reparto. El Pareto sigue la categoría elegida.

**Estado:** implementación candidata pendiente de validación en Windows.

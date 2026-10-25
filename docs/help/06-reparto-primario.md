# Ayuda contextual: Reparto primario
**ID Vista UI:** `View_PrimaryAllocation`  
**Última actualización:** `2026-10-25`
**Estado:** guía funcional mantenida para `main@6885eca` (25/10/2026). Describe el código de esta pantalla; las pruebas visuales interactivas se registran por separado y solo aplican al SHA indicado en cada evidencia.

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

### Referencias al código
- [`src/Valdrio.Desktop/AllocationWorkspacePanel.xaml`](../../src/Valdrio.Desktop/AllocationWorkspacePanel.xaml)
- [`src/Valdrio.Desktop/AllocationWorkspaceViewModel.Primary.cs`](../../src/Valdrio.Desktop/AllocationWorkspaceViewModel.Primary.cs)
- [`src/Valdrio.Application/PrimaryAllocation.cs`](../../src/Valdrio.Application/PrimaryAllocation.cs)

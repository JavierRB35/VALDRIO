# Ayuda contextual: Importar saldos
**ID Vista UI:** `View_ImportBalances`  
**Última actualización:** `2026-10-25`
**Estado:** guía funcional mantenida para `928b263` (25/10/2026). Describe el código de esta pantalla; las pruebas visuales interactivas se registran por separado y solo aplican al SHA indicado en cada evidencia.

## 1. Objetivo y función financiera
Leer CSV/XLSX, interpretar saldos de cuentas, seleccionar registros, agruparlos por código y confirmar el lote que se convertirá en recurso de partida de la contabilidad analítica. La vista previa nunca equivale a confirmación.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Archivo, hoja y codificación | Ruta/selector | Según formato | Determinan el origen y la lectura de CSV o XLSX. |
| Encabezado y primera fila de datos | Filas enteras | Según archivo | Evitan interpretar rótulos como registros. |
| Delimitador CSV | Selector | Si CSV | Separa campos; debe corresponder al archivo original. |
| Base de importe | Selector | Sí | Distingue importe del período, saldo de cierre íntegro o acumulado; condiciona las comparaciones. |
| Sustituir lote / lote anterior | Selector | Opcional | Controla reemplazo expreso de datos ya importados; revisar alcance. |
| Períodos fiscales y comparación | Fechas/selector | Si comparación acumulada | Base de diferencias respecto a saldos anteriores. |
| Diseño del importe | Selector | Sí | Elige columna de importe o columnas Debe/Haber. |
| Columna de cuenta | Selector | Sí | Conservar código como texto, incluidos ceros iniciales. |
| Columna de descripción | Selector | Según origen | Identifica el concepto de cuenta y permite revisar conflictos. |
| Importe / Debe / Haber | Columnas numéricas | Según diseño | Define magnitudes de la entrada sin alterar arbitrariamente sus signos. |
| Separador decimal y de miles | Selectores | Según formato | Interpreta números de acuerdo con el archivo, no con supuestos de la región. |
| Sintaxis negativa | Selector | Según origen | Interpreta valores negativos de modo explícito. |
| Permitir cuentas numéricas | Booleano | Según origen | Habilita interpretación del identificador cuando el archivo representa cuentas numéricas. |
| Selección de registros | Casillas/tabla | Sí | Incluye o excluye filas antes de agrupar. |
| Moneda y conversión | Datos del origen | Según importación internacional | Muestra equivalencias y diferencias de conversión cuando procede. |
| Referencia de aprobación | Texto | Según confirmación | Conserva evidencia de la interpretación escogida. |
| Aprobar interpretación | Booleano | Sí, para confirmar | Acredita revisión previa a escribir datos en el estudio. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Vista previa | Fila/cuenta/descripción/importe/diagnóstico | Detecta errores de lectura, registros y signos. |
| Agrupación de cuentas | Código, descripción, filas e importe agregado | Comprueba la base que se preparará en Recursos. |
| Reconciliación y comparación | Importes, diferencias y ajustes de conversión | Permite verificar la lectura económica antes de confirmar. |
| Importación confirmada | Lote persistido en análisis | Habilita preparación de cuentas; solo existe tras acción explícita. |

## 4. Casos de uso, validaciones y errores frecuentes
- No confirmar columnas o números que no cuadren con el archivo original.
- Un saldo acumulado necesita período/comparación coherentes; no tratarlo como gasto mensual automáticamente.
- Revisar cuentas con idéntico código y descripción conflictiva; nunca inventar nombre.
- No confundir previsualización, validación y confirmación definitiva.
- La sustitución de un lote existente requiere revisar el alcance de los registros afectados.

### Referencias al código
- [`src/Valdrio.Desktop/ImportColumnMappingWindow.xaml`](../../src/Valdrio.Desktop/ImportColumnMappingWindow.xaml)
- [`src/Valdrio.Desktop/ImportColumnMappingViewModel.cs`](../../src/Valdrio.Desktop/ImportColumnMappingViewModel.cs)
- [`src/Valdrio.Application/ImportConfirmation.cs`](../../src/Valdrio.Application/ImportConfirmation.cs)

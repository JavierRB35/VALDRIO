# Ayuda contextual: Actividad y capacidades
**ID Vista UI:** `View_PhysicalActivity`  
**Última actualización:** `2026-11-03`
**Estado:** guía revisada sobre `2599188` y la auditoría contextual de 28/10/2026. Las evidencias de validación se registran por separado.

## 1. Objetivo y función financiera
Registrar observaciones físicas por nodo/pool, análisis y período, manteniendo versiones, fuentes y estados. Permite comparar actividad real con capacidad normal. La pantalla no define por sí sola tarifas económicas.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Proyecto, análisis y período | Selectores/contexto | Sí | Determinan el ámbito exacto de las observaciones; cambiar de estudio no copia datos. |
| Nodo y unidad | Lectura | Sí | Identifican dónde y en qué unidad se medirá la actividad. |
| Capacidad normal | Cantidad por período | Según confirmación | Denominador de utilización, revisado con fuente y calidad. |
| Actividad real | Cantidad por período | Según confirmación | Medición del período; no debe inferirse de la normal. |
| Origen de normal/real | Automático / texto opcional | No | Procedencia generada por el sistema; entrada manual si no hay otra fuente. |
| Tipo de dato y notas | Lectura/texto | Notas opcionales | Diferencian registrado, declarado y calculado; la descripción amplía la fuente cuando sea útil. |
| Nota sobre capacidad cero | Texto secundario | Opcional, visible si normal = 0 | Identifica por qué la capacidad normal es realmente cero. |
| Nota sobre la diferencia | Texto secundario | Opcional, visible si real > normal | Documenta una situación física atípica sin corregir cantidades automáticamente. |
| Teórica, práctica y motivo de corrección | Cantidades y texto avanzado | Opcional | Aportan contexto físico adicional distinto de normal y real. |
| Guardar borrador / confirmar / anular | Acciones | Según estado | Conservan versiones; un borrador incompleto no es observación confirmada. |
| Importar y calculadora de normal | Acciones | Opcional | Abren asistentes sin sustituir aprobación explícita de datos. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Utilización física | Real / normal, cuando normal > 0 | Compara actividad observada y referencia normal sin presuponer eficiencia. |
| Coste publicado asociado | Resultado T12 compatible, de solo lectura | Contextualiza el centro sin generar tarifas aquí. |
| Estado y versión de observación | Historial físico inmutable | Distingue pendiente, borrador, confirmado y anulado. |
| Diagnósticos | Validaciones y datos ausentes | Orienta revisión de fuentes y consistencia. |

## 4. Casos de uso, validaciones y errores frecuentes
- Dato ausente y cero medido son conceptos distintos; no sustituir uno por otro.
- Con normal cero, la utilización no es calculable, aunque exista actividad real positiva.
- Una utilización superior al 100 % puede ser legítima; la nota es opcional, no se trunca.
- Cambiar de estudio con ediciones sin guardar exige resolver el borrador.
- Si el estudio está cerrado, la información física se consulta sin editar.

### Referencias al código
- [`src/Valdrio.Desktop/PhysicalActivityWindow.xaml`](../../src/Valdrio.Desktop/PhysicalActivityWindow.xaml)
- [`src/Valdrio.Desktop/PhysicalActivityEditor.cs`](../../src/Valdrio.Desktop/PhysicalActivityEditor.cs)
- [`src/Valdrio.Application/PhysicalActivity.cs`](../../src/Valdrio.Application/PhysicalActivity.cs)

### Dónde editar
La tabla general es de consulta. Abre **Editar actividad y capacidad** para el nodo seleccionado: comparte normal, real y fuentes con esta pantalla. Guarda borrador o confirma explícitamente; la propuesta seleccionada se incorpora en la misma operación de guardado.

Los parámetros de calendario y su desglose están visibles en el editor; ajustar la capacidad normal no elimina días, turnos, horas ni recursos guardados. Si una versión anterior perdió esos parámetros, se recupera como referencia el último calendario del mismo nodo, modelo, unidad y período, cuando existe en el historial.

## Recorrido recomendado
1. Selecciona nodo, unidad y período para registrar la actividad real y la capacidad normal que correspondan.
2. Introduce o importa observaciones con sus fuentes; confirma el dato y verifica unidad, período y estado antes de comparar.
3. Examina desviaciones de actividad y capacidad; para obtener tarifas será necesario combinar estos datos con una ejecución económica compatible.

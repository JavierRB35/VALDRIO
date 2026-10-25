# Ayuda contextual: Calculadora de capacidad normal
**ID Vista UI:** `View_NormalCapacityAssistant`  
**Última actualización:** `2026-10-25`
**Estado:** guía funcional mantenida para `928b263` (25/10/2026). Describe el código de esta pantalla; las pruebas visuales interactivas se registran por separado y solo aplican al SHA indicado en cada evidencia.

## 1. Objetivo y función financiera
Proponer una capacidad normal a partir de calendario operativo, histórico comparable o valor declarado. El cálculo es una propuesta revisable: no altera la fila física hasta que se adopta expresamente.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Método | Calendario/histórico/declarado | Sí | Elige fuente y procedimiento de la estimación. |
| Días operativos | Días enteros del período | Si calendario | Jornadas del intervalo, no días supuestos de otro mes. |
| Turnos por día | Número | Si calendario | Frecuencia operativa del equipo. |
| Horas por turno | Horas | Si calendario | Duración efectiva de cada turno. |
| Recursos equivalentes | Número de equipos/personas | Si calendario | Escala de medios homogéneos. |
| Paradas y base de descuento | Horas o criterio elegido | Si calendario | Reduce capacidad calculada según criterio explícito; no puede exceder la base. |
| Factor de normalización | Porcentaje | Si calendario | Convierte disponibilidad en actividad normal esperada. |
| Motivo del factor | Texto | Si factor no trivial | Justifica el criterio de capacidad normal. |
| Registros históricos | Períodos comparables | Si histórico | Fuentes posibles de referencia; no introducir automáticamente meses ajenos. |
| Incluir/excluir y revisión de anomalía | Selector/justificación | Si histórico | Excluye valores atípicos solo tras revisión explícita. |
| Capacidad declarada | Cantidad en unidad del nodo | Si declarada | Referencia propuesta por responsable con evidencia. |
| Fuente y descripción declarada | Texto | Si declarada | Identifican origen y criterio de la cifra. |
| Vista previa / adoptar / cancelar | Acciones | Según estado | Solo adoptar transfiere la propuesta a la observación; la fila aún debe guardarse o confirmarse. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Propuesta de capacidad normal | Fórmula según método | Referencia física pendiente de validación final en Actividad y capacidades. |
| Capacidad teórica y práctica | Cálculo auxiliar de calendario, si procede | Explica diferencias físicas sin revalorar costes. |
| Pasos del cálculo y alertas | Operaciones y fuentes | Permite auditar el razonamiento de la propuesta. |

## 4. Casos de uso, validaciones y errores frecuentes
- No extrapolar horarios ni históricos sin compatibilidad de unidad y período.
- No utilizar históricos no comparables o anomalías sin revisión.
- No deducir actividad real a partir de capacidad normal.
- Cancelar descarta la propuesta, no modifica la celda de origen.
- Una propuesta adoptada todavía debe guardarse/confirmarse en Actividad y capacidades.

### Referencias al código
- [`src/Valdrio.Desktop/PhysicalCapacityAssistantWindow.xaml`](../../src/Valdrio.Desktop/PhysicalCapacityAssistantWindow.xaml)
- [`src/Valdrio.Desktop/PhysicalCapacityAssistantEditor.cs`](../../src/Valdrio.Desktop/PhysicalCapacityAssistantEditor.cs)

## Recorrido recomendado
1. Selecciona la base disponible para proponer la capacidad normal: calendario, histórico comparable o valor declarado.
2. Revisa el cálculo y sus hipótesis; distingue siempre una propuesta de una observación ya confirmada.
3. Adopta expresamente la propuesta si es adecuada y confirma su registro en Actividad y capacidades; si no, modifica la base o cancela sin cambiar los datos.

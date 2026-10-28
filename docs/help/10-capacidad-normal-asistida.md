# Ayuda contextual: Editar actividad y capacidad
**ID Vista UI:** `View_NormalCapacityAssistant`
**Última actualización:** `2026-10-28`

## 1. Objetivo y función financiera
Revisar la capacidad normal de referencia y registrar la actividad real del nodo y período seleccionados. Son magnitudes independientes: la actividad real nunca se calcula a partir de turnos ni se copia de otro mes. La propuesta no modifica costes ni ejecuciones publicadas.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Capacidad normal del período | Cantidad en la unidad indicada | Para confirmar | Referencia habitual. Puede mostrarse una propuesta sin confirmar. Editarla selecciona entrada manual. |
| Actividad real del período | Cantidad en la misma unidad | Para confirmar | Total realizado. Vacío significa pendiente; 0 significa ausencia de actividad. |
| Revisar cálculo | Panel desplegable | Opcional | Abre parámetros y propuesta, sin guardar automáticamente. |
| Cómo obtener la capacidad normal | Calendario / histórico / manual | Para calcular una propuesta | Selecciona el procedimiento. La entrada manual usa el mismo campo principal. |
| Días de funcionamiento | Días del período | Si calendario | No se copian días de otro mes. Completar si no existe calendario fiable. |
| Turnos diarios | Turnos diarios | Si calendario | Recuperables del último calendario compatible; revisar su vigencia. |
| Horas de cada turno | Horas | Si calendario | Duración efectiva. No descontar dos veces los descansos. |
| Recursos simultáneos equivalentes | Equipos/personas equivalentes | Si calendario | Recursos simultáneos homogéneos; no sumar equipos y personas que representen la misma capacidad. |
| Tiempo no disponible (h) / Cómo están expresadas las paradas | Horas y frecuencia | Si calendario | La frecuencia convierte el dato en horas totales. No copiar totales de otro período. |
| Nivel habitual de actividad (%) | Porcentaje | Si calendario | Convierte capacidad disponible en normal. No representa actividad real. |
| Nota sobre el nivel habitual (opcional) | Texto en panel secundario | No | Contexto adicional, nunca condición para calcular. |
| Períodos históricos / incluir / excepcional | Selección | Si histórico | Se proponen períodos confirmados del mismo modelo, nodo, unidad y duración; sin futuros, solapamientos o duplicados. Revisar selección. Notas opcionales. |
| Usar esta propuesta al guardar | Casilla | Para sustituir el valor con una propuesta | Guarda el resultado calculado junto con su origen en la siguiente operación de guardado. No sustituye silenciosamente cifras existentes. |
| Origen de la capacidad normal | Texto en Origen, notas e historial | No | Generado por el método o identificado como entrada manual. |
| Origen de la actividad real | Texto en Origen, notas e historial | No | Conserva importación/registro existente; una modificación manual queda identificada como tal. |
| Notas adicionales (opcionales) | Texto | No | Aclaraciones de normal y real; no repetir fórmulas ni procedencias. |
| Nota sobre capacidad cero (opcional) | Texto condicional | No | Visible dentro del detalle cuando normal = 0. |
| Nota sobre la diferencia (opcional) | Texto condicional | No | Visible si real supera normal; revisar cifras sin justificación obligatoria. |
| Nota del cambio (opcional) | Texto condicional | No | Visible al modificar una versión existente. Usuario, fecha y versiones se registran automáticamente. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Qué falta para confirmar | Validación de las dos cantidades | Identifica ausencia o valor inválido. Permite guardar un borrador incompleto. |
| Capacidad de calendario | Días × turnos × horas × recursos | Base antes de paradas. No se vuelve a introducir. |
| Capacidad disponible | Calendario − paradas totales | Base después de paradas. |
| Capacidad normal propuesta | Disponible × factor, o media de períodos seleccionados | Resultado con su precisión completa, separado de la confirmación. |
| Origen y tipo de dato | Evidencia guardada / método | Manual no equivale a respaldado por registros. |
| Historial | Versiones inmutables | Consulta cambios y notas; no altera períodos anteriores. |

## 4. Casos de uso, validaciones y errores frecuentes
- Ejemplo: 22 días × 1 turno × 8 h × 3 recursos = 528 h. Paradas de 1 h por turno y recurso = 66 h. Disponible 462 h; al 90 % normal = 415,8 h. El campo y la propuesta conservan esa precisión.
- Guardar borrador admite actividad desconocida. Confirmar requiere ambas cantidades válidas, pero ninguna nota obligatoria.
- No se inventan calendarios, festivos, turnos ni registros reales. Si falta información fiable, el sistema deja el dato pendiente.
- Cambiar parámetros recalcula la propuesta. Para reemplazar una cifra existente, seleccionar Usar esta propuesta al guardar. Editar el campo numérico principal vuelve a entrada manual.
- Las paradas no pueden superar el calendario, ni los turnos superar 24 horas al día. Las cantidades no pueden ser negativas.
- Cero normal permite una actividad excepcional; no se calcula un porcentaje dividiendo por cero. Real superior a normal no se recorta.
- Una versión cerrada no admite cambios. Los conflictos de versión deben resolverse recargando; no se sobrescribe trabajo concurrente.

## Recorrido recomendado
1. Revisa centro, período y las dos cantidades; completa únicamente lo pendiente.
2. Si necesitas una propuesta, abre Revisar cálculo y revisa parámetros o períodos. Selecciona su uso antes de guardar.
3. Guarda un borrador o confirma los datos. Consulta Origen, notas e historial solo si necesitas más detalle.

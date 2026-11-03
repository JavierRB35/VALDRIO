# Ayuda contextual: Informe de servicios auxiliares
**ID Vista UI:** `View_AuxiliaryServices`  
**Última actualización:** `2026-11-03`

## 1. Objetivo y función financiera
Consultar los servicios auxiliares dentro de **Resultados y trazabilidad → Servicios auxiliares**. Ya no es una pantalla independiente. El informe no permite introducir servicios, guardar, calcular ni reabrir repartos. Conserva el tratamiento económico guardado: ThroughFlow transmite costes sin evaluar capacidad propia; OwnerOnceCapacity conserva la evaluación de su propietario una sola vez.

## 2. Guía de campos y entradas (Inputs)
| Dato | Origen | Uso |
|---|---|---|
| Auxiliar y unidad | Modelo publicado | Identifica la magnitud física; no todas las unidades son horas. |
| Normal y real | Actividad confirmada | Consulta actual; en OwnerOnceCapacity se muestra la evidencia fijada en el cierre. |
| Distribución | Clave con unidad física compatible o servicios documentados | No pide repetir cantidades. |
| Histórico | Ejecución seleccionada en el desplegable secundario | Conserva los resultados económicos originales. |

## 3. Interpretación de resultados (Outputs)
| Resultado | Interpretación |
|---|---|
| Distribuida | Actividad real multiplicada por el peso relativo de cada destino, cuando la base física está identificada; en evaluación de propietario se conserva el servicio documentado. |
| Pendiente | Real menos distribuida. Una cantidad desconocida se muestra como «—», no como cero. |
| Capacidad no utilizada | Máximo de normal menos real y cero. No equivale a actividad realizada pendiente de asignación. |
| Destinos y explicación | Desglose físico y motivo por el que una magnitud no puede deducirse. |

## 4. Casos de uso, validaciones y errores frecuentes
- Un porcentaje sin significado físico acreditado no se transforma en horas, aunque el auxiliar utilice una unidad de tiempo. Las claves porcentuales históricas no guardan la naturaleza del inductor: en ese caso se informa de la limitación.
- Varios grupos económicos con la misma distribución física se muestran una vez. Si sus cantidades difieren, no se suman ni se elige un grupo arbitrariamente.
- El informe no cambia clasificaciones, políticas, costes ni datos físicos. No obliga a reabrir un cierre.
- Los costes y su conciliación están en las demás pestañas de Resultados. La circulación recíproca bruta no se suma como coste final.

## Recorrido recomendado

1. Finaliza correctamente el reparto secundario; el resultado se guarda automáticamente.
2. Abre Resultados y trazabilidad del reparto y selecciona el informe Servicios auxiliares.
3. Compara actividad normal, real, distribuida y pendiente, y consulta la capacidad no utilizada.
4. Revisa las explicaciones si falta una base física compatible. El informe no exige introducir servicios ni reabrir repartos.

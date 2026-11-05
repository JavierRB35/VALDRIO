# Ayuda contextual: Reparto secundario y cierre
**ID Vista UI:** `View_SecondaryAllocation`  
**Última actualización:** `2026-10-28`
**Estado:** guía revisada sobre `2599188` y la auditoría contextual de 28/10/2026. Las evidencias de validación se registran por separado.

## 1. Objetivo y función financiera
Redistribuir costes de nodos auxiliares hacia nodos principales, incluidos servicios recíprocos, y cerrar el reparto sin duplicar circulación interna. No es un cierre integral del período.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Auxiliar / grupo de origen | Selector y tabla | Sí | Identifica coste a redistribuir y reglas aplicables. |
| Clave secundaria publicada | Selector | Sí, según asignación | Define las participaciones de destinos del auxiliar. |
| Asignación por nodo/grupo | Selectores | Según modelo | Permite resolver ausencia de clave en auxiliares o grupos. |
| Reparación de asignaciones pendientes | Acción | Según diagnóstico | Completa claves faltantes sin ocultar grupos obsoletos. |
| Calcular/cancelar | Acciones | Tras primario cerrado | Resuelve prestaciones recíprocas con validación de viabilidad. |
| Cerrar/reabrir secundario | Acciones | Según estado | Fija el resultado o permite cambios posteriores auditados. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Viabilidad recíproca | Diagnósticos por grupo | Detecta ciclos sin salida o configuraciones imposibles. |
| Liquidación auxiliar | Coste inicial, recibido, distribuido y residuo | Confirma salida de costes hacia destinos terminales. |
| Coste final de principal | Primario + contribuciones netas secundarias | Base final por nodo principal. |
| Origen y flujo por principal | Contribuciones y grafo | Permite rastrear procedencias sin volver a sumar tránsito bruto. |
| Circulación bruta | Flujos entre auxiliares | Magnitud de diagnóstico no aditiva al coste final. |

## 4. Casos de uso, validaciones y errores frecuentes
- No ejecutar secundario sin primario cerrado y criterios completos.
- Un ciclo recíproco sin salida terminal viable debe corregirse antes del cálculo.
- Circulación bruta de auxiliares no es un coste adicional de empresa.
- El solver recíproco disponible no implica selector de método directo, escalonado, ABC ni TDABC.
- La capacidad auxiliar evaluada por T16 requiere datos y tratamiento explícitos, nunca imputación implícita.

- Al asignar claves a nodos auxiliares o importes absolutos, Tab y Mayús+Tab recorren los controles editables de las filas y omiten datos de solo lectura. Los errores de asignación se distinguen en rojo y no se sustituyen los diálogos existentes.

### Qué resuelve la reciprocidad
Un auxiliar puede recibir servicios de otro auxiliar y devolverle servicios. VALDRIO resuelve esas relaciones conjuntamente: el importe que redistribuye cada auxiliar incluye su coste de partida y lo recibido. No hay que repetir repartos manuales ni sumar de nuevo los flujos internos al coste final.

Por ejemplo, un auxiliar con 100 de coste inicial que se presta a sí mismo el 20 % y entrega el 80 % a un principal redistribuye 125 en bruto: 25 vuelven al auxiliar y 100 llegan al principal. El coste final sigue siendo 100. Si se asigna el 100 % a sí mismo sin salida, el circuito no es viable. Toda cadena auxiliar debe disponer de salida hacia principales; la validación también comprueba estabilidad numérica.

Cuando se evalúa capacidad auxiliar, parte del coste puede quedar como variación de capacidad. Revisa esa separación en la conciliación antes de comparar coste inicial y coste final de los principales.

### Referencias al código
- [`src/Valdrio.Desktop/SecondaryWorkspacePanel.xaml`](../../src/Valdrio.Desktop/SecondaryWorkspacePanel.xaml)
- [`src/Valdrio.Desktop/AllocationWorkspacePanel.xaml`](../../src/Valdrio.Desktop/AllocationWorkspacePanel.xaml)
- [`src/Valdrio.Desktop/AllocationWorkspaceViewModel.Secondary.cs`](../../src/Valdrio.Desktop/AllocationWorkspaceViewModel.Secondary.cs)
- [`src/Valdrio.Application/SecondaryAllocation.cs`](../../src/Valdrio.Application/SecondaryAllocation.cs)

### Gráfico de Pareto
El gráfico ordena los nodos principales con su coste final, sin volver a sumar la circulación entre auxiliares de mayor a menor coste. Cada barra muestra el coste agregado del nodo; la línea representa el porcentaje acumulado sobre la suma de costes positivos. Puedes consultar la tabla de valores y desplazar horizontalmente el gráfico. Los abonos se muestran con su signo y quedan fuera del acumulado. Positivos más abonos concilian con el neto; si el neto es cero o negativo, no se calcula porcentaje. Sin resultados se indica que todavía no hay datos.

## Recorrido recomendado
1. Comprueba los nodos auxiliares con coste y define las claves que transfieren servicios a los nodos principales; la autoprestación está permitida.
2. Guarda destinos y porcentajes, ejecuta el cálculo y revisa la viabilidad de las rutas, incluidos posibles circuitos sin salida.
3. Contrasta el detalle de aportaciones, totales por principal y Pareto; resuelve los diagnósticos antes de cerrar el reparto secundario.

Las tablas crecen con sus filas: utiliza el desplazamiento exterior de la página. Guardar claves y las acciones de cierre permanecen en la barra inferior; cambian con la pestaña activa. El desplazamiento horizontal permite consultar las columnas que no caben.

## Ampliación de presentación por finalidad — candidatura 2026-11-04

Los resultados terminales por nodo y las contribuciones efectivas se muestran en pestañas/tablas separadas para **Centros generales/indirectos** y **Factores directos** (MOD y aprovisionamiento). La separación es exclusivamente visual: los grupos directos siguen recibiendo prestaciones auxiliares según las claves; el cálculo recíproco y sus flujos no se duplican. Los **totales conciliados** abarcan ambas categorías; el Pareto sigue la categoría seleccionada.

**Estado:** implementación candidata pendiente de validación en Windows.

## Orden de las pestañas

El reparto secundario mantiene el mismo orden: **Claves de reparto → Asignar cuentas / nodos → Comprobar y cerrar**. Define o selecciona primero las claves de prestación auxiliar, asígnalas después a nodos/grupos y comprueba la viabilidad antes de cerrar. Las autoprestaciones siguen sujetas a la validación matemática existente.

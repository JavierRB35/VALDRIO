# Ayuda contextual: Reparto secundario y cierre
**ID Vista UI:** `View_SecondaryAllocation`  
**Última actualización:** `2026-10-21`  
**Estado:** borrador documental contrastado con código de `main@211af0d`; pendiente de prueba interactiva Windows.

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

### Referencias al código
- [`src/Valdrio.Desktop/SecondaryWorkspacePanel.xaml`](../../src/Valdrio.Desktop/SecondaryWorkspacePanel.xaml)
- [`src/Valdrio.Desktop/AllocationWorkspacePanel.xaml`](../../src/Valdrio.Desktop/AllocationWorkspacePanel.xaml)
- [`src/Valdrio.Desktop/AllocationWorkspaceViewModel.Secondary.cs`](../../src/Valdrio.Desktop/AllocationWorkspaceViewModel.Secondary.cs)
- [`src/Valdrio.Application/SecondaryAllocation.cs`](../../src/Valdrio.Application/SecondaryAllocation.cs)

# Ayuda contextual: Período y análisis
**ID Vista UI:** `View_Study`  
**Última actualización:** `2026-10-21`  
**Estado:** borrador documental contrastado con código de `main@211af0d`; pendiente de prueba interactiva Windows.

## 1. Objetivo y función financiera
Crear o seleccionar un análisis ligado a una estructura publicada y a un período. Delimita la moneda, fechas y escenario que controlarán importaciones, recursos, actividad y cálculos.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Análisis existente | Selector | No | Recupera un estudio guardado, sus importaciones y estado. |
| Nombre | Texto | Sí | Identificador legible del estudio. |
| Modelo publicado | Selector | Sí | Fija la versión estructural para el análisis; no es un borrador. |
| Inicio y fin del período | Fechas | Sí | Delimitan los saldos y la actividad a comparar; inicio no posterior al fin. |
| Moneda | Código de moneda | Sí | Moneda funcional del estudio y de los resultados de costes. |
| Escenario | Selector | Según estudio | Contexto del análisis; no implica automáticamente una simulación económica. |
| Nuevo, crear, guardar, actualizar | Acciones | Según estado | Distingue alta, modificación y recarga de contexto. |
| Captura/instantánea | Acción | Opcional | Guarda una referencia consultable para trazabilidad. |
| Cerrar/reabrir estudio | Acción avanzada | Según estado | Modifica posibilidades de edición; no equivale al cierre integral del período financiero. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Análisis activo | Nombre, período, escenario, modelo | Determina el contexto de todos los módulos siguientes. |
| Histórico e instantáneas | Capturas y estados | Permite revisar versiones e importaciones anteriores. |
| Avisos de preparación | Estado y requisitos | Indica si puede continuarse a importación u otras fases. |

## 4. Casos de uso, validaciones y errores frecuentes
- Se necesita una estructura publicada antes de crear un análisis utilizable.
- Las fechas deben ser coherentes y el análisis debe guardarse para importar.
- Un análisis cerrado es de consulta hasta su reapertura explícita.
- Cambiar períodos no traslada automáticamente importaciones ni horas físicas de otro período.

### Referencias al código
- [`src/Valdrio.Desktop/StudyWorkspacePanel.xaml`](../../src/Valdrio.Desktop/StudyWorkspacePanel.xaml)
- [`src/Valdrio.Desktop/StudyWorkspaceViewModel.cs`](../../src/Valdrio.Desktop/StudyWorkspaceViewModel.cs)

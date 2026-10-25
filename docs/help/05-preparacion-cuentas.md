# Ayuda contextual: Preparar cuentas
**ID Vista UI:** `View_ResourcePreparation`  
**Última actualización:** `2026-10-25`
**Estado:** guía funcional mantenida para `main@6885eca` (25/10/2026). Describe el código de esta pantalla; las pruebas visuales interactivas se registran por separado y solo aplican al SHA indicado en cada evidencia.

## 1. Objetivo y función financiera
Transformar recursos importados en costes analíticos clasificables y repartibles, con correcciones, ajustes extracontables reutilizables, exclusiones y destinos. Permite explicar la conciliación contable-analítica antes de repartir.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Cuenta, descripción y origen | Tabla (texto) | Lectura | Identifican registro/procedencia; el código conserva ceros iniciales. |
| Importe importado/convertido | Moneda del estudio | Lectura | Base original para la reconciliación. |
| Corrección manual | Importe firmado | Opcional | Ajusta de forma explícita la base económica; se edita en la tabla. |
| Ajuste por reglas | Moneda | Según reglas aplicadas | Impacto separado de fichas extracontables; no se confunde con corrección manual. |
| Ficha: nombre, cuenta o prefijo | Texto | Según regla | Selecciona cuenta(s) a compensar; cuenta sin origen permite coste supletorio. |
| Ficha: código analítico | Texto | Sí, si crea línea | Identifica nuevo recurso analítico sin inventar una importación contable. |
| Ficha: modalidad y valor | Fijo, porcentaje o suma firmada | Sí | Calcula importe objetivo; no admite programación automática ni fórmulas arbitrarias. |
| Ficha: motivo | Texto | Sí | Sustenta trazabilidad económica. |
| Partes y sus importes | Moneda | Según recurso | Distribuyen base efectiva entre destinos o exclusiones. |
| Ruta/destino | Selector | Por parte | Selecciona pool, objeto directo, pendiente o exclusión conforme a la clasificación. |
| Objeto: código, nombre, tipo | Texto/selector | Si destino directo | Identifica destino al margen del reparto por pools. |
| Pool, conducta, capa | Selectores | Según destino | Clasifican coste por destino y naturaleza fija/variable. |
| Unidad, horizonte y magnitudes físicas avanzadas | Cantidad/fecha | Según clasificación | Metadatos avanzados de recurso: no equivalen por sí mismos a una observación física confirmada en Actividad y capacidades. |
| Revaluación: tipo, fecha, fuente, motivo | Tipo de cambio/fecha/texto | Solo revaluación | Controla conversión/revisión monetaria con trazabilidad. |
| Guardar/confirmar/aplicar/deshacer | Acciones | Según operación | Una regla guardada no se aplica sola y una vista previa no altera el análisis. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Importe efectivo por cuenta | Original ± ajustes/correcciones | Base que alimenta clasificación y reparto. |
| Totales, pendiente y diferencia | Sumas de recursos y partes | Detectan importes sin destino o descuadres. |
| Vista previa de regla | Cuentas afectadas y coste de reemplazo/suplencia | Permite aprobar impacto antes de aplicar. |
| Histórico de cambios | Versiones y motivos | Audita revaloración, corrección, aplicación y reversión. |

## 4. Casos de uso, validaciones y errores frecuentes
- Corregir una cuenta vinculada a regla activa puede exigir confirmación económica adicional.
- Dos fichas activas no pueden compensar la misma cuenta en conflicto.
- Importes efectivos sin clasificar o distribuir deben resolverse antes del cierre.
- Deshacer reglas después de cálculo requiere respetar estados de cierre/reapertura.
- No interpretar magnitudes teóricas heredadas de recursos como tarifas calculadas.

- Los diagnósticos de preparación de cuentas se distinguen de los avisos informativos mediante texto rojo. Los cuadros de confirmación económica no cambian. Los campos de solo lectura se omiten al tabular, sin perder su contenido.

### Referencias al código
- [`src/Valdrio.Desktop/ResourceWorkspacePanel.xaml`](../../src/Valdrio.Desktop/ResourceWorkspacePanel.xaml)
- [`src/Valdrio.Desktop/ResourceWorkspaceViewModel.cs`](../../src/Valdrio.Desktop/ResourceWorkspaceViewModel.cs)
- [`src/Valdrio.Desktop/ResourceWorkspaceViewModel.Rules.cs`](../../src/Valdrio.Desktop/ResourceWorkspaceViewModel.Rules.cs)
- [`src/Valdrio.Application/ReusableAdjustments.cs`](../../src/Valdrio.Application/ReusableAdjustments.cs)

# Ayuda contextual: Reutilizar metodología de reparto
**ID Vista UI:** `View_AllocationReuse`  
**Última actualización:** `2026-10-25`
**Estado:** guía funcional mantenida para `main@6885eca` (25/10/2026). Describe el código de esta pantalla; las pruebas visuales interactivas se registran por separado y solo aplican al SHA indicado en cada evidencia.

## 1. Objetivo y función financiera
Comparar y adoptar claves/metodologías compatibles de otro análisis para no reconstruir criterios desde cero. Reutiliza reglas seleccionadas, no saldos, actividad, cierres ni cálculos históricos.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Análisis de origen | Selector | Sí | Indica de qué estudio se revisará la metodología. |
| Claves candidatas | Tabla con selección y estado | Sí, según adopción | Selecciona reglas compatibles y detecta versiones/conflictos. |
| Reemplazar conflictos | Booleano | No | Controla sustituciones explícitas de definiciones con colisión. |
| Previsualizar y adoptar | Acciones | Sí para aplicar | Primero se revisa el impacto y después se adoptan claves seleccionadas. |
| Cancelar | Acción | Opcional | Abandona sin modificar destino. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Lista de claves y estados | Comparación de origen y destino | Permite decidir qué criterios recuperar. |
| Vista previa de adopción | Validaciones y conflictos | Muestra dependencias que requieren intervención. |
| Metodología adoptada | Claves/reglas en estudio destino | Debe revisarse su aplicabilidad antes de ejecutar reparto. |

## 4. Casos de uso, validaciones y errores frecuentes
- Adoptar metodología no copia balances, horas, repartos cerrados ni ejecuciones.
- La compatibilidad de estructura/versiones debe comprobarse antes de aceptar.
- Reemplazar conflictos es una decisión explícita, no automática.
- Cancelar no debe alterar definiciones existentes.

### Referencias al código
- [`src/Valdrio.Desktop/AllocationReuseWindow.xaml`](../../src/Valdrio.Desktop/AllocationReuseWindow.xaml)
- [`src/Valdrio.Desktop/AllocationReuseEditor.cs`](../../src/Valdrio.Desktop/AllocationReuseEditor.cs)
- [`src/Valdrio.Application/AllocationReuse.cs`](../../src/Valdrio.Application/AllocationReuse.cs)

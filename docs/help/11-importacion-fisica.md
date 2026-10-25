# Ayuda contextual: Importación de actividad física
**ID Vista UI:** `View_PhysicalImport`  
**Última actualización:** `2026-10-25`
**Estado:** guía funcional mantenida para `main@6885eca` (25/10/2026). Describe el código de esta pantalla; las pruebas visuales interactivas se registran por separado y solo aplican al SHA indicado en cada evidencia.

## 1. Objetivo y función financiera
Preparar y aplicar observaciones físicas de capacidad normal y actividad real desde CSV/XLSX hacia los nodos del estudio activo. Separa lectura/vista previa de aplicación y conserva la validación final de cada observación.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Ruta de archivo | Ruta CSV/XLSX | Sí | Identifica fuente de observaciones. |
| Hoja | Selector | Si XLSX | Determina registros de origen. |
| Separador | Selector | Si CSV | Permite leer la estructura original. |
| Seleccionar reemplazo | Casilla por fila | Sí para aplicar | Indica qué filas sobrescribirán el borrador físico. |
| Nodo y unidad | Datos leídos | Lectura | Se usan para comprobar correspondencia con el modelo. |
| Normal y real | Cantidades por período | Según fuente | Observaciones importadas, no tarifas. |
| Motivo de corrección | Texto | Según reemplazo | Explica cambios sobre datos existentes. |
| Motivo de normal cero | Texto | Si aplica | Justifica la magnitud cero informada. |
| Motivo excepcional | Texto | Si aplica | Documenta sobreactividad u otros casos particulares. |
| Vista previa y aplicar | Acciones | Según validaciones | Primero se detectan conflictos; la acción de aplicar escribe selecciones válidas. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Vista previa de registros | Nodo, unidad, normal, real, existentes y alerta | Permite comparar origen con datos guardados. |
| Diagnósticos de importación | Compatibilidad de filas y reglas | Señalan problemas antes de escribir. |
| Observaciones importadas | Filas aplicadas | Quedan sujetas a estado/versionado y comprobación en Actividad y capacidades. |

## 4. Casos de uso, validaciones y errores frecuentes
- El archivo de actividad no sustituye la importación contable de saldos.
- Revisar unidad y período: horas de otro estudio no se copian automáticamente.
- No aplicar registros sin correspondencia con nodo/pool vigente.
- Vista previa no significa guardado ni confirmación de todas las observaciones.
- Anular/reemplazar exige respetar historial y estados protegidos.

### Referencias al código
- [`src/Valdrio.Desktop/PhysicalImportWindow.xaml`](../../src/Valdrio.Desktop/PhysicalImportWindow.xaml)
- [`src/Valdrio.Desktop/PhysicalImportEditor.cs`](../../src/Valdrio.Desktop/PhysicalImportEditor.cs)
- [`src/Valdrio.Application/PhysicalImport.cs`](../../src/Valdrio.Application/PhysicalImport.cs)

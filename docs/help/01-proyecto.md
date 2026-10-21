# Ayuda contextual: Proyecto
**ID Vista UI:** `View_Project`  
**Última actualización:** `2026-10-21`  
**Estado:** borrador documental contrastado con código de `main@211af0d`; pendiente de prueba interactiva Windows.

## 1. Objetivo y función financiera
Crear, abrir y proteger el archivo de empresa `.valdrio` que conserva el trabajo analítico. Permite elegir el proyecto y sus unidades antes de definir estructuras, períodos y repartos. No calcula costes por sí solo.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Nombre del proyecto | Texto | Sí, al crear | Identifica la empresa/proyecto; no es un importe contable. |
| Moneda del proyecto | Código ISO de moneda | Sí, al crear | Establece el contexto monetario del archivo; no convierte saldos importados. |
| Archivo de proyecto | Ruta `.valdrio` | Según operación | Abrir o crear en una ubicación local; las acciones de recuperación trabajan con archivos separados. |
| Unidad de actividad: código | Código de texto | Sí, al crear una unidad | Identificador estable para inductores y observaciones físicas. |
| Dimensión de unidad | Selector | Sí, al crear una unidad | Determina la familia física de magnitudes compatibles. |
| Símbolo de unidad | Texto | Según unidad | Aparece en tablas de actividad, capacidad y tarifas. |
| Escala de unidad | Selector | Según unidad | Representación de la unidad para cantidades; verificar equivalencia antes de comparar. |
| Unidad discreta | Booleano | Según unidad | Identifica cantidades que se tratan como elementos enteros. |
| Acciones abrir/cerrar/copia/restaurar/actualizar | Botones | Según operación | Gestionan el archivo. La actualización de formatos antiguos se realiza sobre copia, no sobre el original. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Proyecto activo y estado | Archivo abierto y metadatos | Confirma dónde se guardará el análisis; no indica cierre económico. |
| Catálogo de unidades | Unidades existentes y creadas | Permite seleccionar unidades coherentes para pools y actividad. |
| Diagnósticos de archivo | Validaciones de lectura/escritura | Orientan recuperación o actualización sin alterar el original. |

## 4. Casos de uso, validaciones y errores frecuentes
- No se puede crear un archivo sin nombre o sin una ruta válida.
- No sobrescribir originales al actualizar proyectos antiguos; utilizar la copia propuesta.
- La carga de un ejemplo abre una copia de trabajo: no modificar los ejemplos de referencia.
- Una unidad física incorrecta puede hacer incomparables las cantidades que se registren después.

### Referencias al código
- [`src/Valdrio.Desktop/CompanyFilePanel.xaml`](../../src/Valdrio.Desktop/CompanyFilePanel.xaml)
- [`src/Valdrio.Desktop/CompanyFileViewModel.cs`](../../src/Valdrio.Desktop/CompanyFileViewModel.cs)

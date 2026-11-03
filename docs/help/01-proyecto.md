# Ayuda contextual: Proyecto
**ID Vista UI:** `View_Project`  
**Última actualización:** `2026-10-28`
**Estado:** guía revisada sobre `2599188` y la auditoría contextual de 28/10/2026. Las evidencias de validación se registran por separado.

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
| Escala de unidad | Selector | Según unidad | Número de decimales admitidos (0–12); no es un factor de conversión entre unidades. |
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

- Los errores al crear, abrir o recuperar un proyecto se destacan en rojo junto al estado de la operación; los mensajes informativos no se presentan como errores. Los diálogos de confirmación existentes se mantienen.

### Referencias al código
- [`src/Valdrio.Desktop/CompanyFilePanel.xaml`](../../src/Valdrio.Desktop/CompanyFilePanel.xaml)
- [`src/Valdrio.Desktop/CompanyFileViewModel.cs`](../../src/Valdrio.Desktop/CompanyFileViewModel.cs)

### Órdenes estimadas y formato de archivo
Las órdenes estimadas aceptadas se guardan dentro de la empresa en formato 13. Al abrir un archivo anterior, actualiza una copia: el original se conserva. Las copias de seguridad incluyen el historial congelado.

## Recorrido recomendado
1. Crea un proyecto o abre una copia de trabajo de un archivo existente; comprueba que el nombre y la ubicación son los previstos.
2. Elige la moneda y define las unidades físicas que necesitarán los centros; revisa dimensión, escala y si cada unidad es discreta.
3. Confirma que el proyecto está abierto y guardado antes de pasar a Diseñar estructura; si el formato es antiguo, actualiza una copia conservando el original.

### Duplicar proyecto
**Duplicar proyecto…** permite elegir otro nombre y ubicación para una hipótesis independiente. No sobrescribe el origen ni destinos existentes. Copia consistentemente los datos guardados y su historial; después ofrece abrir la copia. Guarda primero los cambios pendientes.

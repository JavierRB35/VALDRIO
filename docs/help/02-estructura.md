# Ayuda contextual: Diseñar estructura
**ID Vista UI:** `View_Structure`  
**Última actualización:** `2026-10-28`
**Estado:** guía revisada sobre `2599188` y la auditoría contextual de 28/10/2026. Las evidencias de validación se registran por separado.

## 1. Objetivo y función financiera
Definir y versionar centros, nodos de coste, pools y unidades. Esta estructura publicada gobierna los destinos válidos de los repartos y la lectura de costes por centro. Editar un borrador no altera silenciosamente una versión publicada.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Versión de estructura | Selector | Según operación | Selecciona borrador o versión publicada para consultar, clonar o continuar. |
| Descripción | Texto | Según operación | Explica finalidad y alcance de la versión. |
| Código y nombre de centro | Texto | Sí, en centros | Identifican la agrupación física/organizativa; pueden activarse o desactivarse. |
| Unidad de actividad | Catálogo | Según nodo/pool | Vincula el inductor físico que se usará para la actividad del pool. |
| Centro físico | Selector | Sí, al configurar nodo | Asocia el nodo analítico al centro correspondiente. |
| Código de nodo | Texto | Sí, en nodos | Identificador del destino de coste dentro de la versión. |
| Rol del nodo | Selector | Sí, en nodos | Distingue principales y auxiliares y afecta reparto secundario. |
| Pool / descripción | Texto | Según nodo | Distingue el agrupador de costes/actividad para el modelo. |
| Etiquetas funcionales | Texto/selección | No | Metadatos explicativos; no sustituyen al rol ni a la unidad. |
| Guardar / validar / publicar | Acciones | Según estado | Guardar conserva borrador; validar diagnostica; publicar fija la versión para uso en análisis. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Árbol de estructura | Centros, nodos, roles y pools | Revisa la topología antes de usarla en un análisis. |
| Estado y validación | Comprobaciones de modelo | Informa si existen requisitos pendientes para publicar. |
| Versión publicada | Estructura persistida | Base de referencia para análisis y repartos posteriores. |

## 4. Casos de uso, validaciones y errores frecuentes
- Si no hay centro, nodo principal o unidad válida, completar el borrador antes de publicar.
- No tratar la modificación de una versión publicada como edición directa: generar una versión o clon cuando corresponda.
- Un rol auxiliar mal asignado cambia las exigencias del reparto secundario.
- Antes de abandonar la vista, resolver los cambios pendientes.

### Centro, nodo y función en el reparto
El centro identifica el área física u organizativa. El nodo es el destino analítico de sus costes; un centro puede contener varios nodos. El pool agrupa los costes y la unidad de actividad de ese nodo.

Elige **Principal** para un destino final del reparto secundario y **Auxiliar** para un nodo cuyos costes se redistribuyen por servicios a otros nodos. El rol no decide si un coste es fijo o variable. La unidad debe medir la actividad de forma comparable (por ejemplo, horas); elegirla no registra cantidades ni crea una clave de reparto.

### Referencias al código
- [`src/Valdrio.Desktop/ModelWorkspacePanel.xaml`](../../src/Valdrio.Desktop/ModelWorkspacePanel.xaml)
- [`src/Valdrio.Desktop/ModelWorkspaceViewModel.cs`](../../src/Valdrio.Desktop/ModelWorkspaceViewModel.cs)

### Eliminar un centro
Solo se elimina tras confirmación si no tiene nodos ni referencias históricas. Si está utilizado, desactívalo para conservar sus vínculos.

## Recorrido recomendado
1. Crea los centros y nodos necesarios y asigna los pools y unidades físicas compatibles.
2. Revisa códigos, descripciones y relaciones en la estructura en borrador antes de publicar.
3. Publica la versión cuando sea consistente; selecciónala desde el análisis para que los repartos utilicen destinos válidos.

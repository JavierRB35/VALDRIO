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

## Mapa opcional del proceso (COR-05)

En **4. Revisar y finalizar**, el mapa agrupa nodos por función económica y papel. Para trabajar con él, abre una **versión editable**; las versiones finalizadas se consultan en modo de solo lectura.

1. Selecciona una tarjeta en el diagrama **o elige el nodo de origen en el panel izquierdo**.
2. En **Nodos siguientes** (si es principal) o **Nodos atendidos** (si es auxiliar), marca los destinos. Los existentes aparecen ya marcados. Al marcar o desmarcar se crean o eliminan **flechas inmediatamente** en el borrador.
3. Un **principal** solo se conecta con **otros principales** mediante secuencias operativas; no se admiten autoprestaciones ni ciclos de ruta entre principales. Un **auxiliar** puede prestar servicios a principales, a otros auxiliares o **a sí mismo**, independientemente de la función económica. Las opciones incompatibles aparecen deshabilitadas con explicación.
4. Usa **Validar y guardar mapa** para conservar una versión utilizable como referencia. Sin validar, los cálculos habituales de VALDRIO siguen funcionando y no se publican claves por el hecho de trazar una flecha.

En **Reparto secundario**, las prestaciones auxiliares validadas pueden convertirse en propuestas de **destinos de una clave**; el usuario configura los pesos y acepta o descarta el borrador por separado. Una autoprestación auxiliar del 100 % sin destino económico externo podría hacer irresoluble el reparto recíproco. Las secuencias entre principales **nunca** generan repartos económicos.

Zoom, arrastre, autoordenación y exportación siguen disponibles; la conexión antigua por dos clics queda en **Controles avanzados**.

# T12 — Revisión crítica y mejora de presentación

Fecha: 15 de octubre de 2026. Base local: `85ef4cf`, rama de entrega `fix/t12-presentation-20261015`.

## Dictamen

T12 ofrece una ejecución histórica consultable y una explicación técnica del reparto, pero su pantalla original no constituye por sí sola un informe final de gestión suficientemente claro. La información económica existe en los resultados congelados; faltaba una vista que relacionara las etapas por centro. Mejorar colores sin resolver esa lectura habría sido insuficiente.

La ficha normativa [T12](../../specification/v0.4/tareas/T12.md) exige historia fiable, publicación atómica y actualidad de la revisión. No define un informe final de gestión completo. La documentación [de implementación](../tareas/T12.md) añade gráficos y conciliación. Cumplir esos requisitos técnicos no demuestra que el analista pueda explicar el coste acumulado de cada centro. Tampoco debe confundirse el cierre de reparto entre centros con tarifas, absorción u objetos, que corresponden a fases posteriores como [T15](../../specification/v0.4/tareas/T15.md).

## Alcance y evidencia

- Inspección del código real, pruebas locales y capturas WPF; no solamente de una maqueta.
- Se preserva el trabajo paralelo de T14 en su carpeta y rama. La nueva copia parte de su último commit local disponible. No se pudo contrastar el remoto por falta de resolución de GitHub en la terminal; no se afirma que sea el último estado remoto.
- Se han leído las directrices de usabilidad, la guía visual V1 y la imagen T08 aprobada. No se modificó `sources/`.
- Se conservan los motores T09–T11, publicación, persistencia, moneda y precisión del ledger.
- El usuario autorizó posteriormente aplicar mejoras en código. No se fusionó, publicó ni ejecutó GitHub Actions.

## Diagnóstico priorizado

| Prioridad | Hallazgo comprobado | Consecuencia | Resolución |
|---|---|---|---|
| P1 | La vista original empieza por operaciones e históricos, seguida de dos diagramas; no expone el puente primario–secundario–final por centro. | El usuario debe reconstruir el saldo acumulado entre pantallas y nodos. | Implementado resumen por centro físico y desglose por nodo con primario, secundario neto y final. |
| P1 | La tabla de detalle mezcla hechos primarios, transferencias secundarias brutas y contribuciones finales. Son magnitudes diferentes. | Sumar las filas puede duplicar costes aunque la conciliación interna sea correcta. | El nuevo resumen usa exclusivamente saldos publicados. Se conserva la tabla anterior y sus etapas; sigue pendiente separar sus perspectivas mediante filtros claros. |
| P1 | El resaltado del grafo representa conectividad, no una atribución de cada euro por cuenta a cada salto recíproco. | Puede interpretarse como una trazabilidad económica más fina de la que el contrato proporciona. | Se conserva la distinción bruto/final. La propuesta es navegar por contribución final y vínculo de explicación, sin inventar reparto por cuenta de los saltos brutos. |
| P1 | La aceptación original enfatiza persistencia y controles WPF, pero no exige lectura del coste acumulado por centro. | Una pantalla puede aprobar técnicamente y seguir siendo insuficiente para controlling. | Nueva prueba numérica del puente y comprobaciones WPF del resumen. Criterios de informe ampliados al final. |
| P2 | Etiquetas y curvas del grafo original compiten por espacio; el secundario fuerza una lectura vertical prolongada. | Cuesta comparar el punto de partida con el destino final. | Diagramas horizontales por etapas en ventanas amplias; adaptación vertical en ventanas estrechas; importes de conexiones seleccionables y opción para mostrar todas las etiquetas. |
| P2 | No hay en esta pantalla un paquete de informe exportable, comparación entre ejecuciones ni exposición completa de parámetros de reproducción al usuario. | No puede entregarse autónomamente como informe final auditable. | Pendiente; propuesta concreta abajo. No se añade una exportación ficticia. |
| P2 | La ficha T12 declara fusión y conserva debajo apartados históricos de aceptación pendiente; el ADR también conserva una cabecera anterior. | Estado documental ambiguo. | Esta entrega identifica base, alcance y pruebas propios; no reutiliza cifras de validaciones históricas. |

Evidencia de implementación: [vista](../../src/Valdrio.Desktop/CostFlowPanel.xaml), [presentación de resultados congelados](../../src/Valdrio.Desktop/CostFlowViewModel.cs), [diagramas](../../src/Valdrio.Desktop/CostFlowPanel.xaml.cs), [proyección de saldos](../../src/Valdrio.Reporting/RunCostBalanceProjector.cs), [pruebas de ejecuciones](../../tests/Valdrio.Tests/T12RunTests.cs).

## Qué se ha aplicado

Orden de lectura: ejecución elegida y estado → indicadores → coste por centro → desglose por nodo → detalle y conciliación → gráficos. Se reutilizan las tarjetas, colores, tablas y foco del tema aprobado. Los importes del resumen se ordenan numéricamente, incluyen moneda y se presentan con separadores regionales. Los importes exactos siguen en el detalle y conciliación; el resumen visual a dos decimales no modifica los hechos.

El puente se define como **primario + movimiento neto secundario = final**. El neto se obtiene de la diferencia de los saldos publicados, incorpora sus ajustes y NO representa una nueva ejecución ni la suma de transferencias brutas. El centro físico agrupa sus nodos. Solo se presenta un resumen final cuando la ejecución está completada y contiene ambos resultados; no se fabrican ceros finales para intentos fallidos o cancelados.

Ejemplo local de aceptación:

| Centro | Primario EUR | Secundario neto EUR | Final EUR |
|---|---:|---:|---:|
| Administración | 1.536 | -1.536 | 0 |
| Mantenimiento | 2.304 | -2.304 | 0 |
| Producción | 2.660 | 3.840 | 6.500 |
| Total | 6.500 | 0 | 6.500 |

Los 960 EUR de circulación interna recíproca del ejemplo **no se agregan** a 6.500 EUR. La conciliación global continúa siendo efectivo = directo a objetos + excluido + pendiente + final en principales. No se equipara coste efectivo con coste final si existen otras salidas.

Se mantienen calcular/guardar, cancelar, refrescar, historial, estado actual/histórico, diagnósticos, selección de nodos y transferencias, detalle, limpiar selección, conciliación y ES/EN. Las conexiones del gráfico incorporan foco y selección por Intro/Espacio; mostrar todas sus etiquetas es opcional.

## Propuesta para completar el informe final

1. **Cabecera auditable:** empresa, estudio, periodo, escenario, revisión, fecha de ejecución, moneda y política; versión de algoritmo y huellas en un anexo de auditoría copiable. Siempre datos del snapshot elegido.
2. **Resumen de centros:** conservar el puente implementado y añadir acceso directo desde cada centro a todos sus nodos, selección de comportamiento fijo/variable y subtotales sin duplicar niveles.
3. **Primario:** cuenta con descripción, importe efectivo, clasificación, clave/version, base, porcentaje y destino. Permitir explicar excluidos, directos y pendientes.
4. **Secundario:** dos perspectivas expresas: movimientos brutos entre auxiliares por grupo y contribución final absorbida por principales. Mostrar primario conservado, aportación auxiliar, ajuste y total por principal; no convertir los saltos del grafo en atribuciones por cuenta.
5. **Trazabilidad:** desde un importe final, mostrar recurso/parte → nodo primario → vínculo de contribución → principal final, claves congeladas, ajuste y referencia de origen. La evidencia navegable es el vínculo publicado; el grafo es apoyo.
6. **Conciliación y entrega:** matriz por recurso con diferencias, controles por nodo/grupo, exportación con mismas cifras y revisión, informe PDF legible y tabla de datos. Comparar ejecuciones solo cuando moneda, alcance y políticas permitan hacerlo, mostrando los cambios de contexto.
7. **Visuales adicionales:** barras del coste final por centro y puente de composición de un principal; negativos con eje cero. Para modelos grandes, filtros y búsqueda antes de un grafo masivo. No usar un Sankey que mezcle circulación bruta y costes finales como si fueran aditivos.

## Criterios de aceptación del informe completo

- Cada centro/nodo satisface primario + neto secundario = final con precisión del ledger; los totales físicos agregan nodos una sola vez.
- El ejemplo de 6.500 EUR y los ejemplos T13 mantienen sus cifras publicadas. La circulación recíproca no aumenta el coste final.
- Directos, excluidos, pendientes y finales concilian por recurso; diferencias de signos opuestos no se ocultan porque la suma global sea cero.
- Seleccionar historia no calcula, no usa configuración nueva y no cambia hashes ni importes; fallo/cancelación no muestra un cierre completo.
- Desde cada importe final puede identificarse su cuenta y descripción, reglas/versiones y ajustes. Se distingue expresamente conectividad visual de contribución económica.
- Ordenación numérica, negativos, centros con varios nodos, importes grandes, recursos sin descripción, nombres largos y modelo sin auxiliares disponen de casos de aceptación.
- ES/EN, ventana estrecha, teclado y escalado mantienen acceso a todas las acciones. La verificación por transformación de escala no sustituye pruebas de DPI físico en varios monitores.
- Las exportaciones futuras reproducen revisión, alcance y precisión, y no presentan borradores como resultados publicados.

La mejora visual implementada no constituye aceptación de todos estos criterios futuros ni cierre de las fases metodológicas posteriores.

## Resultado de las comprobaciones de esta entrega

Compilación Release: 0 errores/advertencias. T12/T13: 16/16. WPF T12: 66 comprobaciones. Suite global: 705/706, con un fallo ajeno a esta presentación en la precedencia de diagnósticos de T14. Consulte el [registro de evidencia](../evidencias/t12-presentacion-20261015/README.md) para alcance, capturas y limitaciones.

# VALDRIO — Acuerdos funcionales sobre factores directos, nodos y rentabilidad

**Fecha:** 2026-11-04  
**Estado:** decisiones del propietario aprobadas para evolución documental; **no implica implementación ni aceptación de código**.  
**Fuentes de referencia:** [hoja de ruta R0–R4](../../specification/v0.4/especificacion/01_Producto_y_Hoja_de_Ruta.md), [contratos CM01–CM16](../../specification/v0.4/especificacion/02_Contratos_Metodologicos.md), [decisiones iniciales](../../specification/v0.4/especificacion/03_Decisiones_y_Diccionario.md), [T04](../tareas/T04.md), [T15](../tareas/T15.md), [T17](../tareas/T17.md), [T18](../tareas/T18.md), [T19](../tareas/T19.md) y [ADR-0017](../adr/ADR-0017-ajustes-reutilizables.md).

> **Naturaleza de esta adenda.** Registra las conclusiones aprobadas en la conversación de diseño. Complementa la especificación v0.4 sin sobrescribirla ni alterar los códigos T00–T33 o sus sufijos existentes. No reinterpreta contratos publicados, resultados históricos ni estados de aceptación de entregas anteriores. Si un acuerdo requiere cambiar un contrato o política económica, su implementación deberá versionarse y verificarse expresamente.

## 1. Principios del modelo

- VALDRIO seguirá especializado en repartir, conciliar y explicar costes; los **factores directos** reutilizarán recursos, nodos, pools, repartos y tarifas existentes, **sin otro motor paralelo**.
- La calificación de «directo» depende del objeto al que se relaciona el coste. Un coste puede ser directo a un nodo e indirecto a un producto. Una parte de recurso conserva una única ruta económica (CM02).
- Un traslado entre nodos no genera gasto nuevo. La frontera final distingue coste directo a objeto, coste aplicado por tarifas, capacidad no absorbida, actividad sin objeto, gastos de período, exclusiones y pendientes (CM11).
- La clasificación funcional de los nodos **no restringe los destinos** de los repartos. Los criterios deben representar prestaciones justificadas y mantener las contribuciones efectivas de origen.

## 2. Identidad y clasificación de nodos — ampliación de T04

Se establecen **dos clasificaciones independientes y explícitas**:

| Dimensión | Valores | Significado |
| --- | --- | --- |
| Papel de cálculo | **Principal / Auxiliar** | Determina cómo participa en el primario/secundario; no se añade un papel mixto sin contrato nuevo. |
| Función económica | **Estructura / Producción o actividad principal / Comercial** | Identifica la función del nodo para el análisis y presentación del coste. |

Los nodos deberán tener código e identidad estables. **No se inferirá la función de su nombre, cuenta o driver**. El campo actual `FunctionTags` es libre y puede contener varias etiquetas: la conversión a una clasificación controlada y obligatoria es una **ampliación pendiente**, respetando la lectura de versiones ya publicadas.

Los nodos que acumulan costes para calcular unitarios de factores directos se identificarán **inequívocamente** mediante una **finalidad tarifaria** diferenciada de las dos clasificaciones anteriores: general/indirectos; grupo homogéneo de MOD; aprovisionamiento cuando se utilice. No es un tercer papel del motor de reciprocidad.

Ejemplos: `principal + producción + MOD`; `principal + comercial + general`; `auxiliar + estructura + general`.

## 3. Transferencias entre funciones; estructura residual

Un auxiliar inicialmente clasificado como **Estructura**, por ejemplo Dirección General, puede prestar servicios a **Producción**, **Comercial** y **Estructura**. Se permiten estas transferencias según las claves/inductores aprobados, sin bloqueos por etiqueta funcional y sin pérdida de trazabilidad.

- Se conserva **origen económico** (recurso, capa inicial, nodo y regla) y **destino funcional terminal**. No se borra ni cambia silenciosamente la capa original de la parte de recurso.
- Para análisis de gestión, el coste atribuido a una función puede integrar su bloque de resultados; la admisibilidad como coste inventariable exige **una evaluación contable independiente**. Una transferencia a producción no habilita por sí misma su capitalización.
- Los auxiliares se liquidan conforme a CM06/CM07/CM09. El coste sin atribución causal suficiente a producción o comercialización termina, cuando corresponda, en un **principal de estructura** y constituye **gasto del período**.
- Se intentará asignar causalmente todo servicio identificable, **sin imponer que el residual de estructura sea cero** ni reducirlo arbitrariamente.

**Brecha a concretar en implementación:** el código ya conserva `ResourceCostLayer` de origen y etiquetas de nodo. La proyección del **destino funcional** necesita una regla explícita, versionada y conciliable para CM07/CM11/CM12; no se consigue reinterpretando automáticamente `ResourceCostLayer`.

## 4. Factores directos: mano de obra

**Unidad económica mínima obligatoria:** grupo homogéneo de MOD (categoría, especialidad o función). Identificar a cada trabajador es opcional: no se exige un módulo de nóminas o control de presencia.

Cada grupo tendrá un nodo/pool identificable, normalmente **principal + producción + finalidad MOD**. Acumulará:

1. Costes propios desde recursos (salarios, cotizaciones y otros importes atribuibles).
2. Servicios recibidos del secundario (RR. HH., asesoría, vestuarios u otros componentes justificados).
3. Conservará desglose de costes propios, recibidos, comportamiento económico y procedencia.

Los **nodos MOD serán receptores del secundario** y después calcularán su tarifa horaria. La misma nómina o prestación nunca se añadirá de nuevo como coste independiente a la orden si ya forma parte de esa tarifa.

### 4.1. Política de tarifa y horas

Se acuerda evitar que la caída puntual de actividad eleve automáticamente el **componente fijo** de la tarifa. Se adopta la capacidad **normal o práctica conforme a la política versionada** como referencia del componente sujeto a capacidad.

**Compatibilidad obligatoria:** la política ya adoptada `NormalizedManagement` (CM08/D04) calcula **fijo propio** sobre normal (`Cf / qN`); **variable observado** y **servicios recibidos/consumidos** sobre actividad real (`Cv / qR`, `CConsumed / qR`). **No** se divide todo `Cf + Cv + servicios` entre horas normales ni se vuelve a evaluar la capacidad de un auxiliar cuyo coste ya fue evaluado (CM09). La elección excepcional de práctica frente a normal y otras políticas económicas queda para decisión/versionado específico; no cambia la implementación actual por esta adenda.

Se distinguirán, sin confundir sus diferencias:
- Capacidad normal/práctica aprobada.
- Horas efectivamente trabajadas/actividad observada.
- Horas imputadas a productos, órdenes o servicios.
- Horas trabajadas sin objeto identificado (formación, preparación, mantenimiento, trabajos no atribuidos, según evidencia).
- Capacidad no utilizada.

La actividad observada sin objeto **no es automáticamente ociosidad**. Las diferencias monetarias se separan y concilian, incluso con cero real, sobreactividad o costes recibidos.

**Tres fuentes aceptadas:** importación CSV/Excel, entrada manual y tiempos estándar. Los estándares se marcan como **estimados**, no como hechos reales. Identidad, período, unidad, grupo, objeto/actividad cuando exista, fuente y control de duplicidades deben quedar identificados. No se obliga a identificar trabajadores individualmente.

## 5. Materiales y aprovisionamiento

El **material individual** es la unidad mínima de consumo. Los grupos homogéneos de aprovisionamiento y sus nodos son **opcionales**, no uno por referencia obligatoriamente.

**Modalidad simplificada, prioritaria:** el usuario facilita un **coste unitario de adquisición ya calculado**. VALDRIO puede utilizarlo en recetas y estimaciones y añadir solamente costes adicionales **no incluidos** cuando se justifiquen. T17/T18 ya ofrecen catálogo, precios de referencia y estimaciones de gestión; esto **no equivale** a consumos reales ni a valoración contable de inventarios.

**Modalidad avanzada, futura:** con datos suficientes, VALDRIO calcula componentes de aprovisionamiento desde recursos, nodos/pools, servicios auxiliares e inductores (por ejemplo unidades, masa, valor, pedidos o movimientos). Reutiliza el mismo circuito económico y conserva la procedencia, sin mezclar recargos que usen unidades incompatibles.

Ambas modalidades deben distinguir **coste de adquisición/producción admisible bajo el marco contable aplicable** de **costes adicionales de gestión/pricing** (compras generales, almacenamiento ordinario, etc., sujetos a análisis; no capitalización automática). Comprar no equivale a consumir: receta prevista y coste de referencia no generan un segundo coste real. Un consumo real tendrá identificación de fuente y enlace no duplicado a recursos o fuente externa (CM02/CM10/CM11). El módulo integral de existencias y producción en curso **permanece en T32**.

La fecha/entrega de la modalidad avanzada y los inductores concretos **no se han aprobado** en esta conversación.

## 6. Presentación de repartos y tarifas

**Resultados del primario (T09) y secundario (T11):** en **pestañas o tablas separadas**, sin mezclar en la misma tabla los nodos que se utilizan para tarifas/costes indirectos y los nodos de **factores directos** (MOD y aprovisionamiento, si hay). La agrupación usa la finalidad explícita del nodo, nunca heurísticas por nombre.

Se conserva una **conciliación global única** con desglose por grupo, recursos, contribuciones, transferencias y ajustes de redondeo. Los flujos brutos recíprocos siguen siendo informativos y **no aditivos**. Pareto y otras vistas de resultados deberán distinguir el grupo seleccionado; no se sumarán ni compararán valores con bases incompatibles sin contexto.

**T15 — pantalla actual «Tarifas de centros»:** el menú y el encabezado pasarán a denominarse **«Tarifas y costes unitarios»**, incorporando una pestaña adicional **«Factores directos»**, junto a **«Tarifas de centros»**. La primera agrupa MOD y aprovisionamiento habilitado; la segunda mantiene el resto de centros. Reutilizar cálculo, estado, procedencia, diagnósticos, explicaciones y exportación; no duplicar tarifas ni perder funcionalidad existente. Es un **requisito futuro de UI**, no un cambio ya ejecutado.

## 7. Perímetros de coste, márgenes y pricing — T20

Los resultados distinguirán:

1. **Coste de producción / prestación:** componentes atribuibles a objetos y función productiva, incluidos servicios que hayan llegado desde auxiliares de otra función cuando se justifiquen; las restricciones de valoración de existencias se gestionan por separado.
2. **Margen industrial/de prestación:** ventas netas menos **coste de producción correspondiente a unidades vendidas o servicios reconocidos**, no automáticamente todo el coste producido en el período.
3. **Margen tras comercialización:** margen anterior menos costes comerciales correspondientes, incluidos servicios recibidos de auxiliares.
4. **Resultado analítico:** margen anterior menos **estructura residual del período**, otras partidas de período y diferencias no absorbidas según su naturaleza y el perímetro elegido. No ocultar capacidad o actividad sin objeto.

Los costes comerciales y de estructura no se capitalizan por aparecer en una vista de coste completo. Los gastos de estructura no se fuerzan al coste unitario industrial. Cada margen muestra fuente de ventas, frontera, perímetro, fórmula, porcentaje y denominador (CM11/CM12). Las bases de pricing podrán elegir coste de producción, coste con comercialización u otros perímetros de gestión explícitos, distinguiendo **margen sobre ventas** y **recargo sobre coste**.

La aprobación del **orden de los niveles** no resuelve automáticamente todas las reglas para asignar gastos comerciales a cada objeto, valorar existencias o conciliar costes de períodos diferentes.

## 8. Invariantes y criterios de aceptación futuros

- `recurso efectivo = directo + pools + excluido + pendiente`, por recurso, con signos y precisión correspondientes.
- El secundario conserva contribuciones económicas por origen; no se suman dos veces transmisiones ni servicios recíprocos.
- Los nodos de factores directos y los generales se separan **solo en presentación/finalidad**, no en un ledger económico paralelo.
- Costes de MOD propios y recibidos se aplican una única vez; componentes ya evaluados por capacidad no se evalúan de nuevo.
- Costes de material ya incluidos en el precio de referencia no se recargan otra vez; estimado, real informado, real detallado y conciliado siguen siendo estados distintos (T18/T19).
- Las observaciones ausentes no se confunden con cero, las horas sin objeto no se clasifican automáticamente como ociosidad y los importes de estructura se explican aun cuando permanezcan de período.
- Publicaciones, revisiones, políticas y snapshots históricos conservan su interpretación. Implementaciones futuras deberán tener pruebas matemáticas, de integración y de UI proporcionales al cambio.

## 9. Impacto de hoja de ruta SIN renumerar tareas

| Tarea existente | Ampliación documental / integración prevista | Estado de esta ampliación |
| --- | --- | --- |
| **T04** | Clasificación económica obligatoria por nodo y finalidad tarifaria inequívoca (MOD/aprovisionamiento/general) | Aprobada, no implementada por este documento |
| **T07** | Mantener partes y rutas excluyentes; **sin ampliar** ajustes reutilizables | Implementación previa conservada |
| **T08–T11** | Repartos sin restricción por función; primario y secundario con tablas/pestañas diferenciadas para nodos generales y factores directos | Ampliación aprobada |
| **T12** | Resultados históricos y conciliación común, con función de destino explicable | Ampliación aprobada |
| **T14** | Horas trabajadas, imputadas, sin objeto y fuentes manual/importada/estándar diferenciadas | Ampliación aprobada |
| **T15** | Tarifas MOD por componente y absorción; renombrar pantalla, añadir pestaña de factores directos | Ampliación aprobada |
| **T16** | Conservar evaluación única de capacidad de auxiliares que prestan servicios a MOD | Requisito de compatibilidad |
| **T17** | Material individual; agrupaciones de aprovisionamiento opcionales | Ampliación aprobada |
| **T18** | Uso de coste unitario de adquisición importado, estimaciones de materiales y MOD con fuentes congeladas | Base ya implantada, ampliación de MOD pendiente |
| **T19** | Consumos reales de materiales y horas MOD sin duplicar recursos; separación de comparación informada y detallada | Desarrollo parcial preexistente, ampliación pendiente |
| **T20** | Margen industrial, margen tras comercialización, estructura residual y perímetros de pricing | Requisito aprobado, implementación pendiente |
| **T22** | Informes separados por finalidad y escalones de rentabilidad, explicaciones y consolidación | Ampliación aprobada |
| **T32** | Inventarios y producción en curso bajo marco normativo propio | Sigue siendo opcional; no se adelanta |

**T10 (solver recíproco), redondeo y contratos de conservación:** no se cambian. Las entregas originales R0–R4 y todos los identificadores ya realizados se mantienen. Tampoco se declara terminada una ampliación porque la tarea original esté integrada.

## 10. Ajustes extracontables reutilizables: decisión definitiva

**PEND-AC-01 se retira de los pendientes de desarrollo. No se crea T07A.** A diferencia de la propuesta inicial de descartar una capacidad nueva, se ha comprobado que existen fichas de ajustes reutilizables ya implementadas.

**Acuerdo expreso:** **conservar íntegramente lo ya implementado** (código, UI, persistencia, pruebas y trazabilidad; véase [ADR-0017](../adr/ADR-0017-ajustes-reutilizables.md)), **sin ampliarlo** en esta revisión. La retirada del pendiente **no autoriza borrar ni degradar** funcionalidad anterior.

## 11. No decidido: mantener pendiente para futuras conversaciones

- Reorganización de la estrategia comercial de R1 o desplazamiento de T17–T19.
- Generalización adicional del catálogo de objetos de coste más allá de los usos ya presentes.
- Fecha de entrega, método concreto e inductores definitivos de aprovisionamiento avanzado.
- Políticas alternativas a `NormalizedManagement`, incluyendo el uso de capacidad práctica como denominador donde difiera de la normal; no modificar D04 sin versión/ejemplos.
- Clasificación y destino definitivo de cada clase de horas trabajadas sin objeto.
- Fórmulas y claves de imputación comercial por producto, cliente o canal.
- Requisitos contables completos para la valoración de existencias por jurisdicción y la extensión T32.
- Migración técnica de `FunctionTags` a clasificación única controlada y contratos de persistencia, preservando históricos.
- Definición técnica exacta del registro de finalidad tarifaria y su efecto en filtros/presentación.
- Verificación del estado de código y aceptación de las ampliaciones: **ninguna se da por realizada por este acuerdo**.

Las demás cuestiones del proyecto no tratadas en esta conversación mantienen su estado anterior. Cuando una decisión futura altere una regla económica, habrá que actualizar contrato, casos y versión de política, sin reinterpretar ejecuciones publicadas.

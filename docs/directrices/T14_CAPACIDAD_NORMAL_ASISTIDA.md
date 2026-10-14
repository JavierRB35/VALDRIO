# T14 — Directriz funcional: capacidad normal asistida

**VALDRIO | R1 | 2026-10-13 | Alcance: definición y experiencia del analista.**
**Estado:** dirección de experiencia de usuario adoptada por el propietario para documentar T14; reglas económicas D04 y decisiones de representatividad no aprobadas por esta directriz. **No implementado.**

**Fuentes normativas:** [tarea T14 de la especificación v0.4](../../specification/v0.4/tareas/T14.md), [CM08](../../specification/v0.4/especificacion/02_Contratos_Metodologicos.md), [decisiones D04](../decisiones/DECISIONES_PENDIENTES.md), [usabilidad](USABILIDAD.md) y [sistema visual V1](../diseno/SISTEMA_VISUAL_VALDRIO_V1.md). Esta directriz complementa la especificación canónica sin alterar sus archivos ni sus hashes.

**Integración y entregas aprobadas el 14/10/2026:** [inventario pantalla por pantalla y hoja de ruta T14.1–T14.4](T14_INTEGRACION_Y_HOJA_DE_RUTA.md). Toda nueva medición física se centraliza en T14; la captura real manual precede a la importación; se conserva el coste de referencia T12 identificado sin recalcular; para horas de personal se usan personas equivalentes y calendarios homogéneos con diferencias justificadas.

## 1. Finalidad y frontera de fase

T14 aporta **magnitudes físicas trazables por pool, unidad, nodo y periodo** para que T15 aplique después políticas económicas de tarifas y capacidad. No calcula tarifas, ociosidad monetaria, costes por objeto ni decide por sí sola la política D04.

Se diferencian sin ambigüedad:
- **Capacidad teórica:** máximo ideal bajo un calendario y unas condiciones explícitas.
- **Capacidad práctica:** disponibilidad tras interrupciones inevitables de los recursos suministrados.
- **Capacidad normal:** actividad representativa esperada, para un horizonte y unas condiciones normales definidos.
- **Actividad real:** cantidad observada en el periodo, independiente de consumos asociados a objetos.
- **Actividad observada sin objeto:** actividad real que todavía no se ha vinculado a objetos. **No equivale a capacidad ociosa**.

Una capacidad normal no debe inferirse sin revisión a partir de un presupuesto, una sola observación real o el histórico completo. Un dato no disponible se almacena como **ausente**, no como cero.

## 2. Recorrido de usuario adoptado

La pantalla T14 tiene una **tabla única de actividad y capacidades por pool**, que recupera los pools, unidades y contexto del estudio. Incluye capacidades normal y real, utilización física y un estado verificable; muestra práctica y teórica al activar detalle avanzado. Hay filtro «Todos / Por revisar», errores junto a la fila, origen/calendario y acción «Comprobar datos». La edición habitual se hace **directamente en las celdas**; no se replica un segundo formulario editable.

La celda «Capacidad normal» dispone de la acción contextual **«Calcular / justificar»**. Su asistente contextual no es un segundo dato maestro: genera una **propuesta temporal**, enseña fórmula, observaciones, fuente, periodos y advertencias y, únicamente con confirmación del analista, devuelve el valor a **esa misma celda**, dejando la fila marcada como modificada hasta que se guarde el estudio. Debe existir la opción «Cancelar» sin alterar el valor previo.

Métodos disponibles:
1. **Histórico representativo:** seleccionar explícitamente periodos comparables, revisar anomalías y calcular una propuesta reproducible. La media aritmética solo es aceptable para observaciones homogéneas de igual duración o normalizadas explícitamente; la elección de ponderaciones es visible. No usar por defecto «últimos doce meses».
2. **Calendario operativo:** declarar días, turnos/día, horas/turno, recursos equivalentes, paradas planificadas y un **factor normal sostenible justificado**. VALDRIO muestra por separado magnitud ideal de calendario, disponibilidad tras paradas y actividad normal propuesta. El factor no se estima automáticamente.
3. **Valor declarado y justificado:** introducir la capacidad normal aprobada externamente, con fuente, responsable y fecha/revisión del criterio. No se presenta como dato medido.

Para cualquiera de los métodos: la fuente y calidad del dato son obligatorias para **adoptar** una propuesta; la adopción no implica publicar un estudio ni aprobar D04.

## 2A. Requisito prioritario: comprender cada campo y simplificar

**Decisión explícita del propietario (2026-10-13).** En T14, y como principio transversal de VALDRIO, cada campo debe entenderse sin conocimientos de programación ni de terminología contable avanzada; antes de añadir una opción hay que comprobar si puede eliminarse, calcularse, reutilizarse o mostrarse únicamente cuando sea necesaria. **La simplicidad es del recorrido, nunca una excusa para perder exactitud, datos de origen ni auditoría.**

### Regla de los cuatro interrogantes

Para cada campo que deba ver el analista, proporcionar de forma accesible:
1. **Qué es:** definición breve sin jerga.
2. **Qué tengo que poner:** magnitud, unidad, periodo y ejemplo concreto.
3. **De dónde sale:** origen o método para obtenerlo, incluida indicación si se calcula automáticamente.
4. **Para qué sirve:** decisión o etapa posterior que utilizará ese valor.

Presentar **una frase junto al campo** durante la primera configuración y una ayuda contextual completa accesible con ratón y teclado. No relegar requisitos críticos a un tooltip; en mensajes de error explicar qué falta y cómo arreglarlo. Textos localizables ES/EN.

### Diccionario obligatorio de la tabla

| Campo de pantalla | Explicación para el analista | Entrada / simplificación |
| --- | --- | --- |
| **Centro / Actividad** | «En qué parte de la empresa medimos el trabajo. Por ejemplo, Mecanizado dentro de Producción». | Recuperar del modelo; no volver a pedir código de nodo/pool. |
| **Unidad** | «Cómo contamos el trabajo: horas máquina, horas de personal, lotes…». | Recuperar del pool; no sumar unidades diferentes. |
| **Capacidad normal** | «Cuánto esperamos trabajar habitualmente en este periodo bajo condiciones normales. No es la actividad de este mes ni el máximo que permite el calendario». Ejemplo: **160 horas/mes**. Sirve de posible referencia para tarifas fijas en T15, sujeto a la política adoptada. | Una única celda editable; «Cómo calcularla» es opcional y devuelve la propuesta a la misma celda. |
| **Actividad real** | «Cuánto trabajo se ha realizado de verdad durante el periodo». Ejemplo: **120 horas trabajadas**, aunque la máquina estuviera disponible durante más horas. | Importar o editar una vez, aportando fuente. |
| **Utilización** | «Qué porcentaje de la actividad normal hemos realizado: actividad real / capacidad normal × 100». Ejemplo: **120 / 160 = 75 %**. **No es una medida directa de eficiencia ni equivale al coste de ociosidad**. | Calculado por VALDRIO, **solo lectura**. Si no hay normal positiva o actividad real comparable: «No calculable», no 0 %. |
| **Estado / aviso** | «Qué dato falta o qué comparación debe revisarse». Ejemplo: «Falta actividad real de octubre: introdúcela o selecciona un archivo». | Calculado por VALDRIO; no pedir al usuario que elija «válido». |
| **Origen y calidad** | «De dónde procede y cómo se obtuvo el dato: medido, calculado con registros o estimado». Ejemplo: «Partes de máquina, octubre, medido». | Captura contextual al importar/editar o columna desplegable; no obligar a repetir igual fuente en cuatro campos del mismo lote. |
| **Periodo** | «De qué fechas son los datos». Ejemplo: 1–31 octubre de 2026. | Recuperar del estudio; si se usa referencia histórica diferente, mostrar equivalencia y exigir revisión. |

**Datos avanzados, no obligatorios universalmente:**

- **Capacidad teórica:** «Máximo ideal según el calendario que has definido, antes de descontar paradas». Ejemplo: 22 días × 8 horas = **176 horas** de una máquina. No equivale siempre a 24 × 7.
- **Capacidad práctica:** «Tiempo que el equipo podría estar disponible una vez descontadas paradas habituales inevitables». Ejemplo: 176 − 8 h de mantenimiento = **168 horas**.
- **Actividad sin orden/producto:** «Trabajo realizado que todavía no sabemos asociar a una orden concreta». Ejemplo: 120 horas reales, 100 identificadas: **20 horas sin objeto**. No denominar «ociosidad».

### Asistente opcional: vocabulario de las tres alternativas

**A. «Usar periodos anteriores» (histórico).**
- **Meses o periodos:** «Qué periodos queremos comparar». Requiere misma unidad y duración homogénea o una normalización explícita.
- **Actividad de cada periodo:** «Cuánto se trabajó realmente entonces»; todavía no es capacidad normal.
- **¿Hubo algo excepcional?:** «Señala situaciones que no deberían representar un periodo habitual». Ejemplos: avería extraordinaria, caída puntual de pedidos, cambio permanente de turnos.
- **Incluir/excluir y motivo:** «¿Debe participar esta observación en la propuesta? ¿Por qué?». Las exclusiones requieren confirmación y no borran originales.
- **Media propuesta:** «Promedio calculado solo con los periodos seleccionados y comparables». Mostrar cuántos se usaron y por qué; **no equiparar promedio con referencia aprobada**.

**B. «Calcular desde el calendario».**
- **Días de funcionamiento:** «En cuántos días está previsto que pueda trabajar este equipo o colectivo de personas». Ejemplo: 22.
- **Turnos por día:** «Cuántos turnos trabaja normalmente cada día». Ejemplo: 1.
- **Horas por turno:** «Cuántas horas dura cada turno». Ejemplo: 8.
- **Recursos equivalentes:** «Cuántos equipos o personas con el mismo calendario estamos midiendo». Ejemplo: 1 equipo; para horas de personal, declarar personas equivalentes. Si no comparten jornada o turnos, **justificar primero una equivalencia comprobable**; no sumar calendarios incompatibles.
- **Paradas habituales:** «Horas previstas sin funcionamiento por mantenimiento u otras interrupciones inevitables». Ejemplo: 8 h.
- **Actividad habitual sostenible (factor %):** «De las horas disponibles, qué proporción de actividad suele ser representativa en condiciones normales». Ejemplo: 95 %, **supuesto expresamente justificado, no sugerencia por defecto**.
- **Resultado intermedio:** «Horas de calendario: 176; disponibles tras paradas: 168; propuesta normal: 159,6». Mostrar la fórmula y no etiquetar las tres cifras como si fueran iguales.

**C. «Introducir mi referencia».**
- **Capacidad normal declarada:** «Escribe el nivel habitual que ya tienes aprobado». Ejemplo: 160 h/mes.
- **Justificación:** «Explica de qué estudio, planificación o responsable procede»; sin justificación, no se adopta como capacidad de referencia.

### Regla de máxima simplificación sin pérdida de significado

**Pantalla inicial imprescindible:** Centro/Actividad y unidad (precargados), **Capacidad normal**, **Actividad real**, **Utilización** (calculada) y **Estado**. Periodo (en cabecera) y procedencia (al guardar/importar, en detalle de fila) deben quedar visibles o accesibles sin duplicar formularios.

**Solo cuando el usuario pulse «Cómo calcularla»:** opciones de histórico/calendario/declaración. **Solo cuando sean relevantes:** práctica, teórica, calendario detallado y calidad ampliada. No pedir de entrada días, turnos, horas, equipos, paradas, factor y además una capacidad normal independiente. La adopción del cálculo rellena la misma celda, sin dos valores maestros.

No usar un porcentaje prefijado (por ejemplo 95 %) ni fabricar datos faltantes. Si el usuario ya conoce una capacidad normal defendible, debe poder escribirla y justificarla sin entrar al asistente. La falta de observación real no puede sustituirse silenciosamente por el valor normal.

### Comprobación obligatoria con personas usuarias

En la aceptación funcional, un analista ajeno al desarrollo debe poder explicar qué significa cada campo y realizar el recorrido **sin instrucciones externas**. Probar por lo menos: una máquina (horas máquina), preparación (lotes), servicio (horas de personal), actividad real cero, dato ausente, calendario con paradas, histórico con descenso de demanda y cambio de turnos. Si un campo necesita aclaración oral, revisar etiqueta/ayuda y reducir pasos antes de cerrar T14.

## 2B. Diseño de pantalla integrado con los resultados T11 publicados por T12 — acuerdo de revisión (2026-10-13; precisión de origen 2026-10-14)

**Decisión de interfaz del propietario: mostrar los nodos principales junto al coste final calculado en T11 y publicado por T12, y permitir completar su actividad desde la misma fila, abriendo una calculadora contextual completa.** La fuente de consulta de T14 es la ejecución T12 completada y vigente, no la vista mutable del cálculo T11; T13 certifica esta cadena con escenarios de aceptación, pero no produce un coste alternativo. La variante simplificada presentada posteriormente era únicamente ilustrativa y **no elimina** los tres métodos ni las explicaciones y validaciones del apartado 2A.

### Vista principal: una tabla y ninguna tabla de ratios monetarios

- Al entrar en T14, recuperar los **nodos principales** del estudio y su **coste final conciliado T11 desde los resultados terminales de la ejecución T12 completada y vigente**; los importes no se reescriben ni recalculan en T14. Si no hay ejecución T12 vigente para la revisión compatible, mostrar «Coste publicado no disponible para esta revisión» y acceso a «Flujos y ejecuciones», sin inventar importes ni sustituir T12 silenciosamente por un cierre T11 o una ejecución histórica. La captura de magnitudes físicas puede permanecer accesible si no depende del importe monetario.
- Columnas mínimas: **Centro / Nodo**, **Coste final (€ u otra moneda funcional del estudio; solo lectura)**, **Unidad de actividad (precargada)**, **Capacidad normal (editable)**, **Actividad real (editable/importable)**, **Utilización (calculada, solo lectura)**, **Estado** y **botón de calculadora por fila**. Periodo completo visible en cabecera. Fuente y calidad consultables junto a la fila; capacidades práctica y teórica en detalle avanzado.
- El botón muestra un **icono de calculadora acompañado de texto accesible «Calcular capacidad de [nodo]»** (nombre accesible, ayuda y teclado). La calculadora abierta siempre corresponde a la fila seleccionada; no se permite aplicar valores al nodo equivocado.
- **Regla estructural vigente de R1: exactamente un pool por nodo publicado y una unidad/driver por pool.** La interfaz T14 muestra **una fila por nodo principal, sin subfilas de pools ni unidades múltiples por nodo**. Recupera su única unidad de actividad del pool ya definido en T04; no vuelve a solicitarla ni permite cambiarla desde la calculadora. Un mismo centro físico puede albergar varios nodos con unidades distintas. Esta regla consta en `docs/directrices/T04_SIGUIENTE_FASE.md` y está respaldada por el índice único `IX_CostPools_ModelVersionId_NodeId` de la persistencia. El coste final calculado en T11 y publicado en T12 sigue correspondiendo al nodo y no se prorratea.
- **Eliminar de T14 cualquier tabla de «costes unitarios» o cifras «coste del centro / normal» o «coste del centro / real».** Aunque el cociente sea matemáticamente computable, no presentarlo como tarifa ni indicador económico representativo: puede ocultar la separación fija/variable, capacidad y perímetro de valoración. T14 es captura/validación física; **las tarifas y sus componentes son exclusiva responsabilidad de T15**.

### Fuente del coste y contrato de lectura T12

- Consultar `IRunStore.ReadRuns(CompanyId, StudyId)` y usar `RunHistory.CurrentRunId` para identificar la ejecución de referencia del estudio. El identificador vigente **se deriva** de la revisión y la versión de algoritmo en T12; no debe consultarse como si fuera un campo persistido utilizable de `StudyDefinition.CurrentRunId`.
- Exigir ejecución `Completed`, revisión compatible y `Output.Secondary` publicado. Leer `CalculationRun.Output.Secondary.Totals`, agrupando únicamente los componentes terminales del **mismo nodo principal** sin perder su moneda, comportamiento y clasificación. El coste final no se obtiene sumando transferencias auxiliares brutas (`RunOutput.Transfers`), que son movimientos internos no aditivos.
- Mostrar la moneda funcional, el ID de ejecución T12 y su condición «vigente» o «histórica» en los detalles de procedencia. La consulta no modifica importes, snapshots, recibos ni crea una nueva ejecución; el usuario no tiene que volver a cerrar T11 para visualizar una ejecución T12 ya publicada.
- **Revisiones y cambios T14 (decisión del propietario, 14/10/2026):** el contrato actual de T12 considera vigente un run completado de la revisión actual con versión de algoritmo compatible. Guardar nuevas observaciones físicas T14 puede crear una revisión posterior. **Mantener visible el coste de esa ejecución como referencia histórica identificada**, con RunId, revisión, algoritmo y aviso «No hay coste publicado vigente para esta revisión», **sin recalcular automáticamente** ni promocionar el run anterior a vigente. Antes de implementar la persistencia T14, resolver y documentar la compatibilidad verificada con el run económico T12 (por ejemplo, RunId base y prueba explícita de cambios solo físicos); no cambiar unilateralmente los contratos existentes.
- T13 verifica los escenarios y la lectura persistida T12; sus ficheros de prueba no son la fuente económica de los estudios reales. El origen matemático continúa siendo T11 y la valoración unitaria seguirá siendo T15.

### Ventana flotante: calculadora completa, prellenada y guiada

1. Abrir ventana contextual no destructiva sobre la tabla (modal o flotante según accesibilidad WPF), con **nodo, centro, unidad, coste terminal T11 recuperado de T12 (si es vigente), fechas exactas del estudio** y observaciones ya guardadas precargadas de fuentes verificadas. No solicitar al usuario datos conocidos ni deducir como ciertos campos que faltan.
2. Mostrar los **tres métodos completos** del apartado 2A, accesibles sin perder el estado de edición: **Histórico representativo**, **Calendario operativo** o **Capacidad declarada**. Cada método conserva sus campos y revisión de supuestos: histórico/comparabilidad/anomalías; calendario/días/turnos/horas/equipos/paradas/factor; declaración/cifra/fuente/justificación. Las capacidades práctica y teórica se calculan o muestran cuando existan datos válidos, sin convertirlas en campos obligatorios de todos los métodos.
3. Cada campo muestra en texto de usuario **qué es, qué introducir con unidad/ejemplo, de dónde sale y para qué sirve**, con ayuda contextual ES/EN y teclado. Se resalta en **amarillo de marca, con foco de teclado, el primer dato editable necesario que falte**, y el foco avanza lógicamente al siguiente al completarlo. No pintar amarillo un dato de solo lectura ni señalar campos opcionales como bloqueantes. No utilizar amarillo como único medio de indicación: etiqueta visible «Siguiente dato» y texto de ayuda.
4. Mostrar valores intermedios, fórmula, unidades, procedencia, advertencias y propuesta de capacidad normal **antes de confirmar**. Si falta **actividad real**, ofrecer introducirla o importarla con su fuente; **no calcularla desde normal o práctica**. Si faltan práctica/teórica opcionales, dejarlas ausentes en la tabla, no cero.
5. Acción primaria **«Calcular y aplicar»**: ejecuta validación; solo si los datos y sus periodos son coherentes, incorpora a la fila **cada magnitud física realmente obtenida o declarada** y el método/fuente, cierra la calculadora y deja cambios pendientes de **guardar en el estudio**. Los valores no obtenibles no se rellenan. Si existe error, mantener la ventana abierta, resaltar campo y explicar cómo corregirlo. **Cancelar** cierra sin cambios; no sobrescribir un valor vigente al abrir ni al cambiar de método.
6. Al regresar a la tabla, conservar selección de la fila, actualizar utilización/estado y mostrar si hay **cambios sin guardar**. La aprobación económica D04 y la publicación posterior no son efectos de ese botón.

### Intervalo del estudio como referencia única de T14

**Requisito expreso:** las unidades de actividad normal, real, práctica y teórica mostradas o propuestas se refieren al **mismo intervalo exacto de fechas del estudio ya definido** en la fase Estudios. Se heredarán **fecha inicial, fecha final, unidad, contexto operativo y calendario disponible**; no pedir seleccionar un mes alternativo para guardar cantidades incompatibles.

- Ejemplo: estudio **01/10/2026–31/10/2026**. Mostrar «Actividad del 1 al 31 de octubre de 2026» (no solo «mensual»), unidad «h máquina» y valor total de horas **de ese intervalo**. El cálculo por calendario utiliza los días laborables/operativos efectivos comprendidos en el intervalo y la zona/calendario declarados, incluidos festivos y cierres pertinentes cuando existan, turnos vigentes, cambios de equipo y paradas realmente planificadas.
- Un estudio trimestral, de 15 días o de fechas irregulares debe calcularse **desde sus fechas**, no como «3 × promedio mensual» o por una duración media implícita. Si el calendario no está disponible, solicitar su declaración. Los turnos/festivos no se infieren por idioma o región de visualización.
- En el método histórico, observar períodos fuente con **sus propias fechas**, normalizarlos de manera explícita a una base comparable o utilizar históricos de intervalos equivalentes. Documentar supuestos, equivalencia de jornadas, periodos seleccionados y tratamiento estacional. No multiplicar promedios históricos por días/meses sin comparación verificable.
- Si se ha definido una **referencia normal anual**, distribuirla en los periodos del estudio solo mediante calendario y regla aprobada, manteniendo referencia anual, regla y resultado específico del intervalo. Un cambio duradero de turnos o capacidad crea versión nueva con fecha de vigencia.
- Si la actividad real importada corresponde a **otro intervalo, unidad o calendario no conciliable**, no aplicarla silenciosamente; exigir selección de hechos del periodo o conversión explícita auditada. Si falta una medición fiable, mostrar «Pendiente» en lugar de inventar horas.
- Toda cifra «por hora», «por lote» o «por periodo» necesita unidad explícita. La cantidad normal de un pool del estudio no es automáticamente igual a su capacidad normal anual sin adecuarla al intervalo.

### Ejemplo de recorrido aceptable, con datos ilustrativos

Una **ejecución T12 completada y vigente** contiene el coste terminal **Mecanizado = 12.000 EUR**, calculado por T11 y mostrado en solo lectura; unidad `h máquina`, periodo del estudio 01/10/2026–31/10/2026. T14 muestra normal «Pendiente», real «Pendiente» y botón `[Calculadora]`. El analista abre el botón: la ventana conoce nodo, coste, unidad y fechas. Elige «Desde calendario», declara 22 días operativos, 1 turno, 8 h, 1 equipo, 8 h de mantenimiento y un factor sostenible **de ejemplo justificado** del 95 %. Calcula calendario ideal **176 h**, disponibilidad **168 h** y normal propuesta **159,6 h**. El usuario declara/importa **120 h de actividad real** de octubre con origen identificable y confirma.

La ventana se cierra. La tabla muestra **12.000 EUR**, **159,6 h normales**, **120 h reales** y **75,19 %** de actividad real respecto a la normal, con las 168 h prácticas y 176 h teóricas consultables en detalle. **No muestra 12.000/159,6 ni 12.000/120 como coste unitario.** Todavía habrá que guardar/validar los cambios en el estudio; T15 calculará las tarifas cuando su metodología haya sido aprobada.

### Aceptación funcional específica

1. La tabla recupera exactamente los importes terminales T11 **persistidos en la ejecución T12 vigente** por nodo principal, preservando snapshots, ID de ejecución y revisión, y sin duplicación por pool ni suma de circulaciones auxiliares.
2. La calculadora de cualquier fila hereda unidad, nodo y fechas del estudio; se abre precargada y enfoca en amarillo el **primer dato realmente pendiente**, con texto de ayuda explícito.
3. Alternar histórico/calendario/declarado conserva cada alternativa y no elimina campos necesarios; «Cancelar» deja intacta la fila.
4. «Calcular y aplicar» ejecuta validaciones, rellena solo magnitudes calculadas/medidas, cierra la ventana y conserva la fila correcta; los datos faltantes siguen pendientes.
5. Pruebas de periodos **mensuales, trimestrales, parciales y con cambio de turnos**, festivos configurados y fuentes históricas no comparables. No multiplicar por 3 una referencia mensual sin justificación.
6. El encabezado y cada campo explican período/unidad, y el estado distingue propuesta, aplicado en borrador, guardado y falta de fuente.
7. No existe pantalla de tarifas unitarias ni división monetaria simplista en T14. La frontera económica está en T15.
8. Una ejecución T12 histórica puede consultarse para trazabilidad, pero no etiquetarse como vigente ni mezclarse con magnitudes de la revisión actual sin un vínculo expresamente validado; si no hay ejecución actual, aparece un diagnóstico claro y acceso a «Flujos y ejecuciones».
9. La captura, el guardado y la reapertura de T14 no recalculan T11/T12 ni generan ejecuciones T12. Si guardar datos físicos modifica la revisión del estudio, hay que resolver explícitamente la compatibilidad entre la revisión T14 y el `CurrentRunId` derivado de T12 antes de cerrar T14; no se cambia por interpretación el contrato de actualidad existente.

## 3. Tratamiento de periodos anómalos — criterio propuesto para decisión metodológica

**Regla común:** conservar **todos** los datos originales. Si se excluye una observación del cálculo de referencia, conservar su periodo, cantidad, razón, autor y evidencia. La aplicación puede **señalar** anomalías, pero no excluirlas automáticamente por superar un umbral.

| Situación | Tratamiento propuesto en la referencia normal | Advertencia necesaria |
| --- | --- | --- |
| Caída puntual de demanda | No reducir automáticamente la capacidad normal; revisar si el dato describe actividad real anormalmente baja | Menor utilización no prueba menor capacidad suministrada |
| Avería o parada extraordinaria | Etiquetar el periodo; revisar comparabilidad y excluir/normalizar solo mediante decisión documentada | No mezclar paradas extraordinarias con interrupciones inevitables ya incluidas en capacidad práctica |
| Mantenimiento habitual programado | Incorporarlo a la disponibilidad práctica o calendario comparable, evitando descontarlo dos veces | Documentar horas, recurso y periodo |
| Estacionalidad recurrente | Comparar horizontes equivalentes y considerar referencia específica por temporada o distribución anual explícita | No comparar un mes de temporada baja con una media mensual inadecuada |
| Cambio estructural duradero (turnos, equipos, proceso) | Crear **nueva referencia versionada** desde su fecha efectiva y no reescribir la referencia histórica | No combinar series de regímenes distintos sin normalización justificada |
| Pico excepcional de pedidos u horas extra | Mantener la actividad real; revisar si es representativo para la normal | No usar un máximo extraordinario como capacidad normal implícita |
| Falta de medición o cambio de unidad/calendario | Mantener pendiente y bloquear la adopción sin equivalencia probada | Nunca transformar dato ausente en cero |

La propuesta de etiquetar anomalías y dejar elegir una referencia requiere validación del propietario **antes de fijar reglas automáticas**. No se define un umbral universal de anomalía, exclusión o ponderación.

## 4. Comparabilidad y validaciones

- El dato pertenece inequívocamente a **StudyId, PoolId, UnitId, periodo y calendario**. El driver es homogéneo dentro de un pool para R1. No agregar horas máquina y lotes, ni sumar actividades de pools diferentes como si fueran una única unidad.
- La conversión de históricos de distintos horizontes (meses de distinta duración, turnos, cambios de jornada) debe ser explícita, con calendario y factor de equivalencia justificables.
- Si normal y práctica representan el **mismo recurso y calendario**, una normal superior a la práctica origina diagnóstico; no imponer la desigualdad sin comprobar que ambas son realmente comparables (CM08).
- **Ausente != 0**: no inventar capacidad o actividad. Capacidad normal cero con coste fijo sujeto a normalización impide calcular su tarifa en T15; real cero exige tratamiento según naturaleza del coste (CM08).
- Actividad real superior a normal puede ser válida; no bloquearla por sí sola ni atribuirle «ahorro».
- Un consumo real de objetos mayor que la actividad real comparable requiere diagnóstico y bloqueo en el posterior cierre económico; T14 prepara la validación sin mezclar la estimación con hechos observados.
- No aceptar cantidades negativas de actividad como horas negativas por defecto; las devoluciones/reversiones deben tener hechos diferenciados según contratos existentes.
- Datos editados, borradores, confirmados y publicados tienen estados distintos. La fuente, la calidad, las advertencias y las decisiones de exclusión permanecen auditables.

## 5. Ejemplos de referencia para desarrollo (no políticas aprobadas)

### Histórico y baja de demanda

Seis observaciones mensuales homogéneas (horas): **160, 158, 162, 80, 159 y 161**.
- Media de las seis, **146,67 h**, solo es una descripción del historial.
- Si el analista documenta por qué las 80 h representan una caída puntual de demanda **no representativa**, la propuesta basada en los otros cinco periodos es **160 h**.
- La observación de 80 h se conserva y se identifica explícitamente como no utilizada en **esa propuesta**, sin desaparecer de estadísticas de actividad real.
- Sin una justificación aprobada, VALDRIO no debe excluirla automáticamente. El valor 160 h sigue siendo una propuesta hasta su confirmación.

### Calendario operativo

**22 días × 1 turno/día × 8 h/turno × 1 recurso equivalente = 176 h** de calendario ideal bajo esos supuestos.
Paradas habituales previstas: **8 h**, disponibilidad ilustrativa: **168 h**.
Factor normal sostenible declarado: **95 %** de la disponibilidad; propuesta: **159,6 h**.

El 95 % es un **supuesto del ejemplo, no un valor recomendado por defecto**. La pantalla debe explicar qué horas incorpora el calendario, qué paradas descuenta y a qué base se aplica el factor; no llamar a 159,6 h capacidad práctica si representa actividad normal. Para máquinas paralelas o calendarios desiguales, exigir configuración por recurso o equivalencia explícita.

### Actividad sin objeto

Normal aprobada 160 h; actividad real observada 120 h; de estas, 100 h vinculadas a órdenes. La comparación real/normal es **75 %** y existen **20 h reales sin objeto**. La diferencia normal-real es **40 h físicas**. Las 20 h sin objeto **no** deben denominarse subactividad; la valoración monetaria pertenece a T15/CM08.

## 6. Datos, trazabilidad y guardado

Conservar los contratos de v0.4 **CapacityObservation** y **ActivityObservation**, asociados a pool, unidad, periodo, calendario, fuente y calidad; las cantidades desconocidas son nulas. Para soportar propuestas históricas, revisar persistencia adicional de observaciones seleccionadas, exclusiones justificadas, método, parámetros, procedencia, autor y referencia versionada, **sin cambiar los contratos canónicos unilateralmente**.

El flujo de aprobación es: **capturar datos → proponer → revisar anomalías/comparabilidad → adoptar en el borrador → guardar/validar estudio → consumir en T15**. Si un estudio/evaluación histórica ya está cerrado, el nuevo criterio requiere nueva revisión o versión compatible; **no recalcular ni modificar recibos históricos**.

Presentar interfaz ES/EN, cantidades según perfil regional explícito (sin alterar el dato), unidades claras, navegación por teclado, contraste/foco, DPI 125/150/200 %, acciones editables solo cuando corresponda y diagnósticos legibles.

## 7. Criterios de aceptación para desarrollar

1. Los tres métodos generan propuestas reproducibles y el botón «Cancelar» no altera la capacidad guardada.
2. «Adoptar» copia el valor únicamente a la celda de capacidad normal del pool correcto y registra método/fuente, pero no ejecuta T15 ni publica el estudio.
3. Histórico: periodos de distinta duración o unidad disparan revisión; una exclusión manual conserva el valor bruto y el motivo.
4. Calendario: se distinguen ideal, disponible y normal; se detectan valores incoherentes, paradas mayores que el total y factores fuera de rango.
5. Valor declarado: no es válido sin justificación identificable.
6. Pruebas de cero/ausente, discrepancia normal/práctica comparable, real mayor que normal, real cero, cambio estructural, estacionalidad y actividad sin objeto.
7. Reapertura y nueva versión no alteran estudios/recibos anteriores; los datos enlazan con el pool y la unidad correctos.
8. La tabla permite editar, filtrar y resolver diagnósticos sin perder el foco ni reescribir otras filas; revisión en ES/EN y DPI.
9. Las comprobaciones matemáticas exigidas por CM08 (C15, C16, C18, C19, C31) se ejecutan donde corresponda, sin reemplazarlas por la maqueta.

## 8. Decisiones todavía abiertas (no implementar por interpretación)

Los cuatro acuerdos de integración del 14/10/2026 **sí están adoptados**, pero no resuelven los contratos técnicos de revisiones con T12, la interpretación de datos físicos legados de T07, la estructura de observaciones y versiones, los casos de equivalencia de calendarios de personal ni la política económica de T15. Véase la [matriz de asuntos pendientes](T14_INTEGRACION_Y_HOJA_DE_RUTA.md#6-cuestiones-técnicas-y-metodológicas-todavía-abiertas--no-resolver-por-interpretación).

- **D04**: política económica por defecto para T15; sigue propuesta.
- Regla para elegir muestras, normalización por longitud de periodo, ponderación y representatividad; **no imponer media universal**.
- Criterios concretos para clasificar periodos atípicos y quién autoriza exclusión/normalización.
- Criterios de calendario para recursos heterogéneos y periodicidad de actualización de referencia.
- Si el analista necesita aprobar además una capacidad práctica obligatoria para ciertos objetivos posteriores.

Ante cualquiera de estas decisiones, registrar ejemplo, alternativas, consecuencias y aprobación; no esconder una elección metodológica dentro de un cálculo.

**Costes de desarrollo:** no lanzar GitHub Actions ni modificar workflows por esta directriz. Usar Windows/.NET local o runner self-hosted cuando se autorice el desarrollo.

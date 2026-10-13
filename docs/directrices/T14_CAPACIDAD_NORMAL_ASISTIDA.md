# T14 — Directriz funcional: capacidad normal asistida

**VALDRIO | R1 | 2026-10-13 | Alcance: definición y experiencia del analista.**
**Estado:** dirección de experiencia de usuario adoptada por el propietario para documentar T14; reglas económicas D04 y decisiones de representatividad no aprobadas por esta directriz. **No implementado.**

**Fuentes normativas:** [tarea T14 de la especificación v0.4](../../specification/v0.4/tareas/T14.md), [CM08](../../specification/v0.4/especificacion/02_Contratos_Metodologicos.md), [decisiones D04](../decisiones/DECISIONES_PENDIENTES.md), [usabilidad](USABILIDAD.md) y [sistema visual V1](../diseno/SISTEMA_VISUAL_VALDRIO_V1.md). Esta directriz complementa la especificación canónica sin alterar sus archivos ni sus hashes.

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

- **D04**: política económica por defecto para T15; sigue propuesta.
- Regla para elegir muestras, normalización por longitud de periodo, ponderación y representatividad; **no imponer media universal**.
- Criterios concretos para clasificar periodos atípicos y quién autoriza exclusión/normalización.
- Criterios de calendario para recursos heterogéneos y periodicidad de actualización de referencia.
- Si el analista necesita aprobar además una capacidad práctica obligatoria para ciertos objetivos posteriores.

Ante cualquiera de estas decisiones, registrar ejemplo, alternativas, consecuencias y aprobación; no esconder una elección metodológica dentro de un cálculo.

**Costes de desarrollo:** no lanzar GitHub Actions ni modificar workflows por esta directriz. Usar Windows/.NET local o runner self-hosted cuando se autorice el desarrollo.

# Actualización de decisiones — 2026-11-04

Se aprueba la [adenda de factores directos, funciones económicas, presentación de repartos y márgenes](ACUERDOS_COSTES_DIRECTOS_MARGENES_2026-11-04.md) como referencia para ampliar la hoja de ruta **sin renumerar tareas ni reinterpretar entregas ya aceptadas**. Lo no debatido mantiene su estado de pendiente.

**PEND-AC-01: RETIRADO DE PENDIENTES.** Se conservan las fichas de ajustes extracontables reutilizables **ya implementadas** (ADR-0017, UI, persistencia y pruebas); **no** se crea T07A y **no** se amplía esa capacidad. Retirar el pendiente no significa retirar la funcionalidad.

La resolución de **D07** se actualiza a **principio funcional aceptado** (margen industrial, coste comercial y estructura residual), manteniendo pendientes los criterios detallados de imputación y comparabilidad para T20. Esta actualización prevalece sobre el estado de propuesta de cortes históricos y de la fuente v0.4, que permanece intacta.

---

## T09 — separación entre preview y política económica (2026-10-08)

La T09 implementa un **candidato técnico de preview** para reparto primario
con las claves publicadas y precisión propuesta PDEC-1 ya fijadas por T02/T08.
Los pendientes, incluidas partes sin clave o ruta, bloquean `IsComplete` y
quedan explicados. Una preview completa **no publica** una ejecución ni da por
aceptadas D04 (capacidad), D05 (auxiliares), D10 (autorreparto) o D11 (cobertura
productiva); la autorización económica futura permanece diferenciada de las
comprobaciones de conservación. Véase
[registro T09](../evidencias/T09_CANDIDATO_2026-10-08.md).

# Decisiones de producto y negocio

## Correcciones directamente en tabla — 2026-10-08

El propietario solicita una única pantalla para Preparar cuentas: solamente la
columna Correcciones se edita; cambiarla confirma y guarda automáticamente el total
en la moneda del análisis. Se elimina el formulario adicional de moneda/tipo/motivo.
El tipo y motivo del delta se generan como descripción auditable del cambio, sin
pretender certificar su justificación contable. Los originales, conversiones previas,
revisiones, auditoría, rutas y conservación siguen vigentes. Los destinos se consultan
y editan debajo de la tabla, con protección de borradores. [Evidencia](../evidencias/T07_CORRECCIONES_EN_TABLA_2026-10-08.md).


## Revisión de usabilidad vigente — 2026-10-08

El propietario solicita eliminar confirmaciones redundantes y desbloquear revisión
antes de T07. La UI deja una aprobación del significado en Revisar y guardar;
una nota manual de comprobación es opcional. Cuando falta, el perfil conserva una
referencia de aprobación al archivo, modalidad y periodo. Esto sustituye el requisito
de escribir un ejemplo en el formulario, no certifica ninguna fuente real ni elimina
la aprobación explícita, el perfil de origen, FX o controles transaccionales.
Los hashes se conservan y se presentan como información técnica desplegable.
[Evidencia y límites](../evidencias/USABILIDAD_SEIS_PROBLEMAS_2026-10-08.md).


## Autorización vigente T07 — 2026-10-07

El propietario solicita «Desarrolla completo el T07 teniendo en cuenta que ya hay
una rama abierta en local para esa tarea. Desecha lo que no necesites de esa rama».
Sustituye la espera de conversación anterior. Se reutiliza `codex/t07-resource-rules`
y sus reglas válidas, integrando T06/T06A y el recorrido guiado actualizado. No se
descartan datos del usuario ni se mezclan cambios de la rama de estética.
Las tres subentregas T07 están implementadas; [alcance](../tareas/T07.md) y
[evidencia](../evidencias/T07_CIERRE_2026-10-07.md). D04/D05/D07/D11 permanecen propuestas.
La agrupación por cuenta y la prioridad permanente de usabilidad siguen vigentes.

Los registros siguientes conservan la secuencia histórica de decisiones.

## Adelanto técnico T07 — 2026-10-07

Continuación autorizada en la misma rama: clasificación contextual y operaciones de
borrador. Producción/comercial/estructura se almacenan como categorías explícitas,
sin decidir el perímetro de un informe o aprobar D07. None/AssessAtPool/PeriodCost
son metadatos; no eligen denominador, política de capacidad ni aceptan D04/D05.
Una cantidad ausente no se convierte en cero. El comportamiento no se infiere de
la capa, la ruta o el rol del centro.

El propietario acepta comenzar las reglas y pruebas independientes de T07 mientras
T06 continúa, en una rama y copia de trabajo separadas. Se conserva la dependencia
T06/T06A para la integración. No supone aceptar D04/D05/D07/D11 ni cambiar los
contratos canónicos. Alcance y pendientes: [T07](../tareas/T07.md).

Fecha de actualización: 2026-10-04  
Fuente base: `especificacion/03_Decisiones_y_Diccionario.md` del paquete VALDRIO v0.4.  
Aceptación del propietario registrada el 2026-10-04.

## Aclaración D03 — 2026-10-07

El propietario confirma que el XLSX es solo un ejemplo y que el cierre se declara en el formulario: 31/10/2026. Se acepta el ensayo técnico y el ejemplo 6005=358,35 EUR; no se certifica una fuente productiva. Cada origen real requiere seleccionar registros, declarar periodo/moneda/modalidad y aprobar su ejemplo. T06 se cierra técnicamente con este alcance de prueba y aceptación del commit; el puente FX pertenece a T06A.

## Continuidad T06A/T07 — 2026-10-07

El propietario autoriza completar T06A y pide comentar T07 antes de implementarla.
T06A aplica selecciones manuales explícitas y congeladas por registro; no utiliza
cotizaciones de demostración sin aprobación ni consulta tipos actuales. El catálogo
instalado sigue siendo el subconjunto de ejemplo. Véase ADR-0011 para comparativos,
agrupación funcional por cuenta y conservación de originales separados por moneda.
La dificultad de guardado de centro/modelo queda para revisión posterior a petición
expresa. No se deduce una aprobación productiva de su Excel de prueba.

## Registro D01-D12

| ID | Estado vigente | Decisión / propuesta | Próxima puerta |
|---|---|---|---|
| D01 Finalidad | **ACEPTADA** | R1 se centra en gestión de costes, capacidad, productos, órdenes y rentabilidad. Valoración contable de existencias y producción en curso queda en T32. | T32; no adelantar salvo nueva decisión. |
| D02 Colaboración | **ACEPTADA** | Primera versión: un editor por archivo, funcionamiento local, sin edición simultánea desde equipos distintos. Arquitectura desacoplada. | Revisar solo si cambia el modo de colaboración. |
| D03 Fuente contable | **PENDIENTE DE EJEMPLO** | Perfil explícito para modalidad del saldo, signos, periodo e interpretación. No inferir por idioma ni convenciones españolas. | Antes de importación definitiva/T06, con archivo representativo. |
| D04 Capacidad | **ACEPTADA PARA T15 R1 — 18/10/2026** | El propietario solicita desarrollar y fusionar T15 en los términos debatidos: `NormalizedManagement`, fijo propio sujeto a capacidad sobre normal, variable y consumido sobre real. | Conservar procedencia y validaciones; D05 y las políticas alternativas no quedan aprobadas por esta decisión. |
| D05 Auxiliares | **PROPUESTA** | `OwnerOnceCapacity` para auxiliares medidos; `ThroughFlow` deja capacidad auxiliar sin evaluar. | Antes de T16: datos, fórmula, resultado y alternativas. |
| D06 Recetas y lotes | **PROPUESTA** | Setup por lote iniciado completo; unidades continuas sin redondeo a pieza; merma explícita. | Antes de T17-T18: datos, fórmula, resultado y alternativas. |
| D07 Perímetro de márgenes | **ACEPTADA EN PRINCIPIO (2026-11-04)** | Margen industrial/de prestación, margen tras comercialización y estructura residual del período; transferencias entre funciones justificadas y trazables. | T20: concretar claves comerciales, correspondencia ventas-costes, costes no absorbidos y demás reglas pendientes; ver adenda 2026-11-04. |
| D08 Protección y recuperación | **PENDIENTE DE OPERACIÓN** | No prometer cifrado por defecto. | Hito previsto T24; no bloquea trabajo independiente actual. |
| D09 Venta/licencia | **PENDIENTE COMERCIAL** | Licencia perpetua para versiones adquiridas; actualizaciones con vigencia separada; uso offline. | Hito previsto T24; no bloquea trabajo independiente actual. |
| D10 Autorreparto | **PROPUESTA** | Autorreparto prohibido por defecto; habilitable por versión con diagnóstico. | Revisar si aparecen servicios a sí mismo. |
| D11 Cobertura definitiva | **PROPUESTA** | Ejecución definitiva exige cobertura completa; borrador/provisional conserva pendientes visibles. | Revisar antes de publicación definitiva. |
| D12 Módulos avanzados | **PENDIENTE AVANZADO** | Estándares, TDABC y simulación requieren datos/supuestos propios. | T28-T31; no bloquea R0/R1 actual. |

## Precisión de alcance de la fuente — 2026-10-07

### Registros y códigos (confirmación posterior del mismo día)

El propietario confirma 6005: 280 + 78,35 = 358,35 EUR. La selección debe ofrecer
todos, ninguno, algunos o todos menos algunos registros, incluidas filas individuales
que compartan código. La agrupación se realiza por la columna de código elegida,
que puede contener cuentas, agrupaciones o códigos de gasto. Las descripciones
se ignoran para agrupar y se conservan como procedencia. Esta instrucción sustituye
la exigencia previa de resolver descripciones diferentes. Ver ADR-0009.

El ejemplo XLSX aportado se ha validado para lectura, selección, agrupación y guardado
local. Sus etiquetas incluyen todavía conceptos patrimoniales. La validación técnica
y el ejemplo aprobado no certifican automáticamente su clasificación como costes ni
el intervalo productivo del estudio. La decisión de pérdidas y ganancias sigue vigente.

El propietario indicó en el chat «Guiar instalación de Visual Studio» que para
calcular costes solo son necesarias cuentas de pérdidas y ganancias; las cuentas
de balance quedan fuera. La muestra de patrimonio personal aportada solo sirve
para ensayar lectura, no para aceptar D03 en producción. La selección de cuentas
es explícita y no se deriva de prefijos de un plan contable nacional. El asistente
materializa esta elección mediante todas las cuentas o una lista marcada por el usuario.

## Separación monetaria aceptada como requisito de producto

- La **empresa propone una moneda predeterminada**.
- Cada **estudio fija su propia moneda funcional**.
- Los importes originales pueden conservar monedas distintas.
- Toda conversión es explícita.
- Los tipos de cambio utilizados quedan congelados por ejecución.
- Idioma, región o país no determinan la moneda funcional ni el tipo aplicado.

Esta precisión es coherente con A11/CM16 y sustituye cualquier redacción anterior que atribuyera una única moneda funcional al archivo de empresa.

## Ejemplos que siguen siendo propuestas

### D03 — C01

Acumulado anterior 100 y actual 160 con `CumulativeExpenseDifference` produce 60. Con `PeriodAmount`, 160 sigue siendo 160. La modalidad y los signos se decidirán con archivo real.

### D04 — C15/C18

C15: fijo 1.000, variable 600, normal 100, real 60 y objetos 40/20 → propuesta 800 + 400 + 400 capacidad.  
C18: si solo 40 de actividad real están asociados a objeto, aparece además actividad sin objeto.

### D05 — C20

Coste fuente 1.600 → propuesta 800 a P + 500 capacidad A + 300 capacidad B, sin reevaluar como capacidad propia el coste recibido.

### D06 — C22

Q=120, lote=50, setup 2 h/lote, proceso 0,1 h/u → propuesta 3 lotes, 6 h setup, 12 h proceso, coste 420, unitario 3,5.

### D07 — C23

Venta 200, variable 120, fijo aplicado 30 → propuesta coste 150, margen 50/25 %, recargo 1/3 y contribución 80/40 %.

Comprender estos ejemplos **no** equivale a aceptar D04-D07.

## Regla de continuidad económica

Antes de convertir D04-D07 en reglas definitivas, la tarea correspondiente debe presentar al propietario:

1. datos necesarios;
2. fórmula propuesta;
3. resultado esperado;
4. alternativas razonables y sus consecuencias.

No se altera un resultado esperado para facilitar la implementación.

## Política técnica de precisión — resultado de T02

T02 ha materializado y probado la política técnica versionada `PDEC-1-proposed` descrita por CM04:

- importes/posteados: escala máxima 8;
- cantidades: escala máxima 12;
- porcentajes: escala máxima 18;
- magnitud inicial máxima: 1e12;
- corrección numérica automática máxima: 1e-8;
- `BalancedRound` con Floor, mayores restos y desempate ordinal estable;
- `CurrencyProjection` separada del ledger y con `MidpointToEven`.

Estado: **VALIDADA TÉCNICAMENTE EN T02** mediante tests/corpus, no promovida a decisión económica D04-D07.

Cambiar estos parámetros en el futuro requiere una nueva versión de política, actualización de referencias/pruebas y evidencia de migración/compatibilidad; no se cambiarán silenciosamente para hacer pasar un caso.

## Decisiones técnicas de T04 — 2026-10-05

[ADR-0003](../adr/ADR-0003-modelos-y-esquema-v2.md) fija familia/UUID y número de versión, evolución segura a copia v2, auditoría por agregado, publicación estructural congelada y reintentos de duplicación. No cambia D01–D12. Método y capacidad quedan explícitamente Pending; seleccionar una clasificación de coste no aprueba D04–D07. El siguiente bloque de estudios tiene su propia revisión y snapshots.

## T05 — Decisiones técnicas de contexto

[ADR-0004](../adr/ADR-0004-estudios-y-snapshots-v3.md) define v3, revisiones propias, dos perímetros de hash y captura contextual. No cambia D01–D12. D03 sigue necesitando muestra y resultado esperado antes de confirmar fuentes reales en T06; las restantes propuestas mantienen sus puertas.

## D03 — Fuente de cierre e importe completo, 2026-10-06

El propietario declara «Saldos de cierre» y, ante enero 100 / febrero 160,
elige «160: saldo completo». Se acepta esa interpretación para su estudio,
sin deducir una diferencia de 60. Sigue pendiente contrastar una muestra real,
sus cuentas seleccionadas, fecha, signos y moneda antes de aceptar el perfil productivo.

La implementación distingue ClosingBalanceAsStudyAmount de ClosingBalance
(evidencia sin interpretación) y CumulativeExpenseDifference. Las dos últimas
mantienen su bloqueo anterior. La opción de saldo íntegro exige aprobación y
referencia de ejemplo en cada preparación. La fecha de cierre debe coincidir
con el final del estudio y se muestra antes de aprobar. No se declara aceptada
una interpretación universal de cualquier balance ni de todas sus cuentas.

Esta decisión explícita complementa CM01 y las directrices T06 para esta modalidad;
no cambia los resultados históricos ni las 39 referencias matemáticas originales.


## Decisiones operativas T08 — 2026-10-08

El propietario autorizó completar T08 y acordó conservar todas las descripciones
contradictorias con procedencia, permitiendo elegir el nombre visible sin cambiar
identidad ni agrupación económica. Las asignaciones manuales preceden a reglas de
cuenta exacta y después al prefijo más largo; la prioridad decide dentro de igual
especificidad y los empates se diagnostican. Publicados requieren nueva versión;
importe absoluto queda vinculado a parte/revisión. Autoasignación secundaria
prohibida por defecto; configurar su política no aprueba las decisiones económicas
pendientes. No modifica D01–D12 ni referencias canónicas.

## Aplicación de D04 en T15 — 18/10/2026

La petición expresa del propietario «Desarrolla la T15 en los términos comentados en esta conversación y cuando verifiques su correcto funcionamiento y estética adecuada la fusionas» autoriza implementar el criterio normalizado debatido y fusionarlo tras validación local. Sustituye el estado anterior de propuesta únicamente para esta política de T15 R1. Los ejemplos 1.000/100 + 600/60 = 20 por hora y la distinción entre 400 de capacidad y 1.200 de actividad realizada forman parte del alcance explicado.

No convertir costes con tratamiento `None` en capacidad propia, ni evaluar de nuevo capacidad de auxiliares. Si una aportación `AssessAtPool` exige evaluación en otro propietario y no hay resultado que la acredite, mostrar diagnóstico; esta entrega no aprueba D05/T16. Las vistas alternativas ActualManagement/VariableCostView no se añaden al flujo simple acordado. Las referencias de la especificación canónica y los cortes históricos anteriores se conservan sin reescribir.


## Aplicación de D05 en T16 — 18/10/2026

El propietario autoriza desarrollar la propuesta T16 con los ajustes debatidos y fusionarla al terminar. Se adopta evaluación una vez por propietario para auxiliares medidos, con reparto sin evaluación explícito para los demás y estado pendiente que bloquea el cálculo. La cobertura parcial se muestra sin afirmar que toda la ociosidad esté medida.

C20: 1.600 = 800 transmitidos a principales + 500 de A + 300 de B. Los 100 de A recibidos por B no se vuelven a evaluar. C29 admite variación negativa firmada por sobreactividad sin llamarla ahorro. La normal y real proceden de T14, los servicios se registran en T16 y los costes evaluados se reciben en T15 como consumidos, conservando naturaleza y origen.

La autorización cubre este método y los ejemplos del debate; no sustituye la selección y evidencia de servicios reales de cada empresa. No habilita automáticamente todos los auxiliares, ni cambia contratos canónicos o cálculos históricos. Detalle en [T16](../tareas/T16.md).

## D06 — aprobación acotada T17, 20/10/2026

El propietario autoriza la revisión NIC 2 y la implementación de composición y cantidades; valoración económica en T18. Se adoptan las reglas físicas descritas en [T17](../tareas/T17.md): unidad/lote iniciado/orden, lote parcial completo, merma explícita como exceso o rendimiento, conversión dimensional, redondeo discreto al final y semielaborados publicados sin ciclos. La política económica de gestión T15/T16 permanece inalterada. Los precios de referencia originalmente previstos en T17 pasan a T18 por instrucción posterior del propietario.

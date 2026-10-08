# ESTADO_PROYECTO

## T07: volver a Correcciones y seleccionar el importe — 2026-10-08

Destinos se abre con su botón explícito; la barra fija y el editor permiten cerrar
los destinos y volver a Correcciones, resolviendo los borradores sin ocultarlos.
Un clic selecciona todo el importe para sustituirlo al escribir. Se explica la
fila pendiente inicial y el combo identifica centro, nodo, papel, pool, unidad y
funciones. [Evidencia](docs/evidencias/T07_RETORNO_Y_EDICION_2026-10-08.md).


## T07: correcciones en tabla y guardado automático — 2026-10-08

La columna Correcciones es la única editable, siempre en moneda del análisis. El
cambio confirmado en la celda se guarda y recalcula efectivo/pendiente. Se elimina
la pantalla adicional de ajustes. Tabla, clasificación y destinos permanecen en
una pantalla; originales, conversiones históricas, revisiones y auditoría se conservan.
Cambio en `codex/usability-six-issues`, PR #27; no está fusionado automáticamente.
[Evidencia](docs/evidencias/T07_CORRECCIONES_EN_TABLA_2026-10-08.md).


## Revisión de los seis problemas de usabilidad — 2026-10-08

Cambios aplicados sobre la integración `770c640` en `codex/usability-six-issues`:
feedback específico de estructura, recarga explicada, vaciado contextual de centros,
centro físico visible y orden estable, árbol de finalización con trazabilidad avanzada,
separadores manuales visibles y revisión accesible con aprobación única al guardar.
T07 permanece después de confirmar la importación. Se preservan dominio, origen,
FX, revisiones, hashes y reglas de conservación. Compilación y validación locales,
sin GitHub Actions ni cambios en main. [Evidencia](docs/evidencias/USABILIDAD_SEIS_PROBLEMAS_2026-10-08.md).


## T07 — implementación completa (2026-10-07)

Las tres subentregas están implementadas e integradas en la rama existente
`codex/t07-resource-rules`: ajustes firmados y reversión con Money/FX propios;
clasificación contextual y partes; asignación directa/pool/exclusión/pendiente
con conservación por cuenta, auditoría, transacción y reapertura. Empresa v8,
snapshots v6 y actualización a copia desde v1–v7. Se preservan lectores históricos.

El recorrido guiado ahora es Empresa → Estructura → Estudio → Importación → Recursos.
Recursos guía cuenta → ajustes → partes → revisión y guardado explícito. Incluye
ayuda ES/EN con ratón/teclado, borradores conservados, historial y revaloración
manual con conciliación. La usabilidad sigue siendo prioridad; no es una revisión estética.

Contrato y límites: [T07](docs/tareas/T07.md). Arquitectura:
[ADR-0012](docs/adr/ADR-0012-recursos-y-particiones.md). Aceptación reproducible:
[evidencia T07](docs/evidencias/T07_CIERRE_2026-10-07.md). La validación local y el recibo Windows del commit
acreditan los resultados; la integración en main es posterior. GitHub Actions queda
reservado a una petición expresa del propietario (AGENTS.md, todas las fases).
Las decisiones económicas propuestas mantienen su estado. Próxima fase funcional:
T08, sin adelantar repartos, tarifas ni políticas de capacidad.

La autorización completa T07 sustituye los registros anteriores que pedían esperar
una conversación. Se conserva debajo el historial de T06/T06A y entregas precedentes.

## T06A — puente internacional implementado (2026-10-07)

Las tres subentregas están implementadas: perfil internacional, originales/fechas y
selección explícita de tipos por registro, conversión y conciliación, confirmación
atómica, historia, snapshots y runner. Empresa v7; perfil/preparado v4; snapshot v5.
Actualización siempre a copia desde v1–v6. Ver docs/adr/ADR-0011-importacion-internacional.md
 y docs/evidencias/T06A_CIERRE_2026-10-07.md. El recibo Windows y CI verdes del HEAD
acreditan el cierre final. El catálogo instalado conserva las cuatro monedas de ejemplo.

Registro histórico anterior a la autorización completa de T07: se había solicitado
comentar ajustes/clasificación/particiones antes de su implementación. El problema de guardado de
centro/modelo se revisará más adelante por petición expresa; no se declara solucionado.

## T06 — implementación completa y aceptación del commit final (2026-10-07)

T06.0–T06.6 implementadas: CSV/XLSX, perfil explícito, selección de registros, agrupación
por código, periodo/cierre íntegro/diferencia de acumulados comparables, preparación sin
escritura, confirmación atómica, sustitución identificada, historial, snapshots y runner.
Formato de empresa v6 con actualización a copia desde v1–v5; inputs históricos intactos.
La reapertura y el hash económico usan solo los lotes vigentes y conservan los retirados.

433/433 pruebas Release; recorrido WPF completo con nueve comprobaciones adicionales.
El cierre formal se acredita por el recibo Windows y CI verdes del HEAD asociado al PR;
no basta con compilar. Ver [matriz F01–F24](docs/evidencias/T06_CIERRE_2026-10-07.md) y
[ADR-0010](docs/adr/ADR-0010-comparativos-y-sustitucion.md).

El propietario confirma que su Excel es un ejemplo y que declara 31/10/2026 en el formulario.
D03 se acepta para este ensayo (6005=358,35 EUR), no para una fuente de producción.
Moneda distinta de la funcional sigue bloqueada en T06: el siguiente trabajo es
[T06A](docs/directrices/T06A_SIGUIENTE_FASE.md). Cálculo, particiones y consumos no se adelantan.
Esta sección sustituye los pendientes históricos de comparativos y sustitución descritos abajo.

## Continuidad — conciliación inmediata de registros, 2026-10-07

Se añade un resultado calculado al seleccionar registros: agrupación ordinal por código,
filas de procedencia, positivos/negativos/neto incluidos y neto excluido. Funciona sin estudio
asociado, conserva ceros y códigos distintos como 00123/123 y a/A, e incluye todas las filas.
Releer o cambiar columnas elimina el resultado anterior. Los desbordamientos muestran un error
sin totales parciales; cambiar la selección permite recuperarse. Es una revisión del origen:
la preparación sigue comprobando precisión, periodo, moneda y aprobación antes de confirmar.
No cambia persistencia, esquema v5 ni contratos de snapshots.

Pruebas locales: 411/411 Release (`live-reconciliation.trx`); compilación WPF sin advertencias. Flujo nativo completo con salida 0 en `artifacts/live-reconciliation-2026-10-07`, 11 comprobaciones de agrupación y captura del resultado inmediato revisada visualmente. No se ha ejecutado CI remota.
El siguiente bloque pendiente de persistencia sigue siendo sustituir un lote identificado
sin duplicar importes ni perder el historial. No se declara T06 cerrada.

## Continuidad — acceso a importación y guardado del estudio, 2026-10-07

Se aclaran las acciones: «Limpiar formulario / preparar otro estudio» y «Guardar este estudio».
Los listados vacíos explican la ausencia de estudios/modelos y hay acceso directo al editor de modelos.
El resultado de crear se muestra junto a los botones y se lleva a la vista al terminar.
Abrir la revisión del origen con un estudio nuevo sin guardar conserva el formulario sin pedir
que se guarde o descarte. Preparar/confirmar sigue requiriendo un estudio guardado; no se crean
modelos ni estructuras contables implícitos. Tras publicar, Actualizar estudios/modelos carga las opciones.

Validación: 408 pruebas Release correctas (`study-ux.trx`), compilación WPF sin advertencias,
flujo nativo completo con salida 0 en `artifacts/study-ux-verified-2026-10-07` y revisión visual.
Después de esa captura se quitó un mensaje de estado duplicado y se unificó su color; recompilado.
Dos intentos previos de captura fallaron por usar rutas relativas o separadores `/` en el comprobador
de igualdad de rutas del smoke; la ejecución con ruta absoluta Windows pasó.

## Continuidad — selección de registros y agrupación, 2026-10-07

La elección pasa de cuentas a registros individuales: todos, ninguno, algunos o todos
menos algunos. Los registros seleccionados se suman por el código de la columna elegida
(cuenta, agrupación o gasto). Las descripciones no intervienen, por instrucción expresa
del propietario. Ejemplo confirmado: las filas 7 y 8 de 6005 producen 358,35 EUR;
seleccionar solo la fila 8 produce 78,35 EUR.

Archivo de empresa v5, perfil/preparado v2 para agrupaciones y snapshots v3. Los formatos
históricos conservan sus lectores; los archivos anteriores se actualizan a una copia.
Ver [ADR-0009](docs/adr/ADR-0009-registros-y-agrupacion-por-codigo.md) y
[evidencia](docs/evidencias/AGRUPACION_REGISTROS_2026-10-07.md).
Esta sección sustituye las restricciones de agrupación/una fila por recurso anteriores.
T06 no está cerrada: siguen sustitución de lotes, diferencias comparables, aceptación
global y definición productiva del alcance/periodo de la fuente. T06A/T07 después.

## Continuidad — 2026-10-07: selección explícita de cuentas

Sobre `8909e88`, el asistente permite importar todas las cuentas del archivo o marcar
una lista explícita después de validar las columnas. El propietario ha precisado que
el estudio de costes utiliza cuentas de pérdidas y ganancias, no cuentas de balance.
La elección no se deduce de la numeración, país o idioma. No se considera aceptada
una muestra productiva por añadir esta pantalla.

Cambiar una cuenta invalida la preparación y la aprobación. Una lista vacía bloquea
preparar; releer o cambiar el mapeo borra las marcas anteriores. La selección incluye
todas las cuentas validadas, no solo las primeras 50 filas de la vista previa.
El perfil v1 ya contemplaba `ExplicitList`: se reutiliza con formato de empresa v4,
conservando todas las filas originales y generando recursos solo para las seleccionadas.

Evidencia: [selección de cuentas](docs/evidencias/SELECCION_CUENTAS_2026-10-07.md).
Esta subentrega no cierra T06. Siguen pendientes muestra real de pérdidas y ganancias,
agrupación con resolución de descripciones/procedencia, sustitución de lotes,
diferencias comparables y aceptación global; después T06A y T07.

## Continuidad local — 2026-10-06

Rama `codex/local-tests-and-workspace`, sobre `5b66e3c` (PR #21 integrada).
Pruebas de procesos usan el Runner copiado junto a los tests según Configuration;
se elimina la dependencia de Release al ejecutar Debug. La interfaz destaca la empresa
abierta, separa detalles técnicos y agrupa ejemplos en un desplegable; título de espacio
de trabajo y recursos ES/EN actualizados.

Se añade selección explícita de saldo íntegro al cierre para el estudio según la elección
del propietario 100→160 produce 160, con aprobación renovada y fecha de cierre visible.
Ver [ADR-0008](docs/adr/ADR-0008-saldo-integro-cierre.md). D03 está concretada en esa
interpretación; la muestra real permanece pendiente. T06 no está cerrada y la autodetección
numérica aún no está implementada. El siguiente bloque es validar la fuente real y completar
agrupación/sustitución de lotes antes de T06A y T07.

La validación de esta entrega es local; no se han ejecutado Actions ni publicado una PR.
La evidencia se registra en `docs/evidencias/CONTINUIDAD_LOCAL_2026-10-06.md`.
Esta sección prevalece sobre los pendientes anteriores que resuelve expresamente.
## Estado vigente

| Entrega | Estado |
|---|---|
| T00–T04 y T02A | Cerradas e integradas |
| T05 | Cerrada e integrada en `main@99c9dec289cb749094f6036c6b3f17bb3841e855`; la antigua PR #14 está fusionada |
| T06.1 | Cerrada e integrada por PR #17 en main@2da40fa76108a6edef0492de2ce67dfbad9fa72a; aceptación Windows y UTC/Madrid aprobadas |
| Selección manual de columnas | Integrada por PR #18 en main@e6ba1a9ce7233646577f95a74cc41fc7a4902960; CSV/XLSX, vista previa y validación; la revisión de cuenta obligatoria se acepta con CI de su HEAD |
| Confirmación de importes de periodo | Implementada desde main@776532e4a5d7b51c0ff3e1ade33b8a4826d9104c; formato v4, preparación, confirmación atómica, recursos/enlaces e inputs v2; aceptación del HEAD registrada en la PR asociada |
| Columnas Debe/Haber | Implementadas sobre main@933f854a9223cd70494c4c3d3a89712c72f60153; selección explícita, Debe menos Haber, originales separados, aprobación y guardado de periodo; aceptación del HEAD registrada en la PR asociada |
| Resto de T06.2–T06.6 y T06A | Pendientes las modalidades cierre/diferencia de acumulados, agrupación, sustitución de lotes y puente FX; no se declara T06 completa |

La rama de cierre parte de `audit/t06-import-hardening-20261006@8e193c9d5e909f7c9be34532f2dfdbe331e45f16` y conserva su historia. El bloque corrige compilación, recuperación de modelos/maestros, formatos y tipos de fecha XLSX, errores controlados de libros corruptos, precisión de seriales y semántica de auditoría del estudio. También añade límites técnicos de lectura, lectura conjunta del workspace y aviso visible/reintento de preferencias.

La evidencia del cierre T06.1 está en [AUDITORIA_CIERRE_2026-10-06.md](docs/evidencias/AUDITORIA_CIERRE_2026-10-06.md). La PR #17 conserva los runs definitivos y el recibo de main acredita el commit integrado. El selector posterior se documenta en [IMPORTACION_COLUMNAS_2026-10-06.md](docs/evidencias/IMPORTACION_COLUMNAS_2026-10-06.md). Las suites de los commits anteriores no sustituyen la aceptación de cada HEAD.

## Decisiones y alcance

D01/D02 aceptadas. D03 sigue pendiente de un archivo real representativo aprobado; la aceptación técnica usa fuentes sintéticas. La UI exige una referencia de ejemplo comprobado y aprobación explícita del significado de importe de periodo, signos y moneda para cada lote. D04–D07 siguen como propuestas y D08/D09/D12 diferidas. A07/I14 conserva FX, particiones y ejecución pendientes en T06A/T07/T12; los recursos de importación en moneda funcional ya se persisten.

La especificación canónica v0.4 y las fixtures históricas se conservan. Las 39 referencias matemáticas son referencias sintéticas; no certifican un motor de reparto implementado. El catálogo monetario completo de producto, el motor económico posterior y el soporte de fuentes contables productivas siguen pendientes.

## Entregas y referencias

- [T05](docs/tareas/T05.md) y [evidencia T05](docs/evidencias/T05_2026-10-05.md).
- [T06](docs/tareas/T06.md), [directrices](docs/directrices/T06_SIGUIENTE_FASE.md) y [ADR-0005](docs/adr/ADR-0005-import-staging-profile-v1.md).
- Fuentes canónicas [R07](docs/evidencias/R07_2026-10-05.md).
- [Confirmación de importación](docs/evidencias/IMPORTACION_CONFIRMADA_2026-10-06.md) y [ADR-0006](docs/adr/ADR-0006-importacion-periodo-v4.md).
- [Columnas Debe/Haber](docs/evidencias/IMPORTACION_DEBE_HABER_2026-10-06.md) y [ADR-0007](docs/adr/ADR-0007-importacion-debe-haber.md).

## Registro histórico hasta el 5 de octubre

Las secciones siguientes conservan las comprobaciones y bloqueos en la fecha de cada entrega. Sus referencias a PR abiertas, falta de SDK o bloqueo de T05 están superadas por el estado vigente y no describen la situación actual.

## T02A.1 — Money y metadatos monetarios

Completada y fusionada mediante PR #6.

Resultado:
- Money con moneda explícita;
- catálogo fixture versionado y snapshots;
- JPY quantum 1, KWD quantum 0,001;
- I13 rechaza suma entre monedas;
- materialización WPF y CI verdes.

Evidencia: `docs/evidencias/T02A_SUB1_2026-10-04.md`.

## T02A.2 — fechas canónicas, IClock y zonas

Implementado y verificado:

- `BusinessDate` canónico `yyyy-MM-dd`;
- `UtcInstant` canónico UTC;
- `IClock` en Application y `SystemClock` en Infrastructure;
- `TimeZoneResolver`;
- I08: cambiar zona no muta fecha de negocio ni identidad UTC;
- I09: hora ambigua bloquea con `TIME_LOCAL_AMBIGUOUS`;
- hora inexistente bloquea con `TIME_LOCAL_INVALID`;
- selección explícita de offset para hora ambigua;
- guard CI contra lecturas directas de tiempo ambiente en Domain/Calculation/Application;
- materialización WPF de fecha canónica, UTC y vista local.

Evidencia de referencia:
- run `37229521659`;
- commit `16b03415bfd802d06e95fa45761f163b8ffb09ac`;
- build: success;
- **47/47 tests**;
- Runner temporal: success;
- publish autocontenido: success;
- WPF smoke: success;
- cuatro capturas WPF generadas y revisadas.

Evidencia: `docs/evidencias/T02A_SUB2_2026-10-04.md`.

## T02A.3 — RateSet y conversión FX congelada

Completada y verificada:

- `FxRate` y `FxRateSetVersion` explícitos y versionados;
- quote destino por unidad origen, positivo, con fecha y fuente;
- conversión por importe a escala 8 con MidpointToEven;
- original, convertido, RateId/RateSet y `ConversionRoundingAdjustment` preservados;
- C33–C37 verificados;
- I14: un RateSet nuevo genera conversión/hash nuevos sin mutar históricos;
- `PrecisionPolicy` propagada por la conversión;
- `IFxRateProvider` fuera del motor y guard CI contra acceso de red en Calculation;
- `RevalueStudy` valida antes de invocar el puerto de commit; las transacciones SQLite y revisión persistente siguen pendientes de integración;
- diagnósticos FX localizados EN/ES;
- editor WPF de cotización, fecha y fuente con vista original/funcional;
- navegación por teclado y evidencia visual WPF verificadas.

Evidencia de referencia:
- run `37272274792`;
- commit `ccf5b0bfe3b5f2eaa2af2063bff5fb88f69e4ea7`;
- **82/82 tests**;
- referencias matemáticas: **39/39**;
- Runner T02A.3: success;
- guard sin red en Calculation: success;
- publish y smokes WPF: success;
- 4 capturas visuales generadas.

Evidencia: `docs/evidencias/T02A_SUB3_2026-10-05.md`.

## Decisiones vigentes

- D01/D02 aceptadas.
- D03 pendiente de archivo representativo.
- D04-D07 propuestas.
- D08/D09/D12 diferidas.
- No se ha convertido ninguna propuesta económica pendiente en regla por conveniencia técnica.

## Límites actuales

No se declara implementado todavía:

- catálogo monetario completo de producto;
- importación/cálculo sobre SQLite (el archivo de empresa T03 ya está implementado);
- importación contable;
- motor económico de repartos posterior.

## Correcciones de auditoría — A01–A06 CERRADAS

Implementadas serialización canónica versionada, huellas FX v2 inequívocas, IDs válidos, edición FX regional tipada, cuantización exacta, diagnóstico bajo quantum y colecciones defensivas. RevalueStudy utiliza Revision long y valida sus entradas.

Código comprobado: `881e666fe67ecbd0654a7c796bfe5446b97afa23`.
CI: [run 37298097480](https://github.com/JavierRB35/VALDRIO/actions/runs/37298097480).
**114/114 tests**, 0 fallos/omitidas; 0 warnings/errores; 12 comprobaciones del botón WPF FX.
Se conserva la suite previa, las referencias canónicas y las decisiones vigentes.

Evidencia: [cierre de auditoría](docs/evidencias/AUDITORIA_CIERRE_2026-10-05.md).

**A07 sigue pendiente de integración persistente**, con obligaciones asignadas a T03/T05/T06A/T07. No se declaran probadas transacciones del estudio mediante mocks.

## Continuidad

T02A está cerrada. **T03 — CERRADA**: archivo real, 132/132 pruebas Windows, 24 checks de archivos WPF y 12 FX. Evidencia base CI 37313004940 aprobada; el cierre se integra únicamente con checks aprobados del último commit de PR #12. T04 se completa en la sección siguiente; T05 está implementada con cierre pendiente de CI; T06 dispone de directrices.

[Directrices ejecutables para el equipo de desarrollo](docs/directrices/T03_SIGUIENTE_FASE.md), con subentregas, matriz F01–F18 y prompt de inicio.

## T03 — CERRADA

Archivo de empresa SQLite v1, política completa, unidades, auditoría, bloqueo de editor, transacciones y copias/restauración. Panel WPF conectado a los mismos puertos de Application. [Ficha T03](docs/tareas/T03.md) · [ADR-0002](docs/adr/ADR-0002-archivo-empresa.md).

RevalueStudy sigue pendiente de integración persistente de estudio; T03 certifica el archivo y la transacción básica. D03 y D04–D07 no cambian.

Evidencia: [T03](docs/evidencias/T03_2026-10-05.md). T04 cerrada; cierre integrado únicamente con checks aprobados del último commit de PR #13.

## T04 — CERRADA

Esquema v2, upgrade seguro a copia, centros físicos, modelos con nodos/pools, publicación inmutable y clon editable. 153/153 tests en Windows y local; 21 verificaciones T04. CI de cierre 37329759565 aprobada: 53 checks WPF de modelos (29 habilitación + 24 funcionales), 24 de archivos y 12 FX. Artefactos/SQLite verificados independientemente. El cierre visual incluye lista de nodos y guard de secciones sin solapamiento; el último commit documental se integra solo con sus checks aprobados.

[Ficha T04](docs/tareas/T04.md) · [ADR-0003](docs/adr/ADR-0003-modelos-y-esquema-v2.md) · [Evidencia T04](docs/evidencias/T04_2026-10-05.md).

Plan original posterior: [T05 — estudios, revisiones y snapshots](docs/directrices/T05_SIGUIENTE_FASE.md), implementada en la sección siguiente. D03 y propuestas económicas sin cambio; A07 mantiene integración FX/particiones/publicación pendiente.

## T05 — IMPLEMENTADA, CIERRE PENDIENTE DE CI

[PR #14](https://github.com/JavierRB35/VALDRIO/pull/14), rama t05/studies. Formato v3, upgrade a nueva copia desde v1/v2, Study separado con revisión/auditoría propia, moneda/periodo/modelo/escenario explícitos, snapshots contextuales inmutables, hashes económico/completo, WPF y runner. Catálogos y políticas pendientes no se aprueban automáticamente.

Código 56c38f9: CI 37342742981 aprobada, 183/183 pruebas, 67 checks WPF de estudios, ocho capturas visibles y archivos SQLite/manifest revisados; conserva 53 modelos, 24 archivo y 12 FX. La inspección visual identificó el texto regional del DatePicker sin refresco. La corrección 56a068a compila con 0 avisos/errores y la suite local final pasa 183/183. Su nuevo recorrido WPF está pendiente: Actions terminó antes de arrancar trabajos, incluso al reintentar, sin pasos ni runner. Causa no confirmada; no se declara T05 cerrada ni se integra con checks de otro commit.

La evidencia detalla cómo verificar el HEAD final y cerrar. Se conserva una fixture genuina v3 de desarrollo para la siguiente migración. T05 no incorpora aún recursos, actividad, ejecuciones ni motor de reparto; A07/I14 esperan T06A/T07/T12 por su alcance.

[Ficha T05](docs/tareas/T05.md) · [ADR-0004](docs/adr/ADR-0004-estudios-y-snapshots-v3.md) · [Evidencia y bloqueo](docs/evidencias/T05_2026-10-05.md).

Siguiente fase: [T06 — sumas y saldos verificables](docs/directrices/T06_SIGUIENTE_FASE.md). Directrices completas con subfases T06.0–T06.6 y F01–F24; ejecutar tras cerrar T05. D03 requiere muestra/ejemplos antes de confirmar una fuente real. D04–D07 siguen como propuestas.

## Reauditoría y continuidad vigente — 2026-10-05

Main permanece en `fe01e10231568332c1df59c0b8df43081a5dac01`, con T04 integrada. El código T05 auditado es `d2e499fcf315a1aee5b0ed8b8d4e90f5a1bee6b9`; sus 245 blobs coinciden con la copia verificada. Única PR abierta, #14, conservada en borrador; ninguna incidencia abierta.

Reejecutadas 183/183 pruebas, 0 fallos/omitidas; 71/71 pruebas de archivos/estudios/R01 en cada proceso UTC y Europe/Madrid. Compilación completa incluida WPF con 0 avisos/errores; 57 fuentes canónicas, 39 referencias racionales, 169 claves ES/EN y los cuatro runners anteriores correctos. Esto no ejecuta WPF en Linux.

El aviso de GitHub aportado por el propietario identifica el agotamiento de minutos de la cuenta, coherente con los fallos de los runs 37345202045/37345211857 antes de arrancar. No se han iniciado nuevos reintentos. La actualización conserva intactos los criterios de cierre y añade `.github/scripts/Verify-Windows.ps1`, compartido con CI, para reproducirlos en Windows.

Pendientes de cierre T05.4/T05.5: ejecutar el verificador sobre el HEAD final, obtener 73 checks de estudios y sus ocho PNG, validar manifests de models/studies y receipt real, inspeccionar fechas regionales/RTL, actualizar evidencia y entonces integrar PR #14 comprobando el SHA. La verificación nueva de Windows aún no se presenta como ejecutada. [Auditoría y pendientes concretos](docs/evidencias/AUDITORIA_ESTADO_2026-10-05.md).

T06 ha iniciado en una rama apilada sobre el HEAD final auditado de T05, sin modificar `t05/studies` ni `main`. La primera subentrega añade staging de origen CSV/XLSX con perfil explícito únicamente para fixtures sintéticas; no confirma recursos ni resuelve D03. A07/I14 conserva integración persistente FX/recursos/particiones/ejecución pendiente para T06A/T07/T12. No se aprueban D03–D12 ni se modifica la especificación por este avance.


## T06 — EN CURSO, T06.0/T06.1 inicial

Rama `t06/import`: conserva su historia desde `dacf2dd1`, aplica en `473a4d153c09da94dc462185a08769e6430189c6` los tres ajustes de compilación de la verificación independiente y se sincroniza con T05 endurecido hasta `75aec6d134c6ea996b5de695dde91069bc500476`. PR #14 continúa abierta/draft; `main` no se modifica.

Implementado en la primera subentrega: `SourceParseProfileVersion` inmutable/versionado, tipos de celdas/filas/procedencia, `IImportAdapter`, parser decimal explícito sin cultura ambiente y `CsvImportAdapter` con quoted fields, delimitadores citados, comillas escapadas, multiline, BOM y encoding declarado. Se preservan cuenta como texto, Unicode, lexical original y SHA-256 de bytes.

D03 permanece pendiente. El único perfil actual está marcado `SyntheticFixture`; no se afirma soporte de una fuente productiva. XLSX ya forma parte del staging T06.1. No existen todavía `PrepareImport` completo, confirmación, formato v4, recursos persistidos, WPF ni runner T06.

La suite T06 contiene quince métodos de prueba, incluida una integración SQLite que comprueba que extracción/inspección básica no cambia revisión/audit/snapshots del estudio. En este entorno no está disponible .NET y la descarga temporal del SDK fue bloqueada, por lo que esas pruebas **no se registran como ejecutadas**. Sí se comprobaron estáticamente cobertura EN/ES de las diecisiete claves T06, ausencia de cultura/tiempo ambiente y patrones prohibidos, delta limitado a T06 y conservación del blob histórico `t05-v3.json`. No se usó GitHub Actions.

[Ficha T06](docs/tareas/T06.md) · [ADR-0005](docs/adr/ADR-0005-import-staging-profile-v1.md) · [Evidencia T06](docs/evidencias/T06_2026-10-05.md).


## Correcciones de reauditoría B01/B02/G02 — IMPLEMENTADAS, VERIFICACIÓN PENDIENTE

Sobre el HEAD T05 posterior a `dacf2dd15ecf438f465ca3658cb06c6659c5fdd3` se incorporó la recuperación tras escritura confirmada, el aislamiento de fallos de persistencia de preferencias y el endurecimiento del verificador Windows.

- **B01:** el resultado confirmado de crear/guardar/cambiar estado/capturar se aplica antes de releer; un fallo posterior de lectura deja visible el dato confirmado y muestra `Study.CommittedRefreshPending`. Un refresco manual reconcilia `Current` cuando no hay edición pendiente y no sobrescribe una edición pendiente.
- **B02:** IO/acceso denegado al guardar preferencias no corta los demás suscriptores; se registra un aviso traducible y el archivo temporal usa nombre único con limpieza.
- **G02:** aceptación Windows exige repositorio limpio al inicio/fin, identidad de HEAD estable y manifest de build con SHA-256 de Desktop, Runner, assembly de tests y TRX. `-UseExistingBuild` rechaza binarios sin manifest coincidente.

`64aac29f` fue verificado externamente con 188/188 y recorridos reales B01. Después, G02 pasó a manifest v2 con cobertura recursiva de salidas y B01 añadió diez comprobaciones mantenidas con ViewModel + SQLite reales. Los commits posteriores `2a6b5e70`/`fffda702`/`75aec6d1` aún requieren ejecución .NET/Windows.


## Reparación de compilación T06 tras verificación independiente

La verificación independiente detectó tres ubicaciones que impedían compilar el HEAD T06 `b8b09ad2799c1471f62dafdd85e401d63e27886e`: declaración múltiple con `var` en el parser, posible `Worksheet` nulo y patrones `CellValues` no constantes. En una copia diagnóstica, cambiando únicamente esos puntos, la solución compiló sin avisos/errores y pasó 203/203.

El commit publicado `473a4d153c09da94dc462185a08769e6430189c6` aplica exactamente esos tres ajustes, sin cambiar reglas económicas, fixtures ni expected. El entorno de publicación sigue sin SDK .NET; por honestidad, los 203/203 se atribuyen a la copia diagnóstica equivalente y no todavía a este SHA remoto.

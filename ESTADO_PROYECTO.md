# ESTADO_PROYECTO

Fecha de revisión 2026-10-06. Repositorio `JavierRB35/VALDRIO`.

## Estado vigente

| Entrega | Estado |
|---|---|
| T00–T04 y T02A | Cerradas e integradas |
| T05 | Cerrada e integrada en `main@99c9dec289cb749094f6036c6b3f17bb3841e855`; la antigua PR #14 está fusionada |
| T06.1 | Lectura CSV/XLSX y staging implementados; correcciones E01–E07 y N01–N05 en validación final de Windows |
| T06.2–T06.6 | Pendientes; no existen todavía PrepareImport completo, ConfirmImport, recursos persistidos ni UI/runner T06 |

La rama de cierre parte de `audit/t06-import-hardening-20261006@8e193c9d5e909f7c9be34532f2dfdbe331e45f16` y conserva su historia. El bloque corrige compilación, recuperación de modelos/maestros, formatos y tipos de fecha XLSX, errores controlados de libros corruptos, precisión de seriales y semántica de auditoría del estudio. También añade límites técnicos de lectura, lectura conjunta del workspace y aviso visible/reintento de preferencias.

La evidencia vigente está en [AUDITORIA_CIERRE_2026-10-06.md](docs/evidencias/AUDITORIA_CIERRE_2026-10-06.md). El cierre de T06.1 requiere CI Windows aprobada y artifacts atribuibles al último HEAD antes de integrar. Las suites de los commits anteriores no sustituyen esa puerta.

## Decisiones y alcance

D01/D02 aceptadas. D03 sigue pendiente de un archivo real representativo aprobado; las fuentes de importación son sintéticas. D04–D07 siguen como propuestas y D08/D09/D12 diferidas. A07/I14 conserva la integración persistente de FX, recursos, particiones y ejecución pendiente en T06A/T07/T12.

La especificación canónica v0.4 y las fixtures históricas se conservan. Las 39 referencias matemáticas son referencias sintéticas; no certifican un motor de reparto implementado. El catálogo monetario completo de producto, el motor económico posterior y el soporte de fuentes contables productivas siguen pendientes.

## Entregas y referencias

- [T05](docs/tareas/T05.md) y [evidencia T05](docs/evidencias/T05_2026-10-05.md).
- [T06](docs/tareas/T06.md), [directrices](docs/directrices/T06_SIGUIENTE_FASE.md) y [ADR-0005](docs/adr/ADR-0005-import-staging-profile-v1.md).
- Fuentes canónicas [R07](docs/evidencias/R07_2026-10-05.md).

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

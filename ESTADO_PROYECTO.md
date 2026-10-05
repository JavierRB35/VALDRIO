# ESTADO_PROYECTO

Fecha de revisión: 2026-10-05  
Repositorio: `JavierRB35/VALDRIO`

## Fuentes

El paquete `VALDRIO_Paquete_Desarrollo_v0_4.zip` está disponible e íntegro.

- SHA-256: `1d8f8c21e3386117426f0eede257508a0bae30ed8c48cec7d2fb0e8ac68d7ced`.
- 59 entradas.
- `sha256sum -c SHA256SUMS.txt`: OK.
- referencias matemáticas globales: **39/39**.
- **R07 — CERRADA** mediante PR #9; fuentes canónicas v0.4 versionadas y verificadas. Evidencia: `docs/evidencias/R07_2026-10-05.md`.

## Estado de tareas

- **T00 — CERRADA**.
- **T01 — CERRADA**.
- **T01A — CERRADA**.
- **T02 — CERRADA**.
- **T02A — CERRADA**: Money/metadatos, tiempo canónico y RateSet/FX completados y verificados.

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
- `RevalueStudy` valida antes de commit atómico;
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
- SQLite productivo;
- importación contable;
- motor económico de repartos posterior.

## Continuidad

T02A está cerrada. La siguiente tarea por dependencia es **T03 — archivo de empresa y SQLite real**; no se declara iniciada en este estado.

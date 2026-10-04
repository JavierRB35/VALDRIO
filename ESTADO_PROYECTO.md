# ESTADO_PROYECTO

Fecha de revisión: 2026-10-04  
Repositorio: `JavierRB35/VALDRIO`

## Fuentes

El paquete `VALDRIO_Paquete_Desarrollo_v0_4.zip` está disponible e íntegro.

- SHA-256: `1d8f8c21e3386117426f0eede257508a0bae30ed8c48cec7d2fb0e8ac68d7ced`.
- 59 entradas.
- `sha256sum -c SHA256SUMS.txt`: OK.
- referencias matemáticas globales: **39/39**.

## Estado de tareas

- **T00 — CERRADA**: alcance confirmado; D01/D02 aceptadas.
- **T01 — CERRADA**: solución .NET 10, CI Windows, Runner, publish autocontenido y shell WPF.
- **T01A — CERRADA**: internacionalización de presentación desde fundamentos, preferencias independientes y evidencia visual.
- **T02 — CERRADA**: unidades/IDs, decimal canónico, PDEC-1, BalancedRound, CurrencyProjection y corpus de redondeo.
- **T02A — EN CURSO**: subentrega 1 de Money/metadatos monetarios completada; fechas y FX pendientes.

## T02A.1 — resultado comprobado

Implementado:

- `Money` con código ISO y rechazo de suma entre monedas;
- `CurrencyDefinition` versionada;
- puerto `ICurrencyCatalog`;
- adaptador JSON para el fixture monetario del paquete;
- `CurrencyMetadataSnapshot` congelado;
- `StudyCurrencyContext` con moneda funcional por estudio;
- `MoneyCurrencyProjector` con quantum derivado de minor units;
- materialización WPF de JPY/KWD.

Catálogo de prueba exacto del paquete:

- EUR 978 / 2;
- USD 840 / 2;
- JPY 392 / 0;
- KWD 414 / 3.

El fixture **no** se presenta como catálogo completo de producto.

Evidencia de código e integración:

- run `37228651535`;
- commit `0ca27458b029157b777c9cd6ba62ff4653a0bdb8`;
- build: success;
- **39/39 tests**;
- guard sin `double/float`: success;
- Runner T02 de regresión: success;
- Runner T02A: success;
- publish autocontenido: success;
- WPF smoke: success;
- cuatro capturas WPF generadas y revisadas.

Runner T02A:

- JPY quantum 1 → 34/34/33;
- KWD quantum 0,001 → 0,001/0/0;
- I13 → `MONEY_CURRENCY_MISMATCH`.

## Decisiones vigentes

- D01/D02 aceptadas.
- D03 pendiente de archivo representativo.
- D04-D07 propuestas.
- D08/D09/D12 diferidas.
- PDEC-1 validada técnicamente.
- La implementación monetaria no convierte ninguna propuesta económica pendiente en regla aceptada.

## Límites actuales

No se declara implementado todavía:

- catálogo monetario completo de producto;
- T02A.2: fechas canónicas, `IClock` y tratamiento de horas ambiguas/inexistentes;
- T02A.3: RateSet, C33-C37, I14 y revalorización explícita;
- SQLite productivo;
- importación contable;
- motor económico de repartos posterior.

## Continuidad

Primera subentrega pendiente cuyas dependencias están verificadas:

**T02A.2 — DateOnly ISO, UTC, IClock y TimeZoneResolver**.

Debe cubrir I08 e I09 y no convertir fechas de negocio en instantes.

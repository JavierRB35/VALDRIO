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

## T02 — evidencia

Run final de código: `37226867814`  
Commit: `da2432de10e17a3bc4902a785fd90cea3fea9cd1`

- build: success;
- **32/32 tests**;
- guard sin `double/float` en Domain/Calculation: success;
- Runner T02: **5/5 referencias** C07, C08, C21, C38, C39;
- publish self-contained: success;
- WPF smoke: success;
- 4 capturas WPF generadas y revisadas.

Materialización:

- ejemplo C07 consumido desde Application;
- ViewModel sin fórmula económica/redondeo;
- muestra ID, bruto, ajuste de redondeo y adjudicado;
- presentación ES/EN y región/zona independientes conservadas.

## Decisiones vigentes

- D01/D02 aceptadas.
- D03 pendiente de archivo representativo.
- D04-D07 propuestas.
- D08/D09/D12 diferidas.
- PDEC-1 queda validada técnicamente en T02, sin promover ninguna política económica pendiente.

## Límites actuales

No se declara implementado todavía:

- catálogo monetario completo;
- FX/RateSet;
- contratos temporales completos de T02A;
- SQLite productivo;
- importación contable;
- motor económico de repartos posterior.

## Continuidad

Primera tarea pendiente cuyas dependencias están verificadas:

**T02A — Money, monedas, fechas canónicas y tipos congelados**.

Dependencias T02 y T01A: satisfechas.

T02A deberá empezar por su primera subentrega: Money/CurrencyDefinition versionada, catálogo monetario y snapshot de minor units, preservando la separación empresa → moneda predeterminada / estudio → moneda funcional.

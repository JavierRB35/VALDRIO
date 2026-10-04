# ESTADO_PROYECTO

Fecha de revisión: 2026-10-04  
Repositorio: `JavierRB35/VALDRIO`

## Fuentes

El paquete `VALDRIO_Paquete_Desarrollo_v0_4.zip` está disponible e íntegro.

- SHA-256: `1d8f8c21e3386117426f0eede257508a0bae30ed8c48cec7d2fb0e8ac68d7ced`.
- 59 entradas.
- `sha256sum -c SHA256SUMS.txt`: OK.
- referencias matemáticas: **39/39**.

## Estado de tareas

### T00 — CERRADA

El propietario confirmó el alcance y la separación entre modelo, estudio, ejecución y presentación internacional.

Decisiones:
- D01 **ACEPTADA**.
- D02 **ACEPTADA**.
- D03 pendiente de archivo representativo antes de importación definitiva.
- D04-D07 permanecen propuestas hasta sus puertas económicas.
- D08/D09/D12 se resolverán en hitos previstos y no bloquean trabajo independiente.

Precisión monetaria aceptada:
- empresa propone moneda predeterminada;
- cada estudio fija moneda funcional;
- originales pueden conservar otras monedas;
- conversión explícita;
- tipos congelados por ejecución.

### T01 — CERRADA

Solución .NET 10, CI Windows, Runner, publish autocontenido y shell WPF revalidados contra la ficha v0.4.

### T01A — CERRADA

Implementado:
- inglés neutral + español;
- cobertura/placeholders en CI;
- idioma, región y zona independientes;
- preferencias observables y persistidas en perfil local;
- cambio de idioma sin reinicio;
- `Language`/`FlowDirection` WPF;
- previsualización USD/fecha/instante;
- cultura capturada durante edición;
- pseudotraducción >=40 %;
- RTL de prueba;
- navegación por teclado.

Evidencia final:
- run `37224023831`;
- commit `fbfa77699fce3ee33bb672ee2b456117183cf530`;
- build success;
- **16/16 tests**;
- Runner success;
- publish self-contained success;
- WPF smoke success;
- 4 capturas WPF generadas y revisadas;
- artefacto visual ID `11311466457`.

El run visual permitió detectar un defecto de formato de instante en una iteración anterior; se corrigió antes del cierre y quedó cubierto por prueba.

## Decisiones económicas vigentes

- D01/D02 aceptadas.
- D03 pendiente.
- D04-D07 propuestas.
- D08/D09/D12 diferidas.
- No se han convertido propuestas económicas en reglas por conveniencia técnica.

## Límites actuales

No se declara implementado todavía:

- motor económico;
- primitives completas de T02;
- Money/FX de T02A;
- SQLite productivo;
- importación contable.

## Continuidad

Primera tarea pendiente cuyas dependencias están verificadas:

**T02 — Unidades, decimal, política y redondeo**.

Dependencia T01A: satisfecha.

T02 deberá mantener internacionalización desde fundamentos y no introducir `double` para importes ni redondeos implícitos de moneda.

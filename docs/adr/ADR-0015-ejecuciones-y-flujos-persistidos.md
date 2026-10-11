# ADR-0015 — Ejecuciones y flujos persistidos T12

Fecha: 2026-10-11. Rama: `feature/t12-cost-flow-history`, base `main@e888693`.

## Decisión y dependencias comprobadas

T09 aporta hechos primarios y pendientes. T10 aporta Q/B, solución recíproca y
circulaciones por grupo. T11 aporta publicación terminal, puentes por recurso,
ajustes y vínculos de explicación. Se reutilizan estos contratos y algoritmos;
no se introduce otro solver ni datos de relleno en la aplicación.

El formato 10 añade CalculationRuns. Cada fila guarda entradas completas,
políticas, revisión, versión del algoritmo, resultados, transferencias brutas,
explicaciones y diagnósticos en JSON canónico con hash. Los importes son decimal,
no REAL de SQLite. Los estados son Started, Completed, Failed y Cancelled.
Una ejecución finalizada no se actualiza ni elimina (triggers).

PrepareRun captura entradas mediante el contrato existente y registra el intento.
La captura y el registro tienen transacciones separadas: puede quedar una captura
sin intento si falla el segundo paso, nunca un resultado parcial. Calculate
reutiliza T09–T11 fuera de la transacción de escritura. PublishRun publica todo
el resultado en una transacción y permite conservar un resultado histórico si
la revisión ha cambiado. Cancelar no publica resultados económicos parciales.
Una interrupción de publicación deja Started, recuperable como intento inconcluso.

## Actualidad y compatibilidad

RunHistory.CurrentRunId se deriva de la última ejecución completada de la revisión
actual y versión de algoritmo compatible. No se rellena el campo legado
StudyDefinition.CurrentRunId: su contrato/auditorías y restricción histórica se
conservan. Los consumidores de T12 deben consultar IRunStore.ReadRuns.
Una ejecución fallida posterior no invalida un resultado completo de la misma
revisión. Un cambio de revisión hace históricos todos los resultados anteriores.

Los hashes históricos v7 no se reescriben. El hash económico T12 añade las claves
secundarias publicadas y el mapa parte/grupo que no cubría el hash legado.
FullSnapshotHash conserva la huella completa del recibo. Los datos históricos
se leen del snapshot de la ejecución, nunca de las reglas actuales.

La actualización de archivos antiguos conserva el original y crea una copia,
como las migraciones previas. Los cierres T11 anteriores permanecen en paso 7:
no se presentan como ejecuciones T12 ni se recalculan para fabricar un historial.

## Lectura y representación

ReadRuns usa una transacción de lectura independiente de Workspace: no llama a
ValidateAllocations ni al solver. Valida hash, alcance y conciliación persistida.
La apertura general del archivo conserva la validación histórica T11 existente;
esta decisión no elimina ese mecanismo de integridad previo.

El paso 8 muestra cuatro etapas con los colores y controles globales. Los nodos
se identifican y ordenan por centro. Flechas y nodos son botones accesibles; las
selecciones filtran la tabla. La barra horizontal superior permite consultar el
gráfico ancho sin reducir la letra en ventanas estrechas. Las tablas tienen su
propio desplazamiento y ordenación. La selección de otra ejecución no escribe.

Los flujos auxiliares son brutos por grupo. Se materializan durante Calculate
usando las proporciones canónicas Q/B y el bruto T10; abrir el gráfico no deriva
transferencias nuevas. Los costes finales son los hechos publicados de T11.
La tabla final conserva la procedencia por recurso y los ajustes numéricos y de
redondeo. No se enumeran caminos infinitos de circuitos ni se afirma que exista
un ledger de cada transferencia auxiliar por recurso: ese contrato no lo aporta
T11. Las filas brutas se identifican como todos los recursos del grupo; los
vínculos origen → principal sí están disponibles por recurso.

## Límites explícitos

- Alcance R0: no añade metodología R1 de capacidad u objetos finales ausente.
- Un fallo de solver muestra diagnóstico y hechos primarios disponibles, sin
  inventar importes finales. Directos, exclusiones y pendientes se ven en el puente.
- No se altera PDEC-1 ni se considera aprobada la propuesta numérica A05/PNUM-1.
- Gráficos grandes requieren desplazamiento horizontal; no se certifica una
  disposición sin cruces para cualquier grafo ni rendimiento a escala ilimitada.
- La fecha del selector identifica el instante UTC; el idioma/región solo cambia
  presentación, no hash o importes.

## Validación

Ver `docs/evidencias/t12-flujos-20261011/README.md` para comandos y resultados
locales, capturas WPF y fixture ficticio de 6.500 EUR. No se ejecutan Actions.

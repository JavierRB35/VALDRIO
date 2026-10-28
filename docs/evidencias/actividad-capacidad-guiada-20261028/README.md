# Actividad y capacidad: refactorización guiada

Fecha: 28/10/2026. Base: `9945bfdd55a4b10fbf356b23506aae4b985b4a83`.
Rama: `refactor/activity-capacity-guided-20261028`.

## Alcance autorizado y resultado

Se implementa la auditoría aportada por el propietario: separar cantidades, estado y procedencia; precargar únicamente información disponible y compatible; hacer opcionales las narrativas; mostrar detalles progresivamente; guardar o confirmar la propuesta en una única operación. Se mantienen las fórmulas de costes y las restricciones de unidad, ámbito, revisión, estudio cerrado, cantidades y precisión.

La interfaz principal contiene contexto, capacidad normal, actividad real, ayuda inline propia, procedencia resumida y estado concreto. «Revisar cálculo» abre calendario/histórico/manual; «Origen, notas e historial» permanece cerrado inicialmente. Las notas de normal cero, actividad superior a normal y corrección aparecen solo cuando corresponde. El aviso de actividad superior a la referencia se muestra en el flujo principal y no recorta cantidades.

El usuario puede modificar la capacidad propuesta. Eso pasa a entrada manual y los siguientes recálculos no la sustituyen. Reemplazar una cifra existente con una propuesta exige seleccionar «Usar esta propuesta al guardar». No hay botón intermedio de adopción. Las cantidades se muestran con la unidad física y su precisión, sin convertir 415,8 en 416 silenciosamente. Tras guardar, el estado y los botones reflejan los datos confirmados o pendientes.

## Inventario antes/después

| Función existente | Tratamiento |
|---|---|
| Normal y real | Dos entradas principales independientes; no se deduce real desde calendario. |
| Capacidad declarada | Se usa la misma entrada principal, sin duplicación. |
| Calendario y frecuencia de paradas | Conservados en panel secundario, con etiquetas independientes, ayuda y validación. |
| Histórico e inclusión/exclusión | Conservados; selección inicial compatible, notas opcionales y versiones de origen. |
| Fuente, calidad, descripciones | Fuentes generadas o editables opcionalmente; tipo de dato inferido; notas separadas. |
| Motivos obligatorios | Eliminación autorizada de obligatoriedad en cálculo, guardado y reapertura; se conservan las notas existentes. |
| Teórica/práctica | Calculadas con calendario; entradas avanzadas conservadas para otros métodos. |
| Guardar borrador / confirmar | Persisten propuesta y evidencia en la misma operación; borrador admite ausencia de cantidades. |
| Anular / importar / historial | Conservados en la vista general y sus flujos existentes. |
| Solo lectura / conflictos | Conservados. No se modifica estudio cerrado ni se fuerza una versión concurrente. |
| ES/EN, teclado, ayuda completa | Microtextos localizados, controles etiquetados y guías 09/10 actualizadas en ambos idiomas. |

## Servicios, DTOs y persistencia

La aplicación es WPF/.NET con SQLite local; no existe un frontend web ni una API HTTP que alimentar. Los contratos de aplicación son `IPhysicalActivityWorkspace.ReadCurrent`, `ReadReferences`, `Save` y `ReadHistory`. Se reutilizan sus comprobaciones transaccionales y versionado. No se introduce un servidor ni endpoints innecesarios.

`PhysicalSuggestionContext` delimita empresa, modelo, nodo, pool, unidad y período. `PhysicalActivitySuggestions` devuelve una referencia de calendario anulable y períodos comparables. `PhysicalCalendarDefaults` expresa como anulables los parámetros desconocidos. `PhysicalCalendarSegment` y `CalendarSegments` permiten sumar tramos no solapados usando el cálculo físico existente; el editor proporciona el tramo de su período. Los estados de validación y visibilidad se exponen como propiedades del editor, sin almacenarlos como hechos económicos.

La inferencia excluye referencias futuras, ámbitos incompatibles, versiones anuladas y duplicidades/solapamientos entre períodos. Recupera turnos, horas, recursos y factor de un calendario anterior compatible; no copia días ni paradas totales de otro mes. Las tasas de paradas por turno/día pueden proponerse, indicando el período de origen. Los datos actuales se recuperan de la observación del análisis; una edición manual cambia la procedencia a manual y revertir la cifra restaura su procedencia original.

La ausencia de fuentes introducidas manualmente se registra explícitamente como entrada manual, sin afirmar respaldo documental. Se preservan los límites de longitud. Las narrativas dejan de ser obligatorias, también al validar la reapertura del archivo. Cada corrección sigue generando una versión inmutable con actor, instante y valores anteriores/nuevos. No se cambia el formato canónico de las observaciones antiguas ni sus hashes; la lectura del calendario recupera la evidencia invariante ya persistida.

## Validación

Se incluyen pruebas nuevas de confirmación/corrección sin narrativas y reapertura, guardado de propuesta con actividad desconocida, confirmación posterior, precisión, prevalencia de edición manual, estados condicionales, ámbito de inferencia, duplicidades, calendarios por tramos, precarga sin copiar días/real y solo lectura. Se actualizan dos expectativas antiguas que exigían notas, sustituidas por cobertura positiva de persistencia y auditoría.

Compilación Release final: 0 advertencias y 0 errores. Batería completa: 795 pruebas correctas. Tras los últimos ajustes y dos casos adicionales: 53 pruebas específicas correctas; tras renombrar campos: 21 pruebas de ayuda/localización correctas. No hubo fallos ni omisiones en estas ejecuciones finales. Los contadores se registran en `test-summary.json`. El recorrido WPF real usa una copia desechable, entra en controles por teclado, comprueba cálculo en vivo, errores de paradas, revelado progresivo y guardado sin narrativas en español e inglés. Las capturas adjuntas muestran los estados principales, el cálculo y un tamaño estrecho. No se usa producción para estas pruebas.

## Límites explícitos

- El repositorio no aporta un calendario maestro operativo, catálogo de festivos, planificación efectiva por fechas ni un conector de partes reales. No se inventan esos datos. Se recupera evidencia disponible y se solicita únicamente el parámetro aún desconocido. El soporte de tramos está probado en el servicio; no se afirma que exista una fuente externa conectada que los suministre automáticamente.
- La importación física existente conserva su revisión de filas y duplicados; no se agregan automáticamente archivos externos desconocidos. No se añade un sistema de adjuntos que la pantalla no tenía.
- Las capturas y comprobaciones WPF no certifican todos los monitores, DPI físicos o lectores de pantalla. La navegación real de los controles se comprueba en ambos idiomas.
- Integración solicitada: avance rápido de `main` local tras las pruebas. En el repositorio principal apareció trabajo paralelo sin guardar en `fix/study-period-name-validation`; se preservan esa rama y sus archivos, actualizando únicamente la referencia `main` mediante una transferencia Git local sin forzar. El SHA definitivo se entrega en la respuesta de cierre. No se hace push, no se lanzan Actions y el gasto provocado de Actions es 0 USD.

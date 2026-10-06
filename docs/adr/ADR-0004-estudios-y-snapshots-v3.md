# ADR-0004 — Estudios, formato v3 y snapshots contextuales

Fecha: 2026-10-05. Alcance T05. Decisión técnica; las propuestas económicas no cambian de estado.

## 1. Formatos históricos

`VALDRIO-COMPANY` pasa a esquema 3. `CompanyDbContext/V1Model` y `ModelDbContext/V2Model` conservan sus modelos y snapshots. Se permite heredar del contexto v2 para reutilizar sus conjuntos, pero su mapping no se amplía. `StudyDbContext/V3Model/StudyDbContextSnapshot` es el contexto vigente y tiene migración propia `202610050003_StudiesAndSnapshots`. Las tres configuraciones se comprueban con `HasPendingModelChanges`, sin suprimir advertencias.

Crear un archivo aplica las migraciones v1, v2 y v3 exclusivamente en el temporal propio. Abrir v1/v2 informa `FILE_UPGRADE_REQUIRED`. `Upgrade` acepta v2→v3 y v1→v2→v3: reserva origen y destino, obtiene copia consistente mediante SQLite, rechaza locks de migración ajenos, migra la copia, valida y promueve sin sobrescribir. El original mantiene bytes, identidad, maestros, publicaciones y eventos.

Se conservan fixtures realmente producidos por las interfaces T03/T04, con origen/commit/run/SHA y compresión sin pérdida. `t04-v2.json` contiene publicado/clon; `t04-upgraded-v2.json` contiene el evento original 1→2. No se reconstruyen archivos antiguos con el mapping nuevo.

La procedencia de formato es un agregado `CompanyFile` con revisiones sucesivas desde 0. Cada paso contiene `Before=(from,from,migración anterior)` y `After=(from,from+1,migración nueva)`. Un origen v2 creado directamente inicia 2→3 en revisión 0; uno actualizado desde v1 conserva 1→2/revisión 0 y añade 2→3/revisión 1. Una ruta directa v1→v3 registra ambos pasos. No se duplica Revision=0 ni se aumenta Company.Revision.

## 2. Study y contratos

Study tiene UUID/empresa propios, nombre del cliente, periodo inclusivo `BusinessDate/DateOnly`, finalidad `management/R1-management-v1`, escenario, modelo publicado, moneda/metadatos/vigencia aplicados, revisión `long`, estado Open/Closed y fecha de creación UTC. Los escenarios persistidos son Actual=0, Budget=1 y WhatIf=2; estados Open=0 y Closed=1. Las etiquetas se traducen; los códigos y datos del cliente se conservan.

`ParentStudyId`, `FxRateSetVersionId` y `CurrentRunId` son explícitamente null en este contrato. La duplicación de escenarios, la conversión persistida y las ejecuciones necesitan sus módulos; no se crean interfaces vacías ni resultados de relleno. Los recursos, FX aplicado, actividad y consumos se representan mediante colecciones tipadas vacías. Una entrada no vacía se rechaza en `context-only-t05-v1`; una ampliación económica necesita esquema nuevo.

La moneda del archivo es una sugerencia. Una empresa USD puede tener un estudio EUR o JPY. Crear o cambiar a otra moneda consulta el catálogo y congela metadatos y vigencia; ambas fechas deben estar dentro de esta. Cambiar otros campos conserva la definición monetaria aplicada, aunque se actualice el catálogo instalado. MinorUnitDigits procede del catálogo. El catálogo distribuido sigue siendo una fixture de desarrollo; no es el catálogo internacional completo comercial.

El modelo seleccionado debe estar publicado y pertenecer a la empresa. La aplicación comprueba esa regla en transacción; FK compuestas y triggers de integridad la refuerzan. Los triggers no tienen fórmulas de costes. Seleccionar un publicado cuyos maestros actuales se hayan desactivado sigue usando su definición histórica congelada.

`ICompanyFileSession.Studies` expone `IStudyWorkspace`: Create, UpdateContext, ChangeState, Capture, ReadStudies y ReadSnapshots. Comparte vida, modo y puerta de serialización de la sesión. La UI y el runner no reciben filas EF, conexiones ni DbContext.

## 3. Revisiones y confirmación

Crear comienza en revisión 0 y confirma estudio/audit juntos. UpdateContext aumenta una vez la revisión, incluidos cambios administrativos de nombre; la revisión identifica el estado auditado, no su equivalencia económica. Cerrar/abrir es una operación explícita con revisión. El cierre bloquea edición de contexto y conserva consulta histórica. Company, unidades, centros y modelos mantienen sus contadores separados.

Las mutaciones comparan ExpectedRevision dentro de transacción y escriben datos, revisión y audit de forma indivisible. Los checkpoints prueban fallos después de SaveChanges real y cancelación inmediatamente antes de commit. Se comprueba otra conexión tras reabrir. Una captura no aumenta Study.Revision ni cambia CurrentRunId.

Crear usa un StudyId asignado por el llamador: repetir la solicitud original con el mismo actor/datos devuelve el estudio existente en su estado actual; reutilizar el ID con otros datos falla. Un update con revisión obsoleta falla. Capture exige RequestId: es el SnapshotId, y repetirlo con empresa/estudio/revisión/actor idénticos devuelve la misma foto, incluso si el estudio cambió después; otra combinación falla. Un ID nuevo permite otra captura.

Después de confirmar una mutación, la UI refresca con un token nuevo no cancelado. Una cancelación tardía no se presenta como anulación de lo ya confirmado. El DatePicker utiliza calendario y texto de solo lectura, incluido después de habilitar/deshabilitar su plantilla; la edición pendiente no reinterpreta texto al cambiar región. Guardar/descartar/cancelar se aplica antes de cambiar estudio, archivo o cerrar la ventana; los editores de empresa/modelo/estudio se bloquean mientras otro módulo escribe.

## 4. Captura coherente e inmutable

Una única transacción lee la definición de estudio, política decimal completa y publicación congelada del modelo, construye el input, calcula ambas huellas, guarda la foto y su evento `StudySnapshot/revisión 0`. No consulta preferencias ni maestros vigentes para reemplazar los aplicados. Las colecciones son defensivas, ordenadas por UUID; los valores de dominio son inmutables.

StudySnapshots conserva IDs/ámbito, InputRevision, UTC de captura, esquema, JSON y ambas huellas. Update/Delete se bloquean con triggers; no existe una operación pública de edición. Reabrir valida receipt, envelope, definición histórica de la revisión, modelo/política aplicados y cadena de auditoría. Backup/restauración mantienen esas mismas referencias y bytes canónicos.

`IsCurrentFor` compara ámbito, revisión y contexto congelado. Una foto obsoleta permanece consultable. Esta es actualidad de **entradas**, no de un cálculo: no existen CalculationRuns ni TerminalCostFacts en T05. Las reglas de actualidad y compatibilidad de resultados corresponden a T12.

## 5. JSON y perímetros de hash

El input usa envelope propio `valdrio-study-input-v1` y preparación `context-only-t05-v1`; la proyección económica tiene contrato `valdrio-study-economy-v1`. El codec canónico base `valdrio-values-v1` sigue siendo reutilizable para valores/auditorías, sin convertirlo en el esquema de motor completo.

Campos obligatorios, tipos/moneda/unidad/ámbito validados, arrays vacíos explícitos, propiedades ordinales y SHA-256 UTF-8 sin BOM. Decimales usan strings invariantes. El round-trip tipado rechaza campos ausentes, desconocidos, duplicados, null inválido y defaults sintetizados. Se acepta cambiar orden de propiedades JSON; las listas contractuales emitidas se ordenan por ID y el JSON recibido debe respetar ese orden canónico. No se consulta cultura/hora de proceso ni se generan IDs al canonicalizar.

| Campo aplicado | EconomicInputHash | FullSnapshotHash |
|---|---|---|
| Empresa/ámbito, periodo inclusivo, finalidad/política, escenario | Sí | Sí |
| Moneda, código numérico, MinorUnitDigits, IsMonetary, CatalogVersion y vigencia | Sí | Sí |
| Fuente/fecha de publicación y comprobación del catálogo | No | Sí |
| Política decimal completa y versiones de preparación/esquema | Sí | Sí |
| ModelVersionId y método/políticas de capacidad | Sí | Sí |
| IDs/centro físico/rol/funciones/actividad de nodos | Sí | Sí |
| IDs/nodo/unidad/clasificación/política de pools | Sí | Sí |
| ID/dimensión/escala/discreto de unidades aplicadas | Sí | Sí |
| Códigos, nombres, símbolos y descripciones de maestros/modelo | No | Sí |
| Familia/linaje/número de versión y revisiones administrativas de maestros | No | Sí |
| StudyId, nombre, revisión, estado, fecha de creación | No | Sí |
| SnapshotId/RequestId, UTC de captura, actor | No | Sí |
| Resources/AppliedFx/Activity/Consumptions (vacíos explícitos en T05) | Sí | Sí |
| Idioma/región/zona/orden de presentación | No | No |

La identidad seleccionada del modelo y de sus objetos económicos pertenece al hash económico: dos versiones con IDs distintos no se declaran equivalentes automáticamente. Un nombre distinto con el mismo periodo/modelo/moneda sí mantiene EconomicInputHash. FullSnapshotHash incluye la procedencia de cada foto, de modo que dos capturas distintas pueden tener la misma economía y distinto hash completo. StructuralHash T04 no se usa como sustituto.

## 6. Preparación, UI y runner

La preparación deriva diagnósticos de políticas Pending y ausencia de recursos/actividad/consumos; un estudio cerrado añade su diagnóstico. Se presenta `Context captured`, no ReadyForCalculation, ejecución completada o coste cero. La lista de ejecuciones está vacía y lo indica explícitamente.

WPF permite crear/cargar contexto, elegir publicado, moneda y escenario, guardar, cerrar/abrir y capturar/consultar fotos. Los DatePickers tienen texto de solo lectura y selección por calendario: cambiar región reformatea SelectedDate sin interpretar texto pendiente. IO fuera del hilo WPF; textos/estados/diagnósticos EN/ES; datos del cliente preservados. La configuración de preferencias permanece fuera del archivo.

`Runner --study-snapshot ruta` lee un receipt exportado, valida input/huellas sin SQLite o red y devuelve IDs, periodo, moneda, huellas y preparación. CI lo ejecuta sobre la foto realmente generada por WPF, además de una prueba de proceso que rechaza una huella corrupta. Este runner no ejecuta repartos.

## 7. Alcance internacional y límites

I05: contexto/hash/preferencias verificados; ledger, residuos y reproducción de resultados esperan al motor. I08: periodo fijo/UTC/zona verificados, incluido UTC cercano a medianoche con distinta fecha local. I14: la congelación FX base continúa probada en T02A; integración de originales, RevalueStudy y ejecuciones persistidas permanece en T06A/T07/T12. No se afirma que T05 complete esas etapas mediante mocks.

La validación de reapertura recorre los agregados/históricos disponibles. No se promete escala ilimitada; T23 debe medir volumen y paginación sin perder trazabilidad. Auditoría y hashes detectan incoherencia, no constituyen firma o cifrado frente a un atacante que controle el archivo. D08/D09 y la distribución comercial siguen en T24.

## 8. Fuentes técnicas

Se contrastó el uso de snapshots/migraciones y constructores requeridos con documentación primaria de Microsoft; las garantías del diseño se comprueban con los tests propios:

- https://learn.microsoft.com/ef/core/managing-schemas/migrations
- https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/required-properties
- https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/immutability
- https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/DatePicker.cs — el cambio de Language actualiza FirstDayOfWeek; VALDRIO refresca además el texto desde SelectedDate.
- https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/DatePickerTextBox.cs — su handler de IsEnabledChanged vuelve a establecer IsReadOnly.

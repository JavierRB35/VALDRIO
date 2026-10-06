# VALDRIO — Directrices ejecutables para T05: estudios, revisiones y snapshots

Fecha: 2026-10-05. Documento de planificación de T05 posterior a T04, conservado como referencia. La implementación y evidencia vigentes están en [ficha T05](../tareas/T05.md) y [evidencia T05](../evidencias/T05_2026-10-05.md). La siguiente fase se guía por [T06](T06_SIGUIENTE_FASE.md). No convierte propuestas económicas en reglas aceptadas.

## 1. Resultado visible

Desde un archivo v2, el usuario selecciona una **versión de modelo publicada**, crea un estudio con nombre, periodo, moneda funcional y escenario, lo guarda y vuelve a abrir. Ve su revisión, lo que falta para calcular y un historial de ejecuciones inicialmente vacío. Puede preparar y guardar una fotografía tipada/determinista de sus entradas actuales, con hashes completos y económicos diferenciados, y reproducir esa fotografía en el runner.

No crear una ejecución Completada ni resultados económicos ficticios para rellenar el historial. T05 prepara datos; T06–T22 incorporarán recursos, actividad, cálculo y publicación de resultados. Un snapshot de demostración no es un cálculo ni una validación de conservación monetaria del motor completo.

Ejemplo obligatorio: empresa en USD, estudio en EUR, interfaz inglesa/es-ES/Europe-Madrid, periodo 2026-10-01–2026-10-31 y modelo industrial T04 con SETUP/lote, PROCESO/hora y auxiliar MANT/hora. Moneda del estudio se elige explícitamente; no se deduce de idioma, empresa o región. Pasar a español/en-US/Los-Angeles no cambia el periodo ni los hashes.

## 2. Lectura y base de partida

Leer ESTADO_PROYECTO, evidencia y ficha T04, ADR-0002/0003, registro vigente `docs/decisiones/DECISIONES_PENDIENTES.md`, prompt maestro v0.4, ficha canónica T05, contratos 04 y CM15/CM16, documento internacional e I05/I08/I14. Comprobar commit remoto, rama y CI; no sobrescribir trabajo ajeno. No editar especificación v0.4 ni SHA256SUMS.

Conservar .NET 10/C#/WPF/MVVM/SQLite local, un editor, puertos de Application y Domain/Calculation puros. T04 ofrece ICompanyFileSession.Models, maestros, modelos publicados congelados, ModelFamily/linaje, auditoría por agregado y actualización a archivo nuevo. No utilizar el DbContext histórico v1 como contexto de estudios.

D01/D02 aceptadas: finalidad de gestión R1; inventario/PdC T32; un editor local. D03 requiere muestra contable antes de T06. D04–D07 continúan propuestas; D10/D11 no se aprueban por publicar estructuras. D08/D09 antes de T24. T05 no necesita esos datos para crear estudios, pero debe mostrar la preparación económica pendiente.

## 3. Resolver el esquema antes de las pantallas

T04 congela v2. Añadir **v3** y migración nueva para estudios/snapshots, con snapshot/mapping actual propios. No modificar V1Model/InitialCompanyFile ni V2Model/VersionedModels para introducir columnas futuras. Mantener lectura/validación tipada de payloads anteriores.

Extender el registro de formatos con rutas conocidas: v2→v3 y, si se admite, v1→v2→v3 en una copia nueva. Siempre origen intacto, API SQLite consistente, temporal propio, bloqueo/reserva, validación, promoción sin sobrescritura. Registrar cada paso de procedencia sin duplicar el evento CompanyFile inicial de T04: **evolucionar primero su revisión/handler**, hoy valida exactamente un upgrade 1→2. No forzar otro evento con Revision=0 ni omitir migraciones para ajustar la prueba.

Ensayar archivos realmente producidos por T03 y T04, conservando SHA original, publicados, clones y auditorías. Versiones futuras y EF locks ajenos se rechazan antes de migrar. Cada contexto debe coincidir con su snapshot; no EnsureCreated ni supresión de advertencias.

## 4. Contratos y agregado Study

| Dato | Regla materializada |
|---|---|
| Id, CompanyId | UUID propio y ámbito de empresa; FK compuesta donde corresponda |
| Name | Texto requerido; códigos/etiquetas del cliente no se traducen |
| PeriodStart, PeriodEnd | BusinessDate/DateOnly gregoriano; ISO en disco; inicio ≤ fin; límites inclusivos documentados |
| Purpose | Código de finalidad de gestión y referencia de política; no activar inventario por defecto |
| ScenarioKind | Real/Presupuesto/WhatIf con códigos estables y etiquetas localizadas; no implementa fórmulas diferentes por sí solo |
| ParentStudyId | Opcional, misma empresa; describe origen, sin compartir entradas mutables |
| SelectedModelVersionId | Versión publicada de la misma empresa; jamás un borrador ni “último modelo” dinámico |
| FunctionalCurrencyCode | Elección explícita, monetaria y válida en catálogo; diferente de DefaultCurrency permitido |
| CurrencyCatalogVersion | Referencia y metadatos aplicados congelados en snapshots; no minor digits editable |
| FxRateSetVersionId | Referencia opcional cuando existen originales; no inventar tipos ni conversiones para un estudio vacío |
| Revision | long propio; inicial 0; cada mutación económica valida ExpectedRevision y aumenta una vez |
| CurrentRunId | null en T05; un futuro ID debe pertenecer al estudio y a ejecución compatible/completada |
| State | Estado abierto/cerrado explícito; no un IsCalculated manual |

Separar Study de Company, maestros, ModelVersion y CalculationRun. Un rename de centro no aumenta Study.Revision ni cambia un snapshot previo. Editar periodo/modelo/moneda requiere una transacción y nueva revisión; no modifica el modelo publicado. Moneda con recursos ya existentes requiere revaloración completa en su tarea de integración, no cambiar solo el código.

Documentar qué cambios pertenecen al hash económico (periodo, modelo/políticas, moneda y entradas) y cuáles solo al snapshot completo (nombre descriptivo, procedencia, actor si se captura). Un nombre de estudio no cambia un importe; si aumenta revisión por auditoría administrativa, indicar esa regla y no fingir que la revisión equivale al hash.

## 5. Operaciones y transacciones

Puertos tipados CreateStudy, UpdateStudyContext, ReadStudies, CaptureStudySnapshot y ReadSnapshots. Si se entrega CloneScenario, exigir una subentrega independiente con copia real de datos disponibles, IDs/remapeo, ParentStudyId y pruebas. No añadir interfaces vacías ni simular recursos todavía inexistentes.

Cada comando incluye CompanyId/StudyId/ExpectedRevision/Actor y campos autorizados. La UI no construye graphs EF ni puede trasplantar agregados de empresa. CreateStudy confirma estudio, revisión inicial y audit juntos. UpdateStudyContext valida dentro de transacción coherente, incluidos modelo publicado y moneda.

Extender AuditEvents con AggregateType=Study y handler de payload conocido; Revision es Study.Revision. Company/PhysicalCenter/ModelVersion siguen independientes. Failure injection después de escritura real debe revertir estado/revisión/auditoría; comprobar otra conexión tras reabrir. Reintentos tienen resultado definido, sin duplicar snapshots/eventos/estudios accidentalmente.

Captura: leer contexto, publicación y todas las entradas disponibles en una transacción; copiar valores defensivos; canonicalizar y guardar snapshot identificado con revisión/hashes, sin mezclar varias lecturas de revisiones distintas. Preferencias no se consultan. Guardar una fotografía no aumenta revisión económica ni crea costes. Si se calcula fuera de la lectura en tareas futuras, volver a comprobar revisión al publicar. Un cambio posterior no borra la foto ni la presenta como actual.

## 6. Snapshot tipado y versionado

Crear un esquema de contexto T05, por ejemplo `valdrio-study-input-v1`, distinto del sobre básico `valdrio-values-v1` y de `valdrio-model-structure-v1`. Evitar una bolsa string/object, JsonElement arbitrario o un serializer que recupere campos ausentes mediante defaults.

La foto de T05 contiene StudyContext, moneda funcional y CurrencyMetadataSnapshot, política decimal **completa**, PublishedModelStructure congelada, referencias de método/capacidad explícitamente pendientes, versiones de serialización y preparación, y procedencia real disponible. Colecciones de recursos, FX aplicado, actividad y consumos pueden ser **vacías explícitas** porque sus módulos todavía no existen; distinguir vacío de campo ausente y “no evaluado” de “cero”. No rellenar con importes o capacidades sintéticas un estudio del usuario.

Publicar políticas aceptadas futuras requerirá esquema/validadores propios. La instantánea histórica no debe deserializarse aplicando los nuevos defaults. No leer el nombre del centro ni las unidades de maestros actuales al preparar una foto de un modelo publicado: utilizar su publicación congelada.

El runner puede demostrar hashes/compatibilidad con una foto contextual vacía y referencias FX sintéticas separadas. No declarar I05 completo en cuanto a ledger/residuos/histórico de ejecución antes de existir esos componentes. I08 sí debe probar periodo/UTC/zona. I14 demuestra congelación FX a nivel de contrato y queda pendiente de la integración persistente con originales/particiones y ejecución.

## 7. EconomicInputHash y FullSnapshotHash

Definir dos proyecciones tipadas, con contrato listado campo a campo. Usar SHA-256 sobre UTF-8 canónico sin BOM y números decimales como strings invariantes.

| Elemento | EconomicInputHash | FullSnapshotHash |
|---|---|---|
| Periodo, propósito/política y escenario económicamente relevante | Sí | Sí |
| Moneda funcional, metadatos aplicados y versión de precisión | Sí | Sí |
| Modelo/políticas/configuración económica congelada | Sí | Sí |
| IDs económicos de partes/nodos/pools y unidades | Sí | Sí |
| Importes originales/funcionales, aplicaciones FX, ajustes y rutas disponibles | Sí, según relevancia explícita | Sí |
| Procedencia, archivos, filas y perfil original | Solo si afectan interpretación económica | Sí |
| Etiquetas/nombres administrativos, actor o comentario | No si no alteran semántica | Sí si están en la foto |
| Fecha de ejecución o de captura | No | Solo si se define parte de la envoltura; nunca se consulta al calcular |
| Idioma, región de pantalla, zona, orden UI | No | No |

Acordar y documentar política para StudyId/Revision: sirven para actualidad/identificación, pero no deberían hacer económicamente distintas dos entradas equivalentes por un contador administrativo. Separar identidad de envoltura y proyección económica. No reutilizar StructuralHash de T04 como hash económico; incluye etiquetas congeladas para identificación estructural y su perímetro es diferente.

Ordenar colecciones por IDs estables, etiquetas por ordinal, propiedades por nombre ordinal; objetos sin orden semántico se normalizan, secuencias de operaciones con orden semántico lo conservan. Rechazar IDs duplicados, claves JSON duplicadas, campos desconocidos/ausentes y desajustes unidad/moneda antes de guardar. Probar round-trip, firma del contrato y versiones desconocidas, sin convertir JSON defectuoso en arrays vacíos.

No usar GetHashCode, serialización dependiente de CultureInfo, CurrentCulture, Dictionary sin ordenar o GUID generado durante la canonicalización. Mismo input congelado produce mismos bytes/hashes en Windows y Linux bajo UTC/Madrid. La hora IClock sirve para auditoría/identidad de captura; no decide entradas económicas.

## 8. Preparación y actualidad

Resultado de preparación contiene SnapshotId, StudyId, InputRevision, EconomicInputHash, FullSnapshotHash, esquema y diagnósticos. Distinguir `Captured` de `ReadyForCalculation`. En T05 las políticas y datos pendientes impiden ReadyForCalculation; presentar mensajes concretos y un historial de ejecuciones vacío.

Reservar CalculationRun completo para T12/publicación correspondiente: Started/Completed/Cancelled/Failed, revisión de entrada, hashes/versiones y estado actual derivado. No insertar resultados por CaptureStudySnapshot ni marcar CurrentRunId por pulsar Guardar. Las tarjetas de “actual” futuras comprobarán revisión, hashes y compatibilidad; un snapshot antiguo sigue consultable.

A07 permanece parcialmente pendiente: no declarar PublishRun transaccional ni RevalueStudy persistente completados con mocks. Mantener las obligaciones de T06A/T07/T12 y documentar exactamente lo que T05 ya ha comprobado con archivo en disco.

## 9. Interfaz y fechas

Pantallas/lista de estudios, contexto editable, selector solo de publicados, fechas de periodo, moneda explícita, escenario y botón Capturar snapshot. Mostrar revisión, modelo/version/ID, hashes y preparación. Consultar snapshots anteriores por identidad, sin regenerarlos con maestros o políticas vigentes.

Capturar el formato de edición de fechas; cambiar formato con texto pendiente no debe reinterpretar 01/02/2026. Elegir DatePicker tipado o editor con perfil capturado y decisión guardar/descartar/cancelar. Las fechas de periodo no reciben TimeZoneInfo. Instantes se muestran con PresentationFormatter y zona seleccionada. Usar importes reales solo cuando sus tareas existan; no inferir moneda por CultureInfo.

IO fuera del hilo WPF, cancelación antes de commit, empresa activa intacta si falla. Textos/diagnósticos EN/ES, argumentos tipados junto al campo/entidad, accesibilidad y teclado. Recorrido real con botones, consultas independientes SQLite, capturas EN/es-ES, ES/en-US, de-DE, expansión/pseudo y RTL. Preferencias permanecen fuera del archivo de empresa.

## 10. Subentregas y puertas

| Bloque | Entrega | Puerta |
|---|---|---|
| T05.0 | ADR de formatos/hashes, migración v3 y actualización segura | V1/v2 reales conservados; modelo EF sin cambios pendientes; procedencia válida |
| T05.1 | Study y operaciones de contexto | Periodo/moneda/modelo/ámbito/revisión/auditoría en SQLite real |
| T05.2 | Snapshot contextual tipado y dos proyecciones canónicas | Round-trip estricto, campos listados y hashes deterministas |
| T05.3 | Captura coherente e historial de fotos | Persistencia inmutable, conflicto/cancelación/rollback, ninguna ejecución ficticia |
| T05.4 | WPF + runner + evidencia | Flujo completo, criterios internacionales por su alcance y CI del commit final |
| T05.5 | Cierre y directrices T06 | Decisiones/datos pendientes identificados, muestra D03 requerida antes de importación productiva |

No anunciar un bloque terminado con entidades sueltas o métodos que devuelvan constantes. Una sesión puede cerrar una subentrega probada y registrar continuidad; no presentar T05 entera como finalizada si no existen los flujos.

## 11. Matriz mínima S01–S20

| ID | Caso | Evidencia exigida |
|---|---|---|
| S01 | Actualizar v2 T04 y v1 T03 a nuevo v3 | SHA origen, identidad/modelos/publicaciones/auditoría intactos |
| S02 | Fallo/cancelación/futuro/lock en actualización | Ningún destino parcial ni escritura al origen |
| S03 | Crear estudio EUR en empresa USD | Elección explícita, catálogo aplicado y reapertura |
| S04 | Periodo invertido/fechas inexistentes | Diagnóstico, ninguna fila/auditoría parcial |
| S05 | Modelo borrador o de otra empresa | Puertos y FK compuestas rechazan |
| S06 | Cambiar periodo o modelo | Study.Revision +1; resto de agregados sin aumento |
| S07 | ExpectedRevision obsoleto | Ningún cambio/revisión/audit duplicado |
| S08 | Fallo tras primera escritura Create/Update/Capture | Otra conexión verifica rollback completo |
| S09 | Cancelar inmediatamente antes de commit | Datos previos; después del commit no se finge cancelación |
| S10 | Reordenar entradas/propiedades | Bytes y hashes idénticos; IDs preservados |
| S11 | EN/ES, es-ES/en-US/de-DE/en-GB, UTC/Madrid | Hashes/snapshot persistente idénticos; presentación cambia |
| S12 | I08 Madrid/Los-Angeles con UTC próximo a medianoche | Periodo ISO igual; instante mismo; fecha local puede diferir |
| S13 | Cambiar periodo/modelo/moneda/política aplicada | Hash económico cambia cuando corresponde |
| S14 | Procedencia distinta con economía equivalente | Hash completo distinto; económico igual según contrato |
| S15 | JSON ausente/desconocido/duplicado/tipo inválido | Rechazo tipado; ninguna reparación/default silencioso |
| S16 | Cambiar/desactivar maestro tras capturar | Publicación y foto históricas conservan definiciones anteriores |
| S17 | Snapshot vacío contextual | Diagnósticos pendientes, ningún CalculationRun/CurrentRunId ficticio |
| S18 | WPF guardar→capturar→cerrar→reabrir→consultar | Botones reales y lectura independiente del archivo |
| S19 | Backup/restauración con snapshots | Identidad, hashes, datos y auditoría intactos |
| S20 | CI del commit final | Suite anterior+nueva, corpus/SHA, TRX, artifact, evidencia y directrices T06 |

Añadir pruebas de escala/familias/escenarios si el diseño introduce nuevas garantías. Los límites económicos de I05/I14 deben quedar escritos; no cambiar esperados del corpus para esconder que sus etapas todavía no existen.

## 12. Prompt para el LLM ejecutor

> Ejecuta T05 en JavierRB35/VALDRIO desde el main comprobado con CI. Lee estado, evidencia T04, ADR-0002/0003, decisiones vigentes, prompt maestro/ficha T05, contratos 04, CM15/CM16 e internacionalización. No modifiques fuentes canónicas.
>
> Empieza por T05.0: v3 con migración/mapping nuevos y upgrade a copia nueva. Conserva v1/v2 y evoluciona correctamente el handler de procedencia; nunca migres un archivo futuro/ajeno ni borres locks ajenos. Prueba archivos realmente producidos por T03/T04.
>
> Implementa Study separado de maestros/modelo/ejecución, con periodo DateOnly, escenario, finalidad de gestión, moneda explícita, modelo publicado, Revision long y auditoría propia. Todas las mutaciones comparan revisión y confirman datos/revisión/audit juntos.
>
> Define snapshot T05 tipado/versionado y proyecciones EconomicInputHash/FullSnapshotHash campo a campo. Fotografía coherente, datos congelados, arrays vacíos explícitos para módulos pendientes, políticas Pending sin defaults aprobados, JSON estricto y SHA-256 UTF-8 ordenado. No uses preferencias ni hora de ejecución como entrada económica. StructuralHash no sustituye esos hashes.
>
> Persiste y consulta fotos inmutables; demuestra preparación pendiente e historial de ejecuciones vacío. No inventes cálculo/publicación de resultados o RevalueStudy persistente con mocks. Integra WPF y runner usando puertos; prueba botones y archivos reales. Conserva idioma/región/zona/moneda independientes, cancelación y empresa activa al fallar.
>
> Completa S01–S20, conserva todas las pruebas/corpus previos, y registra alcance real de I05/I08/I14. D03 sigue necesitando muestra contable antes de T06; no aceptes D04–D07 por conveniencia técnica. Cierra solo con CI aprobada del último commit, artifact/evidencia, estado actualizado y directrices T06 con modalidad/signos/perfiles pendientes.

# Datos, operaciones y límites de integración

Versión 0.4. Especificación de diseño, sin código compilado. El LLM implementará el esquema mediante migrations y contratos C# coherentes con estos campos. Podrá simplificar detalles físicos preservando semántica, claves, revisiones y pruebas.

## 1. Convenciones comunes

IDs UUID estables, comparación ordinal por representación canónica para desempates. Una empresa por archivo en R1, moneda funcional por estudio y originales en monedas identificadas. Fechas de periodo DateOnly ISO y auditoría UTC. Importes, tasas y cantidades en decimal canónico, guardados TEXT y JSON como string. Money contiene Amount y CurrencyCode; tarifas identifican moneda y unidad. Revision long generada por la aplicación. No SQL Server rowversion ni cálculo sobre REAL.

R1 tiene un pool homogéneo por nodo lógico. Un centro físico puede agrupar varios nodos, por ejemplo setup y proceso, cada uno con driver, capacidad y reglas propios. Esto evita sumar horas, unidades y lotes en un denominador. T26 puede crear actividad/pools adicionales conservando esta homogeneidad.

Los FK se validan también por ámbito. Se usan claves/índices compuestos cuando sea necesario para impedir una línea que relacione objetos de modelos distintos. No basta con que exista cada UUID por separado. La desactivación de maestro conserva históricos; no se borra en cascada un objeto que participa en modelo publicado o ejecución.

## 2. Esquema mínimo por agregado

### Empresa y catálogos

Company contiene Id, Name, DefaultFunctionalCurrencyCode y CreatedAtUtc. CurrencyDefinition contiene AlphaCode, NumericCode, MinorUnitDigits?, Validity, IsMonetary y CatalogVersion. La escala de moneda viene del catálogo; no un CurrencyDecimals editable que contradiga ISO. UnitDefinition contiene Id, Code, Dimension, Symbol, DecimalScale, IsDiscrete y factores explícitos aprobados. PhysicalCenter contiene Id, Code, Name y estado. Product contiene Id, Code, Name, OutputUnitId y estado. Los snapshots congelan definiciones aplicadas.

### Modelo de costes

ModelVersion contiene Id, CompanyId, VersionNumber, Status, ParentVersionId, Description, Revision, PublishedAtUtc y MethodPolicyId. CalculationNode contiene Id, ModelVersionId, PhysicalCenterId, Code, Role, FunctionTags y estado. CostPoolDefinition contiene Id, ModelVersionId, NodeId, DriverUnitId, CapacityPolicy, CostLayer, Description. ModelVersion+NodeId tiene un pool en R1.

AllocationKeyVersion contiene Id, ModelVersionId, Name, Stage, Mode, UnitId, SelfAllocationPolicy y PublishedAtUtc. AllocationKeyLine contiene KeyId, DestinationNodeId, WeightOrPercentage y SortId estable. Restricción de destino único por clave. PrimaryRule contiene Id, ModelVersionId, SelectorKind, SelectorValue, Priority y KeyVersionId; selección explícita por parte puede prevalecer sobre reglas automáticas solo con precedencia documentada. SecondaryRule contiene ModelVersionId, AuxiliaryNodeId, ComponentRoutingGroup y KeyVersionId; origen único por grupo.

RecipeVersion contiene Id, ModelVersionId, ProductId, VersionNumber, OutputUnitId, BatchSize, Status. RecipeLine contiene Id, RecipeVersionId, ComponentKind, ComponentId, UnitId, Quantity, ConsumptionBasis, WastePolicy y DiscreteQuantityPolicy. Las tarifas no se guardan en una receta como valor vigente editable; una valoración registra la ejecución que suministra cada tarifa.

### Estudio y origen contable

Study contiene Id, CompanyId, Name, PeriodStart, PeriodEnd, Purpose, ScenarioKind, ParentStudyId, SelectedModelVersionId, FunctionalCurrencyCode, CurrencyCatalogVersion, FxRateSetVersionId?, Revision, CurrentRunId y estado de cierre. ImportBatch contiene Id, StudyId, FileHash, SourceParseProfileSnapshot, BalanceMode, PeriodDates, Status y ConfirmedAtUtc. ImportedRow conserva texto original, ParsedMoney, ParsedDate?, RateApplicationId? y diagnóstico; Profile captura cultura y formatos del origen.

Resource contiene Id, StudyId, OriginKind, ImportBatchId, AccountCode, Description, OriginalMoney? si hay un solo original homogéneo, ConvertedOriginalMoney y RateApplicationId? si hay una sola aplicación. ResourceSourceLink mantiene RowId, OriginalMoney, FunctionalMoney y RateApplicationId por fila. Un recurso agregado con varias monedas/tipos deriva ConvertedOriginalMoney desde sus enlaces; la vista original presenta totales por moneda y no un Money nominal combinado. ResourceAdjustment conserva OriginalDeltaMoney, FunctionalDeltaMoney, RateApplicationId?, Type, Reason, ReversalOfId y AuditEventId. EffectiveFunctionalAmount se deriva de convertido original más ajustes funcionales.

ResourcePart contiene Id, ResourceId, FunctionalMoney, Behavior, BehaviorDriverUnitId, BehaviorHorizon, ActivityRange, RoutingKind, CostLayer, CapacityTreatment, CapacityOwnerPoolId y PrimaryRuleOverrideId. DirectAssignment contiene FunctionalMoney en la misma base del estudio. Exclusion contiene PartId y Reason. La aplicación valida particiones y monedas en la transacción del agregado.

### Actividad, capacidad y objetos

ActivityObservation contiene Id, StudyId, PoolId, UnitId, Period, RealQuantity, Source y Quality. CapacityObservation contiene Id, StudyId, PoolId, UnitId, TheoreticalQuantity?, PracticalQuantity?, NormalQuantity?, CalendarDefinition, Source y Quality. Missing usa null; cero es decimal cero.

CostObject contiene Id, StudyId, Kind, Code, ProductId?, ParentTransferId? y DimensionBindings. ProductionOrder contiene Id, StudyId, ProductId, RecipeVersionId, Quantity, Status y ValuationRunId. ConsumptionFact contiene Id, StudyId, ObjectId, PoolId o SourcePartId, Quantity, UnitId, OccurredAt, Evidence, FactKind y ReversalOfId. Un hecho tiene un origen definido; no combina varios orígenes opcionales sin validación de exclusión.

SalesFact contiene Id, StudyId, ObjectId, Quantity, OriginalNetMoney, FunctionalNetMoney, RateApplicationId?, Source y ReversalOfId. Su conversión es explícita antes de comparar con coste funcional. Impuestos no se convierten automáticamente en ingreso neto o coste; su tratamiento depende de finalidad y política seleccionadas.

### Preferencias, FX y localización

UserPresentationPreferences se guarda fuera del snapshot económico y fuera de los valores compartidos de empresa. Incluye UiLanguageTag, FormatCultureTag, DisplayTimeZoneId y presets. LanguageCatalog incluye etiqueta, dirección, revisión y cobertura. Mensajes contienen MessageKey y TypedArguments, con Code estable; su texto no entra al solver.

FxRateSetVersion es inmutable tras publicar. FxRate contiene SourceCurrencyCode, TargetCurrencyCode, Quote, RateDate, Source y Version. FxRateApplication enlaza importe original, convertido bruto/adjudicado, ajuste y tipo. AccountingPolicyContext contiene finalidad, marco/jurisdicción opcionales y versión. ExportContext identifica idioma/cultura/fecha/zona, moneda funcional o proyección aprobada, precisión y plantilla.

### Snapshot y resultados

CalculationRun contiene Id, StudyId, InputRevision, ModelVersionId, EconomicInputHash, FullSnapshotHash, AlgorithmVersion, PrecisionPolicyVersion, MethodPolicyVersion, FxPolicyVersion, SnapshotSchemaVersion, Status, StartedAtUtc, CompletedAtUtc y IsCurrent. RunSnapshot contiene CanonicalJson, valores originales/funcionales, perfiles, tipos y metadatos congelados. FullSnapshotHash identifica procedencia completa; EconomicInputHash identifica valores, IDs, políticas y tipos relevantes para cálculo, excluyendo preferencias de presentación. Cambiar idioma no cambia ninguno; importar otra procedencia equivalente puede cambiar FullSnapshotHash sin cambiar resultado económico.

AllocationFact contiene RunId, Stage, SourcePartId, OriginPoolId, DestinationKind, DestinationId, ComponentKind, RawAmount, NumericAdjustment, PostedAmount, RoundingAdjustment y RuleVersionId. RawAmount decimal es una aproximación de cálculo; PostedAmount cuantizado es autoridad del ledger. PostedAmount = RawAmount + NumericAdjustment + RoundingAdjustment. ComponentKind separa fijo propio, variable observado, recibido evaluado y otros consumidos conservando Behavior.

RateResult contiene RunId, PoolId, ComponentKind, NumeratorCost, DenominatorQuantity, UnitId, CurrencyCode, Policy, RateValue?, RateStatus y DiagnosticId. CapacityVarianceFact contiene RunId, SourcePartId, CapacityOwnerPoolId, AssessmentId, NormalQuantity, RealQuantity, RawVariance y PostedVariance. AssessmentId identifica evaluación y no se genera de nuevo al transferir coste recibido.

TerminalCostFact contiene RunId, SourcePartId, ObjectKind, ObjectId, CostLayer, Behavior, CapacityAssessmentId?, RawAmount, NumericAdjustment, RoundingAdjustment, PostedAmount y ExplanationRootId. Reconciliation contiene RunId, Stage, ScopeId, InputPostedTotal, OutputPostedTotal, ExactDifference, NumericalResidual?, Status. ReciprocalDiagnostic contiene matriz/grupo, IDs, cobertura, grafos cerrados, residual, amplificación y umbrales de versión.

CurrencyProjection conserva RunId, SourcePartId, FrontierId, CurrencyMetadataSnapshot, SourceLedgerAmount, TargetAmount, ProjectionDifference y líneas proyectadas. Es una vista separada y no sustituye hechos ni ajustes económicos.

ExplanationLink relaciona hechos y operación/clave con atributos estructurados; las referencias deben sobrevivir a reapertura. AuditEvent registra entidad, operación, antes/después, usuario local, fecha y motivo. No guarda claves de licencia o contraseñas en texto.

## 3. Operaciones de aplicación

| Operación | Entrada mínima | Garantía y salida |
| --- | --- | --- |
| CreateCompanyFile | Ruta nueva, empresa y moneda | Crea archivo válido sin sobrescribir uno existente. |
| OpenCompanyFile | Ruta y modo | Comprueba esquema y bloqueo; abre lectura o edición identificada. |
| PublishModel | ModelId, ExpectedRevision | Valida grafo/configuración, publica atómicamente y devuelve versión inmutable. |
| CloneModel | PublishedModelId | Nueva versión editable con referencias de origen. |
| CreateStudy | Periodo, propósito y modelo | Estudio válido con revisión inicial. |
| PrepareImport | Archivo, SourceParseProfile, fechas, RateSet/selecciones | Previsualización sin cambios económicos. |
| ConfirmImport | PreparedImportId, ExpectedRevision, Mode | Lote y recursos completos o ningún cambio. |
| RevalueStudy | StudyId, RateSetVersionId, selecciones, ExpectedRevision | Recalcula puente funcional en revisión nueva y revalida partes/consumos; falla sin cambios si queda inconsistente. |
| ChangeResource | ResourceId, comando, ExpectedRevision | Particiones y ajustes consistentes, auditoría y nueva revisión. |
| SaveActivity | Pool, unidad, periodo, cantidad, ExpectedRevision | Distingue ausencia y cero; conserva evidencia. |
| PrepareRun | StudyId, ExpectedRevision, Policy | Snapshot coherente y diagnóstico de preparación. |
| Calculate | RunInput y cancelación | RunOutput determinista; sin IO ni publicación. |
| PublishRun | RunOutput, EconomicInputHash, FullSnapshotHash, ExpectedRevision | Snapshot y resultados completos; actual solo si revisión coincide. |
| ExplainAmount | RunId, FactId o objeto y frontera | Grafo de origen con totales y diferencias explícitas. |
| EstimateOrder | Orden, receta y RunId | Estimación diferenciada de consumo real. |
| CloseOrder | OrderId, ExpectedRevision, RunId | Valoración congelada y coherencia de hechos. |
| CloneScenario | StudyId, revisión de origen | Copia independiente y referencia de procedencia. |
| ExportRun | RunId, vista, ExportContext, ruta | Exporta una ejecución concreta sin fórmulas recalculadas de otra política. |
| CreateBackup | Archivo y destino | Copia consistente, manifiesto y comprobación de lectura. |
| RestoreBackup | Copia y ruta nueva | Valida primero; conserva el archivo anterior. |

No presentar estos casos de uso como API web. Son contratos internos. La UI los invoca con DTO tipados y recibe Result con errores de negocio. Las excepciones técnicas se registran y se traducen a un mensaje que explique si hubo o no cambios.

## 4. Snapshot de motor y algoritmo de ejecución

RunInput incluye StudyContext, FunctionalCurrencyContext, CurrencyMetadataSnapshot, AppliedFxSnapshot, ModelSnapshot, ResourceParts funcionales, AppliedPrimaryRules, SecondaryRoutingGroups, CapacityInputs, ActualActivityInputs, ObjectConsumptionInputs, DirectAssignments, PolicySet y versiones. El motor recibe valores serializables, sin proxies EF, DbContext, CultureInfo actual ni ViewModels. Conversión prepara los importes funcionales y conserva su trazabilidad; Calculation no consulta proveedores.

Orden de ejecución R1.

1. Validar contexto, unidades, moneda, particiones, claves y fronteras.
2. Aplicar primario y cuantizar cada parte a destinos; producir hechos y conciliación.
3. Para componentes de capacidad propios de auxiliares, separar servicio aplicado y variación una vez, si OwnerOnceCapacity está activo.
4. Construir grupos de matrices según rutas efectivas; comprobar grafo y factorizar.
5. Resolver contribuciones de servicio por origen; añadir terminales de capacidad previos y calcular secundario.
6. Formar componentes de principales con marcas de capacidad propia o previamente evaluada.
7. Calcular tarifas; aplicar a consumos reales; separar capacidad propia, actividad sin objeto y costes de periodo.
8. Añadir imputaciones directas y valorar transferencias dentro de la frontera elegida.
9. Cuantizar terminales por fuente y construir vistas, reconciliaciones y explicación desde esos hechos.
10. Devolver resultados y diagnósticos. La aplicación decide si el resultado completo puede publicarse como definitivo o provisional.

Cada cuantización intermedia crea hechos adjudicados cuya suma conserva la fuente. No se calcula un total final distinto al que resulta de las contribuciones. Las estimaciones usan un caso de uso separado para aplicar tarifas a recetas sin consumir ledger real.

## 5. Publicación, fallo y revisión

Capturar snapshot mediante lectura coherente y guardar hash/revisión. Calcular fuera de bloqueo de escritura. Publicar mediante transacción corta que guarda snapshot, resultados y estado. Si ExpectedRevision no coincide, no sustituir CurrentRunId. Puede conservar la ejecución completada como histórica y marcarla obsoleta con mensaje; nunca atribuirla a la revisión nueva.

Un fallo previo a publicar deja los resultados anteriores disponibles. Un fallo en publicación hace rollback completo. Una cancelación produce intento cancelado y no TerminalCostFacts publicados. El progreso muestra etapas, no una falsa garantía de porcentaje si el coste de cada etapa es desconocido.

La eliminación de ejecuciones necesita una operación explícita y restricciones de referencias desde órdenes. La primera versión puede permitir archivar sin eliminar. El modelo publicado y los snapshots se consultan aunque sus maestros estén desactivados.

## 6. Catálogo de diagnósticos mínimo

| Código | Severidad y actuación |
| --- | --- |
| IMP_INVALID_AMOUNT | Error de fila. Corregir origen/perfil; confirmación bloqueada. |
| IMP_SIGN_UNCONFIRMED | Bloqueo de perfil. Confirmar interpretación con ejemplo. |
| RES_PARTITION_MISMATCH | Error económico. Mostrar importe efectivo y diferencia de partes. |
| KEY_INCOMPLETE | Borrador/advertencia; bloquea publicación definitiva. |
| KEY_ZERO_DRIVER_TOTAL | Error. Introducir pesos o seleccionar otro criterio. |
| GRAPH_CLOSED_COMPONENT | Error. Mostrar auxiliares sin salida y claves implicadas. |
| GRAPH_SELF_NOT_ALLOWED | Error de política. Corregir o crear versión que permita diagonal. |
| SOLVER_SENSITIVE | Advertencia o bloqueo conforme a umbral; mostrar métricas y salidas pequeñas. |
| SOLVER_RESIDUAL_EXCEEDED | Error. No publicar ni cambiar de método. |
| NUM_CONSERVATION_FAILURE | Error. Diferencia mayor que precisión permitida; investigar cálculo. |
| CAP_NORMAL_MISSING | Bloqueo de tarifa normal para componente afectado. |
| CAP_ZERO_DENOMINATOR | Error de componente con coste; no inventar tarifa cero. |
| ACT_COST_WITH_ZERO_ACTIVITY | Error. Coste variable/consumido sin cantidad real. |
| ACT_CONSUMPTION_EXCEEDS_OBSERVED | Error de datos reales comparables. |
| ACT_UNASSIGNED | Importe visible de actividad observada sin objeto; según política puede impedir cierre definitivo. |
| AUX_CAPACITY_NOT_EVALUATED | Información metodológica explícita en ThroughFlow. |
| AUX_SERVICE_COVERAGE_MISMATCH | Error. Servicios no concilian con actividad auxiliar. |
| RECIPE_CYCLE | Error. Mostrar productos que forman ciclo. |
| MARGIN_UNDEFINED | Resultado parcial válido; porcentaje sin denominador válido. |
| RUN_REVISION_CHANGED | Ejecución histórica válida, sin actualidad. |
| FILE_SCHEMA_NEWER | Lectura/edición bloqueada según compatibilidad; conservar archivo. |

Los mensajes se muestran en el idioma seleccionado con datos tipados y acción correctiva. Severidad, código, MessageKey y argumentos se guardan separados del texto. Traducción forma parte de la primera implementación, no de una fase futura.

Códigos adicionales, IMP_AMBIGUOUS_FORMAT, MONEY_CURRENCY_MISMATCH, FX_RATE_MISSING, FX_RATE_INVALID, FX_AMOUNT_BELOW_QUANTUM, LOC_RESOURCE_MISSING y TIME_LOCAL_AMBIGUOUS. Se definen datos y severidad en CM15/CM16 y en la especificación 05.

Puertos de extensión, IStudyRepository, IRunStore, IImportAdapter, ICurrencyCatalog, IFxRateProvider, IExportWriter, IBackupService, IClock e ILicenseProvider. Localization/Reporting implementan presentación e informes sin referencias desde Domain/Calculation. La composición conecta implementaciones; una prueba de dependencias evita ciclos y DbContext en DTO públicos.

## 7. Pruebas de arquitectura y archivo

Domain y Calculation se compilan sin referencia a WPF/EF. El runner calcula sin base ni Internet. Un archivo SQLite temporal prueba migraciones, restricciones, transacciones y redondeo de texto real. El runner y la UI producen los mismos resultados para un snapshot, porque comparten motor.

No usar únicamente proveedor in-memory de EF para validar SQLite. No sumar, ordenar o comparar costes mediante CAST a REAL. Las consultas filtran por IDs, periodo y ámbito; cargan valores tipados y agregan en C# con límites medidos.

El benchmark mide fase de importación, factorización, contribuciones, cuantización, persistencia y reapertura. Si el tamaño de trazas crece excesivamente, se puede guardar representación dispersa o factores más hechos necesarios, preservando el contrato de explicación y resultados. El LLM no elimina origen para ahorrar tiempo.
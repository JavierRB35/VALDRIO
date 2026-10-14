# Auditoría integral VALDRIO — 14/10/2026

## Base y alcance

Repositorio verificado por GitHub: JavierRB35/VALDRIO (privado). Base autorizada por el propietario: main, SHA 0a1dd6ea29e7c691f900cea48fb8ad74612f0c14. Rama de entrega: audit/integral-20261014. La rama funcional local anterior era feat/workflow-phase-accordion-20261013, ya integrada en main con contenido idéntico. No se han integrado ramas antiguas por inferencia.

Fuentes consultadas: prompt y paquete v0.4 bajo specification/v0.4; PDF de estrategia, especificación internacional y manual de marca adjuntos al proyecto; PENDIENTES_DESARROLLO.md; AGENTS.md, USABILIDAD.md y SISTEMA_VISUAL_VALDRIO_V1.md. sources/ permanece intacto. La conversación de referencia recuperada está truncada después de «FASE 1: AU»; la solicitud explícita de esta sesión gobierna el alcance. No se inventa el texto perdido.

Esta es una auditoría de la implementación presente y una primera refactorización gradual. No equivale a implementar las fases comerciales futuras ni a demostrar ausencia universal de regresiones.

## Diagnóstico ejecutivo y actuaciones

| ID / prioridad | Evidencia verificable (rutas relativas al repositorio) | Impacto | Solución / estado |
|---|---|---|---|
| A01 / alta | Desktop/AllocationWorkspaceViewModel.SecondaryFlow.cs contenía SecondaryFlowProjector y productos gross × coefficient | La explicación de flujos dependía de WPF y no podía reutilizarse directamente en ejecución headless | Extraído a Calculation/ReciprocalFlowProjector.cs. Adaptador de compatibilidad conserva contratos de Desktop. No refactoriza ni vuelve a resolver el solver. |
| A02 / alta | Desktop/SecondaryResultPresentation.cs atribuía residuos y llamaba BalancedRounder; Desktop/AllocationCurrencyDisplay.cs conciliaba filas/columnas monetarias | Reporting estaba vacío y la conciliación visual tenía implementación exclusiva de UI | Extraído a Reporting/SecondaryContributionProjector.cs y AllocationCurrencyProjection.cs. Se mantienen orden, quantum, signos, desempates y adapters; Reporting reutiliza Calculation. |
| A03 / alta | Runner/Program.cs, rama --study-snapshot: switch solo incluía ImportedStudySnapshotInput y ResourceStudySnapshotInput | Snapshot T08/T09/T11 informado como T05, con imports y coverage vacíos aunque existían | Corregido el tipo AllocationStudySnapshotInput, etiqueta y cobertura. Tests de subprocess con ambos snapshots sintéticos. Mantiene esquema JSON, hashes y comandos anteriores. |
| A04 / media | Desktop/AllocationWorkspaceViewModel.SecondaryFlow.cs, bucle de terminales filtraba todos los hechos y arcos en cada vuelta | Trabajo repetido proporcional a terminales × (hechos + arcos) en construcción de vista | Totales precalculados por grupo/nodo y por destino. La conciliación consulta diccionarios; se conserva orden de agregación dentro de cada grupo. No se afirma mejora porcentual no medida. |
| A05 / alta, pendiente | Infrastructure/CompanyFiles/Migrations/StudiesAndSnapshots.cs: CK_StudyNoRun; SqliteCompanyFileService.Studies.cs exige CurrentRunId nulo | No existe en main el ciclo completo PrepareRun/Calculate/PublishRun con historial T12 | Implementar T12 en una entrega con migración y pruebas de concurrencia/cancelación. Los cierres secundarios existentes sí conservan publicación; no confundirlos con historial general de ejecuciones. Rama T12 antigua respaldada, no reintroducida. |
| A06 / alta, pendiente | SqliteCompanyFileService.Allocations.cs: ChangeAllocations llama Calculate dentro de Workspace(true); ValidateAllocations reproduce resultados históricos | Cálculo durante escritura y coste creciente de validación de historia al abrir | Separar preparación/cálculo fuera de transacción y publicación con revisión esperada en T12. Conservar chequeos de integridad; no eliminarlos para acelerar. |
| A07 / alta, pendiente de producto | Domain/CostModels.cs ModelPolicyReference rechaza resoluciones distintas de Pending; docs/directrices/T14_CAPACIDAD_NORMAL_ASISTIDA.md; CM08+ | Etiquetas de comportamiento/capacidad no constituyen un motor de ociosidad, ABC, TDABC o comparación multimétodo | Mantener estas capacidades como hoja de ruta y aplicar contratos T14–T31 con referencias aprobadas. No inventar actividad/capacidad ni llamar ociosidad a un residuo numérico. |
| A08 / media, pendiente | Application/PrimaryAllocation.cs y SecondaryAllocation.cs contienen cálculos puros además de coordinación | Separación de capas parcialmente satisfecha; motor puro distribuido entre Application y Calculation | Extracción futura de DTO económicos a Domain y servicios puros a Calculation en cambios pequeños. Esta entrega extrae primero tres componentes sin introducir dependencias circulares ni alterar serialización. |
| A09 / media, parcial | MainWindow.xaml: ProjectPhase, AnalysisPhase, AllocationPhase; siete pasos operativos y ajustes | Tres fases operativas visibles ya implementadas; no hay selector global básico/experto ni registro de seis fases ampliable | Conservar los tres grupos existentes. Las tres fases futuras no se anuncian como operativas. Diseñar modo experto sin ocultar errores, trazabilidad ni funciones existentes. |
| A10 / media, pendiente de aceptación UX | PrimaryExplain, PrincipalOrigins, SecondaryFlow y diálogos de claves | Existen recorridos de explicación, pero «cualquier cifra a origen en ≤3 clics» no está demostrado en todos los estados | Medir matriz resultado→origen→regla con estados abiertos/cerrados, ES/EN y teclado. No confundir prueba de ViewModel con aceptación visual universal. |
| A11 / media | ESTADO_PROYECTO.md conserva secciones antiguas que dicen que no hay importación/motor; ramas y PR obsoletas | Contexto contradictorio para usuarios y próximos desarrollos | Añadida cabecera vigente que enlaza esta auditoría. Limpieza autorizada de ramas y PR; historial documental queda como historial, no evidencia actual. |
| A12 / baja, ya resuelta en base | CompanyFilePanel.xaml usa HasProject/NoProject, ActiveCompanyNameText y NewProjectCommand | El pendiente adjunto del nombre de empresa describe una UI anterior | La base actual separa creación y proyecto abierto, mostrando nombre no editable. Se registra como superado por diseño actual; no se reintroduce un campo duplicado. |

| A13 / media | Desktop/AllocationWorkspacePanel.xaml.cs, FocusKeyLibrary: retorno incondicional después de row.Focus/Keyboard.Focus | La primera clave se seleccionaba pero el foco podía quedar fuera de la tabla; WPF reprodujo index=0, focus=False, visible/enabled/active=True | Se retorna solo si KeyGrid.IsKeyboardFocusWithin; en caso contrario se ejecuta el fallback ya existente a la tabla. Se mantiene la misma aserción del smoke y se amplía su diagnóstico. |

Prefijos Desktop/, Calculation/, Reporting/, Runner/, Application/, Domain/ corresponden a src/Valdrio.<capa>/.

## Contratos y arquitectura contrastados

- Domain: IDs tipados, Money/decimal, FrozenList, políticas versionadas. Tests de arquitectura prohíben referencias WPF/EF y dependencia inversa. No se ha demostrado inmutabilidad profunda de todo DTO; ReciprocalInput/Solution todavía exponen arrays, que merecen revisión separada de compatibilidad.
- Calculation: BalancedRounder/LargestRemainderCore, FxConverter, AllocationKeyRules, ReciprocalFactorization y ahora ReciprocalFlowProjector. Solver LU decimal, orden canónico, reutilización de factorización y diagnósticos de componentes cerrados, autorreparto, amplificación y residuo. PNUM-1 sigue marcado proposed: aprobar un algoritmo técnico no aprueba una metodología económica pendiente.
- CM01 significa interpretar sumas y saldos, con perfil, periodo y signos explícitos. T06/T06A prueban interpretación, debe/haber y confirmación; la modalidad actual de importación de moneda única está documentada en ADR-0016 y no se revierte.
- CM02: particiones/rutas en ResourcePreparationRules y ResourceWorkspaceRules; pruebas T07 y reconciliaciones T09/T11. No basta un neto global cero: el corpus incluye orígenes positivos y negativos, y conservación por parte.
- CM03: AllocationKeyRules y AllocationWorkspaceRules validan pesos, porcentajes, destinos, unidades y cobertura; borradores incompletos no equivalen a publicación válida.
- CM04: cuantización a quantum, mayores restos e IDs estables; proyección monetaria separada del ledger. T02, AllocationCurrencyDisplayTests y T11SecondaryAllocationTests cubren signos, microimportes, permutaciones, residuos y totales por fuente. Las circulaciones internas no se suman al coste final.
- SQLite: StudySnapshots y AllocationChanges tienen triggers contra UPDATE/DELETE; receipts, hashes canónicos y revisiones se verifican al leer. Esto es protección de contrato de aplicación/archivo, no defensa frente a alguien que reescribe el esquema SQLite. No se cambia esquema ni política de hashes en esta entrega.
- Runner: comandos headless para snapshots de estudio, primario, grafo recíproco y secundario. Los tests de subprocess son evidencia del ejecutable real. La suite general usa net10.0-windows/WPF por fuentes de ViewModels enlazadas; eso limita su portabilidad aunque Runner y motor sean net10.0.
- Reporting deja de estar vacío. Conserva proyección explicativa, no crea nuevas ejecuciones ni sustituye hechos publicados.
- Desktop conserva controles, bindings, comandos, textos y paleta; no se ha modificado XAML. El mapa continúa mostrando por separado primario, circulación recíproca y finales.

## Rendimiento y deuda

La prueba HundredAuxiliariesBenchmarkAndCancellation de la línea base ejecutó 100 auxiliares: 42 ms, 839.208 bytes asignados en el hilo, amplificación 1,1111… en este equipo. Es una medición sintética puntual, no SLA, benchmark de importación ni límite de capacidad comercial. La suite inicial completa tardó 3 min 23 s.

Quedan pendientes cargas representativas de importación, apertura con historial largo y latencia UI, además de la separación del cálculo dentro de la transacción. El cambio A04 reduce recorridos repetidos por estructura del código; no se atribuye un porcentaje de aceleración sin comparación controlada.

## Limpieza autorizada

El propietario confirmó que lo último está en main y autorizó borrar lo demás. Se cerraron sin fusionar las PR #36, #40, #41 y #42 y se eliminaron 49 ramas remotas antiguas con comprobación del SHA y respaldo. Se retiraron los worktrees locales valdrio-integration y valdrio-t09-fix, y las ramas antiguas de las dos copias de desarrollo. La copia C:/Users/cjrui/source/repos/VALDRIO quedó limpia en main@0a1dd6e; VALDRIO-T07 dejó de estar registrado como worktree (la eliminación de su carpeta devolvió acceso denegado, y la inspección posterior no enumeró contenido).

Respaldo fuera del repositorio, en ../audit-backup-20261014/: valdrio-all-refs.bundle, development-all-refs.bundle, refs.txt, integration-uncommitted.patch y integration-untracked/. No contiene una promesa de preservar outputs ignorados de compilación. sources/ y main no se borraron. La nueva rama de auditoría se conserva para revisión.

La revisión automática rechazó inicialmente una selección dinámica demasiado amplia de ramas. Se sustituyó por una lista explícita de 49 nombres, excluyendo main y audit/integral-20261014 y verificando los SHAs en el bundle; esa operación fue aceptada y ejecutada.

## Validación y límites

Resultados finales y comandos en VALIDACION.md. Una suite verde no demuestra ausencia absoluta de regresiones. No se afirma completada la aceptación de todos los flujos/DPI ni implementado el roadmap económico pendiente. No se iniciaron GitHub Actions, no se tocó facturación y no se modificó el workflow manual existente.

## Localizadores de evidencia en la entrega

- [src/Valdrio.Calculation/ReciprocalFactorization.cs:27](../../../src/Valdrio.Calculation/ReciprocalFactorization.cs#L27) — `public sealed class ReciprocalFactorization`.
- [src/Valdrio.Infrastructure/CompanyFiles/SqliteCompanyFileService.Allocations.cs:124](../../../src/Valdrio.Infrastructure/CompanyFiles/SqliteCompanyFileService.Allocations.cs#L124) — `public AllocationChangeReceipt ChangeAllocations`.
- [src/Valdrio.Infrastructure/CompanyFiles/Migrations/StudiesAndSnapshots.cs:21](../../../src/Valdrio.Infrastructure/CompanyFiles/Migrations/StudiesAndSnapshots.cs#L21) — `CK_StudyNoRun`.
- [src/Valdrio.Runner/Program.cs:61](../../../src/Valdrio.Runner/Program.cs#L61) — `IReadOnlyList<ImportedBatch> batches`.
- [src/Valdrio.Desktop/MainWindow.xaml:37](../../../src/Valdrio.Desktop/MainWindow.xaml#L37) — `x:Name="ProjectPhase"`.
- [src/Valdrio.Domain/CostModels.cs:39](../../../src/Valdrio.Domain/CostModels.cs#L39) — `Resolution != PolicyResolution.Pending`.
- [src/Valdrio.Desktop/CompanyFilePanel.xaml:51](../../../src/Valdrio.Desktop/CompanyFilePanel.xaml#L51) — `ActiveCompanyNameText`.

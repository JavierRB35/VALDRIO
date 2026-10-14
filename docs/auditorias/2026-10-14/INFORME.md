# AuditorÃ­a integral VALDRIO â€” 14/10/2026

## Base y alcance

Repositorio verificado por GitHub: JavierRB35/VALDRIO (privado). Base autorizada por el propietario: main, SHA 0a1dd6ea29e7c691f900cea48fb8ad74612f0c14. Rama de entrega: audit/integral-20261014. La rama funcional local anterior era feat/workflow-phase-accordion-20261013, ya integrada en main con contenido idÃ©ntico. No se han integrado ramas antiguas por inferencia.

Fuentes consultadas: prompt y paquete v0.4 bajo specification/v0.4; PDF de estrategia, especificaciÃ³n internacional y manual de marca adjuntos al proyecto; PENDIENTES_DESARROLLO.md; AGENTS.md, USABILIDAD.md y SISTEMA_VISUAL_VALDRIO_V1.md. sources/ permanece intacto. La conversaciÃ³n de referencia recuperada estÃ¡ truncada despuÃ©s de Â«FASE 1: AUÂ»; la solicitud explÃ­cita de esta sesiÃ³n gobierna el alcance. No se inventa el texto perdido.

Esta es una auditorÃ­a de la implementaciÃ³n presente y una primera refactorizaciÃ³n gradual. No equivale a implementar las fases comerciales futuras ni a demostrar ausencia universal de regresiones.

## DiagnÃ³stico ejecutivo y actuaciones

| ID / prioridad | Evidencia verificable (rutas relativas al repositorio) | Impacto | SoluciÃ³n / estado |
|---|---|---|---|
| A01 / alta | Desktop/AllocationWorkspaceViewModel.SecondaryFlow.cs contenÃ­a SecondaryFlowProjector y productos gross Ã— coefficient | La explicaciÃ³n de flujos dependÃ­a de WPF y no podÃ­a reutilizarse directamente en ejecuciÃ³n headless | ExtraÃ­do a Calculation/ReciprocalFlowProjector.cs. Adaptador de compatibilidad conserva contratos de Desktop. No refactoriza ni vuelve a resolver el solver. |
| A02 / alta | Desktop/SecondaryResultPresentation.cs atribuÃ­a residuos y llamaba BalancedRounder; Desktop/AllocationCurrencyDisplay.cs conciliaba filas/columnas monetarias | Reporting estaba vacÃ­o y la conciliaciÃ³n visual tenÃ­a implementaciÃ³n exclusiva de UI | ExtraÃ­do a Reporting/SecondaryContributionProjector.cs y AllocationCurrencyProjection.cs. Se mantienen orden, quantum, signos, desempates y adapters; Reporting reutiliza Calculation. |
| A03 / alta | Runner/Program.cs, rama --study-snapshot: switch solo incluÃ­a ImportedStudySnapshotInput y ResourceStudySnapshotInput | Snapshot T08/T09/T11 informado como T05, con imports y coverage vacÃ­os aunque existÃ­an | Corregido el tipo AllocationStudySnapshotInput, etiqueta y cobertura. Tests de subprocess con ambos snapshots sintÃ©ticos. Mantiene esquema JSON, hashes y comandos anteriores. |
| A04 / media | Desktop/AllocationWorkspaceViewModel.SecondaryFlow.cs, bucle de terminales filtraba todos los hechos y arcos en cada vuelta | Trabajo repetido proporcional a terminales Ã— (hechos + arcos) en construcciÃ³n de vista | Totales precalculados por grupo/nodo y por destino. La conciliaciÃ³n consulta diccionarios; se conserva orden de agregaciÃ³n dentro de cada grupo. No se afirma mejora porcentual no medida. |
| A05 / alta, pendiente | Infrastructure/CompanyFiles/Migrations/StudiesAndSnapshots.cs: CK_StudyNoRun; SqliteCompanyFileService.Studies.cs exige CurrentRunId nulo | No existe en main el ciclo completo PrepareRun/Calculate/PublishRun con historial T12 | Implementar T12 en una entrega con migraciÃ³n y pruebas de concurrencia/cancelaciÃ³n. Los cierres secundarios existentes sÃ­ conservan publicaciÃ³n; no confundirlos con historial general de ejecuciones. Rama T12 antigua respaldada, no reintroducida. |
| A06 / alta, pendiente | SqliteCompanyFileService.Allocations.cs: ChangeAllocations llama Calculate dentro de Workspace(true); ValidateAllocations reproduce resultados histÃ³ricos | CÃ¡lculo durante escritura y coste creciente de validaciÃ³n de historia al abrir | Separar preparaciÃ³n/cÃ¡lculo fuera de transacciÃ³n y publicaciÃ³n con revisiÃ³n esperada en T12. Conservar chequeos de integridad; no eliminarlos para acelerar. |
| A07 / alta, pendiente de producto | Domain/CostModels.cs ModelPolicyReference rechaza resoluciones distintas de Pending; docs/directrices/T14_CAPACIDAD_NORMAL_ASISTIDA.md; CM08+ | Etiquetas de comportamiento/capacidad no constituyen un motor de ociosidad, ABC, TDABC o comparaciÃ³n multimÃ©todo | Mantener estas capacidades como hoja de ruta y aplicar contratos T14â€“T31 con referencias aprobadas. No inventar actividad/capacidad ni llamar ociosidad a un residuo numÃ©rico. |
| A08 / media, pendiente | Application/PrimaryAllocation.cs y SecondaryAllocation.cs contienen cÃ¡lculos puros ademÃ¡s de coordinaciÃ³n | SeparaciÃ³n de capas parcialmente satisfecha; motor puro distribuido entre Application y Calculation | ExtracciÃ³n futura de DTO econÃ³micos a Domain y servicios puros a Calculation en cambios pequeÃ±os. Esta entrega extrae primero tres componentes sin introducir dependencias circulares ni alterar serializaciÃ³n. |
| A09 / media, parcial | MainWindow.xaml: ProjectPhase, AnalysisPhase, AllocationPhase; siete pasos operativos y ajustes | Tres fases operativas visibles ya implementadas; no hay selector global bÃ¡sico/experto ni registro de seis fases ampliable | Conservar los tres grupos existentes. Las tres fases futuras no se anuncian como operativas. DiseÃ±ar modo experto sin ocultar errores, trazabilidad ni funciones existentes. |
| A10 / media, pendiente de aceptaciÃ³n UX | PrimaryExplain, PrincipalOrigins, SecondaryFlow y diÃ¡logos de claves | Existen recorridos de explicaciÃ³n, pero Â«cualquier cifra a origen en â‰¤3 clicsÂ» no estÃ¡ demostrado en todos los estados | Medir matriz resultadoâ†’origenâ†’regla con estados abiertos/cerrados, ES/EN y teclado. No confundir prueba de ViewModel con aceptaciÃ³n visual universal. |
| A11 / media | ESTADO_PROYECTO.md conserva secciones antiguas que dicen que no hay importaciÃ³n/motor; ramas y PR obsoletas | Contexto contradictorio para usuarios y prÃ³ximos desarrollos | AÃ±adida cabecera vigente que enlaza esta auditorÃ­a. Limpieza autorizada de ramas y PR; historial documental queda como historial, no evidencia actual. |
| A12 / baja, ya resuelta en base | CompanyFilePanel.xaml usa HasProject/NoProject, ActiveCompanyNameText y NewProjectCommand | El pendiente adjunto del nombre de empresa describe una UI anterior | La base actual separa creaciÃ³n y proyecto abierto, mostrando nombre no editable. Se registra como superado por diseÃ±o actual; no se reintroduce un campo duplicado. |
| A13 / media | Desktop/AllocationWorkspacePanel.xaml.cs, FocusKeyLibrary: retorno incondicional despuÃ©s de row.Focus/Keyboard.Focus | La primera clave se seleccionaba pero el foco podÃ­a quedar fuera de la tabla; WPF reprodujo index=0, focus=False, visible/enabled/active=True | Se retorna solo si KeyGrid.IsKeyboardFocusWithin; en caso contrario se ejecuta el fallback ya existente a la tabla. Se mantiene la misma aserciÃ³n del smoke y se amplÃ­a su diagnÃ³stico. |
| A14 / media, pendiente UX | Captura layout-scale-200.png del shell final, 1264×824 | Al 200 % el contexto superior consume gran altura; existe scroll pero disminuye el área de trabajo | Compactar contexto y ruta con acceso explícito al detalle, conservando información y navegación. Requiere inventario y aceptación visual específica; no resuelto en esta refactorización. |

Prefijos Desktop/, Calculation/, Reporting/, Runner/, Application/, Domain/ corresponden a src/Valdrio.<capa>/.

## Contratos y arquitectura contrastados

- Domain: IDs tipados, Money/decimal, FrozenList, polÃ­ticas versionadas. Tests de arquitectura prohÃ­ben referencias WPF/EF y dependencia inversa. No se ha demostrado inmutabilidad profunda de todo DTO; ReciprocalInput/Solution todavÃ­a exponen arrays, que merecen revisiÃ³n separada de compatibilidad.
- Calculation: BalancedRounder/LargestRemainderCore, FxConverter, AllocationKeyRules, ReciprocalFactorization y ahora ReciprocalFlowProjector. Solver LU decimal, orden canÃ³nico, reutilizaciÃ³n de factorizaciÃ³n y diagnÃ³sticos de componentes cerrados, autorreparto, amplificaciÃ³n y residuo. PNUM-1 sigue marcado proposed: aprobar un algoritmo tÃ©cnico no aprueba una metodologÃ­a econÃ³mica pendiente.
- CM01 significa interpretar sumas y saldos, con perfil, periodo y signos explÃ­citos. T06/T06A prueban interpretaciÃ³n, debe/haber y confirmaciÃ³n; la modalidad actual de importaciÃ³n de moneda Ãºnica estÃ¡ documentada en ADR-0016 y no se revierte.
- CM02: particiones/rutas en ResourcePreparationRules y ResourceWorkspaceRules; pruebas T07 y reconciliaciones T09/T11. No basta un neto global cero: el corpus incluye orÃ­genes positivos y negativos, y conservaciÃ³n por parte.
- CM03: AllocationKeyRules y AllocationWorkspaceRules validan pesos, porcentajes, destinos, unidades y cobertura; borradores incompletos no equivalen a publicaciÃ³n vÃ¡lida.
- CM04: cuantizaciÃ³n a quantum, mayores restos e IDs estables; proyecciÃ³n monetaria separada del ledger. T02, AllocationCurrencyDisplayTests y T11SecondaryAllocationTests cubren signos, microimportes, permutaciones, residuos y totales por fuente. Las circulaciones internas no se suman al coste final.
- SQLite: StudySnapshots y AllocationChanges tienen triggers contra UPDATE/DELETE; receipts, hashes canÃ³nicos y revisiones se verifican al leer. Esto es protecciÃ³n de contrato de aplicaciÃ³n/archivo, no defensa frente a alguien que reescribe el esquema SQLite. No se cambia esquema ni polÃ­tica de hashes en esta entrega.
- Runner: comandos headless para snapshots de estudio, primario, grafo recÃ­proco y secundario. Los tests de subprocess son evidencia del ejecutable real. La suite general usa net10.0-windows/WPF por fuentes de ViewModels enlazadas; eso limita su portabilidad aunque Runner y motor sean net10.0.
- Reporting deja de estar vacÃ­o. Conserva proyecciÃ³n explicativa, no crea nuevas ejecuciones ni sustituye hechos publicados.
- Desktop conserva controles, bindings, comandos, textos y paleta; no se ha modificado XAML. El mapa continÃºa mostrando por separado primario, circulaciÃ³n recÃ­proca y finales.

## Rendimiento y deuda

La prueba HundredAuxiliariesBenchmarkAndCancellation de la lÃ­nea base ejecutÃ³ 100 auxiliares: 42 ms, 839.208 bytes asignados en el hilo, amplificaciÃ³n 1,1111â€¦ en este equipo. Es una mediciÃ³n sintÃ©tica puntual, no SLA, benchmark de importaciÃ³n ni lÃ­mite de capacidad comercial. La suite inicial completa tardÃ³ 3 min 23 s.

Quedan pendientes cargas representativas de importaciÃ³n, apertura con historial largo y latencia UI, ademÃ¡s de la separaciÃ³n del cÃ¡lculo dentro de la transacciÃ³n. El cambio A04 reduce recorridos repetidos por estructura del cÃ³digo; no se atribuye un porcentaje de aceleraciÃ³n sin comparaciÃ³n controlada.

## Limpieza autorizada

El propietario confirmÃ³ que lo Ãºltimo estÃ¡ en main y autorizÃ³ borrar lo demÃ¡s. Se cerraron sin fusionar las PR #36, #40, #41 y #42 y se eliminaron 49 ramas remotas antiguas con comprobaciÃ³n del SHA y respaldo. Se retiraron los worktrees locales valdrio-integration y valdrio-t09-fix, y las ramas antiguas de las dos copias de desarrollo. La copia C:/Users/cjrui/source/repos/VALDRIO quedÃ³ limpia en main@0a1dd6e; VALDRIO-T07 dejÃ³ de estar registrado como worktree. Su carpeta quedÃ³ vacÃ­a, pero Windows impidiÃ³ eliminarla porque otro proceso la estÃ¡ usando; no se detuvieron procesos del usuario.

Respaldo fuera del repositorio, en ../audit-backup-20261014/: valdrio-all-refs.bundle, development-all-refs.bundle, refs.txt, integration-uncommitted.patch y integration-untracked/. No contiene una promesa de preservar outputs ignorados de compilaciÃ³n. sources/ y main no se borraron. La nueva rama de auditorÃ­a se conserva para revisiÃ³n.

La revisiÃ³n automÃ¡tica rechazÃ³ inicialmente una selecciÃ³n dinÃ¡mica demasiado amplia de ramas. Se sustituyÃ³ por una lista explÃ­cita de 49 nombres, excluyendo main y audit/integral-20261014 y verificando los SHAs en el bundle; esa operaciÃ³n fue aceptada y ejecutada.

## ValidaciÃ³n y lÃ­mites

Resultados finales y comandos en VALIDACION.md. Una suite verde no demuestra ausencia absoluta de regresiones. No se afirma completada la aceptaciÃ³n de todos los flujos/DPI ni implementado el roadmap econÃ³mico pendiente. No se iniciaron GitHub Actions, no se tocÃ³ facturaciÃ³n y no se modificÃ³ el workflow manual existente.

## Localizadores de evidencia en la entrega

- [src/Valdrio.Calculation/ReciprocalFactorization.cs:27](../../../src/Valdrio.Calculation/ReciprocalFactorization.cs#L27) â€” `public sealed class ReciprocalFactorization`.
- [src/Valdrio.Infrastructure/CompanyFiles/SqliteCompanyFileService.Allocations.cs:124](../../../src/Valdrio.Infrastructure/CompanyFiles/SqliteCompanyFileService.Allocations.cs#L124) â€” `public AllocationChangeReceipt ChangeAllocations`.
- [src/Valdrio.Infrastructure/CompanyFiles/Migrations/StudiesAndSnapshots.cs:21](../../../src/Valdrio.Infrastructure/CompanyFiles/Migrations/StudiesAndSnapshots.cs#L21) â€” `CK_StudyNoRun`.
- [src/Valdrio.Runner/Program.cs:61](../../../src/Valdrio.Runner/Program.cs#L61) â€” `IReadOnlyList<ImportedBatch> batches`.
- [src/Valdrio.Desktop/MainWindow.xaml:37](../../../src/Valdrio.Desktop/MainWindow.xaml#L37) â€” `x:Name="ProjectPhase"`.
- [src/Valdrio.Domain/CostModels.cs:39](../../../src/Valdrio.Domain/CostModels.cs#L39) â€” `Resolution != PolicyResolution.Pending`.
- [src/Valdrio.Desktop/CompanyFilePanel.xaml:51](../../../src/Valdrio.Desktop/CompanyFilePanel.xaml#L51) â€” `ActiveCompanyNameText`.

Validación de cierre: 655/655 pruebas locales; 539 comprobaciones WPF registradas (48 metodológicas), build sin errores/advertencias. Véase VALIDACION.md para alcance, intentos fallidos y límites.

# VALDRIO — inventario de la aplicación implementada (línea base del futuro manual)

**Corte de revisión:** 15/10/2026. **Repositorio:** `JavierRB35/VALDRIO`. **Referencia examinada:** `main@8b6c89e47d69d53bb5adad2fe0a2b215b4d68a95`. Documento vivo, **no** manual definitivo ni certificación universal de funcionamiento.

## 1. Criterio de verdad y alcance de la comprobación

Este documento confronta la **aplicación implementada** con sus documentos; no convierte la especificación de producto en una descripción de capacidades disponibles. Para cada afirmación distinguimos:

- **CÓDIGO:** control XAML, enlace/comando, ViewModel o servicio existente en el SHA revisado; acredita implementación, no una interacción de usuario exitosa.
- **WPF PREVIO:** capturas de la aplicación Windows y registros de automatización WPF que están versionados. Acreditan el escenario y el SHA de aquella ejecución; **no son una sesión interactiva realizada en esta auditoría ni validan automáticamente el HEAD actual**.
- **PRUEBA PREVIA:** test .NET/integración o acta con resultados y commit indicado; tampoco sustituye comprobación final sobre el mismo binario.
- **PENDIENTE DE RECORRIDO:** acción, error, combinación de datos o UX para la que no se ha podido producir aquí evidencia nativa interactiva.

**Límite material:** el entorno de esta revisión es Linux sin `dotnet`, PowerShell ni sesión Windows/WPF. Se ha leído el código real de producción (no solo la documentación), los controles y sus comandos, las pruebas, y se han inspeccionado capturas reales incluidas en el repositorio: [proyecto](../auditorias/2026-10-14/company-panel.png), [claves primarias](../auditorias/2026-10-14/allocation-keys.png), [liquidación secundaria](../auditorias/2026-10-14/secondary-results.png) y [reglas aplicadas](../evidencias/2026-10-14-ajustes-reutilizables/02-es-applied.png). La comprobación manual en Windows queda pendiente y está parametrizada en [la matriz de aceptación](PLAN_VERIFICACION_MANUAL_2026-10-15.md).

El material de estrategia, marca, especificación internacional v0.4 y tareas de desarrollo constituye **referencia normativa/propuesta**, no evidencia suficiente de un botón, validación ni resultado realmente disponible. La evidencia de ejecución histórica se conserva con su fecha y SHA. No se modifican binarios, datos, contratos ni código económico.

## 2. Mapa de navegación implementado

Fuente directa: [MainWindow.xaml](../../src/Valdrio.Desktop/MainWindow.xaml), [MainWindow.Guidance.cs](../../src/Valdrio.Desktop/MainWindow.Guidance.cs) y recursos ES/EN [Strings.es.resx](../../src/Valdrio.Localization/Resources/Strings.es.resx). Existen **tres fases desplegables** y ocho pasos, más Preferencias fuera del proceso económico:

| Fase visible | Paso y nombre en español (recursos reales) | Acceso o requisito comprobado en el código |
|---|---|---|
| Proyecto y estructura | 1. Proyecto | Disponible siempre; crear, abrir o recuperar un proyecto |
| Proyecto y estructura | 2. Diseñar estructura | Requiere proyecto abierto |
| Análisis y datos económicos | 3. Periodo / análisis | Requiere proyecto abierto |
| Análisis y datos económicos | 4. Importar saldos | Requiere destino de importación (análisis) |
| Análisis y datos económicos | 5. Preparar cuentas | Requiere importación confirmada |
| Repartos y cálculo | 6. Reparto primario | Requiere importación confirmada para acceder |
| Repartos y cálculo | 7. Reparto secundario y cierre | El menú requiere que el primario esté cerrado |
| Repartos y cálculo | 8. Flujos y ejecuciones | El acceso requiere análisis; **ejecutar** tiene validaciones económicas adicionales |
| Fuera de las fases | Preferencias | Idioma, formatos, zona horaria y herramientas de desarrollo |

El acordeón expande la fase del paso visitado y cierra las otras. Navegar a otra fase **no significa** que su cálculo esté completo: los accesos están condicionados por la existencia de proyecto, análisis/importación y el cierre primario; no se deduce de ello un indicador económico global de progreso. La cabecera presenta proyecto, estructura y análisis activos; existen controles de menú reversible, detalle de contexto y densidad de tablas en el shell. `Anterior/Continuar` maneja requisitos, cambios sin guardar y subpasos internos. [Código](../../src/Valdrio.Desktop/MainWindow.Guidance.cs).

**No figuran como módulos operativos** las otras tres fases del mapa comercial futuro (producción/objetos, rentabilidad/decisiones, informes/cierre integral). «Cierre del reparto secundario» no equivale a «cierre integral del período».

## 3. Inventario de pantallas y comportamiento implementado

### 1. Proyecto

**Se ve/se puede intentar:** abrir archivo existente, crear proyecto con nombre y moneda, abrir copia del ejemplo de servicios, iniciar un recorrido de importación orientado al proceso, cerrar proyecto, crear copia, restaurar y actualizar un archivo antiguo a **otra copia**, y gestionar unidades de actividad disponibles y personalizadas. La pantalla de proyecto abierto muestra su nombre como dato, no como campo duplicado editable. Las tareas frecuentes (continuar estructura/análisis) están separadas de recuperación y detalles técnicos. El diálogo de nueva creación exige nombre y usa selección local de archivo.

**Controles reales:** [CompanyFilePanel.xaml](../../src/Valdrio.Desktop/CompanyFilePanel.xaml) y [CompanyFileViewModel.cs](../../src/Valdrio.Desktop/CompanyFileViewModel.cs): `OpenExampleCommand`, `CreateCommand`, `OpenCommand`, `CloseCommand`, `BackupCommand`, `RestoreCommand`, `UpgradeCommand`, `AddUnitCommand`. **WPF PREVIO:** [captura de proyecto](../auditorias/2026-10-14/company-panel.png) y [continuación visual](../auditorias/2026-10-14-visual/CONTINUACION.md). **Pendiente:** observar manualmente crear/abrir, cancelaciones, rutas inválidas y migración sobre copia con el binario final.

### 2. Diseñar estructura

**Se ve/se puede intentar:** seleccionar/crear borrador de estructura, clonar versión para editar, mantener centros y nodos con su centro físico, papel, pool y unidad, guardar borrador, validar, finalizar/publicar estructura y consultar un resultado publicado. Hay navegación interna por configuración, centros, nodos y revisión/finalización. Los estados de edición/publicación y el tratamiento de referencias antiguas requieren respeto de versiones; no debe describirse como «editar un modelo publicado en el sitio».

**Controles reales:** [ModelWorkspacePanel.xaml](../../src/Valdrio.Desktop/ModelWorkspacePanel.xaml), [ModelWorkspaceViewModel.cs](../../src/Valdrio.Desktop/ModelWorkspaceViewModel.cs). Acciones `NewDraft`, `Clone`, `SaveCenter`, `SaveNode`, `Validate`, `Publish`; `ToggleCenter` y `ToggleUnit`. **Pendiente:** prueba manual con centros activos/inactivos y cambios en estructura ya vinculada a análisis.

### 3. Periodo / análisis

**Se ve/se puede intentar:** recuperar análisis anterior, seleccionar estructura publicada, introducir nombre/fechas/moneda/escenario, crear nuevo período, guardar contexto, conservar/consultar instantáneas, consultar datos importados y cerrar o reabrir el estudio en opciones avanzadas. El nuevo período reutiliza contexto estructural apropiado sin reutilizar importaciones del anterior. El cierre de un estudio condiciona la edición; no es equivalente a la publicación de una ejecución T12.

**Controles reales:** [StudyWorkspacePanel.xaml](../../src/Valdrio.Desktop/StudyWorkspacePanel.xaml), [StudyWorkspaceViewModel.cs](../../src/Valdrio.Desktop/StudyWorkspaceViewModel.cs); `Create`, `Save`, `New`, `Load`, `Capture`, `ToggleState`, `Discard`. **Pendiente:** confirmar el texto exacto de cada advertencia de cierre/reapertura y sus consecuencias en ejecuciones vigentes.

### 4. Importar saldos

**Se ve/se puede intentar:** asistente modal de lectura de fuente CSV/XLSX; opciones de hoja, encabezado, primera fila, separador/codificación, columnas de código y descripción de cuenta, importes en columna única o Debe/Haber, interpretación de números, vista previa, selección de registros/cuentas, agrupación, conversión cuando procede, comparación de períodos, referencia/aprobación de interpretación y **confirmación expresa**. La vista previa **no** debe documentarse como importación confirmada.

**Controles reales:** [ImportColumnMappingWindow.xaml](../../src/Valdrio.Desktop/ImportColumnMappingWindow.xaml), [ImportColumnMappingViewModel.cs](../../src/Valdrio.Desktop/ImportColumnMappingViewModel.cs) y parciales de confirmación; [adaptadores CSV](../../src/Valdrio.Infrastructure/CsvImportAdapter.cs) y [XLSX](../../src/Valdrio.Infrastructure/XlsxImportAdapter.cs). **Pendiente:** repetir recorrido manual en ambas modalidades de importe, origen repetido, cero/negativo, sustitución de lote y distintas regiones.

### 5. Preparar cuentas

**Se ve/se puede intentar:** tabla por cuenta con código/descripción/procedencia e importes; correcciones manuales editables en la **propia tabla**, importe efectivo, diferencia y columna diferenciada de ajustes por reglas. La celda manual confirmada se guarda según flujo de edición; si la cuenta participa en una regla activa, se interpone confirmación económica. Apertura de destinos/partes, clasificación y asignación a pool/nodo, coste directo a objeto o exclusión según operación; revisión de pendientes, guardado y retorno a Correcciones. Se conservan herramientas separadas para revaloración/reinicio/histórico. Los campos de comportamiento y actividad que aparecen como datos de clasificación **no acreditan** un cálculo T14/T15.

**Ajustes extracontables reutilizables incluidos en `main`:** fichas por proyecto con cuenta exacta/prefijo o sin origen, modalidad `Fixed` / `Percentage` / `AddAmount`, código/nombre/motivo, guardado, duplicación, retirada, vista previa, confirmación modal de aplicación por análisis y deshacer controlado mientras los cálculos permiten modificaciones. Cuenta compensada y línea analítica adicional se distinguen; los ajustes manuales se mantienen independientes. **No** existe programación automática mensual ni fórmulas arbitrarias como parte de este editor.

**Controles reales:** [ResourceWorkspacePanel.xaml](../../src/Valdrio.Desktop/ResourceWorkspacePanel.xaml), [ResourceWorkspaceViewModel.Rules.cs](../../src/Valdrio.Desktop/ResourceWorkspaceViewModel.Rules.cs), [ReusableAdjustments.cs](../../src/Valdrio.Application/ReusableAdjustments.cs); prueba [ReusableAdjustmentTests.cs](../../tests/Valdrio.Tests/ReusableAdjustmentTests.cs). **WPF PREVIO:** [captura de cuenta compensada y coste analítico](../evidencias/2026-10-14-ajustes-reutilizables/02-es-applied.png) y [acta con ampliaciones sucesivas](../evidencias/2026-10-14-ajustes-reutilizables/INFORME.md). **Pendiente:** observar en binario final las variantes cuenta sin origen, cancelar dos tipos de modales, segundo período sin autoaplicación y conciliación tras deshacer.

### 6. Reparto primario

**Se ve/se puede intentar:** biblioteca de claves versionadas, nueva clave/versión, definición y destinos por porcentaje/unidades de inductor/reparto igualitario, guardado/publicación de claves, reglas por código exacto/prefijo, asignaciones individuales o masivas, revisión de cobertura y partes sin asignar, edición de composición fija/variable, cálculo/explicación por cuenta, tabla de importes y grafo **de la cuenta seleccionada**, cierre/reapertura con restricciones. **Guardar una regla no es aplicarla**, y guardar una clave no equivale a cerrar el primario. Eliminar una clave puede quedar bloqueado por referencias históricas.

**Controles reales:** [AllocationWorkspacePanel.xaml](../../src/Valdrio.Desktop/AllocationWorkspacePanel.xaml), [AllocationWorkspaceViewModel.cs](../../src/Valdrio.Desktop/AllocationWorkspaceViewModel.cs), [PrimaryAllocation.cs](../../src/Valdrio.Application/PrimaryAllocation.cs). **WPF PREVIO:** [captura de biblioteca, destinos y cierre](../auditorias/2026-10-14/allocation-keys.png). **Pendiente:** abrir/publicar/usar una clave real, probar reversibilidad y confirmar explicaciones de importes con signos opuestos.

### 7. Reparto secundario y cierre

**Se ve/se puede intentar:** asignar claves secundarias publicadas a auxiliares/grupos, completar asignaciones faltantes, probar viabilidad, calcular recíprocos con cancelación, examinar liquidación de auxiliares, costes finales por principal, procedencias y grafo de flujos, diagnosticar rutas sin salida/ciclos inviables y cerrar/reabrir el secundario. El motor actual utiliza el sistema recíproco **LU**; no exponer selector de método directo, escalonado, ABC o TDABC que no existe. Los importes brutos circulantes entre auxiliares **no se suman otra vez** al coste terminal.

**Controles reales:** [AllocationWorkspacePanel.xaml](../../src/Valdrio.Desktop/AllocationWorkspacePanel.xaml), [AllocationWorkspaceViewModel.Secondary.cs](../../src/Valdrio.Desktop/AllocationWorkspaceViewModel.Secondary.cs), [SecondaryAllocation.cs](../../src/Valdrio.Application/SecondaryAllocation.cs). **WPF PREVIO:** [captura de viabilidad y liquidación](../auditorias/2026-10-14/secondary-results.png). **Pendiente:** Windows con prestación mutua positiva, autoprestación, ciclo imposible y dos o más grupos; la captura de coste cero no valida legibilidad de redes densas.

### 8. Flujos y ejecuciones (T12)

**Se ve/se puede intentar:** cálculo explícito, cancelación y refresco; lista de ejecuciones guardadas y selección de ejecución histórica o vigente, diagramas de flujo primario y secundario, selección de nodo o arco para detalle, tabla de contribuciones y conciliación. La ejecución se prepara, calcula y publica como corrida persistida; consultar historial **no** vuelve a ejecutar el solver. Una ejecución que no corresponde a la revisión vigente se identifica como **histórica** y no se rotula vigente por el mero hecho de aparecer. Las cuatro etapas de visualización son origen → primario → secundario → final y **no** representan movimientos contables inventados por cuenta en cada salto recíproco.

**Controles reales:** [CostFlowPanel.xaml](../../src/Valdrio.Desktop/CostFlowPanel.xaml), [CostFlowViewModel.cs](../../src/Valdrio.Desktop/CostFlowViewModel.cs), [CalculationRuns.cs](../../src/Valdrio.Application/CalculationRuns.cs). **PRUEBA PREVIA:** T12 [fusionada tras aceptación Windows](../tareas/T12.md); T13 [servicios/industria con ejecución/reapertura](../tareas/T13.md). **Pendiente:** observación manual de comparación vigente/histórica, errores/cancelación, importes y UX a escala 125–200 % en el ejecutable final.

### Preferencias y ayudas

Opciones transversales de idioma ES/EN, formato cultural y zona horaria; ayuda contextual con ratón/Tab y ajustes visuales de navegación/densidad. Los cuadros «herramientas de desarrollo», catálogo monetario experimental y ejemplo FX **no** deben presentarse en el manual comercial como módulos de controlling generalizados. [MainWindowViewModel.cs](../../src/Valdrio.Desktop/MainWindowViewModel.cs), [MainWindow.xaml](../../src/Valdrio.Desktop/MainWindow.xaml).

## 4. Lo que la base todavía NO demuestra como producto disponible

| Capacidad/afirmación | Estado en este corte | Consecuencia documental |
|---|---|---|
| Captura/confirmación de actividad y capacidad física T14 | Desarrollo T14 en PR [#55](https://github.com/JavierRB35/VALDRIO/pull/55), **borrador**, fuera del `main` revisado | No incluir pantalla T14 como disponible; revisar al fusionarse y probarse |
| Tarifas, absorción, ociosidad económica T15/T16 | Hoja de ruta/decisiones, no entrega completada en este SHA | No prometer tasas de capacidad ni resultados |
| ABC, TDABC o selector multimétodo | Futuro R2–R4 | No confundir con solver recíproco actual |
| Materiales, recetas, órdenes, costes estándares/inventarios | Fases futuras | No describir operaciones como implementadas |
| Rentabilidad por producto/cliente/canal, presupuestos, simulaciones, informes comerciales y cierre integral | Fases futuras | No figurar como navegación activa ni como manual actual |
| Ejecución/despliegue Windows propio de **este corte** | **No realizada en esta auditoría** | Exigir pase de aceptación final y capturas ligadas al SHA |
| «Cualquier cifra a origen en tres interacciones», lector de pantalla, 200 % sin desplazamiento horizontal, redes densas | Evidencia parcial y deudas UX [visual 14/10](../auditorias/2026-10-14-visual/INFORME.md) | No certificar facilidad/accesibilidad universales |

## 5. Incoherencias documentales detectadas y decisión

1. `README.md` permanece centrado en T08, presenta T09+ como no disponible e incluye instrucciones históricas T05 y estado de fichero obsoletos. **Corrección:** reescribir portada con mapa del `main` actual, estado y enlace a este inventario; conservar actas antiguas como historial, no como producto vigente.
2. `ESTADO_PROYECTO.md` mezcla múltiples épocas y ramas. **Corrección:** añadir encabezado de corte fechado e índice hacia esta línea base, preservando el registro histórico sin borrarlo.
3. `docs/tareas/T12.md` contiene una cabecera de «fusionada» seguida de criterios redactados como «pendientes». **Tratamiento:** su cuerpo es memoria de la rama candidata; no tomar sus listas históricas por el estado del `main`. No reescribir actas técnicas sin autorización.
4. `docs/evidencias/2026-10-14-ajustes-reutilizables/INFORME.md` arranca como rama candidata y recoge varias ampliaciones posteriores. **Tratamiento:** la existencia efectiva se comprueba contra archivos de `main`; conservar cifras, SHA y limitaciones por incremento.
5. Guías de marca/estrategia/especificación v0.4: **no se modifican** por el mero desfase entre producto planificado y entregas; sirven de requisitos y visión. La realidad operacional vive en la presente capa de documentación.

## 6. Regla para mantener este inventario y escribir el manual definitivo

Cada cambio de pantalla/flujo exige: (a) comparar nombre y controles con XAML ES/EN; (b) seguir comando y validaciones hasta servicio/persistencia; (c) recorrer escenario positivo, negativo y reapertura **en Windows sobre el SHA exacto**; (d) guardar capturas sin datos personales, logs y matriz con resultado; (e) corregir aquí el apartado correspondiente; (f) **solo entonces** trasladar al manual de usuario. Separar de forma explícita «posibilidad implementada» de «comportamiento observado». No inferir resultados de capturas de prototipo ni de textos T14/R1–R4.

**Este corte documental no modifica `.valdrio`, esquemas de base de datos, cálculos, pruebas, workflows ni menús. GitHub Actions: no ejecutado; gasto provocado 0 USD.**

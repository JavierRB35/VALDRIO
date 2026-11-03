# Flujo consolidado, catálogo y espacios de trabajo — 2026-11-03

## Alcance y decisiones

Trabajo realizado en el repositorio real `C:/Users/cjrui/source/repos/VALDRIO`, partiendo de `e4af587`, en la rama `fix/consolidated-workflow-20261103`. No se modificaron fuentes sincronizadas ni la copia de auditoría preexistente. Sin nuevas dependencias ni migraciones de esquema. Integración local autorizada posteriormente por el propietario; sin publicaciones remotas.

Se consultó íntegramente el PDF del proyecto cuyo título interno es «PROMPT MAESTRO — PROTOCOLO DE DESARROLLO ASISTIDO POR INTELIGENCIA ARTIFICIAL», además de AGENTS.md y las directrices aplicables del repositorio.

Incrementos: (1) cierre y persistencia; (2) informes y tarifas; (3) catálogo y duplicación; (4) espacios adaptables, regresión y revisión visual.

## Resultado por área

- **Cierre secundario**: cierre, captura de entradas y ejecución consolidada se guardan en la misma transacción. Un fallo revierte los tres. El primario debe estar completo; se conservan validación matemática, clasificación y políticas económicas. Las solicitudes antiguas de cálculo/guardado reutilizan el resultado cerrado idéntico, sin otra ejecución.
- **Compatibilidad**: al abrir en edición un archivo con cierre antiguo sin resultado consolidado, se materializa una única ejecución desde la publicación cerrada, sin repetir el solucionador recíproco ni reabrir repartos. La apertura de solo lectura no escribe ni realiza esta adaptación.
- **Trazabilidad**: la ejecución vigente se elige internamente; el histórico permanece inmutable. Cambiar la revisión invalida la vigencia anterior. El informe permite consultar históricos secundariamente y conserva la consulta del primario antes del cierre.
- **Servicios auxiliares**: se retira la entrada de menú y toda navegación normal a la pantalla independiente. Su informe se integra en Resultados, exclusivamente de consulta. Se distinguen actividad no asignada y capacidad ociosa, se evitan duplicados por grupos y se respeta la evidencia congelada de OwnerOnceCapacity. Los componentes antiguos se conservan como compatibilidad interna, sin acceso desde la interfaz normal.
- **Magnitudes físicas**: se derivan cantidades cuando la clave identifica el mismo inductor/unidad física; las proporciones se concilian exactamente con la actividad. No se interpretan porcentajes sin significado físico documentado como horas. Se explica la ausencia de base, actividad o compatibilidad; no se mezclan observaciones de otra estructura o periodo.
- **Tarifas**: carga automática desde el resultado vigente cerrado y los datos físicos confirmados. Se elimina la acción principal de actualización manual. Cambiar actividad física no obliga a reabrir el coste cerrado; se conservan las bases fijas/variables y la valoración auxiliar guardada.
- **Diagnóstico**: AUX_CLASSIFICATION_REQUIRED identifica recurso, centro y clave. No se aceptan costes mixtos pendientes de descomponer. Se conservan los diagnósticos y coberturas existentes del primario y secundario. Los errores de tarifas muestran su causa de dominio.
- **Catálogo**: coste de referencia editable con formato regional y procedencia manual; importar abre el diálogo con el propietario integrado correcto, valida antes de aplicar y combina con el borrador actual. Las importaciones siguen siendo previsualizaciones hasta guardar. Se preservan validación de unidades, duplicados y guardado transaccional. La acción por fila abre la composición por ID y carga borrador o versión publicada respetando su estado.
- **Duplicar proyecto**: copia SQLite consistente, destino nuevo obligatorio, sin sobrescribir origen; informa la ruta y ofrece abrir la copia. Se resuelven cambios pendientes antes de copiar. Los dos archivos evolucionan independientemente y conservan su historial inicial.
- **Interfaz**: más superficie útil en actividad, capacidades, claves, tarifas, catálogo, composición y órdenes; tablas con altura natural y desplazamiento exterior; paneles relacionados se apilan en ventanas estrechas. Acciones del catálogo y pie de pantalla permanecen accesibles.
- **Fuera de alcance deliberado**: no se añaden materiales a recetas ni se cambian consumos reales, semielaborados, mermas o política contable.

## Archivos principales

- Persistencia: `SqliteCompanyFileService.Allocations.cs`, `.Studies.cs`, `.Runs.cs` y apertura de sesión.
- Cálculo/informes: `CalculationRuns.cs`, `PrincipalRateReport.cs`, `AuxiliaryCapacity.cs`, nuevo `AuxiliaryServiceReport.cs`.
- Interfaz: `CostFlowViewModel*`, `CostFlowPanel*`, `CompositionEditor.cs`, `CompositionWindow*`, `ProductCostImport.cs`, `CompanyFileViewModel.cs`, `CompanyFilePanel.xaml`, ventanas afectadas y paneles adaptables.
- Recursos y ayuda: cadenas español/inglés y capítulos 01, 08, 12, 13 y 14 en ambos idiomas.
- Evidencia reproducible: `ConsolidatedClosureTests.cs`, ampliación de `T18CompositionCostTests.cs`, `MainWindow.ConsolidatedSmoke.cs` y opción `--consolidated-checks-only`.

## Verificación ejecutada

- Compilación Release de la solución: correcta, sin advertencias ni errores.
- Batería completa: **815 casos**, 809 superados inicialmente; seis fallos de compatibilidad detectados y corregidos (consulta del primario sin ejecución y llamadas antiguas de cálculo tras el cierre automático).
- Reejecución focalizada de auditoría, T13, cierre y localización tras esas correcciones: **19/19**. No se repitieron los otros 809 casos sin cambios relevantes.
- Informe auxiliar, importación/composición y localización tras los últimos ajustes: **16/16**, incluidos siete nuevos casos de formatos regionales y decimales extranjeros no ambiguos.
- Vista consolidada predeterminada, auditoría y consultas T12: **18/18** tras el último ajuste de selección del informe.
- Comando de duplicación: **1/1**, cubriendo copia sin abrir, rechazo de sobrescritura y apertura opcional.
- Pruebas anteriores focalizadas: 20 y 50 casos superados de cierre, reciprocidad, tarifas, composición y validación.
- Interfaz WPF real: **57 comprobaciones** en español/inglés, anchos 1536/1000 y capturas con escala 150%; edición de celda real, importación, receta por ID, reapertura, tarifas, informe y espacios de trabajo. Revisión visual del ajuste final de capacidades, catálogo e informe.
- `scripts/verify_help.py`: 17 vistas lógicas y 18 XAML cubiertos.
- `git diff --check`: sin errores.

Resultados locales: `tests/Valdrio.Tests/TestResults/consolidated-final.trx`, `consolidated-corrections.trx`, `consolidated-report-import.trx`, `consolidated-duplicate.trx`, `consolidated-default-report.trx`; capturas y lista de controles en `artifacts/consolidated-ui-verified/`.

## Límites y riesgos conocidos

Los porcentajes históricos sin unidad física no permiten deducir horas con rigor: el informe explica esa falta de información. Los números ambiguos con tres cifras tras un separador se interpretan según el formato regional seleccionado; revisar la previsualización antes de guardar. Tablas muy grandes crecen con el contenido, a cambio de mayor trabajo de presentación que una tabla virtualizada con desplazamiento interior. Las pruebas se realizaron sobre archivos sintéticos y copias desechables, no sobre datos de producción del usuario.

Integración local autorizada por el propietario el 2026-11-03 mediante «fusiona». Se verificaron rama, árbol de trabajo, ausencia de divergencia con main, diff y resultados de las comprobaciones anteriores. Se reutilizan las verificaciones satisfactorias, pues no hay cambios funcionales posteriores. Las evidencias generadas permanecen locales; no se publica al remoto.

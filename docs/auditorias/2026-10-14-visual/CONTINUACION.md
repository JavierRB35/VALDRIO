# Continuación de la revisión visual — 14/10/2026

Segunda entrega sobre `ui/visual-ergonomics-20261014`, posterior a `315ef9f`. La PR #48 queda pendiente de revisión y aprobación del propietario; esta entrega no autoriza su fusión. [Galería de esta entrega](continuacion/galeria.html). [Diagnóstico y referentes iniciales](INFORME.md).

## Cambios y resultado

| Hallazgo | Criterio de diseño | Implementación y alcance |
|---|---|---|
| El arranque no ofrecía un ejemplo utilizable desde la interfaz. | Aprendizaje con datos representativos y acción explícita. | «Cargar ejemplo» crea una copia del proyecto sintético de servicios ya versionado, abre sus análisis y preserva el original. Cancelación y destino existente comprobados. No es un nuevo modelo de cálculo. |
| La intención de importar no estaba expresada al iniciar. | Guiar por intención conservando los requisitos del proceso. | «Importar contabilidad» lleva al nombre del proyecto y explica estructura → análisis → archivo. Reutiliza el recorrido existente; no se presenta como un asistente completamente nuevo ni permite omitir requisitos. |
| Marcos anidados y columnas fijas dificultaban leer el formulario de análisis. | Jerarquía por títulos y adaptación del contenido al espacio. | Estilo compartido de sección sin marco interior; los campos pasan de dos columnas a una manteniendo bindings y orden de lectura. El alta de proyecto también usa etiquetas sobre los campos. |
| El menú ocupaba espacio necesario en ventanas estrechas. | Control reversible del espacio de trabajo. | Botón «Menú» oculta/restaura la navegación; permanece accesible y conserva sus fases y acciones. No se oculta automáticamente. |
| Biblioteca de claves y destinos compartían un espacio rígido. | Presentación progresiva sin retirar la edición en tabla. | Los dos paneles se apilan cuando no alcanzan su ancho mínimo. Se conservan edición, selección, comandos, estados bloqueados y altura de las tablas. |
| La explicación de una aportación quedaba lejos de la tabla. | Drill-down contextual y retorno al punto de origen, coherente con Cube/Power BI citados en el informe inicial. | Acción explícita que expande la explicación y mueve el foco; retorno a aportaciones conservando selección. Comprobado en el recorrido metodológico. No acredita la regla de tres interacciones para cualquier cifra del producto. |

Se preservan los estilos, paleta y lenguaje visual aprobados. El motor, los contratos económicos, el esquema de SQLite y los algoritmos no se modifican. El nuevo comando de ejemplo copia un archivo y utiliza el servicio de apertura existente.

## Registro de archivos de esta continuación

Prefijo `src/Valdrio.Desktop/`, salvo donde se indica otra ruta.

| Archivo | Cambio |
|---|---|
| `AdaptiveColumnsPanel.cs` | Panel reutilizable de una/dos columnas. |
| `CompanyFilePanel.xaml`, `CompanyFilePanel.xaml.cs` | Accesos de inicio y formulario vertical; foco en el inicio de importación. |
| `CompanyFileViewModel.cs` | Apertura de copia del ejemplo mediante el servicio existente y estado de actividad. |
| `Valdrio.Desktop.csproj` | Distribuye el ejemplo sintético versionado en la salida de compilación/publicación. |
| `StudyWorkspacePanel.xaml` | Secciones y formulario adaptable. |
| `Styles/ValdrioTheme.xaml` | Estilo reutilizable `GroupBox.Section`. |
| `MainWindow.xaml`, `MainWindow.Presentation.cs` | Menú reversible, etiquetas accesibles y ancho de contenido. |
| `AllocationWorkspacePanel.xaml`, `AllocationWorkspacePanel.xaml.cs` | Paneles adaptables y navegación a explicación/retorno. |
| `CompanyFilePanel.Smoke.cs` | Cancelación, copia real, preservación del original/destino existente y foco de importación. |
| `MainWindow.VisualSmoke.cs` | Menú, espacio recuperado, orden de campos al 200 % y capturas. |
| `MainWindow.MethodologySmoke.cs` | Selección real en tabla, explicación y retorno con foco. |
| `src/Valdrio.Localization/Resources/Strings.resx`, `Strings.es.resx` | Nuevas etiquetas en inglés y español. |

La evidencia está en `continuacion/`: capturas originales con SHA-256, resultados WPF, inventario estático respecto a `315ef9f` y registros de compilación/pruebas. El inventario complementa las pruebas, no demuestra por sí solo ausencia de regresiones.

## Validación local de esta entrega

- Compilación Release del escritorio: **0 errores, 0 advertencias**.
- Suite local: **655/655**, 0 fallos y 0 omitidas; duración registrada **3 min 45 s**. Resultado TRX: `tests/Valdrio.Tests/TestResults/visual-continuation.trx`.
- Recorrido completo WPF: salida **0**, **564 comprobaciones** en **17 registros**, todos `passed`; **85 capturas**. Son comprobaciones automatizadas, no 564 tareas distintas realizadas por usuarios.
- Inventario frente a `315ef9f`: ningún control nombrado retirado. La única diferencia de binding retirada es `Height` del contenedor de claves, trasladada a `MinimumRowHeight` del panel adaptable con la misma fuente y conversor. No se retiran bindings de datos ni comandos.
- Ejecución: `dotnet build src/Valdrio.Desktop/Valdrio.Desktop.csproj -c Release --no-restore`; `dotnet test Valdrio.slnx -c Release --no-restore --logger "trx;LogFileName=visual-continuation.trx"`. Capturas mediante el ejecutable con `samples/t01-smoke.valdrio-dev.json --capture-dir=<directorio-nuevo> --methodology`.

## Límites y siguiente decisión

- Revisar con el propietario la densidad y el espacio dedicado a instrucciones antes de abreviar más texto en estructura/cuentas. Se conserva la información metodológica.
- La escala 125/150/200 % se ejercita mediante LayoutTransform; no equivale a pruebas físicas multimonitor/DPI.
- La navegación por foco y AutomationPeer no sustituye una sesión con lector de pantalla ni una evaluación de accesibilidad completa.
- La explicación/retorno se prueba en el escenario metodológico automatizado. Falta aceptación por tareas con datos representativos, importes negativos y grandes volúmenes para extender la garantía a todas las cifras.
- El ejemplo es sintético. Su empaquetado se verifica en la salida local; no se ha probado un instalador distribuido.
- No se ejecutan GitHub Actions, no se alteran workflows ni facturación. Consumo provocado por esta tarea: **0 USD de Actions**.

Después de la revisión visual, el visto bueno y la fusión, se abordará una etapa separada para madurar las reglas reutilizables de ajustes extracontables a partir de las fuentes y luego implementarlas. No se incluye esa lógica en esta PR.

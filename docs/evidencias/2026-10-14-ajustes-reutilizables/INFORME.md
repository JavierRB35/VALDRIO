# Ajustes reutilizables — entrega candidata

Fecha: 2026-10-14. Base main@8d7ce5c4a661c9bee1e396feef1a422f72f73bfe.
Rama: `design/reusable-adjustments-20261014`. Pendiente de revisión y fusión.

## Resultado y recorrido

En **Preparar cuentas**, abrir Ajustes reutilizables, crear la ficha y guardarla. Aplicar presenta las cuentas y efectos antes de confirmar. La ficha admite cuenta exacta o prefijo, importe fijo, porcentaje o importe adicional; permite modificar, duplicar y retirar. Deshacer revierte la aplicación mientras los cálculos estén abiertos. Las fichas se comparten en el proyecto, pero cada análisis exige aplicación expresa.

Se bloquean los solapamientos entre sustituciones activas antes de aplicar y nuevamente en la transacción. Las cuentas originales se conservan y compensan; el coste analítico tiene nombre/código propios y queda pendiente de preparación. Las correcciones manuales aparecen separadas de los ajustes automáticos. No se añaden fases ni se cambian los comandos existentes de importación, preparación, reparto o cierre.

La [decisión ADR-0017](../../adr/ADR-0017-ajustes-reutilizables.md) detalla las reglas, versiones, reversión, compatibilidad y límites.

## Cobertura añadida

Nueve pruebas de integración/unidad cubren sustitución y reversión, reaplicación, solapamiento, ausencia de cuentas, revisión obsoleta, idempotencia, porcentaje/adición/saldos negativos, independencia de correcciones manuales, versiones/retirada, replay, reapertura SQLite, snapshots, disponibilidad sin autoaplicación en otro análisis, rollback transaccional, cierre/reapertura, comandos reales del ViewModel y cálculo primario antes y después de deshacer.

La comprobación WPF adicional usa controles nativos y servicios reales para guardar una ficha, abrir la previsualización, confirmar y deshacer. Se capturan la previsualización y aplicación en español y el estado revertido en inglés. La batería visual existente también se ejecuta.

## Límites y observaciones

- La aceptación visual del propietario queda pendiente. Estas comprobaciones no demuestran ausencia absoluta de regresiones ni sustituyen una prueba de usuario con datos propios.
- Los archivos históricos siguen siendo legibles; lectores antiguos pueden rechazar archivos que contienen el nuevo contrato. Usar una versión compatible con fichas.
- No se ha medido rendimiento del catálogo con miles de versiones. No se implementan programación automática, fórmulas arbitrarias ni periodificación por calendario.
- Deshacer restaura la preparación previa; se informa de que los destinos posteriores de los recursos afectados deben revisarse. Las correcciones monetarias independientes permanecen como diferencias pendientes.
- En una primera ejecución de la suite se detectó una etiqueta inglesa de historial ausente; fue corregida antes de la ejecución final. Un intento concurrente de recompilación encontró DLL bloqueada por las pruebas; se repitió secuencialmente. El primer capturador enfocaba Preferencias al buscar un botón templado; se corrigió para revelar el control real.
- No se ejecutaron workflows. GitHub Actions: **0 USD provocado por esta entrega**. Los disparadores manuales existentes no se modifican.

## Archivos de implementación

Los cambios de dominio añaden procedencia analítica opcional; Calculation valida esa procedencia; Application contiene el motor, contratos, reversión e integración con preparación/reparto; Infrastructure persiste y audita las operaciones atómicamente. Desktop añade fichas y separa columnas, conserva acciones existentes y reutiliza estilos globales. Las traducciones son ES/EN. No se modifica Reporting ni el solver matricial.

Lista de archivos de código modificados o añadidos:

- `src/Valdrio.Application/AllocationWorkspace.cs`
- `src/Valdrio.Application/PrimaryAllocation.cs`
- `src/Valdrio.Application/ResourceCorrectionCommand.cs`
- `src/Valdrio.Application/ResourceDraftEditor.cs`
- `src/Valdrio.Application/ResourceHistory.cs`
- `src/Valdrio.Application/ResourceWorkspace.cs`
- `src/Valdrio.Application/ResourceWorkspaceRules.cs`
- `src/Valdrio.Calculation/ResourcePreparationRules.cs`
- `src/Valdrio.Desktop/App.xaml.cs`
- `src/Valdrio.Desktop/ResourceEditorForm.cs`
- `src/Valdrio.Desktop/ResourceWorkspacePanel.xaml`
- `src/Valdrio.Desktop/ResourceWorkspacePanel.xaml.cs`
- `src/Valdrio.Desktop/ResourceWorkspaceViewModel.cs`
- `src/Valdrio.Domain/ResourceDraft.cs`
- `src/Valdrio.Infrastructure/CompanyFiles/SqliteCompanyFileService.Resources.cs`
- `src/Valdrio.Localization/Resources/Strings.es.resx`
- `src/Valdrio.Localization/Resources/Strings.resx`
- `src/Valdrio.Application/ReusableAdjustments.cs`
- `src/Valdrio.Desktop/MainWindow.AdjustmentSmoke.cs`
- `src/Valdrio.Desktop/ResourceWorkspaceViewModel.Rules.cs`
- `tests/Valdrio.Tests/ReusableAdjustmentTests.cs`

## Resultados finales sobre el código entregado

- Compilación Desktop Release: **0 errores, 0 advertencias** (`build.log`).
- Suite completa .NET: **664/664 superadas, 0 fallidas, 0 omitidas**, duración 4 min 9 s (`tests.log`, `test-summary.json`). Incluye las nueve pruebas nuevas.
- WPF real: proceso finalizado con código 0; **558 comprobaciones superadas en 17 registros de interacción** (`wpf-checks.json`). El total incluye 21 comprobaciones repetidas para preparar la segunda empresa de prueba y diez del nuevo recorrido de ajustes; no son 558 escenarios independientes.
- Inspección visual de las tres capturas: ficha y aviso legibles, botones de confirmación/cancelación presentes, separación entre correcciones manuales y reglas en la tabla, localización de controles en inglés. Los nombres y motivos introducidos por el usuario conservan su idioma original.
- `git diff --check`: sin errores de espacios.

Comandos locales: `dotnet build src/Valdrio.Desktop/Valdrio.Desktop.csproj -c Release --no-restore`; `dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-restore`; aplicación Release con `samples/t01-smoke.valdrio-dev.json --capture-dir=artifacts/adjustments-final --methodology --adjustments`.

### Capturas de la aplicación real

![Previsualización antes de aplicar](01-es-preview.png)

![Coste analítico y compensaciones aplicadas](02-es-applied.png)

![Ficha después de deshacer, en inglés](03-en-undone.png)

## Corrección posterior: etiquetas de trazabilidad (captura del propietario)

La excepción `Missing neutral English UI resource AllocationUI.ShowSource` reveló dos claves con prefijo incorrecto: `Allocation.ShowSource` y `Allocation.BackToAmounts`. Se corrigen a `AllocationUI.*` en ambos idiomas. No cambia la lógica de cálculo.

La nueva prueba lee los bindings del XAML real de AllocationWorkspacePanel y exige que existan en todos los idiomas publicados. Antes del arreglo falla exactamente con `AllocationUI.ShowSource`; después pasan las siete pruebas de completitud/localización seleccionadas. Desktop Release compila con cero errores y cero advertencias. No se repitió la suite económica completa ni el recorrido WPF: los 664 tests y 558 comprobaciones anteriores corresponden a b70075c. Los hashes anteriores conservan esa evidencia histórica. El proceso abierto en Visual Studio debe detenerse y recompilarse para cargar los recursos nuevos.

## Corrección posterior: destino ocupado al cargar ejemplo

Se comprueba si el nombre elegido ya corresponde a un archivo o directorio antes de copiar. Se muestra el diagnóstico localizado `File.AlreadyExists`, se conserva el proyecto abierto y no se escribe sobre el destino. La copia sigue usando `overwrite: false`; una colisión concurrente se traduce al mismo diagnóstico. El selector puede volver a abrirse para elegir otro nombre.

Validación local: cinco pruebas seleccionadas superadas (archivo existente, directorio existente, cancelación y conservación de sesión, nombres y errores de apertura relacionados). Desktop Release: cero errores y advertencias. La primera preparación de las pruebas retenía el archivo abierto y se corrigió para cerrarlo antes de abrirlo desde el ViewModel. No se repitió la batería completa ni el recorrido WPF. Archivos: CompanyFileViewModel.cs y ExampleDestinationTests.cs; logs adjuntos. GitHub Actions no ejecutado.

## Saldo visible al indicar las cuentas

La ficha muestra bajo Cuenta exacta o prefijo el número de cuentas coincidentes, el saldo importado convertido a moneda funcional y el saldo efectivo actual. Se actualiza al escribir, antes de guardar/aplicar, y tras actualizar el workspace. No incluye recursos analíticos generados ni altera importes. Los códigos conservan ceros iniciales. Un patrón vacío/inválido muestra ayuda; sin coincidencias se indica expresamente, sin presentar un cero engañoso.

Validación: 13 pruebas de reglas y localización superadas; nueva prueba con prefijo, cuenta exacta, ceros iniciales, saldo negativo, patrón inválido y ausencia de coincidencias. El caso de aplicación verifica que el saldo importado sigue visible mientras el efectivo pasa a cero. Compilación Release sin errores ni advertencias. Se añade recorrido WPF específico `--capture-dir=... --adjustments-only` para verificar las fichas sin repetir pantallas ajenas al cambio. No se repite la suite económica completa. Archivos: ResourceWorkspaceViewModel.Rules.cs, ResourceWorkspacePanel.xaml, Strings.resx, Strings.es.resx, ReusableAdjustmentTests.cs y capturador App.xaml.cs/MainWindow.AdjustmentSmoke.cs.

## Ampliación aprobada: costes sin cuentas de origen y feedback

La cuenta/prefijo pasa a ser opcional. Vacío o sin coincidencias crea una línea analítica adicional y no compensa cuentas existentes. La vista previa explica expresamente este efecto antes de confirmar. Importe fijo usa el valor indicado; añadir usa base cero; porcentaje de cero produce cero. Los patrones mal formados siguen siendo inválidos. La protección de códigos únicos, aplicación expresa, revisión, cierre y reversión se conserva.

Cada ficha muestra instrucciones para guardar/aplicar/confirmar y feedback contextual: cambios pendientes de guardar, vista previa preparada (sin aplicar todavía), cancelación, regla aplicada y aplicación deshecha. Los diagnósticos aparecen junto a los botones, además del estado general. La fila adicional utiliza código y nombre analíticos y queda pendiente de preparación/reparto.

Validación final de este incremento: 15 pruebas seleccionadas superadas, incluida creación/confirmación/reversión con campo vacío y prefijo sin coincidencias; Desktop Release sin errores ni advertencias. Esta ampliación sustituye el rechazo anterior por ausencia de cuentas descrito en la evidencia inicial. No se ha repetido la suite económica completa. GitHub Actions: sin ejecución.

WPF real: proceso terminado con código 0, 17 comprobaciones del recorrido de ajustes y 21 del recorrido guiado. Comprobado saldo visible antes de guardar, vista previa sin origen, fila analítica y mensaje de éxito visible. [Captura final con feedback](feedback/04-es-additional-feedback.png).

## Código propuesto y visibilidad del apartado

Las fichas nuevas proponen un código `AJ-<cuenta sin comodín>-<nombre normalizado>` (sin cuenta: `AJ-<nombre>`), con mayúsculas, sin tildes y separadores de guion, hasta 80 caracteres. Se preservan ceros iniciales. El código se actualiza con nombre/cuenta hasta que el usuario lo modifica o guarda la ficha; los códigos guardados no se renumeran al editar el nombre. Las validaciones existentes siguen detectando colisiones al aplicar; la propuesta no reserva códigos ni implica aplicación.

El apartado se presenta como tarjeta con borde verde de marca, título «Reglas de ajustes extracontables» a 20 px, desplegado inicialmente y botón principal Añadir ficha. Conserva la posibilidad de plegarlo y su ubicación exclusiva en Preparar cuentas. No cambia el motor económico.

Validación: pruebas seleccionadas de reglas y localización y compilación Release, logs rule-code adjuntos. La nueva prueba comprueba inferencia por nombre/cuenta, tildes, ceros iniciales y preservación de códigos manuales y guardados. No se repite la batería económica completa.

Resultado: 16/16 pruebas superadas; compilación con 0 errores y 0 advertencias. Recorrido WPF finalizado con código 0 y captura inspeccionada: [apartado destacado](section/03-en-undone.png). Sin GitHub Actions.

## Confirmación modal de reglas y correcciones manuales

Aplicar regla abre ahora un modal con explicación económica y botones Confirmar operación/Cancelar. Cerrar o cancelar no aplica nada. La confirmación usa la operación preparada y su revisión; el almacenamiento conserva su control de concurrencia. Para cuentas coincidentes se aclara que el saldo efectivo incluye las correcciones manuales anteriores. Sin cuentas de origen se explica exclusivamente la creación de una línea adicional.

Guardar una corrección manual sobre una cuenta compensada o una línea analítica de una aplicación activa abre el mismo tipo de modal. Identifica cuenta y regla, corrección manual anterior/nueva y saldo efectivo antes/después; explica que la corrección es independiente y se conserva al deshacer la regla. Se permiten ambos órdenes: corrección→regla y regla→corrección. Cancelar restaura la visualización de importes guardados. Una edición sin cambio de importe no necesita confirmación. Las cuentas no afectadas conservan la edición directa existente.

El modal tiene propietario, texto desplazable, botones siempre visibles, Escape/cierre como cancelación y foco inicial en Cancelar. No se aplica por pulsar Intro accidentalmente. Las pruebas usan el contrato de confirmación; el recorrido WPF abre la ventana real y pulsa sus botones mediante automatización nativa.

Validación: 25 pruebas seleccionadas superadas (reglas, correcciones inline y localización), compilación Release sin errores ni advertencias. Nuevo caso con manual +20, regla que compensa 1020, cancelación de manual +50 sin escritura, aceptación con saldo efectivo 30 y deshacer con total restaurado 1050. No se repitió la suite completa. No se ejecutó GitHub Actions.

Recorrido WPF finalizado con código 0. Verificados cancelar y aceptar en ambos modales, preservación de importes al cancelar y actualización de 500 a 525 al aceptar la corrección independiente. [Modal de regla](modals/01-es-modal.png) y [modal de corrección manual](modals/05-es-manual-modal.png).

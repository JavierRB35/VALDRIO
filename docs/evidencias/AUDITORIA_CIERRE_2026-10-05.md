# VALDRIO — cierre de correcciones de la auditoría
Fecha: 5 de octubre de 2026.
Base revisada: `14b6501a9fe38cff700d8e772347a63101997dfc`.
Código final comprobado: `881e666fe67ecbd0654a7c796bfe5446b97afa23`.
[CI de comprobación](https://github.com/JavierRB35/VALDRIO/actions/runs/37298097480).

## Resultado

La rama main no había avanzado desde la auditoría del 5 de octubre. R01–R07 estaban aplicadas y A01–A06 seguían pendientes. Esta entrega implementa y verifica A01–A06. A07 queda identificado como integración persistente posterior, no como una transacción ya probada.

Se mantiene el stack y el alcance. No se modifican las fuentes canónicas v0.4, los esperados económicos ni D03/D04–D07.

## Reproducción antes de corregir

Se añadió primero una suite de 15 regresiones sin cambiar producción.

- Commit `5aff170f590189a85b0ccec57be1ddd94b5e5648`.
- [Run de reproducción](https://github.com/JavierRB35/VALDRIO/actions/runs/37295809956).
- Compilación correcta.
- **82 pruebas existentes aprobadas y 15 regresiones fallidas**, 0 omitidas.

Los fallos reprodujeron pérdida de primitivas al serializar, la ambigüedad de identificadores del hash, aceptación de IDs por defecto, desviación de un quantum FX en ambos signos y modificación de colecciones de resultados.

No se cambiaron esos esperados para cerrar los defectos. Las 15 regresiones pasan con las correcciones.

## Correcciones implementadas

| Hallazgo | Resultado | Implementación/pruebas principales |
|---|---|---|
| A01 | Fechas, instantes, IDs y Quantity se reconstruyen correctamente. Base canónica versionada; rechazo de campos ausentes/duplicados/extra, versiones futuras y conversiones FX incoherentes. | CanonicalValueJsonConverters, CanonicalSnapshotJson; Audit20261005RegressionTests, SerializationAndFxTests y BoundaryTests |
| A02 | Codificación inequívoca previa a SHA-256, con campos/arrays JSON, null explícito, políticas completas y esquema fx-v2. A\|B/C y A/B\|C dan huellas distintas. | FxConversionHash; regresión de separadores, Unicode, políticas e invariancia cultural |
| A03 | Edición regional capturada fuera de Application, decimal/BusinessDate tipados, resultado regional y diagnósticos concretos. Fecha ISO identificada expresamente. | FxConversionExampleService, MainWindowViewModel, controles WPF y MainWindow.FxSmoke |
| A04 | IDs vacíos se rechazan en constructores, arithmetic/boundaries, serialización y destinos de redondeo. Monedas/políticas/contextos se validan. | Identifiers, Quantity, FxModels, redondeadores y BoundaryTests |
| A05 | Quantización desde producto exacto BigInteger con MidpointToEven; bruto decimal aproximado documentado y puente conservado. FX_AMOUNT_BELOW_QUANTUM disponible sin perder origen. | ExactDecimalProduct, FxConverter, FxRateApplication; límites, signos, empates, overflow y diagnóstico EN/ES |
| A06 | Colecciones copiadas y no modificables en resultados balanceados, proyecciones, candidatos y diagnósticos. | FrozenList y constructores; mutación de originales y rechazo de modificación de colección expuesta |
| A07 | Estado corregido: orquestación y validación de entrada, no atomicidad SQLite certificada. Revisiones long y límites comprobados. | RevalueStudyContracts; integración pendiente de T03/T05/T06A/T07 |

Las excepciones técnicas del editor FX se envían a Trace por separado. Los errores esperados usan DomainDiagnosticFormatter y mantienen el último resultado válido. La gestión persistente del log técnico se concretará con la operación de archivos.

La presentación dispone de un overload Money + CurrencyMetadataSnapshot que preserva unidades menores de moneda. Se prueban KWD 0,001 y JPY. Las futuras pantallas de producto deben usar sus metadatos congelados; no deducir moneda de CultureInfo.

## Verificación del código final

[Run 37298097480](https://github.com/JavierRB35/VALDRIO/actions/runs/37298097480), head `881e666fe67ecbd0654a7c796bfe5446b97afa23`:

- **114 pruebas aprobadas, 0 fallidas, 0 omitidas**. Se conservan las 82 y se añaden 32.
- Build Release con **0 warnings y 0 errores**.
- 57 fuentes canónicas de texto/JSON/Python verificadas por SHA-256.
- 39/39 referencias racionales; corresponden al verificador Python, no a recorridos completos del motor.
- 26 claves de diagnóstico de producción cubiertas en EN/ES.
- Runners de redondeo, metadatos monetarios, tiempo y FX aprobados.
- Guards de dependencias/precisión, tiempo ambiente y red conservados.
- Trabajos de invariancia temporal en UTC y Europe/Madrid aprobados.
- Publicación Windows x64 autocontenida aprobada.
- Arranque WPF y recuperación de preferencias inválidas aprobados.
- **12 comprobaciones del botón WPF FX** aprobadas, además de cuatro capturas.
- TRX y JSON de interacción conservados como artifacts de CI.

No se atribuye ejecución local en esta revisión: el entorno estaba desconectado. La comprobación se hizo en runners Windows/Ubuntu de GitHub Actions.

## Recorrido WPF comprobado

La comprobación usa TextBox y ComboBox enlazados, actualiza sus bindings e invoca el botón mediante ButtonAutomationPeer. No llama directamente al método privado ApplyFxRate.

Casos: es-ES, en-US y de-DE; cultura capturada; conservación del texto durante cambio de región; confirmación con la cultura anterior; agrupación incorrecta y resultado anterior intacto; fecha ISO inválida; fuente vacía; quote negativo; cambio de idioma sin cambiar la región.

La CI comprueba el archivo `fx-editor-interaction.json`, su estado y las 12 comprobaciones. El proceso debe terminar correctamente y producir también las cuatro capturas. No equivale a una revisión visual manual completa de Windows.

## Límites y próxima entrega

- CanonicalSnapshotJson es una base de valores. Los esquemas económicos/completos de RunInput y sus hashes se implementarán en T05.
- No hay todavía SQLite productivo, importador ni motor completo de centros/capacidad/productos/márgenes.
- FxConversionExampleService conserva un original fijo de demostración. No es un gestor persistente de RateSets.
- A07 requiere originales autorizados en persistencia, revisión esperada, cobertura, revalidación, commit/auditoría atómicos, rollback y reapertura. La infraestructura se prueba en T03 y el agregado completo cuando existan estudios/recursos/particiones.
- D03 requiere archivo representativo antes de T06. D04–D07 continúan como propuestas.
- El catálogo monetario actual sigue siendo un fixture.

La siguiente fase es [T03, con estas directrices](../directrices/T03_SIGUIENTE_FASE.md). No se declara iniciada por añadir este documento.

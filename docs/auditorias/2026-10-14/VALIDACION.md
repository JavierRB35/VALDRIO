# ValidaciÃ³n local y archivos de la entrega

## Entorno y comandos

Windows, SDK .NET 10.0.401, Release. Base main@0a1dd6e. NingÃºn workflow de GitHub Actions iniciado.

```powershell
dotnet build Valdrio.slnx -c Release --no-restore
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --logger 'trx;LogFileName=audit-final.trx'
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~AuditPortableFlowTests|FullyQualifiedName~SecondaryFullFlowProjectorTests|FullyQualifiedName~AllocationCurrencyDisplayTests|FullyQualifiedName~PresentedOrigins'
python specification/v0.4/casos/verificar_referencias.py specification/v0.4/casos/casos_metodologicos.json
python 'Synthetic Projects/verify_scenarios.py'
```

La primera restauraciÃ³n/compilaciÃ³n en sandbox no terminÃ³ correctamente y no se contabiliza como prueba vÃ¡lida. Se repitiÃ³ con permiso de ejecuciÃ³n local, sin CI remota.

## Resultados

- LÃ­nea base: 651/651, sin fallos ni omisiones, 3 min 23 s. Resumen audit-baseline.json; TRX original en tests/Valdrio.Tests/TestResults/audit-baseline.trx.
- Primera extracciÃ³n intermedia: 653/653, sin fallos ni omisiones. No es el resultado final de toda la entrega.
- Suite final de lÃ³gica/CLI: **655/655**, sin fallos ni omisiones, 4 min 19 s; audit-final.json conserva contador, tiempo y SHA-256 del TRX.
- Build final: correcto, 0 errores y 0 advertencias; build.log.
- Pruebas focalizadas finales: 14/14; audit-focused.json.
- Referencias racionales v0.4: 39/39. Comprueban el orÃ¡culo, no por sÃ­ solas C#/SQLite/WPF.
- Dos referencias sintÃ©ticas independientes: Services 17.500 EUR; Industrial 36.000 EUR. La suite C# incluye su importaciÃ³n/guardado/reapertura.

## Recorrido WPF

Ejecutable local: src/Valdrio.Desktop/bin/Release/net10.0-windows/Valdrio.Desktop.exe. Lanzamiento con Start-Process -WindowStyle Hidden; la aplicaciÃ³n abre sus ventanas de prueba.

1. Intento --allocation-only --methodology sobre copia de Industrial Company: salida 2, `Single` en MainWindow.AllocationSmoke.cs:52 porque ese smoke exige exactamente un auxiliar y el ejemplo tiene dos. Es una precondiciÃ³n del harness incumplida, no una aceptaciÃ³n correcta. Evidencia: artifacts/audit-wpf/company-failure.txt.
2. Se usa el escenario autocontenido que construye sus archivos con `samples/t01-smoke.valdrio-dev.json --capture-dir=<ruta absoluta a artifacts/audit-wpf-full> --methodology`.

El flujo utiliza copias de prueba; no modifica las empresas del usuario. Las capturas y manifests originales permanecen bajo artifacts/audit-wpf-full. La captura de empresa inspeccionada confirma nombre visible no editable, separaciÃ³n de crear/abrir, tres fases visibles, jerarquÃ­a verde/blanco y controles accesibles sin duplicaciÃ³n del campo de creaciÃ³n.

## Archivos de cÃ³digo

- src/Valdrio.Calculation/ReciprocalFlowProjector.cs: proyecciÃ³n pura de coeficientes y brutos publicados.
- src/Valdrio.Reporting/SecondaryContributionProjector.cs: atribuciÃ³n conciliada por origen.
- src/Valdrio.Reporting/AllocationCurrencyProjection.cs: proyecciones monetarias de lÃ­neas y desgloses.
- src/Valdrio.Desktop/SecondaryResultPresentation.cs: adaptador al servicio Reporting.
- src/Valdrio.Desktop/AllocationCurrencyDisplay.cs: adaptador compatible de proyecciÃ³n monetaria.
- src/Valdrio.Desktop/AllocationWorkspaceViewModel.SecondaryFlow.cs: adaptador del motor y totales precalculados.
- src/Valdrio.Runner/Program.cs: reconoce importaciones/cobertura de snapshots con repartos.
- tests/Valdrio.Tests/AuditPortableFlowTests.cs: invariantes de conservaciÃ³n y no mutaciÃ³n, dependencias y ejecuciÃ³n CLI de dos snapshots reales.
- src/Valdrio.Desktop/AllocationWorkspacePanel.xaml.cs: fallback de foco si la fila no acepta el teclado.
- src/Valdrio.Desktop/MainWindow.MethodologySmoke.cs: diagnÃ³stico explÃ­cito del foco, sin debilitar la condiciÃ³n de aceptaciÃ³n.
- tests/Valdrio.Tests/Valdrio.Tests.csproj: referencia a Reporting.
- ESTADO_PROYECTO.md: cabecera vigente que separa diagnÃ³stico actual de historia.
- docs/auditorias/2026-10-14/: informe, validaciÃ³n y evidencia.

No se cambian XAML, formatos SQLite/JSON persistidos, contratos metodolÃ³gicos canÃ³nicos, dependencias NuGet ni workflows. El valor de `task` de --study-snapshot se corrige para AllocationStudySnapshotInput; consumidores que dependieran de la etiqueta errÃ³nea T05 deben aceptar T08-allocation-preparation. La estructura JSON de salida mantiene su versiÃ³n.

## LÃ­mites de aceptaciÃ³n

La revisiÃ³n visual por captura no equivale a todos los tamaÃ±os/DPI, lectores de pantalla ni todos los datos reales. No se certifica â‰¤3 clics universal, modo experto global, historial general T12 ni motor completo de ociosidad/multimÃ©todo. La limpieza remota no implica fusionar esta nueva PR ni borrar main. La refactorizaciÃ³n es revisable y conserva adaptadores de Desktop para evitar cambios de contratos de vistas.

## InvestigaciÃ³n visual y regresiÃ³n de foco

El recorrido autocontenido pasÃ³ 375 comprobaciones registradas en 12 manifests antes de fallar en el paso metodolÃ³gico de foco. Se conservan los JSON en wpf/; no se presenta ese recorrido parcial como aceptaciÃ³n completa. Una reproducciÃ³n limpia con diagnÃ³stico ampliado confirmÃ³ Ã­ndice 0 seleccionado, tabla visible y habilitada, ventana activa y ausencia de foco dentro de la tabla. Se corrigiÃ³ el retorno incondicional de FocusKeyLibrary y se repitiÃ³ el escenario original, sin eliminar ni suavizar la aserciÃ³n.

Un intento de reutilizar el archivo guided ya modificado por los smokes tambiÃ©n se descartÃ³: el harness esperaba un Ãºnico elemento y ese archivo ya tenÃ­a objetos de pruebas anteriores. Las repeticiones vÃ¡lidas parten del escenario autocontenido en directorios nuevos.

Tras la correcciÃ³n, la aserciÃ³n de foco pasÃ³ y el recorrido avanzÃ³ hasta la selecciÃ³n de clave secundaria. El harness esperaba SecondaryAssignmentTask antes de que WPF despachara DropDownClosed; ahora espera ese despacho y luego la tarea actual, conservando la comprobaciÃ³n de selecciÃ³n, error y persistencia SQLite. Se actualizaron tambiÃ©n dos expectativas obsoletas frente a main: etiqueta secundaria ES/EN sin nÃºmero duplicado y reapertura de fase cerrada centrada en resultados, con detalles de clave restaurados. No se cambia la navegaciÃ³n de producciÃ³n para satisfacer esas expectativas histÃ³ricas.

## Cierre de comprobaciones

El recorrido final autocontenido, con la corrección de foco y el harness actualizado, completó **539 comprobaciones registradas en 17 manifests**, todos `passed`, incluyendo **48 metodológicas** y **67 del shell**. No son 539 requisitos independientes ni sustituyen a las 655 pruebas de la suite. Resumen: wpf-summary.json; evidencia final: wpf-final/. No existe company-failure.txt en artifacts/audit-wpf-final y se produjeron las cuatro capturas finales ES/EN/formato independiente/pseudo-RTL. Se inspeccionaron capturas de empresa, claves, secundario y escala 200 %.

La prueba de escala al 200 % conserva scroll, pero el contexto superior ocupa demasiada altura en la captura de 1264×824 y el contenido exige desplazamiento horizontal. Se registra como deuda UX, no como conformidad visual universal. La aceptación de los escenarios ensayados no certifica accesibilidad completa ni todos los DPI/datos de producción.

Compilación WPF posterior a la corrección: 0 errores/advertencias (wpf-final-build.log). Los cambios posteriores a las 655 pruebas se limitaron a foco WPF, harness visual, un salto de línea final y documentación; se verificaron con el recorrido WPF anterior. Hashes de fuentes y binarios en tested-files.json.

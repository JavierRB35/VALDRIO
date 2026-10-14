# Validación local y archivos de la entrega

## Entorno y comandos

Windows, SDK .NET 10.0.401, Release. Base main@0a1dd6e. Ningún workflow de GitHub Actions iniciado.

```powershell
dotnet build Valdrio.slnx -c Release --no-restore
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --logger 'trx;LogFileName=audit-final.trx'
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~AuditPortableFlowTests|FullyQualifiedName~SecondaryFullFlowProjectorTests|FullyQualifiedName~AllocationCurrencyDisplayTests|FullyQualifiedName~PresentedOrigins'
python specification/v0.4/casos/verificar_referencias.py specification/v0.4/casos/casos_metodologicos.json
python 'Synthetic Projects/verify_scenarios.py'
```

La primera restauración/compilación en sandbox no terminó correctamente y no se contabiliza como prueba válida. Se repitió con permiso de ejecución local, sin CI remota.

## Resultados

- Línea base: 651/651, sin fallos ni omisiones, 3 min 23 s. Resumen audit-baseline.json; TRX original en tests/Valdrio.Tests/TestResults/audit-baseline.trx.
- Primera extracción intermedia: 653/653, sin fallos ni omisiones. No es el resultado final de toda la entrega.
- Suite final de lógica/CLI: **655/655**, sin fallos ni omisiones, 4 min 19 s; audit-final.json conserva contador, tiempo y SHA-256 del TRX.
- Build final: correcto, 0 errores y 0 advertencias; build.log.
- Pruebas focalizadas finales: 14/14; audit-focused.json.
- Referencias racionales v0.4: 39/39. Comprueban el oráculo, no por sí solas C#/SQLite/WPF.
- Dos referencias sintéticas independientes: Services 17.500 EUR; Industrial 36.000 EUR. La suite C# incluye su importación/guardado/reapertura.

## Recorrido WPF

Ejecutable local: src/Valdrio.Desktop/bin/Release/net10.0-windows/Valdrio.Desktop.exe. Lanzamiento con Start-Process -WindowStyle Hidden; la aplicación abre sus ventanas de prueba.

1. Intento --allocation-only --methodology sobre copia de Industrial Company: salida 2, `Single` en MainWindow.AllocationSmoke.cs:52 porque ese smoke exige exactamente un auxiliar y el ejemplo tiene dos. Es una precondición del harness incumplida, no una aceptación correcta. Evidencia: artifacts/audit-wpf/company-failure.txt.
2. Se usa el escenario autocontenido que construye sus archivos con `samples/t01-smoke.valdrio-dev.json --capture-dir=<ruta absoluta a artifacts/audit-wpf-full> --methodology`.

El flujo utiliza copias de prueba; no modifica las empresas del usuario. Las capturas y manifests originales permanecen bajo artifacts/audit-wpf-full. La captura de empresa inspeccionada confirma nombre visible no editable, separación de crear/abrir, tres fases visibles, jerarquía verde/blanco y controles accesibles sin duplicación del campo de creación.

## Archivos de código

- src/Valdrio.Calculation/ReciprocalFlowProjector.cs: proyección pura de coeficientes y brutos publicados.
- src/Valdrio.Reporting/SecondaryContributionProjector.cs: atribución conciliada por origen.
- src/Valdrio.Reporting/AllocationCurrencyProjection.cs: proyecciones monetarias de líneas y desgloses.
- src/Valdrio.Desktop/SecondaryResultPresentation.cs: adaptador al servicio Reporting.
- src/Valdrio.Desktop/AllocationCurrencyDisplay.cs: adaptador compatible de proyección monetaria.
- src/Valdrio.Desktop/AllocationWorkspaceViewModel.SecondaryFlow.cs: adaptador del motor y totales precalculados.
- src/Valdrio.Runner/Program.cs: reconoce importaciones/cobertura de snapshots con repartos.
- tests/Valdrio.Tests/AuditPortableFlowTests.cs: invariantes de conservación y no mutación, dependencias y ejecución CLI de dos snapshots reales.
- src/Valdrio.Desktop/AllocationWorkspacePanel.xaml.cs: fallback de foco si la fila no acepta el teclado.
- src/Valdrio.Desktop/MainWindow.MethodologySmoke.cs: diagnóstico explícito del foco, sin debilitar la condición de aceptación.
- tests/Valdrio.Tests/Valdrio.Tests.csproj: referencia a Reporting.
- ESTADO_PROYECTO.md: cabecera vigente que separa diagnóstico actual de historia.
- docs/auditorias/2026-10-14/: informe, validación y evidencia.

No se cambian XAML, formatos SQLite/JSON persistidos, contratos metodológicos canónicos, dependencias NuGet ni workflows. El valor de `task` de --study-snapshot se corrige para AllocationStudySnapshotInput; consumidores que dependieran de la etiqueta errónea T05 deben aceptar T08-allocation-preparation. La estructura JSON de salida mantiene su versión.

## Límites de aceptación

La revisión visual por captura no equivale a todos los tamaños/DPI, lectores de pantalla ni todos los datos reales. No se certifica ≤3 clics universal, modo experto global, historial general T12 ni motor completo de ociosidad/multimétodo. La limpieza remota no implica fusionar esta nueva PR ni borrar main. La refactorización es revisable y conserva adaptadores de Desktop para evitar cambios de contratos de vistas.

## Investigación visual y regresión de foco

El recorrido autocontenido pasó 375 comprobaciones registradas en 12 manifests antes de fallar en el paso metodológico de foco. Se conservan los JSON en wpf/; no se presenta ese recorrido parcial como aceptación completa. Una reproducción limpia con diagnóstico ampliado confirmó índice 0 seleccionado, tabla visible y habilitada, ventana activa y ausencia de foco dentro de la tabla. Se corrigió el retorno incondicional de FocusKeyLibrary y se repitió el escenario original, sin eliminar ni suavizar la aserción.

Un intento de reutilizar el archivo guided ya modificado por los smokes también se descartó: el harness esperaba un único elemento y ese archivo ya tenía objetos de pruebas anteriores. Las repeticiones válidas parten del escenario autocontenido en directorios nuevos.

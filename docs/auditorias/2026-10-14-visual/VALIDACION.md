# Validación visual — 14/10/2026

Base: main e1952e360881fe99aaec4fb49bfb7d94c000a3a8. Cambios en ui/visual-ergonomics-20261014.

## Resultado

- Compilación local de la solución: **0 errores, 0 advertencias**.
- Suite local: **655/655**, 0 fallos, 0 omitidas; 3 min 41 s. Registro original `tests/Valdrio.Tests/TestResults/visual-ergonomics.trx`.
- Recorrido WPF final: salida **0**, **548 comprobaciones en 17 registros**, todos `passed`; 76 comprobaciones del shell y 48 metodológicas. No son 548 requisitos distintos ni pruebas de usabilidad con personas.
- Capturas: **70 antes + 83 después = 153 imágenes**. La galería compara los pares disponibles; las 13 capturas complementarias no se presentan como comparaciones equivalentes.
- Inventario estático de **10 archivos XAML** de producción: ningún control nombrado eliminado, ni bindings eliminados de Command, ItemsSource, SelectedItem, SelectedValue, Text, IsEnabled o IsReadOnly. No equivale a demostrar todos los efectos de estilo o toda la accesibilidad.
- Pruebas nuevas de interfaz: abrir contexto mediante AutomationPeer, ruta completa visible, foco en selector de densidad, aplicación de densidad a un grid ya existente, conservación de filas/selección, retorno a espaciado cómodo, controles de cabecera dentro de límites a 125/150/200 %.

## Ejecución reproducible

```powershell
dotnet build Valdrio.slnx -c Release --no-restore
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=visual-ergonomics.trx'

# Usar un directorio de capturas nuevo: el recorrido crea sus proyectos de prueba.
$captureRoot = Join-Path (Get-Location) 'artifacts/visual-final'
$captureProcess = Start-Process -FilePath 'src/Valdrio.Desktop/bin/Release/net10.0-windows/Valdrio.Desktop.exe' -ArgumentList @('samples/t01-smoke.valdrio-dev.json', "--capture-dir=$captureRoot", '--methodology') -WindowStyle Hidden -PassThru
$captureProcess.WaitForExit()
$captureProcess.ExitCode
```

Se ejecutó una captura completa previa (`visual-before`), una comprobación intermedia (`visual-after`) y una final (`visual-final`) tras los cambios de diagnóstico, KPIs y capturas adicionales. Las cifras anteriores corresponden a la final. Una captura inicial iniciada en segundo plano (`visual-review-20261014`) no se usa como aceptación: se sustituyó por una ejecución esperando el fin del proceso.

## Archivos de evidencia

- `galeria.html`, `antes/`, `despues/`: imágenes reales y comparativa navegable.
- `capturas.json`: inventario y SHA-256 de imágenes originales.
- `inventario-controles.json`: inventario anterior/posterior por archivo y salvaguarda de bindings.
- `validacion-wpf.json`: resultados originales agregados, con etiquetas de comprobación.
- `pruebas.json`: contadores y huella del TRX local.
- `build.log`, `tests.log`: salida de compilación y pruebas.

## Límites

No hay cambios en el motor de cálculo, servicios económicos, SQLite, formatos persistidos ni workflows. El getter HasStatusError expone un diagnóstico existente únicamente para presentación. La densidad no cambia datos ni se persiste entre sesiones.

No se ha ensayado toda combinación de error, todos los tamaños de datos ni lector de pantalla. Las capturas muestran datos sintéticos; no prueban adecuación comercial de grandes matrices o redes densas. La escala es una transformación del layout, no un ensayo multimonitor. El scroll horizontal a gran escala sigue siendo una deuda documentada. La regla universal de tres interacciones, onboarding de ejemplo y futuras fases económicas no están completados.

Los controles existentes conservan sus acciones; el texto de explicación completo pasa del tooltip al panel. La ruta del archivo se consulta ahora con una acción explícita. Estos son cambios deliberados de presentación, registrados en el informe.

Coste de GitHub Actions provocado: **0 USD**. No se inició ningún workflow ni se modificó facturación. La PR se entrega para revisión, sin fusionarla automáticamente.

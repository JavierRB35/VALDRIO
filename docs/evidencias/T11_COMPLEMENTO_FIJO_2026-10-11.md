# Componente fijo calculado — 2026-10-11

Base: 5467c15, rama feature/t11-reparto-secundario-cierre, PR #35.

En las filas Mixto solo se introduce el porcentaje variable. El fijo es una propiedad
calculada, sin setter, y su celda tiene binding OneWay y solo lectura. Cambiar variable
actualiza inmediatamente fijo = 100 - variable. Valores fuera de rango o no numéricos
muestran un guion en el fijo y no se guardan. El guardado deriva el fijo del variable.
Se conserva el botón Guardar y la protección de cambios pendientes. Documentado en
USABILIDAD.md; ayudas ES/EN actualizadas.

Comandos locales:

```powershell
dotnet build Valdrio.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false --verbosity minimal
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~T11_' --logger 'trx;LogFileName=complement.trx' --results-directory artifacts/complement
$captureDir = Join-Path $PWD 'artifacts/complement/wpf'
$p = Start-Process .\src\Valdrio.Desktop\bin\Release\net10.0-windows\Valdrio.Desktop.exe -ArgumentList @("--capture-dir=$captureDir", '--methodology', '.\samples\t01-smoke.valdrio-dev.json') -WindowStyle Hidden -Wait -PassThru
$p.ExitCode
```

Compilación: 0 errores, 0 advertencias. Pruebas T11: 13/13 correctas. Recorrido WPF: salida 0, 472 comprobaciones correctas, incluida celda fija de solo lectura y actualización automática a 70 al introducir 30 variable. Captura revisada visualmente.
Evidencias locales: artifacts/complement*.log y artifacts/complement/.
Sin Actions, coste 0 USD; sin fusionar main.

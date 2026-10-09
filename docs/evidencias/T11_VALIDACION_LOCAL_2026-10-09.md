# Validación local de T08–T11 — 2026-10-09

Rama: `feature/t11-reparto-secundario-cierre`  
HEAD remoto validado: `b1b6b08bd9a0c7821b8abbdc5613f7c76a1d2b6a` (PR #35)  
Entorno: Windows x64, .NET SDK 10.0.401, Release.  
GitHub Actions: no ejecutado; coste de CI generado: **0 USD**.

Esta nota actualiza el estado anterior de candidato sin validación nativa en
`T11_CANDIDATO_2026-10-09.md`. Se compilaron y probaron localmente los cambios
de la rama; no se fusionó `main`.

## Incidencias corregidas

1. El test project enlazaba los parciales principal, recíproco y secundario del
   ViewModel, pero omitía `AllocationWorkspaceViewModel.Composition.cs`. La
   compilación de pruebas fallaba con nueve `CS0103` por comandos y métodos no
   encontrados. Se añadió el parcial y las referencias WPF necesarias al
   proyecto de tests.
2. El target de tests ahora es `net10.0-windows`. El target que copia el Runner
   usaba erróneamente el TFM del test project y dejaba vacío el directorio
   `runner`; se fijó la fuente al TFM real del Runner (`net10.0`).
3. Al ejecutar las pruebas con WPF disponible, el `ICollectionView` se creaba
   durante la inicialización del ViewModel aunque las pruebas no tenían un
   Dispatcher WPF. Al volver de operaciones asíncronas, sus cambios de
   colección fallaban por afinidad de hilo. La vista ahora se crea de manera
   diferida para el enlace de UI; selección y filtrado de comandos consultan la
   colección por separado.
4. La captura aportada por el propietario mostró un `XamlParseException` al
   resolver `{StaticResource Visible}` en `ModelWorkspacePanel.xaml:181`.
   Faltaba declarar el `BooleanToVisibilityConverter` local, necesario para dos
   bindings de visibilidad. Se añadió al diccionario de recursos del panel.

## Comandos y resultados

Restauración offline con los paquetes ya almacenados. NuGet no podía consultar
el servicio remoto de auditoría; se omitió únicamente esa consulta para la
restauración local, sin modificar la política del repositorio:

```powershell
dotnet restore Valdrio.slnx --ignore-failed-sources -p:NuGetAudit=false -m:1 --verbosity minimal
```

Resultado: **EXIT 0**, 9 proyectos restaurados.

Suite completa:

```powershell
dotnet test tests\Valdrio.Tests\Valdrio.Tests.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false --verbosity minimal
```

Resultado: **EXIT 0 — 615 superadas, 0 fallidas, 0 omitidas**; 3 min 8 s.

Pruebas enfocadas a T08–T11:

```powershell
dotnet test tests\Valdrio.Tests\Valdrio.Tests.csproj -c Release --no-build --no-restore -m:1 --filter "FullyQualifiedName~T08|FullyQualifiedName~T09|FullyQualifiedName~T10|FullyQualifiedName~T11" --logger "console;verbosity=minimal"
```

Resultado: **EXIT 0 — 61 superadas, 0 fallidas, 0 omitidas**; 33 s.

Compilación de la solución completa, incluidos Desktop/WPF, Runner y tests:

```powershell
dotnet build Valdrio.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false --verbosity minimal
```

Resultado: **EXIT 0 — 9 proyectos, 0 advertencias, 0 errores**; 8,78 s.

Smoke WPF integrado, con el proyecto sintético y captura aislada:

```powershell
Start-Process -FilePath .\src\Valdrio.Desktop\bin\Release\net10.0-windows\Valdrio.Desktop.exe `
  -ArgumentList @('--capture-dir=artifacts\t11-wpf-smoke', '.\samples\t01-smoke.valdrio-dev.json') `
  -WindowStyle Hidden -Wait -PassThru
```

Resultado: **EXIT 0**. `artifacts/t11-wpf-smoke/models/interaction.json` marca
`Status: passed` y contiene 64 comprobaciones superadas. El panel de modelo abrió
sin `XamlParseException`; el flujo comprobó nodos principal/auxiliar, descripción
del grupo, publicación, clonación, edición, reapertura SQLite, restauración y
preservación del modelo publicado.

`git diff --check`: **EXIT 0**, sin errores de whitespace.

## Límites de esta validación

Estas comprobaciones no equivalen a aceptación A05 ni a aprobación de merge.
La PR #35 debe continuar en borrador y `main` no se modifica.

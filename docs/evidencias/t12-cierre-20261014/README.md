# T12 — Acta de cierre y validación local

**Estado: PENDIENTE DE PRUEBAS WINDOWS Y ACEPTACIÓN.** Preparado el 14/10/2026. No constituye certificado de cierre.

- Repositorio: `JavierRB35/VALDRIO`
- PR: [#52](https://github.com/JavierRB35/VALDRIO/pull/52), de tipo borrador mientras no haya evidencias.
- Rama de validación: `feature/t12-runs-history-20261014`. Registrar el **SHA de HEAD probado** con `git rev-parse HEAD`.
- Validación: **Windows local / PowerShell 7.3+ / .NET 10**, sin GitHub-hosted runners.
- Resultado global: **NO EJECUTADO**; se rellenará tras obtener resultados reales.

## Alcance funcional incluido

1. Ejecuciones T12: captura congelada, cálculo, publicación atómica, lectura de historial sin solver, estados y conciliación.
2. Bloqueo de duplicados: `Completed` de la misma revisión y versión de algoritmo se reutiliza, sin crear otra snapshot ni ejecutar cálculo; ejecuciones de otras revisiones y fallos siguen independientes.
3. Historial **siempre visible**, sin eliminar ni archivar. Se preservan los triggers de inmutabilidad.
4. Interfaz T12: dos diagramas independientes, reparto primario y secundario/final; importes en formato moneda a dos decimales y porcentaje; detalle preciso, trazabilidad e iluminación de recorridos al seleccionar.
5. Fase de preparación: realce de «Importe efectivo (base de reparto)», conservando edición, guardado, i18n y cálculos.
6. Regresiones adyacentes: alerta/actualización a copia de archivos antiguos y validación previa de códigos de centros duplicados.

## Puertas de aceptación

| Puerta | Estado | Evidencia exigida |
|---|---|---|
| Árbol limpio y SHA fijado | Pendiente | `git status --short`, `git rev-parse HEAD` |
| `dotnet restore` y build Release | Pendiente | Salida, 0 errores |
| Tests focalizados T12, visual contracts, migración y duplicados | Pendiente | Resultado MSTest y fallos |
| Suite completa MSTest | Pendiente | TRX, recuento y duración |
| Aceptación Windows general | Pendiente | `Verify-Windows.ps1` y salida, sin GitHub Actions |
| WPF T12 con fixture 6.500 EUR | Pendiente | `checks.json` y capturas ES/EN y estrecha |
| Comprobación manual | Pendiente | Moneda, porcentajes, recorrido, revisión idéntica, validación y foco |
| Integridad de histórico y migraciones | Pendiente | Original preservado, no recalcular al consultar, no duplicar |
| Revisión de documentación y aprobación de fusión | Pendiente | PR, hash probado y acta completada |

## Comandos para reproducir en PowerShell (raíz del repositorio)

Realizar tras guardar o apartar de manera segura los cambios locales. No hacer reset forzado ni limpiar evidencia existente.

```powershell
git fetch origin
git switch feature/t12-runs-history-20261014
git pull --ff-only origin feature/t12-runs-history-20261014
git status --short
git rev-parse HEAD
dotnet --info

dotnet restore Valdrio.slnx
dotnet build Valdrio.slnx -c Release --no-restore
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --filter "Name~T12_|Name~EffectiveAllocationBasisAndSplitCostFlows|Name~OpeningGenuineOldFile|Name~DuplicatePhysicalCenterCode"
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --logger "trx;LogFileName=valdrio-tests.trx"
pwsh -File .github/scripts/Verify-Windows.ps1
```

`Verify-Windows.ps1` exige árbol limpio y publica un ejecutable Windows en `artifacts/desktop`. El smoke T12 no está integrado en dicho script: debe ejecutarse aparte usando la fixture creada por el test siguiente.

### Prueba específica de T12 con ejecución persistida

```powershell
$sha = (git rev-parse HEAD).Trim()
$evidence = Join-Path (Get-Location).Path ("artifacts/t12-local-" + $sha.Substring(0,12))
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$env:VALDRIO_T12_DEMO = Join-Path $evidence "demo-t12.valdrio"
try {
    dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --filter "Name~T12_6500FlowsPersistAndHistoricalReadDoesNotRecalculate"
    if ($LASTEXITCODE -ne 0) { throw "Fixture T12: prueba fallida." }
} finally {
    Remove-Item Env:VALDRIO_T12_DEMO -ErrorAction SilentlyContinue
}
if (-not (Test-Path -LiteralPath (Join-Path $evidence "demo-t12.valdrio"))) { throw "No se generó la fixture T12." }
$visual = Join-Path $evidence "visual"
New-Item -ItemType Directory -Force -Path $visual | Out-Null
# Quote each path so Start-Process also works when the repository is under "OneDrive - ...".
$demoArg = '"' + (Join-Path $evidence "demo-t12.valdrio") + '"'
$captureArg = '"--capture-dir=' + $visual + '"'
$p = Start-Process -FilePath (Resolve-Path "artifacts/desktop/Valdrio.Desktop.exe").Path -ArgumentList @(
    $demoArg, "--cost-flow-only", $captureArg
) -PassThru -Wait
if ($p.ExitCode -ne 0) { throw "Smoke WPF T12 falló: $($p.ExitCode)" }
Get-Content (Join-Path $visual "checks.json")
```

Si cualquier comprobación falla, **no fusionar**; corregir en la rama y repetir desde un nuevo SHA. Nunca inventar estados «PASSED».

## Primera ejecución local comunicada por el propietario — 14/10/2026

El propietario ejecutó en Windows local el commit `60c8e73826b211a5e2de26b3e4f8bd6de997da6a`. Resultados de su salida de PowerShell:

- `git fetch/switch/pull`: correcto, avance rápido local `d9b8202 → 60c8e73`; `git rev-parse HEAD` comprobado.
- `dotnet restore Valdrio.slnx`: **superado** (1,3 s).
- `dotnet build Valdrio.slnx -c Release --no-restore`: **fallido**, 3 errores (12,5 s):
  - `src/Valdrio.Desktop/CostFlowPanel.xaml.cs:86`: `CS1955` llamada `Brush(...)` no invocable.
  - `tests/Valdrio.Tests/LegacyCompanyOpenAndBatchTests.cs:78`: `CS0103` referencia `SqliteCompanyFileService` inexistente en ese ámbito.
  - `tests/Valdrio.Tests/LegacyCompanyOpenAndBatchTests.cs:82`: `CS0103` referencia `CompanyFileOpenMode` inexistente en ese ámbito.
- Las pruebas focalizadas **NO se ejecutaron**, porque el script detuvo la ejecución tras el build fallido.
- Corrección textual en rama, commit `9217c55868e2636039ab8f6820ad9e1c5a272454`: `ThemeBrush` en la línea residual y directivas `using` de aplicación/infraestructura para los tests antiguos.
- **Pendiente:** el propietario debe volver a ejecutar `dotnet build` y las pruebas focalizadas en ese commit o posterior. Esta corrección no acredita build exitoso.

La captura del gráfico de cuatro etapas aportada en la conversación muestra la UI de la revisión anterior; no valida ni invalida la nueva disposición de dos gráficos hasta disponer de una compilación Windows actualizada.

## Acta de resultados — por cumplimentar

- Fecha/hora, Windows, .NET: **pendiente**
- SHA probado / comando exacto: **pendiente**
- Build (errores, avisos): **pendiente**
- MSTest focalizadas: **pendiente**
- MSTest total: **pendiente**
- Windows acceptance y WPF T12 ES/EN: **pendiente**
- Defectos, corrección y retest: **pendiente**
- Evidencias (logs/TRX/capturas), ubicación: **pendiente**
- Revisión metodológica e integridad: **pendiente**
- Decisión final: **NO APROBADA TODAVÍA**

## Protocolo posterior a la aceptación

1. Registrar las evidencias reales, el SHA y la resolución de incidencias en esta acta (si este cierre documental añade commit, diferenciar SHA de código probado y SHA documental final).
2. Confirmar que `main` no se ha alejado y que los cambios de cierre no alteran el código probado; fusionar PR #52 solo con las puertas satisfechas.
3. Verificar `merged=true` y el SHA de merge en GitHub.
4. **Después**, eliminar la rama remota `feature/t12-runs-history-20261014` y, si procede, la copia local. No borrar antes de verificar la integración.
5. Conservar la PR, histórico y acta como evidencia. No lanzar GitHub Actions ni modificar el presupuesto/facturación.

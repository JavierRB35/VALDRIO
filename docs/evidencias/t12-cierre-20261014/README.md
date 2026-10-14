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

## Segunda ejecución Windows local — aceptación detenida en scanner (14/10/2026)

El propietario ha aportado resultados de `Verify-Windows.ps1` para el SHA `68d3829b1d8f710d9cf3cf145ac589ed40ca212b`:

- Compilación Release: **correcta**, 1,4 s.
- MSTest integral: **691/691 correctas**, 0 fallidas, 0 omitidas, 250,1 s; TRX local `tests/Valdrio.Tests/TestResults/valdrio-tests.trx` (sobrescrito por el verificador, según su aviso).
- Prueba de autocontrol del scanner: **superada**, pero el paso de cobertura de mensajes falla en la línea 259 de `Verify-Windows.ps1`.
- Claves erróneamente señaladas: `Accent.ActiveGreen`, `Border.Soft`, `Brand.DeepGreen`, `Brand.White`, `Brand.Yellow`, `Button.Primary`, `Status.SuccessBg`, `Status.WarningBg`, `Surface.Canvas`, `Surface.Subtle`, `Table.CellPadding`, `Table.MinRowHeight`, `Table.NumberPadding`, `Text.Primary`.
- Diagnóstico del código: el extractor de literales C# confunde claves del `ResourceDictionary` WPF con mensajes traducibles. Son referencias a estilos, brushes y espaciados, no faltas de traducción. Aparecen tanto en llamadas `ThemeBrush(...)` como en accesos `resources["..."]` y `SetResourceReference`.
- Corrección: `Get-ProductionMessageKeys.ps1` lee las claves `x:Key` declaradas en `src/Valdrio.Desktop/Styles/ValdrioTheme.xaml` y las excluye del conjunto de mensajes traducibles. `Test-ProductionMessageKeys.ps1` amplía la fixture con esos tres mecanismos de acceso y exige detectar **dos mensajes genuinamente ausentes**, para no silenciar fallos reales.
- **Pendiente**: repetir autoprueba y cobertura de mensajes con el nuevo commit; si pasan, continuar la aceptación Windows general y el smoke T12 específico. No se ha certificado la aceptación integral, ni se debe fusionar.
- GitHub Actions no ejecutado.

## Tercera aceptación Windows local: bloqueo por plantilla antigua (14/10/2026)

El propietario ejecutó `Verify-Windows.ps1` en el commit `c0147acf1b74e8cddadbb2a5d6542e469ab450f8`. Resultados comunicados:

- Verificación de origen canónico: **57** archivos comprobados.
- Referencias matemáticas Python: **39/39**; runner T02 y T02A (moneda, tiempo, FX) superados.
- Compilación Release: **correcta** (13,0 s).
- MSTest: **691/691 correctas** (250,3 s).
- Autoprueba del extractor de mensajes: **superada**.
- Cobertura de traducciones: **420** claves de mensajes comprobadas.
- Publicación Windows x64: **correcta**.
- WPF launch smoke y recuperación de preferencias inválidas: **superados**.
- **Fallo** al iniciar el smoke visual de empresa: `CompanyFilePanel.Smoke.cs:48`, comprobación «example opens a real editable copy with saved analyses», provocando código de salida 2.
- Nota de entorno: la comprobación ad hoc del usuario ejecutada desde Windows PowerShell (política local restrictiva) no lanzó `Get-ProductionMessageKeys.ps1`; la impresión posterior «Verificación de traducciones superada» de ese bloque ad hoc es un falso positivo del envoltorio. **Dentro de `Verify-Windows.ps1` ejecutado con `pwsh` sí pasó la cobertura de 420 claves.** No hace falta cambiar la política global de ejecución.
- Diagnóstico reproducible: se inspeccionó la base SQLite empaquetada `Synthetic Projects/Service Company/services.valdrio` y su tabla `FileMetadata` devuelve `SchemaVersion = 9` (formato `VALDRIO-COMPANY`); T12 utiliza esquema 10. `OpenExample` copiaba los bytes a destino y llamaba a `Open` sin migrar, por lo que no podía crear una sesión editable.
- Corrección: `CompanyFileViewModel.OpenExample` consulta `RequiresUpgrade` sobre el origen en modo solo lectura y usa `Upgrade(source,destination,"local-user",token)`, que trabaja en fichero temporal, lo migra, valida y lo mueve al destino nuevo sin sobrescritura. Si el ejemplo ya es compatible, se conserva la ruta anterior de copia sin sobrescritura. La plantilla distribuida no se modifica.
- Se añade `ExampleOpensBundledV9AsEditableV10WithoutChangingTemplate` contra el **mismo archivo real** distribuido y su huella; exige sesión editable, esquema vigente, análisis guardados, destino y archivo original intacto. El smoke informa diagnóstico y estado si vuelve a fallar.
- **Pendiente**: compilar en Windows, probar la regresión nueva y repetir aceptación visual. Todavía no se ha fusionado ni aprobado T12.

## Cuarta aceptación Windows local — smoke WPF de códigos duplicados (14/10/2026)

El propietario ha remitido la ejecución Windows del commit `8ea77b315f23a56358284cd0373a5e80acea53a0`:

- Restauración y compilación Release: **correctas** (build inicial 8,4 s; build aceptación 13,0 s).
- Pruebas focalizadas del proyecto de ejemplo: **3/3 correctas**.
- Prueba integral: **692/692 correctas**, 0 fallidas, 0 omitidas (259,9 s).
- Fuentes canónicas: **57 comprobadas**; Python **39/39**; scanner local y **420 claves** traducibles correctos; T02/T02A y publicación `win-x64` correctos.
- Smoke de arranque WPF y recuperación de preferencias inválidas: **correctos**.
- Fallo de aceptación visual: `ModelWorkspacePanel.Smoke.cs:96`, `Model WPF: enabled: SaveCenterButton`. En el smoke antiguo se intentaba pulsar Guardar con el código de centro `MANT-001`, deliberadamente duplicado, y se esperaba una excepción `MODEL_DUPLICATE`.
- Diagnóstico: después del cambio UX de validación preventiva, el botón **debe estar deshabilitado** con un mensaje junto al campo. No se trata de un nuevo error del modelo ni de que se permita duplicar; es una expectativa obsoleta de aceptación WPF.
- Corrección del smoke: verificar que código repetido bloquea Guardar, muestra el mensaje y no crea otro maestro; al editar el código a `MANT-002` debe reactivarse Guardar sin insertar nada. Se mantienen **dos comprobaciones** en lugar de la comprobación previa más el `Click` (que comprobaba `enabled`) para conservar las 64 comprobaciones esperadas por `Verify-Windows.ps1` y no debilitar la cobertura.
- **Pendiente**: ejecutar de nuevo la aceptación Windows con la corrección y realizar smoke dedicado de ambos gráficos T12. No fusionar hasta obtener `Status = passed` y evidencias completas. Sin GitHub Actions.

## Quinta aceptación Windows local — desajuste en conteo de evidencias de empresa (14/10/2026)

El propietario informa que para el SHA `f4c09df40c12632581f41dc7f3d2d9045640b2a2`, el ejecutable WPF completo **finalizó con código 0**, y el verificador aceptó los **12 controles FX**; a continuación falló en `.github/scripts/Verify-Windows.ps1:388` por `Company WPF interaction evidence is missing or incomplete`.

Análisis estático de `src/Valdrio.Desktop/CompanyFilePanel.Smoke.cs`: **29** comprobaciones explícitas y **15** verificaciones de botón habilitado en `Click`, total **44**. El verificador todavía esperaba **35**, anterior a las mejoras para ejemplo editable, persistencia y recuperación de formatos antiguos. El smoke no falló, ya que `interaction.json` fue escrito y el proceso terminó con éxito; la discrepancia es una expectativa antigua del script de aceptación.

Corrección: `Verify-Windows.ps1` exige exactamente **44** controles y además comprueba la presencia de las cuatro evidencias clave de migración segura y conservación de datos, sin reducir el alcance de los tests. El error informa ahora resultado real, esperado y estado. No se han modificado la aplicación, los resultados económicos ni el histórico. **Pendiente**: ejecutar localmente el verificador actualizado y comprobar el resto de puertas de aceptación y el smoke específico T12. No fusionar hasta obtener los recibos finales. Sin GitHub Actions.

## Auditoría preventiva de la quinta corrección — manifiestos SQLite de aceptación

La misma revisión estática de `Verify-Windows.ps1` encontró que cinco aserciones de `SchemaVersion` seguían fijadas a **9**, aunque `SqliteCompanyFileService.SchemaVersion` y los cinco emisores de JSON usan **10**: manifest de confirmación, débito/crédito, agrupación, cierre de importación e interacción de recursos. Se actualizan a `$expectedCompanySchemaVersion = 10` (versión explícitamente fijada para esta aceptación). Se deja intacta la expectativa `SchemaVersion = 1` para el manifiesto internacional, porque corresponde a un formato independiente, no al archivo SQLite de empresa.

Este ajuste es **solo del contrato de verificación**: no cambia esquemas, migraciones, cálculos ni datos. Pendiente repetir la aceptación Windows sobre el SHA actualizado.

## Auditoría transversal preventiva de scripts y evidencias WPF (14/10/2026)

El usuario solicita revisar conjuntamente todas las expectativas del verificador para evitar nuevas aceptaciones costosas que se detengan ante el primer contrato desactualizado.

- Último intento en el SHA c33f6f57: el proceso WPF y la validación de importes/snapshots alcanzan la comprobación del visual shell; error en Verify-Windows.ps1:522, que exige 67 comprobaciones obsoletas.
- Recuento estático documentado de MainWindow.VisualSmoke.cs: 7 pasos × 2 idiomas × 4 = 56; herramientas por idioma = 2; contexto/densidad/ventana estrecha = 10; escalado x3 × 2 = 6; reflujo 200 % = 1; escalado de herramientas x4 = 4; RTL = 1. Total **80**. Se modifica el contrato sin borrar comprobaciones.
- Auditoría de 16 recibos de smoke, 7 manifiestos con nombres/huellas SHA256 y 12 grupos de imágenes. Incluye versiones SQLite v10 e internacional v1, textos de salvaguarda de empresa y estudio. En .github/scripts/Audit-WindowsEvidence.ps1 se entrega un informe de TODAS las discrepancias; se puede ejecutar sobre evidencias existentes sin compilar ni ejecutar tests.
- Verify-Windows.ps1 invoca el auditor agregado tras terminar el proceso visual, antes de los controles individuales de hashes/snapshots/validación matemática que permanecen intactos.
- Mínimo de pruebas automatizadas requerido: ahora **692**, frente al heredado 389, siempre 0 fallos y 0 omitidas.
- Se inspeccionó el workflow windows-ci.yml, que llama al mismo Verify-Windows.ps1; no se ejecutó ningún runner GitHub ni se autorizó gasto.
- **Pendiente** validar el nuevo auditor en PowerShell Windows y repetir solo una vez la aceptación completa sobre SHA final. No fusionar ni eliminar la rama todavía.

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

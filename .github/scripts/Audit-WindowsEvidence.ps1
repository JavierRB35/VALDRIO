#Requires -Version 7.3
[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory)

$ErrorActionPreference = 'Stop'
$root = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($EvidenceDirectory)
if (-not (Test-Path -LiteralPath $root -PathType Container)) { throw "No existe la carpeta de evidencias: $root" }
$errors = [System.Collections.Generic.List[string]]::new()
$observed = @{}

# Complete evidence matrix: review alongside the smoke emitters when changing any test.
$checks = @(
    @{ File = 'fx-editor-interaction.json'; Count = 12 }
    @{ File = 'company-file/interaction.json'; Count = 44 }
    @{ File = 'models/interaction.json'; Count = 64 }
    @{ File = 'studies/interaction.json'; Count = 74 }
    @{ File = 'studies/recovery.json'; Count = 10 }
    @{ File = 'import-columns/interaction.json'; Count = 18 }
    @{ File = 'import-confirmation/interaction.json'; Count = 26 }
    @{ File = 'import-debit-credit/interaction.json'; Count = 22 }
    @{ File = 'import-grouping/interaction.json'; Count = 11 }
    @{ File = 'import-completion/interaction.json'; Count = 9 }
    @{ File = 'import-international/interaction.json'; Count = 10 }
    @{ File = 't07-resources/interaction.json'; Count = 49 }
    @{ File = 't08-allocations/interaction.json'; Count = 36 }
    @{ File = 'guided-workflow/interaction.json'; Count = 21 }
    @{ File = 'guided-workflow/audit.json'; Count = 27 }
    @{ File = 'visual-shell/interaction.json'; Count = 80 }
)
foreach ($spec in $checks) {
    $path = Join-Path $root $spec.File
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $errors.Add("Missing checks: $($spec.File)"); continue }
    try {
        $json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -ErrorAction Stop
        $observed[$spec.File] = $json
        $count = if ($null -eq $json.PassedChecks) { 0 } else { @($json.PassedChecks).Count }
        if ($json.Status -ne 'passed' -or $count -ne $spec.Count) {
            $errors.Add("Checks $($spec.File): expected $($spec.Count), actual $count, status=$($json.Status)")
        }
        else { Write-Host "OK $($spec.File): $count" }
    }
    catch { $errors.Add("Invalid checks $($spec.File): $($_.Exception.Message)") }
}
$company = $observed['company-file/interaction.json']
if ($null -ne $company) {
    foreach ($required in @(
        'example opens a real editable current-format copy with saved analyses',
        'shipped example remains byte-for-byte unchanged',
        'upgrade action is visible for a previous-format file without reselecting its source',
        'previous format leaves active project and source file untouched'
    )) {
        if (-not @($company.PassedChecks | Where-Object {
            $_ -is [string] -and $_.StartsWith($required, [StringComparison]::Ordinal)
        }).Count) { $errors.Add("Missing company safeguard: $required") }
    }
}
$recovery = $observed['studies/recovery.json']
if ($null -ne $recovery) {
    foreach ($required in @(
        'confirmed create survives refresh failure',
        'confirmed save survives refresh failure',
        'next save uses confirmed revision',
        'confirmed close survives refresh failure',
        'confirmed reopen survives refresh failure',
        'confirmed snapshot survives refresh failure',
        'manual refresh retains confirmed snapshot selection',
        'clean refresh adopts newer persisted revision',
        'dirty refresh preserves editor while list advances',
        'discard then refresh reconciles current revision'
    )) {
        if ($required -notin @($recovery.PassedChecks)) { $errors.Add("Missing study recovery: $required") }
    }
}

$manifests = @(
    @{ File = 'models/manifest.json'; Schema = 10; Names = @('example.valdrio', 'example.valdrio-backup', 'restored.valdrio', 'original-v1.valdrio', 'upgraded-current.valdrio') }
    @{ File = 'studies/manifest.json'; Schema = 10; Names = @('example.valdrio', 'study.valdrio-backup', 'restored.valdrio', 'original-v2.valdrio', 'upgraded-v4.valdrio', 'snapshot.json') }
    @{ File = 'import-confirmation/manifest.json'; Schema = 10; Names = @('import.valdrio', 'import.valdrio-backup', 'restored.valdrio', 'snapshot.json', 'period-amounts.csv') }
    @{ File = 'import-debit-credit/manifest.json'; Schema = 10; Names = @('import.valdrio', 'import.valdrio-backup', 'restored.valdrio', 'snapshot.json', 'debit-credit.csv', 'debit-credit.xlsx') }
    @{ File = 'import-grouping/manifest.json'; Schema = 10; Names = @('grouping.valdrio', 'grouping.valdrio-backup', 'restored.valdrio', 'snapshot.json', 'grouping.csv') }
    @{ File = 'import-completion/manifest.json'; Schema = 10; Names = @('completion.valdrio', 'completion.valdrio-backup', 'restored.valdrio', 'snapshot.json', 'earlier.csv', 'current.csv', 'corrected.csv') }
    @{ File = 'import-international/manifest.json'; Schema = 1; Names = @('international.valdrio', 'international.valdrio-backup', 'restored.valdrio', 'snapshot.json', 'source.csv') }
)
foreach ($spec in $manifests) {
    $path = Join-Path $root $spec.File
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $errors.Add("Missing manifest: $($spec.File)"); continue }
    try {
        $manifest = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -ErrorAction Stop
        $names = @($manifest.Files | ForEach-Object { $_.Name })
        $different = @($spec.Names | Where-Object { $_ -notin $names }).Count -gt 0 -or
            @($names | Where-Object { $_ -notin $spec.Names }).Count -gt 0
        if ($manifest.SchemaVersion -ne $spec.Schema -or $names.Count -ne $spec.Names.Count -or $different) {
            $errors.Add("Manifest $($spec.File): schema $($manifest.SchemaVersion) (expected $($spec.Schema)); files $($names -join ', ')")
        }
        else { Write-Host "OK manifest $($spec.File): schema $($spec.Schema), files $($names.Count)" }
        foreach ($entry in @($manifest.Files)) {
            $target = Join-Path (Split-Path $path -Parent) $entry.Name
            if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
                $errors.Add("Missing artifact: $($spec.File) / $($entry.Name)")
            }
            elseif ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Sha256) {
                $errors.Add("Artifact SHA256 mismatch: $($spec.File) / $($entry.Name)")
            }
        }
    }
    catch { $errors.Add("Invalid manifest $($spec.File): $($_.Exception.Message)") }
}
$resource = $observed['t07-resources/interaction.json']
if ($null -ne $resource -and $resource.SchemaVersion -ne 10) {
    $errors.Add("Resource company schema: expected 10, actual $($resource.SchemaVersion)")
}

$imageContracts = @(
    @{ Dir = 'visual-shell'; Names = @('es-step-1.png', 'es-step-2.png', 'es-step-3.png', 'es-step-4.png', 'es-step-5.png', 'es-step-6.png', 'es-step-7.png', 'en-step-1.png', 'en-step-2.png', 'en-step-3.png', 'en-step-4.png', 'en-step-5.png', 'en-step-6.png', 'en-step-7.png', 'es-context-details.png', 'es-density-compact.png', 'es-density-comfortable.png', 'minimum-window.png', 'narrow-analysis-focus.png', 'layout-scale-125.png', 'layout-scale-150.png', 'layout-scale-200.png', 'scale-200-analysis-focus.png', 'pseudo-rtl.png'); Count = 24; Exact = $true }
    @{ Dir = 'import-columns'; Names = @(); Count = 2; Exact = $false }
    @{ Dir = 'import-confirmation'; Names = @('accounts-es.png', 'prepared-es.png', 'confirmed-pseudo.png'); Count = 3; Exact = $true }
    @{ Dir = 'import-debit-credit'; Names = @('csv-prepared-es.png', 'csv-confirmed-pseudo.png', 'xlsx-prepared-es.png'); Count = 3; Exact = $true }
    @{ Dir = 'import-grouping'; Names = @('records-es.png', 'groups-es.png', 'groups-pseudo.png'); Count = 3; Exact = $true }
    @{ Dir = 'import-completion'; Names = @('comparison-es.png', 'comparison-pseudo.png'); Count = 2; Exact = $true }
    @{ Dir = 'import-international'; Names = @('conversion-es.png', 'conversion-pseudo.png'); Count = 2; Exact = $true }
    @{ Dir = 't07-resources'; Names = @('review-es.png', 'review-pseudo-rtl.png', 'saved-en.png'); Count = 3; Exact = $false }
    @{ Dir = 't08-allocations'; Names = @('es-keys.png', 'es-rules.png', 'en-keys.png', 'en-rules.png', 'pseudo-rtl.png'); Count = 5; Exact = $false }
    @{ Dir = 'guided-workflow'; Names = @('structure-es.png', 'destination-es.png', 'review-es.png', 'review-pseudo-rtl.png'); Count = 4; Exact = $false }
    @{ Dir = 'studies'; Names = @(); Count = 8; Exact = $false }
    # The startup capture and all four ES/EN/pseudo-RTL presentation presets are required.
    @{ Dir = '.'; Names = @('startup.png', '01-en-esES-ltr.png', '02-es-esES-ltr.png', '03-es-enUS-ltr.png', '04-pseudo-rtl.png'); Count = 5; Exact = $true }
)
foreach ($spec in $imageContracts) {
    $folder = Join-Path $root $spec.Dir
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { $errors.Add("Missing image directory: $($spec.Dir)"); continue }
    $actual = @(Get-ChildItem -LiteralPath $folder -Filter '*.png' -File | ForEach-Object { $_.Name })
    $missing = @($spec.Names | Where-Object { $_ -notin $actual })
    if ($missing.Count -gt 0) { $errors.Add("Missing screenshots $($spec.Dir): $($missing -join ', ')") }
    if (($spec.Exact -or $spec.Names.Count -eq 0) -and $actual.Count -ne $spec.Count) {
        $errors.Add("Screenshot count $($spec.Dir): expected $($spec.Count), actual $($actual.Count)")
    }
}
if ($errors.Count -gt 0) {
    Write-Host "Auditoria WPF: $($errors.Count) discrepancias encontradas:" -ForegroundColor Yellow
    $errors | ForEach-Object { Write-Host "  - $_" }
    throw "Auditoria global de evidencias fallida. Corregir juntas todas las discrepancias; NO FUSIONAR."
}
Write-Host "AUDITORIA GLOBAL SUPERADA: $($checks.Count) recibos, $($manifests.Count) manifiestos, $($imageContracts.Count) grupos de imagenes."

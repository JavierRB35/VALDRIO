#Requires -Version 7.3
[CmdletBinding()]
param([string]$EvidenceDirectory = 'artifacts/t13-t12-integration')

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
if (-not $IsWindows) { throw 'T13/T12 visual acceptance requires Windows.' }
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $repository
try {
    $dirty = @(& git status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0 -or $dirty.Count -gt 0) {
        throw 'Acceptance requires a clean Git working tree.'
    }
    $head = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the tested commit.' }
    $output = [IO.Path]::GetFullPath($EvidenceDirectory)
    New-Item -ItemType Directory -Path $output -Force | Out-Null

    python 'Synthetic Projects/verify_scenarios.py' --self-test
    if ($LASTEXITCODE -ne 0) { throw 'T13 independent oracle failed.' }

    dotnet restore Valdrio.slnx
    if ($LASTEXITCODE -ne 0) { throw '.NET restore failed.' }
    dotnet build Valdrio.slnx -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

    $export = Join-Path $output 'generated'
    $env:VALDRIO_T13_EXPORT_DIR = $export
    try {
        dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~T13_' --logger 'trx;LogFileName=t13-t12-integration.trx'
        if ($LASTEXITCODE -ne 0) { throw 'T13 tests or T12 integration failed.' }
    }
    finally {
        Remove-Item Env:VALDRIO_T13_EXPORT_DIR -ErrorAction SilentlyContinue
    }
    [xml]$trx = Get-Content -LiteralPath 'tests/Valdrio.Tests/TestResults/t13-t12-integration.trx' -Raw
    $counts = $trx.TestRun.ResultSummary.Counters
    if ([int]$counts.total -lt 7 -or [int]$counts.failed -ne 0 -or
        [int]$counts.notExecuted -ne 0 -or [int]$counts.total -ne [int]$counts.passed) {
        throw "T13 test receipt incomplete: total=$($counts.total), passed=$($counts.passed), failed=$($counts.failed)."
    }

    dotnet publish src/Valdrio.Desktop/Valdrio.Desktop.csproj -c Release -r win-x64 --self-contained true --output artifacts/desktop
    if ($LASTEXITCODE -ne 0) { throw 'Windows desktop publish failed.' }
    $exe = (Resolve-Path 'artifacts/desktop/Valdrio.Desktop.exe').Path
    $expectedImages = @(
        'es-screen.png', 'es-primary-graph.png', 'es-secondary-graph.png', 'es-narrow.png',
        'en-screen.png', 'en-primary-graph.png', 'en-secondary-graph.png', 'en-narrow.png'
    )
    foreach ($case in @(
        @{ Folder = 'Service Company'; Id = 'services'; Mode = '--t13-service-flow-only'; Total = 17500 },
        @{ Folder = 'Industrial Company'; Id = 'industrial'; Mode = '--t13-industrial-flow-only'; Total = 36000 }
    )) {
        $dir = Join-Path $export $case.Folder
        $file = Join-Path $dir ($case.Id + '.valdrio')
        $runReceipt = Join-Path $dir 'completed-t12-run.json'
        if (-not (Test-Path -LiteralPath $file -PathType Leaf) -or
            -not (Test-Path -LiteralPath $runReceipt -PathType Leaf)) {
            throw "Missing v10 generated company and T12 receipt: $($case.Id)"
        }
        $visual = Join-Path $output ('visual-' + $case.Id)
        New-Item -Path $visual -ItemType Directory -Force | Out-Null
        $arguments = @('"' + $file + '"', $case.Mode, '"--capture-dir=' + $visual + '"')
        $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -Wait
        if ($process.ExitCode -ne 0) {
            throw "T13/$($case.Id) WPF smoke failed with code $($process.ExitCode)"
        }
        $checks = Get-Content -LiteralPath (Join-Path $visual 'checks.json') -Raw | ConvertFrom-Json
        if ($checks.Status -ne 'passed' -or $checks.ExpectedTotal -ne $case.Total -or
            @($checks.Checks).Count -ne 52) {
            throw "Incomplete T13/$($case.Id) WPF verification: status=$($checks.Status), count=$(@($checks.Checks).Count)."
        }
        $actualImages = @(Get-ChildItem -LiteralPath $visual -File -Filter '*.png' | ForEach-Object { $_.Name })
        if ($actualImages.Count -ne $expectedImages.Count -or
            @(Compare-Object ($expectedImages | Sort-Object) ($actualImages | Sort-Object)).Count -ne 0) {
            throw "Wrong ES/EN graph screenshots for $($case.Id): $($actualImages -join ', ')"
        }
        foreach ($name in $expectedImages) {
            $hash = (Get-FileHash -LiteralPath (Join-Path $visual $name) -Algorithm SHA256).Hash
            if ([string]::IsNullOrWhiteSpace($hash)) { throw "Unreadable capture: $name" }
        }
        Write-Host "T13/T12 $($case.Id): 52 WPF checks, 8 ES/EN screenshots passed."
    }
    $after = (& git rev-parse HEAD).Trim()
    if ($after -ne $head) { throw 'HEAD changed during acceptance.' }
    $changed = @(& git status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0 -or $changed.Count -ne 0) {
        throw 'Working tree changed during acceptance.'
    }
    Write-Host "T13/T12 ACCEPTANCE PASSED. Commit=$head; evidence=$output"
}
finally {
    Pop-Location
}

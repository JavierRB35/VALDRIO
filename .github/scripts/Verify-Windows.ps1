#Requires -Version 7.3
[CmdletBinding()]
param(
    [switch]$UseExistingBuild,
    [switch]$PrepareBuildManifestOnly,
    [string]$EvidenceDirectory = "artifacts/visual"
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true
if (-not $IsWindows) { throw "The WPF acceptance flow must run on Windows." }
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Push-Location $repositoryRoot
try {
    $buildManifestPath = "artifacts/acceptance-build.json"

    function Assert-CleanRepository([string]$stage) {
        $dirty = @(& git status --porcelain=v1 --untracked-files=all)
        if ($LASTEXITCODE -ne 0) { throw "Could not inspect repository state at $stage." }
        if ($dirty.Count -gt 0) {
            $dirty | ForEach-Object { Write-Host "dirty: $_" }
            throw "Acceptance by commit requires a clean repository at $stage."
        }
    }

    function Get-RequiredSha256([string]$path) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required acceptance file is missing: $path"
        }
        return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    }

    function Get-BuildPaths {
        return [ordered]@{
            DesktopExe = "artifacts/desktop/Valdrio.Desktop.exe"
            RunnerDll = "src/Valdrio.Runner/bin/Release/net10.0/Valdrio.Runner.dll"
            TestAssembly = "tests/Valdrio.Tests/bin/Release/net10.0/Valdrio.Tests.dll"
            Trx = "tests/Valdrio.Tests/TestResults/valdrio-tests.trx"
        }
    }

    function Get-BuildOutputSets {
        return [ordered]@{
            DesktopPublish = "artifacts/desktop"
            RunnerOutput = "src/Valdrio.Runner/bin/Release/net10.0"
            TestOutput = "tests/Valdrio.Tests/bin/Release/net10.0"
        }
    }

    function Get-OutputFileManifest([string]$root) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            throw "Required acceptance output directory is missing: $root"
        }

        $files = @(
            Get-ChildItem -LiteralPath $root -File -Recurse |
              Sort-Object FullName |
              ForEach-Object {
                  $relative = [IO.Path]::GetRelativePath($repositoryRoot, $_.FullName).Replace('\', '/')
                  [ordered]@{
                      Path = $relative
                      Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                  }
              }
        )

        if ($files.Count -eq 0) {
            throw "Required acceptance output directory is empty: $root"
        }

        return $files
    }

    function Write-AcceptanceBuildManifest([string]$commit) {
        $paths = Get-BuildPaths
        $outputSets = @(
            foreach ($name in (Get-BuildOutputSets).Keys) {
                $root = (Get-BuildOutputSets)[$name]
                [ordered]@{
                    Name = $name
                    Root = $root
                    Files = @(Get-OutputFileManifest $root)
                }
            }
        )
        $manifest = [ordered]@{
            Schema = "valdrio-windows-build-manifest-v2"
            SourceCommit = $commit
            CreatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
            DesktopExe = [ordered]@{ Path = $paths.DesktopExe; Sha256 = Get-RequiredSha256 $paths.DesktopExe }
            RunnerDll = [ordered]@{ Path = $paths.RunnerDll; Sha256 = Get-RequiredSha256 $paths.RunnerDll }
            TestAssembly = [ordered]@{ Path = $paths.TestAssembly; Sha256 = Get-RequiredSha256 $paths.TestAssembly }
            Trx = [ordered]@{ Path = $paths.Trx; Sha256 = Get-RequiredSha256 $paths.Trx }
            OutputSets = $outputSets
        }
        New-Item -ItemType Directory -Force -Path (Split-Path $buildManifestPath -Parent) | Out-Null
        $manifest | ConvertTo-Json -Depth 8 |
          Set-Content -LiteralPath $buildManifestPath -Encoding utf8NoBOM
    }

    function Assert-AcceptanceBuildManifest([string]$expectedCommit) {
        if (-not (Test-Path -LiteralPath $buildManifestPath -PathType Leaf)) {
            throw "Acceptance build manifest is missing. Rebuild without -UseExistingBuild."
        }

        $manifest = Get-Content -LiteralPath $buildManifestPath -Raw | ConvertFrom-Json
        if ($manifest.Schema -ne "valdrio-windows-build-manifest-v2" -or
            $manifest.SourceCommit -ne $expectedCommit) {
            throw "Acceptance build manifest does not belong to source commit $expectedCommit."
        }

        $paths = Get-BuildPaths
        foreach ($name in @("DesktopExe", "RunnerDll", "TestAssembly", "Trx")) {
            $entry = $manifest.$name
            $expectedPath = $paths.$name
            if ($null -eq $entry -or $entry.Path -ne $expectedPath) {
                throw "Acceptance build manifest path mismatch: $name."
            }
            $actualHash = Get-RequiredSha256 $expectedPath
            if ($entry.Sha256 -ne $actualHash) {
                throw "Acceptance build hash mismatch: $name."
            }
        }

        $expectedSets = Get-BuildOutputSets
        $manifestSets = @($manifest.OutputSets)
        if ($manifestSets.Count -ne $expectedSets.Count) {
            throw "Acceptance build manifest output-set count mismatch."
        }

        foreach ($name in $expectedSets.Keys) {
            $matches = @($manifestSets | Where-Object { $_.Name -eq $name })
            if ($matches.Count -ne 1 -or $matches[0].Root -ne $expectedSets[$name]) {
                throw "Acceptance build manifest output-set mismatch: $name."
            }

            $expectedFiles = @(Get-OutputFileManifest $expectedSets[$name])
            $manifestFiles = @($matches[0].Files)
            if ($manifestFiles.Count -ne $expectedFiles.Count) {
                throw "Acceptance build file count mismatch: $name."
            }

            $manifestByPath = @{}
            foreach ($entry in $manifestFiles) {
                if ($manifestByPath.ContainsKey($entry.Path)) {
                    throw "Acceptance build manifest contains duplicate path: $($entry.Path)"
                }
                $manifestByPath[$entry.Path] = $entry.Sha256
            }

            foreach ($entry in $expectedFiles) {
                if (-not $manifestByPath.ContainsKey($entry.Path) -or
                    $manifestByPath[$entry.Path] -ne $entry.Sha256) {
                    throw "Acceptance build output hash mismatch: $($entry.Path)"
                }
            }
        }

        return $manifest
    }

    Assert-CleanRepository "start"

    $sourceCommit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceCommit)) {
        throw "Could not resolve source commit."
    }

    if ($PrepareBuildManifestOnly) {
        if ($UseExistingBuild) {
            throw "-PrepareBuildManifestOnly cannot be combined with -UseExistingBuild."
        }
        Write-AcceptanceBuildManifest $sourceCommit
        $null = Assert-AcceptanceBuildManifest $sourceCommit
        Write-Host "Acceptance build manifest prepared for source commit $sourceCommit."
        return
    }

    if (Test-Path -LiteralPath $EvidenceDirectory) {
        if (@(Get-ChildItem -LiteralPath $EvidenceDirectory -Force).Count -gt 0) {
            throw "Evidence directory is not empty. Choose a new -EvidenceDirectory; existing files are preserved."
        }
    }

    if (-not $UseExistingBuild) {
        # Restore
        dotnet restore Valdrio.slnx

        # Verify canonical specification sources
        Push-Location specification/v0.4
        try {
          $checked = 0
          $errors = @()

          foreach ($line in Get-Content SHA256SUMS.txt) {
            if ([string]::IsNullOrWhiteSpace($line)) {
              continue
            }

            $parts = $line -split '\s+', 2
            $expected = $parts[0].ToLowerInvariant()
            $relative = $parts[1].Trim()

            if ($relative.EndsWith('.pdf', [StringComparison]::OrdinalIgnoreCase)) {
              continue
            }

            if (-not (Test-Path -LiteralPath $relative -PathType Leaf)) {
              $errors += "Missing canonical source: $relative"
              continue
            }

            $actual = (Get-FileHash -LiteralPath $relative -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actual -ne $expected) {
              $errors += "SHA-256 mismatch: $relative expected=$expected actual=$actual"
              continue
            }

            $checked++
          }

          if ($errors.Count -gt 0) {
            $errors | ForEach-Object { Write-Host $_ }
            throw "Canonical specification verification failed."
          }

          Write-Host "Canonical specification sources verified: $checked text/JSON/Python files."
        }
        finally {
          Pop-Location
        }

        # Run canonical mathematical reference verifier
        python --version
        python specification/v0.4/casos/verificar_referencias.py specification/v0.4/casos/casos_metodologicos.json

        # Build
        dotnet build Valdrio.slnx --configuration Release --no-restore

        # Test
        dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj --configuration Release --no-build --logger "trx;LogFileName=valdrio-tests.trx"

        # Guard diagnostic resource coverage
        $sourceFiles = Get-ChildItem src/Valdrio.Domain,src/Valdrio.Calculation,src/Valdrio.Application,src/Valdrio.Infrastructure,src/Valdrio.Desktop -Recurse -Filter *.cs |
          Where-Object { $_.FullName -notmatch '[\\\\/](obj|bin)[\\\\/]' -and $_.Name -ne 'AssemblyInfo.cs' }
        # AssemblyInfo only declares assembly attributes, including the test friend assembly.
        $pattern = '"(?<key>[A-Z][A-Za-z0-9]*(?:\.[A-Z][A-Za-z0-9]*)+)"'

        $keys = @(
          foreach ($file in $sourceFiles) {
            foreach ($match in (Select-String -Path $file.FullName -Pattern $pattern -AllMatches -CaseSensitive).Matches) {
              $match.Groups['key'].Value
            }
          }
        ) | Sort-Object -Unique

        [xml]$englishXml = Get-Content src/Valdrio.Localization/Resources/Strings.resx -Raw
        [xml]$spanishXml = Get-Content src/Valdrio.Localization/Resources/Strings.es.resx -Raw

        $englishKeys = @($englishXml.root.data | ForEach-Object { $_.name })
        $spanishKeys = @($spanishXml.root.data | ForEach-Object { $_.name })

        $missingEnglish = @($keys | Where-Object { $_ -notin $englishKeys })
        $missingSpanish = @($keys | Where-Object { $_ -notin $spanishKeys })

        if ($missingEnglish.Count -gt 0 -or $missingSpanish.Count -gt 0) {
          Write-Host "Missing English diagnostic keys: $($missingEnglish -join ', ')"
          Write-Host "Missing Spanish diagnostic keys: $($missingSpanish -join ', ')"
          throw "Diagnostic resources do not cover production message keys."
        }

        Write-Host "Diagnostic resource coverage passed for $($keys.Count) production keys."

        # Guard economic code against binary floating point
        $hits = Get-ChildItem src/Valdrio.Domain,src/Valdrio.Calculation -Recurse -Filter *.cs |
          Select-String -Pattern '\b(double|float)\b'

        if ($hits) {
          $hits | ForEach-Object { Write-Host $_.Line }
          throw "Domain/Calculation contains double or float."
        }

        # Runner T02 reference corpus
        dotnet run --project src/Valdrio.Runner/Valdrio.Runner.csproj --configuration Release --no-build -- --verify-t02-reference

        # Runner T02A currency metadata
        dotnet run --project src/Valdrio.Runner/Valdrio.Runner.csproj --configuration Release --no-build -- --verify-t02a-currency

        # Guard core against ambient current time
        $hits = Get-ChildItem src/Valdrio.Domain,src/Valdrio.Calculation,src/Valdrio.Application -Recurse -Filter *.cs |
          Select-String -Pattern 'DateTime\.(Now|UtcNow)|DateTimeOffset\.(Now|UtcNow)'

        if ($hits) {
          $hits | ForEach-Object { Write-Host $_.Line }
          throw "Domain/Calculation/Application reads ambient current time directly; use IClock."
        }

        # Runner T02A temporal contracts
        dotnet run --project src/Valdrio.Runner/Valdrio.Runner.csproj --configuration Release --no-build -- --verify-t02a-time

        # Runner T02A frozen FX
        dotnet run --project src/Valdrio.Runner/Valdrio.Runner.csproj --configuration Release --no-build -- --verify-t02a-fx

        # Guard Calculation against network APIs
        $hits = Get-ChildItem src/Valdrio.Calculation -Recurse -Filter *.cs |
          Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' } |
          Select-String -Pattern 'HttpClient|System\.Net\.|WebRequest|Socket'

        if ($hits) {
          $hits | ForEach-Object { Write-Host $_.Line }
          throw "Calculation references network APIs."
        }

        # Publish self-contained Windows x64 development artifact
        dotnet publish src/Valdrio.Desktop/Valdrio.Desktop.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/desktop

        # WPF launch smoke
        $exe = (Resolve-Path "artifacts/desktop/Valdrio.Desktop.exe").Path
        $sample = (Resolve-Path "samples/t01-smoke.valdrio-dev.json").Path
        $process = Start-Process -FilePath $exe -ArgumentList @($sample) -PassThru

        try {
          Start-Sleep -Seconds 6
          if ($process.HasExited) {
            throw "VALDRIO WPF exited during smoke test with code $($process.ExitCode)."
          }

          Write-Host "WPF launch smoke passed. Process remained alive for 6 seconds."
        }
        finally {
          if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
          }
        }

        # WPF invalid-preferences recovery smoke
        $exe = (Resolve-Path "artifacts/desktop/Valdrio.Desktop.exe").Path
        $sample = (Resolve-Path "samples/t01-smoke.valdrio-dev.json").Path
        $invalidPreferences = Join-Path $env:RUNNER_TEMP "valdrio-invalid-preferences.json"

        @{
          UiLanguageTag = "en"
          FormatCultureTag = "en-US"
          DisplayTimeZoneId = "Invalid/ReviewZone"
          DateDisplayPreset = 999
          NumberDisplayOptions = $null
        } | ConvertTo-Json | Set-Content -Path $invalidPreferences -Encoding utf8NoBOM

        $process = Start-Process -FilePath $exe -ArgumentList @(
          $sample,
          "--preferences-path=$invalidPreferences"
        ) -PassThru

        try {
          Start-Sleep -Seconds 5
          if ($process.HasExited) {
            throw "VALDRIO exited while recovering invalid presentation preferences with code $($process.ExitCode)."
          }

          Write-Host "WPF invalid-preferences recovery passed. Process remained alive for 5 seconds."
        }
        finally {
          if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
          }
        }

        Write-AcceptanceBuildManifest $sourceCommit
    }

    $buildManifest = Assert-AcceptanceBuildManifest $sourceCommit

    # Same UI acceptance flow used by GitHub Actions.
    $exe = (Resolve-Path "artifacts/desktop/Valdrio.Desktop.exe").Path
    $sample = (Resolve-Path "samples/t01-smoke.valdrio-dev.json").Path
    $captureDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($EvidenceDirectory)
    New-Item -ItemType Directory -Force -Path $captureDir | Out-Null

    $process = Start-Process -FilePath $exe -ArgumentList @(
      $sample,
      "--capture-dir=$captureDir"
    ) -PassThru -Wait

    if ($process.ExitCode -ne 0) {
      $failure = Join-Path $captureDir "company-failure.txt"
      if (Test-Path -LiteralPath $failure) { Get-Content -LiteralPath $failure | Write-Host }
      throw "Visual evidence process failed with code $($process.ExitCode)."
    }

    $fxChecks = Get-Content -LiteralPath (Join-Path $captureDir "fx-editor-interaction.json") -Raw | ConvertFrom-Json
    if ($fxChecks.Status -ne "passed" -or $fxChecks.PassedChecks.Count -ne 12) {
      throw "FX WPF interaction evidence is missing or incomplete."
    }
    Write-Host "FX WPF button interaction checks passed: $($fxChecks.PassedChecks.Count)."

    $companyChecks = Get-Content -LiteralPath (Join-Path $captureDir "company-file/interaction.json") -Raw | ConvertFrom-Json
    if ($companyChecks.Status -ne "passed" -or $companyChecks.PassedChecks.Count -ne 24) {
      throw "Company WPF interaction evidence is missing or incomplete."
    }
    Write-Host "Company WPF checks passed: $($companyChecks.PassedChecks.Count)."

    $modelChecks = Get-Content -LiteralPath (Join-Path $captureDir "models/interaction.json") -Raw | ConvertFrom-Json
    if ($modelChecks.Status -ne "passed" -or $modelChecks.PassedChecks.Count -ne 53) { throw "Model WPF interaction evidence is incomplete." }
    Write-Host "Model WPF checks passed: $($modelChecks.PassedChecks.Count)."
    $importChecks = Get-Content -LiteralPath (Join-Path $captureDir "import-columns/interaction.json") -Raw | ConvertFrom-Json
    if ($importChecks.Status -ne "passed" -or $importChecks.PassedChecks.Count -ne 13) {
      throw "Import column selection WPF interaction evidence is incomplete."
    }
    $importImages = @(Get-ChildItem -LiteralPath (Join-Path $captureDir "import-columns") -Filter *.png)
    if ($importImages.Count -ne 2) { throw "Expected CSV and Excel import column selection images." }
    Write-Host "Import column selection WPF checks passed: $($importChecks.PassedChecks.Count)."
    $studyChecks = Get-Content -LiteralPath (Join-Path $captureDir "studies/interaction.json") -Raw | ConvertFrom-Json
    if ($studyChecks.Status -ne "passed" -or $studyChecks.PassedChecks.Count -ne 73) { throw "Expected all 73 final study WPF checks." }
    foreach ($region in @("es-ES", "en-US", "de-DE", "en-GB")) {
      if ("visible calendar text follows region without input mutation: $region" -notin $studyChecks.PassedChecks) {
        throw "Missing visible calendar date verification: $region."
      }
    }
    foreach ($check in @(
      "region switch preserves pending edits and formats typed date",
      "study workflow preserves the model-stage artifact and manifest"
    )) {
      if ($check -notin $studyChecks.PassedChecks) { throw "Missing final study verification: $check." }
    }
    Write-Host "Study WPF checks passed: $($studyChecks.PassedChecks.Count)."

    $recoveryChecks = Get-Content -LiteralPath (Join-Path $captureDir "studies/recovery.json") -Raw | ConvertFrom-Json
    $expectedRecoveryChecks = @(
      "confirmed create survives refresh failure",
      "confirmed save survives refresh failure",
      "next save uses confirmed revision",
      "confirmed close survives refresh failure",
      "confirmed reopen survives refresh failure",
      "confirmed snapshot survives refresh failure",
      "manual refresh retains confirmed snapshot selection",
      "clean refresh adopts newer persisted revision",
      "dirty refresh preserves editor while list advances",
      "discard then refresh reconciles current revision"
    )
    if ($recoveryChecks.Status -ne "passed" -or
        $recoveryChecks.PassedChecks.Count -ne $expectedRecoveryChecks.Count -or
        @(Compare-Object ($expectedRecoveryChecks | Sort-Object) (@($recoveryChecks.PassedChecks) | Sort-Object)).Count -ne 0) {
      throw "Study post-commit recovery regression evidence is missing or incomplete."
    }
    Write-Host "Study post-commit recovery checks passed: $($recoveryChecks.PassedChecks.Count)."

    dotnet run --project src/Valdrio.Runner/Valdrio.Runner.csproj --configuration Release --no-build -- --study-snapshot (Join-Path $captureDir "studies/snapshot.json")
    if ($LASTEXITCODE -ne 0) { throw "Runner could not verify the actual WPF snapshot." }

    $studyImages = Get-ChildItem -LiteralPath (Join-Path $captureDir "studies") -Filter *.png
    if ($studyImages.Count -ne 8) { throw "Expected 8 study context/history captures, found $($studyImages.Count)." }

    foreach ($group in @("models", "studies")) {
      $folder = Join-Path $captureDir $group
      $manifest = Get-Content -LiteralPath (Join-Path $folder "manifest.json") -Raw | ConvertFrom-Json
      $expectedFiles = if ($group -eq "models") {
        @("example.valdrio", "example.valdrio-backup", "restored.valdrio", "original-v1.valdrio", "upgraded-current.valdrio")
      } else {
        @("example.valdrio", "study.valdrio-backup", "restored.valdrio", "original-v2.valdrio", "upgraded-v3.valdrio", "snapshot.json")
      }
      $actualNames = @($manifest.Files | ForEach-Object { $_.Name })
      if ($actualNames.Count -ne $expectedFiles.Count -or
          @(Compare-Object ($expectedFiles | Sort-Object) ($actualNames | Sort-Object)).Count -ne 0) {
        throw "Artifact manifest has missing, duplicate or unexpected files: $group."
      }
      foreach ($entry in $manifest.Files) {
        $actual = (Get-FileHash -LiteralPath (Join-Path $folder $entry.Name) -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $entry.Sha256) { throw "Artifact hash mismatch: $group/$($entry.Name)." }
      }
      Write-Host "Artifact manifest verified: $group ($($manifest.Files.Count) files)."
    }

    $files = Get-ChildItem -Path $captureDir -Filter *.png
    if ($files.Count -ne 4) {
      throw "Expected 4 visual evidence PNG files, found $($files.Count)."
    }

    $files | ForEach-Object {
      Write-Host "Visual evidence: $($_.Name) $($_.Length) bytes"
    }
    [xml]$trx = Get-Content -LiteralPath "tests/Valdrio.Tests/TestResults/valdrio-tests.trx" -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    if ([int]$counters.total -lt 183 -or [int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0 -or [int]$counters.total -ne [int]$counters.passed) {
        throw "The acceptance receipt requires all discovered tests to pass without omissions."
    }

    $endingCommit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $endingCommit -ne $sourceCommit) {
        throw "Repository HEAD changed during Windows acceptance."
    }
    Assert-CleanRepository "end"
    $buildManifest = Assert-AcceptanceBuildManifest $sourceCommit
    $buildManifestSha256 = Get-RequiredSha256 $buildManifestPath

    $receipt = [ordered]@{
        Status = "passed"
        SourceCommit = $sourceCommit
        VerifiedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        Tests = [int]$counters.passed
        StudyChecks = $studyChecks.PassedChecks.Count
        StudyRecoveryChecks = $recoveryChecks.PassedChecks.Count
        ModelChecks = $modelChecks.PassedChecks.Count
        CompanyChecks = $companyChecks.PassedChecks.Count
        FxChecks = $fxChecks.PassedChecks.Count
        ImportColumnChecks = $importChecks.PassedChecks.Count
        ImportColumnImages = $importImages.Count
        StudyImages = $studyImages.Count
        ModelAndStudyManifests = "verified"
        RepositoryClean = $true
        BuildManifestSha256 = $buildManifestSha256
        DesktopExeSha256 = $buildManifest.DesktopExe.Sha256
        RunnerDllSha256 = $buildManifest.RunnerDll.Sha256
        TestAssemblySha256 = $buildManifest.TestAssembly.Sha256
        TrxSha256 = $buildManifest.Trx.Sha256
    }
    $receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $captureDir "verification.json") -Encoding utf8NoBOM
    Write-Host "Windows acceptance passed for source commit $sourceCommit."
}
finally { Pop-Location }

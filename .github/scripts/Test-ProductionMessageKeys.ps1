[CmdletBinding()]
param()

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("valdrio-message-keys-" + [guid]::NewGuid().ToString('N'))
try {
    @('Domain', 'Calculation', 'Application', 'Infrastructure', 'Desktop') | ForEach-Object {
        New-Item -ItemType Directory -Path (Join-Path $fixtureRoot "src/Valdrio.$_") -Force | Out-Null
    }
    $source = @'
status.StartsWith("Model.Center", StringComparison.Ordinal);
status.StartsWith(
    "Model.Node", StringComparison.Ordinal);
Text("Resource.RealMissing");
Text("Resource.RealMissing");
'@
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'src/Valdrio.Desktop/Fixture.cs') -Value $source
    $keys = @(& (Join-Path $PSScriptRoot 'Get-ProductionMessageKeys.ps1') -RepositoryRoot $fixtureRoot)
    if ($keys.Count -ne 1 -or $keys[0] -ne 'Resource.RealMissing') {
        throw "Scanner lost a real diagnostic or included a state prefix: $($keys -join ', ')"
    }
    Write-Output 'Production-message scanner: prefix exclusion and real-missing-key control passed.'
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}

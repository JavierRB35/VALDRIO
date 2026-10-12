# Keep the local and workflow diagnostic-resource guards on the same scanner.
[CmdletBinding()]
param([string]$RepositoryRoot = (Get-Location).Path)

$sourceFiles = @('Domain', 'Calculation', 'Application', 'Infrastructure', 'Desktop') |
    ForEach-Object { Get-ChildItem (Join-Path $RepositoryRoot "src/Valdrio.$_") -Recurse -Filter *.cs } |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' -and $_.Name -ne 'AssemblyInfo.cs' }
$pattern = '"(?<key>[A-Z][A-Za-z0-9]*(?:\.[A-Z][A-Za-z0-9]*)+)"'

@(
    foreach ($file in $sourceFiles) {
        $source = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($match in [regex]::Matches($source, $pattern)) {
            # A StartsWith argument classifies state keys; it is not a resource lookup.
            # Match the call context, including line breaks, rather than allowlisting keys.
            if ($source.Substring(0, $match.Index) -match '\.StartsWith\s*\(\s*$') { continue }
            # WPF visual resource lookups are not localized business messages.
            if ($source.Substring(0, $match.Index) -match '(FindResource|TryFindResource)\s*\(\s*$') { continue }
            $match.Groups['key'].Value
        }
    }
) | Sort-Object -Unique

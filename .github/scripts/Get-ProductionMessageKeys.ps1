# Keep the local and workflow diagnostic-resource guards on the same scanner.
[CmdletBinding()]
param([string]$RepositoryRoot = (Get-Location).Path)

$sourceFiles = @('Domain', 'Calculation', 'Application', 'Infrastructure', 'Desktop') |
    ForEach-Object { Get-ChildItem (Join-Path $RepositoryRoot "src/Valdrio.$_") -Recurse -Filter *.cs } |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' -and $_.Name -ne 'AssemblyInfo.cs' }
$pattern = '"(?<key>[A-Z][A-Za-z0-9]*(?:\.[A-Z][A-Za-z0-9]*)+)"'

# UI theme keys are WPF ResourceDictionary references, never translated messages.
# Read the actual x:Key declarations rather than maintaining a growing allowlist
# of ThemeBrush(), Resources["..."], SetResourceReference(), or other callers.
$visualKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$themePath = Join-Path $RepositoryRoot 'src/Valdrio.Desktop/Styles/ValdrioTheme.xaml'
if (Test-Path -LiteralPath $themePath -PathType Leaf) {
    [xml]$themeXml = Get-Content -LiteralPath $themePath -Raw
    $ns = [System.Xml.XmlNamespaceManager]::new($themeXml.NameTable)
    $ns.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
    foreach ($entry in $themeXml.SelectNodes('//*[@x:Key]', $ns)) {
        $null = $visualKeys.Add($entry.GetAttribute('Key', 'http://schemas.microsoft.com/winfx/2006/xaml'))
    }
}

@(
    foreach ($file in $sourceFiles) {
        $source = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($match in [regex]::Matches($source, $pattern)) {
            # A StartsWith argument classifies state keys; it is not a resource lookup.
            # Match the call context, including line breaks, rather than allowlisting keys.
            if ($source.Substring(0, $match.Index) -match '\.StartsWith\s*\(\s*$') { continue }
            # WPF visual resource lookups are not localized business messages.
            if ($source.Substring(0, $match.Index) -match '(FindResource|TryFindResource)\s*\(\s*$') { continue }
            $key = $match.Groups['key'].Value
            if ($visualKeys.Contains($key)) { continue }
            $key
        }
    }
) | Sort-Object -Unique

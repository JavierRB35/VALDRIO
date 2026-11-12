[CmdletBinding()]
param()

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("valdrio-message-keys-" + [guid]::NewGuid().ToString('N'))
try {
    @('Domain', 'Calculation', 'Application', 'Infrastructure', 'Desktop') | ForEach-Object {
        New-Item -ItemType Directory -Path (Join-Path $fixtureRoot "src/Valdrio.$_") -Force | Out-Null
    }
    # The fixture has real translation lookups and several different WPF visual
    # resource lookups. Only actual declared x:Key values should be excluded.
    $styleDirectory = Join-Path $fixtureRoot 'src/Valdrio.Desktop/Styles'
    New-Item -ItemType Directory -Path $styleDirectory -Force | Out-Null
    $theme = @'
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="Brand.Red" Color="#B73536"/>
  <SolidColorBrush x:Key="Status.SuccessBg" Color="#EAF6EE"/>
  <Thickness x:Key="Table.CellPadding">4,4</Thickness>
  <Style x:Key="Button.Primary" TargetType="Button"/>
</ResourceDictionary>
'@
    Set-Content -LiteralPath (Join-Path $styleDirectory 'ValdrioTheme.xaml') -Value $theme
    $source = @'
status.StartsWith("Model.Center", StringComparison.Ordinal);
status.StartsWith(
    "Model.Node", StringComparison.Ordinal);
Text("Resource.RealMissing");
Text("Resource.RealMissing");
Text("Resource.OtherMissing");
study.Guide("Help.Name");
texts.Get("AllocationUI.SecondaryBehavior" + behavior, culture);
var brush = FindResource("Brand.Red");
var brush2 = TryFindResource("Brand.Red");
var brush3 = ThemeBrush("Brand.Red");
var brush4 = ThemeBrush(
    "Status.SuccessBg");
resources["Table.CellPadding"] = new Thickness(4);
button.SetResourceReference(StyleProperty, "Button.Primary");
'@
    Set-Content -LiteralPath (Join-Path $fixtureRoot 'src/Valdrio.Desktop/Fixture.cs') -Value $source
    $keys = @(& (Join-Path $PSScriptRoot 'Get-ProductionMessageKeys.ps1') -RepositoryRoot $fixtureRoot)
    $expected = @('AllocationUI.SecondaryBehaviorAssumedFixed', 'AllocationUI.SecondaryBehaviorFixed',
        'AllocationUI.SecondaryBehaviorMixed', 'AllocationUI.SecondaryBehaviorMixedPending',
        'AllocationUI.SecondaryBehaviorUnclassified', 'AllocationUI.SecondaryBehaviorVariable',
        'Guide.Help.Name', 'Resource.OtherMissing', 'Resource.RealMissing')
    if ($keys.Count -ne $expected.Count -or ($keys -join '|') -ne ($expected -join '|')) {
        throw "Scanner lost a missing diagnostic or treated a visual resource as a message: $($keys -join ', ')"
    }
    Write-Output 'Production-message scanner: theme resource exclusion and genuine-missing-key control passed.'
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}

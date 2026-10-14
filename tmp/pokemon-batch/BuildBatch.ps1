param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'output'))
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$bin = Join-Path $root 'ActEditor\bin\Release'
[Environment]::CurrentDirectory = $bin
foreach ($name in @('Utilities.dll', 'GRF.dll', 'TokeiLibrary.dll', 'GrfToWpfBridge.dll', 'ActImaging.dll')) { [Reflection.Assembly]::LoadFrom((Join-Path $bin $name)) | Out-Null }
Add-Type -AssemblyName System.Drawing, PresentationCore, PresentationFramework, WindowsBase
$gifTrialConverter = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'GrfToWpfBridge.dll')).GetExportedTypes() | Where-Object { $_.Name -eq 'DefaultImageConverter' }
[GRF.Image.ImageConverterManager]::AddConverter([Activator]::CreateInstance($gifTrialConverter))
$references = @(
'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll',
'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll',
'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll',
'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll',
'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll',
'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll',
'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\netstandard.dll',
[System.Windows.Media.ImageSource].Assembly.Location,
[System.Windows.Controls.Image].Assembly.Location,
[System.Windows.DependencyObject].Assembly.Location,
(Join-Path $bin 'GRF.dll'),(Join-Path $bin 'Utilities.dll'),(Join-Path $bin 'ActImaging.dll'))
Add-Type -ReferencedAssemblies $references -Path (Join-Path $PSScriptRoot 'BuildBatch.cs'), (Join-Path $PSScriptRoot 'MaskRecolor.cs'), (Join-Path $PSScriptRoot 'MaskTests.cs'), (Join-Path $PSScriptRoot 'BatchComparison.cs')
[MaskTests]::Run()
$batchInput='C:\Users\ShivaN\Desktop\pokes'
$batchTemplate='C:\Users\ShivaN\Desktop\Reverse SPR'
$batchConfigs=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'configs') -Filter '*.json' | Sort-Object Name)
$batchSummary=@()
foreach($batchFile in $batchConfigs){
 $batchCfg=Get-Content -LiteralPath $batchFile.FullName -Raw | ConvertFrom-Json
 $batchName=$batchCfg.name
 $batchOutput=Join-Path $OutputDirectory $batchName
 if(Test-Path -LiteralPath (Join-Path $batchOutput 'pupilas-color.json')){
  Write-Output ("SKIP existing verified artifact: "+$batchName)
  continue
 }
 $batchSources=@((Join-Path $batchInput ($batchName+'.gif')),(Join-Path $batchInput ($batchName+'-back.gif')),(Join-Path $batchTemplate 'ho-oh.act'),(Join-Path $batchTemplate 'ho-oh.spr'))
 $batchHashes=@($batchSources | ForEach-Object {(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash})
 [PokemonBatch]::Run($batchSources[0],$batchSources[1],$batchSources[2],$batchSources[3],(Join-Path $batchOutput 'original'),$false,$batchFile.FullName)
 [PokemonBatch]::Run($batchSources[0],$batchSources[1],$batchSources[2],$batchSources[3],$batchOutput,$true,$batchFile.FullName)
 [BatchComparison]::Run($batchOutput,$batchFile.FullName)
 for($batchIndex=0;$batchIndex -lt $batchSources.Count;$batchIndex++){
  if((Get-FileHash -LiteralPath $batchSources[$batchIndex] -Algorithm SHA256).Hash -ne $batchHashes[$batchIndex]){throw "Source modified: $($batchSources[$batchIndex])"}
 }
 Copy-Item -LiteralPath $batchFile.FullName -Destination (Join-Path $batchOutput 'pupilas-color.json')
 $batchSources | ForEach-Object {Get-FileHash -LiteralPath $_ -Algorithm SHA256} | Select-Object Path,Hash | Export-Csv -LiteralPath (Join-Path $batchOutput 'fuentes-sha256.csv') -NoTypeInformation -Encoding utf8
 Write-Output ("DONE "+$batchName+" RGB "+($batchCfg.color -join ','))
}


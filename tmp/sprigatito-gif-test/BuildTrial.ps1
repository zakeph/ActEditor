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
'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\netstandard.dll',
[System.Windows.Media.ImageSource].Assembly.Location,
[System.Windows.Controls.Image].Assembly.Location,
[System.Windows.DependencyObject].Assembly.Location,
(Join-Path $bin 'GRF.dll'),(Join-Path $bin 'Utilities.dll'),(Join-Path $bin 'ActImaging.dll'))
Add-Type -ReferencedAssemblies $references -Path (Join-Path $PSScriptRoot 'BuildTrial.cs')
$originals = @(
'C:\Users\ShivaN\Desktop\dfju3qx-87be8a73-6d90-435a-89b5-2e8c97d25ea3.gif',
'C:\Users\ShivaN\Desktop\dfxrwuw-85bebb3e-21c8-4e4f-bbb4-2fcc9a97d43c.gif',
'C:\Users\ShivaN\Desktop\Reverse SPR\ho-oh.act',
'C:\Users\ShivaN\Desktop\Reverse SPR\ho-oh.spr')
$hashes = @($originals | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash })
[SprigatitoTrial]::Run($originals[0], $originals[1], $originals[2], $originals[3], $OutputDirectory)
for ($i=0;$i -lt $originals.Count;$i++) {
 if ((Get-FileHash -LiteralPath $originals[$i] -Algorithm SHA256).Hash -ne $hashes[$i]) { throw "Original changed: $($originals[$i])" }
}
Write-Host 'PASS: all four original files retain their SHA-256 hashes.'


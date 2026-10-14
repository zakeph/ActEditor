param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root "ActEditor\bin\$Configuration"
$editorPath = Join-Path $bin 'Act Editor.exe'
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase,System.Xaml
$editor = [Reflection.Assembly]::LoadFrom($editorPath)
[Windows.Application]::ResourceAssembly = $editor
$references = @(
    $editorPath,
    (Join-Path $bin 'GRF.dll'),
    (Join-Path $bin 'Utilities.dll'),
    (Join-Path $bin 'TokeiLibrary.dll'),
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll',
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll',
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll',
    'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\netstandard.dll',
    [Windows.UIElement].Assembly.Location,
    [Windows.Application].Assembly.Location,
    [Windows.Point].Assembly.Location,
    [System.Xaml.XamlReader].Assembly.Location
)
$compiler = New-Object Microsoft.CSharp.CSharpCodeProvider
$options = New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateInMemory = $true
$options.ReferencedAssemblies.AddRange([string[]]$references)
$result = $compiler.CompileAssemblyFromFile($options, (Join-Path $PSScriptRoot 'ZoomDrawSlots.cs'))
if ($result.Errors.HasErrors) { throw (($result.Errors | ForEach-Object ToString) -join "`n") }
foreach ($reference in $references) { [Reflection.Assembly]::LoadFrom($reference) | Out-Null }
[ZoomDrawSlots]::Run()

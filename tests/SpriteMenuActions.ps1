param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$assemblyPath = Join-Path $root "ActEditor\bin\$Configuration\Act Editor.exe"
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Build ActEditor.sln in $Configuration before running this test: $assemblyPath"
}

$editor = [Reflection.Assembly]::LoadFrom($assemblyPath)
$selector = $editor.GetType('ActEditor.Core.WPF.EditorControls.SpriteSelector', $true)
$modeType = $editor.GetType('ActEditor.Core.SpriteEditMode', $true)

# Check the argument at the compiled menu-handler/Execute boundary. This catches
# dispatching Add After to Add (which Execute silently ignores) without opening
# a native file picker or requiring a loaded editor document.
foreach ($case in @(
    @{ Handler = '_miAddAfter_Click'; Mode = 'After' },
    @{ Handler = '_miAddBefore_Click'; Mode = 'Before' }
)) {
    $handler = $selector.GetMethod($case.Handler, [Reflection.BindingFlags]'Instance,NonPublic')
    # Release handlers load this, load a small enum constant, and call Execute.
    $rawIl = $handler.GetMethodBody().GetILAsByteArray()
    $callOffset = [Array]::IndexOf($rawIl, [byte]0x28)
    if ($callOffset -lt 2) { throw "Cannot find Execute call in $($case.Handler)" }
    $callee = $handler.Module.ResolveMethod([BitConverter]::ToInt32($rawIl, $callOffset + 1))
    if ($callee.Name -ne 'Execute' -or $callee.DeclaringType -ne $selector) {
        throw "$($case.Handler) does not call SpriteSelector.Execute"
    }
    $constantOpcode = $rawIl[$callOffset - 1]
    if ($constantOpcode -lt 0x16 -or $constantOpcode -gt 0x1E) {
        throw "Unsupported mode argument in $($case.Handler)"
    }
    $actual = [Enum]::GetName($modeType, [int]($constantOpcode - 0x16))
    if ($actual -ne $case.Mode) {
        throw "$($case.Handler): expected $($case.Mode), got $actual; the wrong mode prevents the file picker from opening."
    }
    Write-Host "PASS $($case.Handler) dispatches $actual"
}

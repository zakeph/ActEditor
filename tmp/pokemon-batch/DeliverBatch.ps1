$ErrorActionPreference='Stop'
$deliverySource='D:\xampp\htdocs\ActEditor\tmp\pokemon-batch\output'
$deliveryTarget='C:\Users\ShivaN\Desktop\pokes\Convertidos'
$deliveryPilot=Join-Path $deliveryTarget 'dragonite'
if(-not(Test-Path -LiteralPath (Join-Path $deliverySource 'verificacion-lote.json'))){throw 'Missing batch verification'}
$deliverySummary=Get-Content -LiteralPath (Join-Path $deliverySource 'verificacion-lote.json') -Raw | ConvertFrom-Json
if($deliverySummary.species -ne 42 -or -not $deliverySummary.sourceHashesIntact -or -not $deliverySummary.comparisonScriptsValid){throw 'Batch verification incomplete'}
$deliveryPilotHashes=@{}
foreach($deliveryFile in Get-ChildItem -LiteralPath $deliveryPilot -File -Recurse){$deliveryPilotHashes[$deliveryFile.FullName]=(Get-FileHash -LiteralPath $deliveryFile.FullName -Algorithm SHA256).Hash}
$deliveryFiles=@(Get-ChildItem -LiteralPath $deliverySource -File -Recurse)
$deliveryCount=0
foreach($deliveryFile in $deliveryFiles){
 $deliveryRelative=$deliveryFile.FullName.Substring($deliverySource.Length+1)
 $deliveryDestination=Join-Path $deliveryTarget $deliveryRelative
 $deliveryParent=Split-Path -Path $deliveryDestination -Parent
 [IO.Directory]::CreateDirectory($deliveryParent) | Out-Null
 if(Test-Path -LiteralPath $deliveryDestination){
  if((Get-FileHash -LiteralPath $deliveryDestination -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $deliveryFile.FullName -Algorithm SHA256).Hash){throw "Existing different file preserved: $deliveryDestination"}
 } else {[IO.File]::Copy($deliveryFile.FullName,$deliveryDestination,$false)}
 if((Get-FileHash -LiteralPath $deliveryDestination -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $deliveryFile.FullName -Algorithm SHA256).Hash){throw "Delivery hash mismatch: $deliveryDestination"}
 $deliveryCount++
}
foreach($deliveryPath in $deliveryPilotHashes.Keys){if((Get-FileHash -LiteralPath $deliveryPath -Algorithm SHA256).Hash -ne $deliveryPilotHashes[$deliveryPath]){throw "Dragonite modified: $deliveryPath"}}
if(@(Get-ChildItem -LiteralPath $deliveryTarget -Directory).Count -ne 43){throw 'Expected 43 final Pokemon folders'}
Write-Output "PASS: $deliveryCount files copied and SHA256 verified; 42 new Pokemon, 43 total; approved Dragonite unchanged."

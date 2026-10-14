param([string]$BatchRoot = $PSScriptRoot)
$ErrorActionPreference='Stop'
$finalOutput=Join-Path $BatchRoot 'output'
$finalCards=[Collections.Generic.List[string]]::new()
$finalRows=[Collections.Generic.List[object]]::new()
$finalTotalChecked=0L
$finalTotalRendered=0L
$finalCards.Add(@'
<article><h2>Dragonite · aprobado</h2><img src="dragonite/comparacion.png" alt="Dragonite: original y ajuste aprobado" loading="lazy"><p>Prueba anterior aprobada · 102 fotogramas por vista</p><nav><a href="dragonite/comparacion.html">Comparar animación</a><a href="dragonite/dragonite.act">ACT</a><a href="dragonite/dragonite.spr">SPR</a></nav></article>
'@)
foreach($finalDirectory in @(Get-ChildItem -LiteralPath $finalOutput -Directory | Sort-Object Name)){
 $finalName=$finalDirectory.Name
 $finalConfig=Get-Content -LiteralPath (Join-Path $finalDirectory.FullName 'pupilas-color.json') -Raw | ConvertFrom-Json
 $finalReport=Get-Content -LiteralPath (Join-Path $finalDirectory.FullName 'verification.txt') -Raw
 if($finalReport -notmatch 'PASS:.*frames compared between saved original and adjusted ACT/SPR') {throw "Missing comparison verification: $finalName"}
 $finalFront=Get-Content -LiteralPath (Join-Path $BatchRoot ("probe\$finalName-front.json")) -Raw | ConvertFrom-Json
 $finalBack=Get-Content -LiteralPath (Join-Path $BatchRoot ("probe\$finalName-back.json")) -Raw | ConvertFrom-Json
 $finalChecked=[long][regex]::Match($finalReport,'Round-trip frames checked: (\d+)').Groups[1].Value
 $finalRendered=[long][regex]::Match($finalReport,'Native ACT renderer distinct poses per direction checked: (\d+)').Groups[1].Value
 $finalProtectedFront=0L;foreach($finalMask in $finalConfig.front.frames){$finalProtectedFront+=$finalMask.Count}
 $finalProtectedBack=0L;foreach($finalMask in $finalConfig.back.frames){$finalProtectedBack+=$finalMask.Count}
 $finalHex='#{0:x2}{1:x2}{2:x2}' -f [int]$finalConfig.color[0],[int]$finalConfig.color[1],[int]$finalConfig.color[2]
 $finalRgb=$finalConfig.color -join ', '
 $finalTitle=[Globalization.CultureInfo]::InvariantCulture.TextInfo.ToTitleCase($finalName)
 $finalCards.Add(@"
<article><h2>$finalTitle <span class="color" style="background:$finalHex" title="RGB $finalRgb"></span></h2><img src="$finalName/comparacion.png" alt="${finalTitle}: original a la izquierda, ajustado a la derecha" loading="lazy"><p>$($finalFront.count) fotogramas delante · $($finalBack.count) detrás</p><nav><a href="$finalName/comparacion.html">Comparar animación</a><a href="$finalName/$finalName.act">ACT</a><a href="$finalName/$finalName.spr">SPR</a></nav></article>
"@)
 $finalNotes=@"
# ${finalTitle}: ACT/SPR con color ajustado

Abre $finalName.act en Act Editor y mantén $finalName.spr a su lado.
comparacion.html compara las animaciones original y ajustada, con pausa,
zoom y fondos claro/oscuro. La subcarpeta original conserva los colores originales.

Color nuevo del negro: RGB $finalRgb. Se cambia el negro neutro de los GIF
(RGB 0 a 16) salvo las pupilas indicadas por máscaras por fotograma.
Los colores de pupila que ya son distintos del negro no se modifican.
Se han revisado ambas vistas, incluyendo los ojos visibles desde atrás.
No se escala el sprite ni se cambia la transparencia.

$($finalFront.count) fotogramas delanteros y $($finalBack.count) traseros.
Se conservan todas las poses y sus pausas. Cuando el GIF tiene tiempos variables,
el ACT repite poses para representarlos con su intervalo uniforme.
Se usan los 40 slots y las posiciones/reflejos de Ho-Oh; no se crean dibujos
de ataque, daño ni muerte, sino que se reutilizan los bucles de mascota.

Verificación: $finalChecked pasos reabiertos por versión, $finalRendered
renderizados nativos distintos por versión, comparación original/ajustado,
colores, transparencia, tiempos, dimensiones, alineación, reflejos y
conservación de los hashes de los GIF y la plantilla.
verification.txt contiene los resultados. pupilas-color.json permite auditar
las máscaras y el color. Notas de la revisión: $($finalConfig.notes)

Abre comparacion.html en tu navegador para revisar la animación.
La prueba dentro del cliente del juego queda pendiente.
"@
 Set-Content -LiteralPath (Join-Path $finalDirectory.FullName 'LEEME.md') -Value $finalNotes -Encoding utf8
 $finalRows.Add([pscustomobject]@{Pokemon=$finalName;FrontFrames=$finalFront.count;BackFrames=$finalBack.count;RGB=$finalRgb;ProtectedFront=$finalProtectedFront;ProtectedBack=$finalProtectedBack;CheckedPerVersion=$finalChecked;RendersPerVersion=$finalRendered;Notes=$finalConfig.notes})
 $finalTotalChecked+=$finalChecked;$finalTotalRendered+=$finalRendered
}
$finalIndex=@"
<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Pokémon · ACT y SPR</title><style>*{box-sizing:border-box}body{font:16px system-ui;margin:28px;background:#edf1f5;color:#243044}h1{margin-bottom:8px}header p{max-width:850px;line-height:1.5}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(290px,390px));gap:20px}article{background:white;border:1px solid #cbd5e1;border-radius:12px;overflow:hidden}h2{font-size:20px;margin:16px}.color{display:inline-block;width:18px;height:18px;border:1px solid #777;border-radius:50%;vertical-align:middle}img{width:100%;height:auto;display:block;image-rendering:pixelated}article p{font-size:14px;margin:12px 16px}nav{display:flex;gap:14px;flex-wrap:wrap;padding:4px 16px 20px}a{color:#165f9b}</style><header><h1>Pokémon · ACT y SPR</h1><p>$($finalRows.Count) Pokémon nuevos más Dragonite: $($finalRows.Count+1) en total, con pupilas protegidas y negro adaptado a su paleta. En cada imagen, el original está a la izquierda y el ajuste a la derecha. Mantén juntos el ACT y el SPR y abre el ACT en Act Editor.</p><p>Dragonite conserva su prueba aprobada. Los GIF originales están intactos. Queda pendiente la comprobación dentro del juego.</p></header><main>$($finalCards -join "`n")</main></html>
"@
Set-Content -LiteralPath (Join-Path $finalOutput 'indice.html') -Value $finalIndex -Encoding utf8
$finalRows | Export-Csv -LiteralPath (Join-Path $finalOutput 'resumen-conversion.csv') -NoTypeInformation -Encoding utf8
Set-Content -LiteralPath (Join-Path $finalOutput 'LEEME.txt') -Encoding utf8 -Value @"
Abre indice.html para revisar todos los Pokemon y sus comparaciones animadas.
Cada Pokemon tiene su carpeta con ACT y SPR, que deben permanecer juntos.
Dragonite conserva su prueba anterior aprobada.
GIF originales intactos. Negro RGB0/16 recoloreado salvo pupilas revisadas.
Verificados $finalTotalChecked pasos ACT por versión y $finalTotalRendered renderizados distintos por versión.
Falta comprobar los resultados dentro del juego.
"@
Write-Output "PASS: $($finalRows.Count) Pokemon finalizados; $finalTotalChecked pasos por version; $finalTotalRendered renderizados por version."

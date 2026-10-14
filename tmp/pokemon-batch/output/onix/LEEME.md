# Onix: ACT/SPR con color ajustado

Abre onix.act en Act Editor y mantén onix.spr a su lado.
comparacion.html compara las animaciones original y ajustada, con pausa,
zoom y fondos claro/oscuro. La subcarpeta original conserva los colores originales.

Color nuevo del negro: RGB 41, 33, 33. Se cambia el negro neutro de los GIF
(RGB 0 a 16) salvo las pupilas indicadas por máscaras por fotograma.
Los colores de pupila que ya son distintos del negro no se modifican.
Se han revisado ambas vistas, incluyendo los ojos visibles desde atrás.
No se escala el sprite ni se cambia la transparencia.

99 fotogramas delanteros y 143 traseros.
Se conservan todas las poses y sus pausas. Cuando el GIF tiene tiempos variables,
el ACT repite poses para representarlos con su intervalo uniforme.
Se usan los 40 slots y las posiciones/reflejos de Ho-Oh; no se crean dibujos
de ataque, daño ni muerte, sino que se reutilizan los bucles de mascota.

Verificación: 4880 pasos reabiertos por versión, 124
renderizados nativos distintos por versión, comparación original/ajustado,
colores, transparencia, tiempos, dimensiones, alineación, reflejos y
conservación de los hashes de los GIF y la plantilla.
verification.txt contiene los resultados. pupilas-color.json permite auditar
las máscaras y el color. Notas de la revisión: White-sclera anchors locate both front eyes in all 99 frames and the rear profile eye in all 143 frames. Front near pupil is a 1-2 pixel vertical mark; far eye pupil/border are inseparable in the two-pixel sclera, so 1-2 immediately adjacent local eye pixels are conservatively protected. Rear protects its single eye pixel. All pose variants visually overlaid and reviewed; no body outline protected.

Abre comparacion.html en tu navegador para revisar la animación.
La prueba dentro del cliente del juego queda pendiente.

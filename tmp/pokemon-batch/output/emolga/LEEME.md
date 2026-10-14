# Emolga: ACT/SPR con color ajustado

Abre emolga.act en Act Editor y mantén emolga.spr a su lado.
comparacion.html compara las animaciones original y ajustada, con pausa,
zoom y fondos claro/oscuro. La subcarpeta original conserva los colores originales.

Color nuevo del negro: RGB 33, 33, 33. Se cambia el negro neutro de los GIF
(RGB 0 a 16) salvo las pupilas indicadas por máscaras por fotograma.
Los colores de pupila que ya son distintos del negro no se modifican.
Se han revisado ambas vistas, incluyendo los ojos visibles desde atrás.
No se escala el sprite ni se cambia la transparencia.

188 fotogramas delanteros y 186 traseros.
Se conservan todas las poses y sus pausas. Cuando el GIF tiene tiempos variables,
el ACT repite poses para representarlos con su intervalo uniforme.
Se usan los 40 slots y las posiciones/reflejos de Ho-Oh; no se crean dibujos
de ataque, daño ni muerte, sino que se reutilizan los bucles de mascota.

Verificación: 7560 pasos reabiertos por versión, 592
renderizados nativos distintos por versión, comparación original/ajustado,
colores, transparencia, tiempos, dimensiones, alineación, reflejos y
conservación de los hashes de los GIF y la plantilla.
verification.txt contiene los resultados. pupilas-color.json permite auditar
las máscaras y el color. Notas de la revisión: Two exact pixel eye templates match uniquely in every front frame across the full canvas, including the large flight translation. All nine neutral eye pixels protected per frame. Body, ears, claws and other neutral contour pixels remain recolorable. Rear has no visible pupils.

Abre comparacion.html en tu navegador para revisar la animación.
La prueba dentro del cliente del juego queda pendiente.

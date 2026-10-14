# Politoed: ACT/SPR con color ajustado

Abre politoed.act en Act Editor y mantén politoed.spr a su lado.
comparacion.html compara las animaciones original y ajustada, con pausa,
zoom y fondos claro/oscuro. La subcarpeta original conserva los colores originales.

Color nuevo del negro: RGB 49, 115, 0. Se cambia el negro neutro de los GIF
(RGB 0 a 16) salvo las pupilas indicadas por máscaras por fotograma.
Los colores de pupila que ya son distintos del negro no se modifican.
Se han revisado ambas vistas, incluyendo los ojos visibles desde atrás.
No se escala el sprite ni se cambia la transparencia.

95 fotogramas delanteros y 95 traseros.
Se conservan todas las poses y sus pausas. Cuando el GIF tiene tiempos variables,
el ACT repite poses para representarlos con su intervalo uniforme.
Se usan los 40 slots y las posiciones/reflejos de Ho-Oh; no se crean dibujos
de ataque, daño ni muerte, sino que se reutilizan los bucles de mascota.

Verificación: 3840 pasos reabiertos por versión, 260
renderizados nativos distintos por versión, comparación original/ajustado,
colores, transparencia, tiempos, dimensiones, alineación, reflejos y
conservación de los hashes de los GIF y la plantilla.
verification.txt contiene los resultados. pupilas-color.json permite auditar
las máscaras y el color. Notas de la revisión: All190 frames matched17 visually reviewed white-eye template variants. Front far eye keeps3 aperture ink pixels in every frame; main eye13/14 pixels depending pose. Rear eye6-8 pixels normally; frames58,60,62 have antenna crossing the aperture and retain only the visible conservative eye-region ink, excluding adjacent head contour. Both initial masks exactly reproduce supplied reference coordinates.

Abre comparacion.html en tu navegador para revisar la animación.
La prueba dentro del cliente del juego queda pendiente.

# Glaceon: ACT/SPR con color ajustado

Abre glaceon.act en Act Editor y mantén glaceon.spr a su lado.
comparacion.html compara las animaciones original y ajustada, con pausa,
zoom y fondos claro/oscuro. La subcarpeta original conserva los colores originales.

Color nuevo del negro: RGB 49, 58, 74. Se cambia el negro neutro de los GIF
(RGB 0 a 16) salvo las pupilas indicadas por máscaras por fotograma.
Los colores de pupila que ya son distintos del negro no se modifican.
Se han revisado ambas vistas, incluyendo los ojos visibles desde atrás.
No se escala el sprite ni se cambia la transparencia.

171 fotogramas delanteros y 171 traseros.
Se conservan todas las poses y sus pausas. Cuando el GIF tiene tiempos variables,
el ACT repite poses para representarlos con su intervalo uniforme.
Se usan los 40 slots y las posiciones/reflejos de Ho-Oh; no se crean dibujos
de ataque, daño ni muerte, sino que se reutilizan los bucles de mascota.

Verificación: 6880 pasos reabiertos por versión, 204
renderizados nativos distintos por versión, comparación original/ajustado,
colores, transparencia, tiempos, dimensiones, alineación, reflejos y
conservación de los hashes de los GIF y la plantilla.
verification.txt contiene los resultados. pupilas-color.json permite auditar
las máscaras y el color. Notas de la revisión: Front pupils are already dark blue RGB49,58,74 and stay unchanged; neutral upper/left lines are eye borders. Rear profile protects neutral pupil pixel below the unique white glint when present; glint tracked in all171 frames. Existing dark blue used for outline.

Abre comparacion.html en tu navegador para revisar la animación.
La prueba dentro del cliente del juego queda pendiente.

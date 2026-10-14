# Dragonite: prueba desde dos GIF

Extrae el ZIP y abre dragonite.act en Act Editor. Mantén dragonite.spr en la misma carpeta y con el mismo nombre base.

Abre preview.html para ver las ocho posiciones animadas. La vista previa lee imágenes decodificadas del SPR final y los tiempos del ACT. directions.png resume las posiciones. renderer-front.png y renderer-back.png proceden del renderizador nativo del editor.

FUENTES
- dragonite.gif: 102 fotogramas, lienzo de 80 x 82 píxeles.
- dragonite back.gif: 102 fotogramas, lienzo de 86 x 81 píxeles.
- Ambos ciclos duran 6300 ms. Contienen fotogramas de 50, 150, 600 y 400 ms.

TIEMPOS DEL ACT
Cada vista tiene 126 pasos de 50 ms, numerados del 0 al 125. Los fotogramas con pausas largas repiten su imagen durante varios pasos, porque el ACT usa un único intervalo por acción. Se conservan todas las poses y sus duraciones exactas. Se comparten las imágenes repetidas en el SPR: 76 imágenes únicas y 15 colores opacos exactos, con el índice cero reservado para transparencia. No se ha aplicado escalado ni cuantización con pérdida.

POSICIONES
Se utiliza la distribución de Ho-Oh: 0/1 delante, 6/7 delante reflejado, 4/5 detrás y 2/3 detrás reflejado. Se conservan los 40 slots del ACT; cada uno reproduce el bucle de la vista correspondiente. No se añaden dibujos de combate.

ALINEACIÓN
Se mantiene fijo el lienzo de cada vista para preservar el movimiento original entre poses. Ambas vistas tienen un desplazamiento Y de -41 para alinear su límite inferior con el suelo. La vista previa amplía a 2x con píxeles nítidos; el SPR conserva su tamaño original.

VERIFICACIÓN
Los lectores del editor han reabierto los archivos y comparado 5040 fotogramas: colores opacos, transparencia, tiempos, posiciones y reflejos. El motor nativo ha renderizado 304 combinaciones distintas de pose y dirección. Se han revisado las imágenes delanteras, traseras y de las ocho posiciones. Los cuatro originales mantienen sus hashes SHA-256.

La prueba dentro del cliente del juego queda pendiente. Este resultado es una prueba de conversión; el importador de dos GIF todavía no está incorporado a la interfaz del editor. La prueba de Sprigatito se conserva aparte.

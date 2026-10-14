# Sprigatito: prueba desde dos GIF

Extrae el ZIP y abre sprigatito.act en Act Editor. Mantén sprigatito.spr en la misma carpeta y con el mismo nombre base.

Abre preview.html en tu navegador para ver las ocho posiciones animadas. La vista previa usa imágenes decodificadas del SPR final y conserva los tiempos del ACT. directions.png resume las ocho posiciones. renderer-front.png y renderer-back.png salen del renderizador nativo del editor.

Delante: 53 fotogramas de 80 ms (4,24 s por ciclo). Detrás: 5 fotogramas de 90 ms (0,45 s por ciclo). Se mantienen los fotogramas repetidos en la línea de tiempo y se comparten sus imágenes en el SPR: 19 imágenes únicas, 27 colores opacos exactos y transparencia en el índice cero. SPR indexado, sin pérdida de color ni escalado.

Distribución de Ho-Oh: posiciones 0/1 delanteras, 6/7 delanteras reflejadas, 4/5 traseras y 2/3 traseras reflejadas. Se conservan los 40 slots del ACT, con el bucle de la vista correspondiente en cada uno. No se añaden dibujos de combate.

Se conserva un lienzo fijo de 96 x 96 y un desplazamiento Y de -22 en ambas vistas para alinear los pies al suelo. No se recentran las poses individualmente.

VERIFICACIÓN
Los lectores del editor han vuelto a abrir el ACT y el SPR. Se han comparado colores opacos, transparencia, tiempos, reflejos y posiciones en los 1160 fotogramas de los 40 slots. El renderizador nativo ha procesado los 232 fotogramas de las ocho direcciones del primer grupo. Los cuatro originales conservan sus hashes SHA-256.

La prueba dentro del cliente del juego queda pendiente. Este resultado es una prueba de conversión; la función de importar dos GIF todavía no está incorporada a la interfaz del editor.

El generador puntual y su lanzador se guardan junto a la carpeta output: BuildTrial.cs y BuildTrial.ps1. Usan las bibliotecas ya compiladas del proyecto. No se han modificado el código del editor ni los archivos originales.

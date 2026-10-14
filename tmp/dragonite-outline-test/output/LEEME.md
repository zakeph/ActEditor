# Dragonite: prueba de ACT/SPR con borde ajustado

Abre comparacion.html para comparar las animaciones original y ajustada.
Se muestran ambas vistas sincronizadas; puedes pausar, ampliar y cambiar el fondo.
comparacion.png ofrece la misma comparación en el primer fotograma.

## Abrir en Act Editor

Abre dragonite.act. Mantén dragonite.spr junto a él, con el mismo nombre base.
La subcarpeta original contiene la conversión con sus colores originales.
preview.html muestra las ocho posiciones del resultado ajustado.

## Ajuste del borde

RGB original: 0, 0, 0. Color nuevo: 90, 58, 33 (ARGB 255, 90, 58, 33).
Solo se cambia negro opaco adyacente a la transparencia exterior.
La máscara empieza desde fuera del lienzo; los huecos transparentes cerrados
quedan excluidos. Se ajusta una capa de un píxel, sin cambiar la transparencia.
Se conserva el negro interior, incluido el de la pupila. Algunas líneas negras
internas y zonas de borde más gruesas permanecen negras por este criterio.
El mismo marrón se usa en todos los fotogramas delanteros y traseros.

## Animación y posiciones

Cada GIF contiene 102 fotogramas y un ciclo de 6300 ms.
El ACT los representa con 126 pasos de 50 ms, repitiendo poses para conservar
exactamente las pausas del GIF. El SPR contiene 76 imágenes únicas.
Se conservan el tamaño original y la alineación estable: no hay escalado.
Se usan las posiciones de Ho-Oh y sus reflejos:
0/1 delante, 6/7 delante reflejado; 4/5 detrás, 2/3 detrás reflejado.
Los 40 slots reproducen estos bucles; no se han creado dibujos de combate.

## Verificación

Los lectores del editor han reabierto ambos pares ACT/SPR.
Se han comprobado 5040 pasos por versión y 304 renderizados nativos distintos.
También se han comparado los 5040 pasos de la versión original con la ajustada:
solo cambia negro opaco al marrón indicado; se conservan tiempos, tamaños,
transparencia, reflejos y posiciones.
Los GIF y la plantilla Ho-Oh conservan sus hashes SHA-256.
verification.txt y fuentes-sha256.csv contienen el registro de verificación.

Se ha revisado visualmente comparacion.png. La apertura automática del HTML
local está bloqueada por la política del navegador integrado, por lo que los
controles de la comparación deben revisarse al abrirla manualmente.
La prueba dentro del cliente del juego queda pendiente.

# Dragonite: negro recoloreado con pupila protegida

Abre comparacion.html para comparar las animaciones original y ajustada.
Abre dragonite.act en Act Editor, manteniendo dragonite.spr junto a él.

## Corrección respecto a la primera prueba

Se cambia TODO el negro opaco RGB 0,0,0 a RGB 90,58,33, excepto la pupila frontal.
En la espalda se cambia todo el negro, incluidas las líneas internas.
La pupila no se protege con un rectángulo fijo: se localizan los seis píxeles
claros del ojo en cada fotograma y se conservan exactamente los cuatro píxeles
negros de la pupila en su posición correspondiente.
La regla se ha comprobado en los 102 fotogramas delanteros. Si no encuentra
la forma esperada del ojo, el conversor rechaza ese fotograma antes de cambiarlo.

Esta detección es específica para el GIF de Dragonite que hemos inspeccionado.
Otros Pokémon necesitan su propia máscara/detección de ojos.

## Animación y posiciones

102 fotogramas por vista; ciclo de 6300 ms.
El ACT usa 126 pasos de 50 ms para conservar exactamente las pausas del GIF.
76 imágenes únicas en el SPR; dimensiones y alineación originales.
Posiciones de Ho-Oh y reflejos: 0/1 delante, 6/7 delante reflejado,
4/5 detrás, 2/3 detrás reflejado. Los 40 slots reproducen estos bucles.
No se han creado dibujos de combate.

## Verificación

Pruebas de pupila móvil, recoloreado interior/exterior y trasero,
conservación de transparencia/colores no negros y rechazo si falta el ojo.
5040 pasos por versión reabiertos con los lectores del editor.
304 renderizados nativos distintos por versión.
Comparación de los dos ACT/SPR guardados:
- Exactamente cuatro píxeles negros de pupila por pose delantera.
- Cero píxeles negros opacos en las poses traseras.
- Solo cambia negro opaco a RGB 90,58,33.
- Se conservan tiempos, dimensiones, transparencia, posiciones y reflejos.
Los GIF y la plantilla Ho-Oh mantienen sus hashes SHA-256.

comparacion.png se ha revisado visualmente. La sintaxis JavaScript de la
comparación animada está verificada. Su apertura automática está bloqueada por
el navegador integrado; los controles deben revisarse al abrirla manualmente.
La prueba dentro del cliente del juego queda pendiente.

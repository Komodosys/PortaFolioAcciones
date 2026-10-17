# Guía de uso

Para la familia. Dos cosas: cómo se entra y qué significa cada número.

Y una tercera, que es la que más importa: **qué hacer cuando algo no cuadra**.

---

## 1. Entrar

La aplicación se abre en el navegador, en la dirección de la casa — `http://localhost:8080` si estás en el
mismo equipo donde corre—. No hay que instalar nada.

Se entra con un **usuario** y una **contraseña**. La primera vez, la contraseña es **temporal**: la entrega
el administrador en mano, y la aplicación pide cambiarla al entrar. **Esa contraseña temporal no se envía
por correo ni por mensaje**: se da en persona, porque un mensaje se reenvía y un papel se pierde.

Si te olvidas de la contraseña, **no hay «recuperar contraseña»**. Es a propósito: no hay correo, y una
pregunta secreta se la sabe cualquiera que te conozca. Se le pide al administrador que **genere una
temporal nueva** y te la dé en mano.

Cada persona ve **solo sus carteras**, y no puede ver las de los demás ni aunque escriba la dirección a
mano. El administrador lo ve todo, y **cada vez que toca datos de otra persona queda anotado** — con qué
cambió, cuándo y desde dónde.

---

## 2. Qué significa cada columna

En **Carteras** está la lista. Al entrar en una, la tabla de posiciones:

| Columna | Qué es | Cuidado con esto |
|---|---|---|
| **Símbolo** | El nombre corto con el que se conoce el valor | Es el del bróker. El que usa la fuente de precios puede ser otro: para los fondos es un código que no se parece en nada, y por eso están mapeados |
| **Cantidad** | Cuántas unidades tienes | En los fondos son decimales, y no es un error: se compran por importe |
| **Coste** | Lo que pagaste por esas unidades | Es el **coste medio**, no el de la última compra. Si compraste en varios momentos, es la media |
| **Precio** | A cuánto cotiza hoy | Debajo pone **de qué día es**. Si el precio es viejo, sale un aviso en ámbar |
| **Valor** | Cantidad × precio | Si no hay precio, sale un guion. **El guion no es un cero**: es que no se sabe |
| **Hoy** | Cuánto ha cambiado **hoy** | Guion cuando no hay cierre anterior con el que comparar — una compra de hoy no tiene «ayer» |
| **Ganancia** | Valor − coste | Sin vender. No es dinero en el bolsillo hasta que se vende |
| **Peso** | Qué parte de la cartera es | Se calcula sobre lo que está valorado, que puede no ser toda la cartera |

Arriba, los totales. Y tres cifras que conviene mirar **antes** que las demás:

- **«Total parcial»** — cuando falta el precio de alguna posición. El total que ves es **solo lo que se
  sabe**, y por eso es más bajo que la realidad. No es un fallo: es la aplicación diciendo que no puede
  sumar todo.
- **«Desactualizado»**, en ámbar — el precio es anterior a la última sesión del mercado. Sirve para saber
  que la cifra puede no ser de ahora.
- **«Hoy: solo lo que se sabe»** — igual que el anterior, pero con el cambio del día.

---

## 3. Los dos avisos que piden que hagas algo

### «Hay un desdoblamiento sin aplicar»

Un **desdoblamiento** es cuando una empresa parte cada acción en varias: con un 5:1, por cada una que
tenías pasas a tener cinco, y el precio se divide entre cinco. **No ganas ni pierdes nada**: tienes más
unidades que valen menos cada una.

Pero si no se aplica, la cantidad se queda como estaba y el precio sí baja, así que la aplicación lee una
caída del 80 % que no ha ocurrido. **Por eso el aviso es importante.** Y tiene dos salidas:

- **Aplicarlo** — multiplica las cantidades. Es lo correcto si capturaste tus posiciones **antes** del
  desdoblamiento.
- **「Ya está reflejado」** — no toca nada, solo quita el aviso. Es lo correcto si copiaste tus cifras de un
  estado de cuenta **posterior** al desdoblamiento: ya las tenías bien, y aplicarlo las multiplicaría por
  segunda vez.

**Ante la duda, mira el estado de cuenta**: si la cantidad que ves en la aplicación coincide con la del
estado, elige «ya está reflejado».

### «Precio desactualizado»

Sale cuando el precio es anterior a la última sesión del mercado de ese valor. Las causas normales son dos:
el mercado está cerrado y el precio es de la sesión anterior —lo normal—, o el trabajo diario de precios no
ha podido traerlo.

Si aparece en algo que cotiza en bolsa y el mercado lleva días abierto, **mira la pantalla de Precios o
espera al siguiente cierre**. Los fondos tardan uno o dos días en publicar su valor, y eso no es un fallo.

---

## 4. Lo que la aplicación NO hace

Conviene saberlo, para no pedirle lo que no tiene:

- **No lleva la contabilidad de las compras y ventas.** Se le dicen las posiciones que tienes, no lo que
  hiciste para llegar ahí.
- **No hace la declaración de impuestos** ni calcula el coste ajustado fiscal. Para eso están los estados
  del bróker.
- **No opera.** No compra ni vende nada: solo enseña lo que tienes.
- **No da consejos.** No dice si algo está caro o barato.

---

## 5. Cuando algo no cuadra

**Antes de tocar nada, apunta qué ves**: la cartera, el valor, la cifra que esperabas y cuándo.

Y después, en este orden:

1. **Mira la fecha del precio.** Si es viejo, no hay nada roto: falta actualizarlo.
2. **Mira si pone «Total parcial».** Entonces el total no suma todo, y la diferencia puede ser justo eso.
3. **Compara la CANTIDAD con el estado de cuenta.** Si la cantidad coincide, el problema está en el precio;
   si no coincide, está en los datos.
4. **Si es una cantidad**, mira si hubo un desdoblamiento o una venta que no se ha reflejado.

Y si hay que cambiar datos: **mejor preguntar antes al administrador**, porque cada cambio queda anotado con
nombre y fecha, y deshacerlo también hay que hacerlo a mano.

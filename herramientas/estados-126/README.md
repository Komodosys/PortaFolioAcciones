# Reconstrucción de la cuenta RRSP ···126

Extrae los movimientos de los **73 estados de cuenta** de una cuenta de jubilación de Questrade
(septiembre de 2020 a septiembre de 2026, en francés) y los deja en un fichero que se puede leer y
discutir antes de meter nada en la base de datos.

## Cómo se ejecuta

```powershell
# 1. Leer los 73 PDF y volcar los movimientos crudos.
node extraer.mjs "C:\ruta\a\los\estados" salida\estados.json

# 2. Cuadrar contra los totales que imprimen los propios estados. Sale con codigo 1 si algo no cuadra.
node cuadrar.mjs salida\estados.json "C:\ruta\a\los\estados"

# 3. Ponerlo en claro: resumen por concepto, lista completa y CSV.
node resumir.mjs salida\estados.json salida

# 4. Calcular el plan de carga, sin escribir nada. Predice efectivo, cantidades y ganancia realizada.
node planificar.mjs ..\..\DOCUMENTACION\movimientos.csv tipos.csv

# 5. Cargar en la aplicacion. --simulacro ensena lo que haria; --ejecutar lo hace.
node cargar.mjs --simulacro
node cargar.mjs --ejecutar

# 6. Comprobar contra la API que lo cargado coincide con lo previsto, partida por partida.
node comprobar-carga.mjs
```

`diagnostico-pagina.mjs <pdf> <pagina…>` vuelca los fragmentos de una página con sus coordenadas. Es
la herramienta con la que se descubre si el formato ha cambiado de un año a otro.

**El paso 5 necesita un preparativo que no tiene endpoint**: las posiciones que haya hay que borrarlas antes,
porque se rehacen desde las compras. No hay forma de borrar una posición por la API (solo cerrarla, que
conserva los lotes y los seguiría contando), así que va con SQL y copia de seguridad hecha. Es el único paso
que se salta el libro de auditoría, y por eso está escrito aquí.

La salida (`salida/`) **no se publica**: lleva el número de cuenta y todos los importes. Está en el
`.gitignore` y se regenera volviendo a ejecutar lo de arriba.

## Por qué se usan coordenadas y no el texto seguido

Cuatro cosas del formato rompen cualquier lectura línea a línea, y las cuatro se comprobaron en los
estados de verdad:

- **La descripción de una operación ocupa varias líneas y puede empezar por encima de la línea
  principal.** El dividendo de MSFT de diciembre de 2025 y el de MGK de octubre de 2021 son así. Por
  líneas, una operación se parte en tres.
- **El tipo también se parte**: «Opérations Sur» / «Devises» es un solo concepto, escrito en dos
  líneas, y por eso el tipo hay que recomponerlo antes de interpretarlo.
- **La cantidad puede ser entera o decimal.** La primera compra de la cuenta es de 49 unidades y las
  de 2025 son de 2,0000. Un patrón que exija cuatro decimales falla en la primera.
- **Las columnas vacías no llevan nada, ni siquiera un guion.** En la tabla de totales, un hueco y un
  guion son igual de invisibles al pasar el PDF a texto, y seis columnas de importes se quedan en
  cuatro números sin saber a cuál pertenecen.

Lo que sí se aprovecha: las columnas de importe van alineadas a la **derecha**, cada una en su
coordenada exacta. Eso es lo que permite saber a qué columna pertenece cada número aunque la fila
esté medio vacía. Los bordes de las **nueve** columnas (Cantidad + CAD: Precio, Bruto, Comisión, Neto
+ USD: los mismos cuatro) **se deducen de la cabecera de cada página**, no se escriben a fuego: si un
año cambia la maquetación, el programa se para en vez de asignar importes a la columna equivocada.

## Las dos secciones que se confunden a propósito

- **04 ACTIVITÉS** es la tabla de lo que pasó en el mes. Es la buena.
- **05 OPÉRATIONS RÉGLÉES APRÈS LA FIN DU MOIS** tiene **otra** maquetación y repite lo que el estado
  del mes siguiente lista como actividad normal. Se extrae aparte y marcada, y **no se suma**. No es
  una precaución teórica: las **9 de las 9** operaciones sin liquidar de esta cuenta reaparecen al mes
  siguiente, comprobado una a una.

## Tres comprobaciones, y las tres pasan

Una extracción no vale nada porque alguien la lea y parezca bien. Estas tres se pueden repetir:

| Qué | Resultado |
|---|---|
| **Conceptos**: cada concepto del mes —ventas, compras, dividendos, aportaciones, traspasos, conversiones, comisiones— sumado por moneda, contra la línea que imprime el estado | **73 de 73 meses**, en CAD y en USD |
| **Cadena del saldo**: saldo de cierre = saldo de apertura + todos los movimientos del mes | **73 de 73 meses**, en CAD y en USD |
| **Cantidades**: las unidades que salen de compras, ventas y desdoblamientos contra las acciones que el propio dividendo declara («CASH DIV ON N SHS») | **70 de 70 dividendos** |
| **Tenencias finales**: lo reconstruido contra la tabla de posiciones del último estado | **9 de 9 posiciones** |

Sin descuadres. Cualquier fila mal leída —un importe en la columna de al lado, una operación perdida,
una cantidad con el signo cambiado— rompe la cadena en el mes donde ocurre.

## Lo que el formato enseña y no era evidente

- **Las ventas y las compras, el estado las imprime por su importe BRUTO**, y las comisiones en su
  propia línea. Compararlas por el neto da una diferencia de 4,95 por operación que parece un
  redondeo y no lo es.
- **La línea de comisiones del estado no es la columna «Comm.» de la operación**: es bruto menos neto,
  y las dos cifras **no siempre coinciden**. En la venta de VOO de enero de 2021 la columna dice 4,95
  y la diferencia entre bruto y neto es 5,06. El céntimo de más es una tasa que el estado se guarda
  dentro del neto sin desglosarla.
- **El concepto «Frais» sale en la línea «Autres opérations»**, no en «Frais et remises». Hay una
  sola comisión de traspaso en siete años (febrero de 2022) y es la que lo demuestra.
- **Los traspasos de entrada (tipo `TF6`) no traen símbolo ni cantidad**: son dinero que entra, con
  una descripción que se repite casi literal entre meses y que no identifica ningún valor.
- **Dentro del mismo estado conviven dos formatos numéricos**: la tabla de actividades usa el formato
  francés (espacio de millares, coma decimal) y la de posiciones, el inglés. Leer una con las reglas
  de la otra da cifras cien veces mayores.
- **Hay tres desdoblamientos en la cuenta**, y el estado los declara como «STK SPLIT» con las unidades
  que **suma**: AMZN el 2022-06-08 (+19, de 1 a 20), TSLA el 2022-08-29 (+56, de 28 a 84) y MGK el
  2026-04-22 (+48, de 12 a 60).

## Lo que el modelo no representaba, y cómo se resolvió

- **La conversión de divisas** (10 conversiones, en dos apuntes cada una: CAD que salen y USD que entran).
  **No se modela.** La aplicación lleva un solo saldo en la moneda base, así que una conversión no lo mueve: el
  dinero cambia de moneda, no de dueño. Lo que cuesta no modelarla está **medido**: el efectivo calculado sale
  −1.050,39 CAD frente a los +36,41 CAD del bróker, unos 1.087 CAD en siete años. Meterlas como aportación y
  retiro habría sido mucho peor: habría sumado 21.541,86 a lo retirado y 16.324,93 a lo aportado.
- **La comisión de traspaso de 25,00 CAD viene en positivo** en el estado, y la cadena del saldo cierra sumándola:
  el efectivo **subió**. Entra como `Deposit` con la nota del estado; como `Commission` habría restado 25 donde
  había que sumarlos.
- **Los traspasos no son aportaciones.** El modelo no tenía un tipo para ellos y se añadió `TransferIn` /
  `TransferOut`: suman al efectivo y quedan fuera de «lo aportado».
- **Una venta no se puede anotar sobre una posición que ya no tiene esas acciones.** Por eso la carga rehace las
  posiciones desde las 46 compras y **aplica** los tres desdoblamientos en su fecha, en vez de marcarlos como
  «ya reflejado».

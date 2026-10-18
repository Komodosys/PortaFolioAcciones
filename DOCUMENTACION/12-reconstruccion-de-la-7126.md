# 12 · Reconstruir una cuenta desde sus estados de cuenta

Este documento existe para **retomar el trabajo en una sesión nueva** sin repetir lo andado. Si
estás leyendo esto al empezar, empieza por aquí y sigue por `TODO.md`.

---

## 1. En qué punto está el efectivo

El efectivo de las carteras está **terminado y funcionando**. Lo que hay:

| Pieza | Dónde |
|---|---|
| `MovementKind`: `Opening`, `Deposit`, `Withdrawal`, `Sale`, `Dividend`, `Commission` | `Domain/ValueObjects/MovementKind.cs` |
| Saldo calculado, nunca guardado | `Infrastructure/Persistence/Repositories/CashReader.cs` |
| Foto diaria con el efectivo del día | `PortfolioSnapshot.CashValue` |
| Informe con efectivo y «ganado» | `PerformanceReport` + `PerformanceReader` |
| Pantalla de saldo, apertura y corrección | `web/.../carteras/movimientos/` |
| Vista informativa en USD | `CashBalanceSummary.BalanceInUsd` |

**La regla que hace que cuadre**, y que costó tres intentos:

```
efectivo = apertura + aportaciones − retiros + ventas + dividendos − comisiones − TODO lo comprado
```

- **La apertura es «lo que habías puesto», no «el efectivo que había».** Si fuera solo el dinero de la
  cuenta, las posiciones que ya existían no estarían pagadas por nadie y el saldo saldría inflado.
- **Solo cuentan los movimientos posteriores a la apertura**, salvo las compras, que se cuentan todas.
- **La apertura es única.** Se corrige con `PUT /api/portfolios/{id}/opening`; declararla otra vez no suma.
- **`Ganado` es nulo** si falta la apertura o falta la foto diaria. Nulo es «no se sabe», y es mejor que un
  número falso.

### Comprobado

Caso redondo: pongo 5.000, compro 10 de un valor a 50 USD (694,10 CAD), y sale efectivo 4.305,90 y ganado
3.700,91 — igual al valor menos el coste. Y sobre la cuenta real: corregir la apertura la movió de
−37.544,91 a +17.335,80.

---

## 2. Lo que falta

| Qué | Nota |
|---|---|
| ~~Reconstruir la cuenta `RRSP ···126`~~ | **Hecho el 2026-10-18.** Ver *Cómo quedó la reconstrucción* |
| ~~El «aportado» del informe mezclaba monedas~~ | **Arreglado el 2026-10-18**, con prueba que se falsificó |
| ~~Las fotos viejas de esta cartera~~ | **Borradas el 2026-10-18** y tomada una nueva con los datos cargados |
| **El efecto divisa del coste** | La única diferencia que queda en el informe, medida: 2.982,24 CAD. Es la hoja de ruta nº 2 |
| Dividendo y comisión en la **interfaz** | Ya están en el desplegable; falta probarlos a mano |
| `TODO.md` al día | Sigue diciendo que el proyecto acaba en la Fase 8 |
| Ganancia por periodo, por instrumento, índice, CSV | Hoja de ruta de `10-estadisticas-del-administrador.md` |

---

## 3. La reconstrucción de la cuenta `RRSP ···126`

### Por qué esta cuenta

Tiene siete años, dos monedas, dividendos, comisiones y compras. Si su efvo calculado coincide con el real,
el modelo funciona para todas. **Es la prueba de fuego.**

### Los estados de cuenta

Están en `C:\Users\PC\Downloads\Quest`, un directorio por año:

```
2020   4 ficheros      2023  12      2026   9
2021  12 ficheros      2024  12
2022  12 ficheros      2025  12      TOTAL: 73 ficheros, ~53 MB
```

**Son PDF con texto real**, no imágenes escaneadas. Se leen con `pdf-parse` (Node), ya probado:

```js
import { PDFParse } from 'pdf-parse';
const parser = new PDFParse({ data: readFileSync(ruta) });
const r = await parser.getText();
```

Está instalado en `.tmp/pdf` (fuera del repositorio, no se commitea).

### Lo que ya se sabe del primero (septiembre 2020)

```
Cuenta:                RRSP ···126 (REER particulier)
Apertura:              20 de agosto de 2020
Primer estado:         septiembre 2020, 14 páginas
Saldo del mes:         20.097,19 CAD
Saldo del mes previo:  0,00
Tipo de cambio:        1 USD = 1,3048 CAD
```

**Los estados están en francés.** La sección de movimientos se llama **«Activités»**, y hay una leyenda al
final que explica cada concepto. Los conceptos de la leyenda son: intérêts, dividendes, dépôts, commissions,
dividendes réinvestis, conversion de devises, transferts, opérations stratégiques sur le capital, autres
opérations.

### El plan

1. **Extraer** de los 73 PDFs la tabla de operaciones de cada mes. Ojo: **el formato puede cambiar entre
   años**, así que no vale un patrón único a ciegas — hay que mirar uno de cada año.
2. **Casar los conceptos** con lo que el modelo sabe representar:

| Concepto del estado | Qué hacer |
|---|---|
| Dépôt | `Deposit` |
| Retrait | `Withdrawal` |
| Achat / Vente | Compra (lote) / `Sale` |
| Dividende | `Dividend` |
| Dividende réinvesti | **Dos** movimientos: `Dividend` + compra |
| Commission | `Commission` |
| Intérêts | `Dividend` con nota «intérès» |
| Conversion de devises | **No modelado**. Es la diferencia que quedará |
| Transfert | `Deposit`/`Withdrawal`, con nota. **El coste original** hay que ponerlo a mano |

3. **Enseñar la lista extraída al usuario antes de meter nada.** Es su dinero y su historia.
4. **Introducir** con la API (`POST /api/portfolios/{id}/movements`, `POST .../positions`).
5. **Comparar mes a mes**: el efectivo calculado contra el saldo de cierre de cada estado. Si se desvía,
   el mes del desvío dice qué concepto falta.

### La apertura, con una reconstrucción completa

**La primera aportación es la apertura.** La cuenta empezó vacía el 20 de agosto de 2020, así que el primer
dinero que entró es el punto de partida. El resto, `Deposit` normales.

**Ojo**: los 56.971 USD que se pusieron en una verificación **hay que quitarlos** — están puestos como
apertura y harían que los depósitos contaran doble. **Comprobado el 2026-10-18: ya no están.** La cuenta tenía
un solo movimiento:

```
Apertura   14 sep 2020   15.300,00 USD   (20.150,10 CAD al cambio del día, 1,3170)
```

### Cómo quedó la reconstrucción (hecha el 2026-10-18)

La cuenta está reconstruida de punta a punta desde los 73 estados. Lo que hay ahora:

```
153 movimientos   6 aportaciones · 6 traspasos recibidos · 1 traspaso enviado
                  70 dividendos · 34 comisiones · 16 ventas
 9 posiciones     46 compras, 3 desdoblamientos aplicados en su fecha, 2 vendidas enteras
```

Y las comprobaciones que la sostienen, todas repetibles:

| Qué | Resultado |
|---|---|
| Los 160 movimientos extraídos contra los totales de los 73 estados | **73 de 73 meses**, CAD y USD |
| La cadena del saldo de cada mes | **73 de 73** |
| Las cantidades contra las acciones que declara cada dividendo | **70 de 70** |
| Las posiciones reconstruidas contra la foto que ya había en la base | **9 de 9** |
| El efectivo calculado por la aplicación contra el que predijo el plan | **9 partidas, al céntimo** |
| La ganancia realizada | **12.345,79 USD**, exactamente la prevista |

**Las dos diferencias que quedan, y por qué:**

1. **El efectivo sale −1.050,39 CAD y el bróker dice +25,71 USD.** La diferencia, unos **1.087 CAD**, es el
   precio de la decisión de **no modelar las conversiones de divisas**: los 21.541,86 CAD cambiados a dólares
   se valoran al tipo del día de cada compra y no al que se obtuvo. Medido, no supuesto.
2. **El «ganado» del informe no es igual a «no realizada + realizada»**, y ahora se sabe por qué **al céntimo**:

   ```
   ganado                42.148,65   = total 85.831,13 − aportado 30.476,83 − traspasos 13.205,66
   no realizada + realizada  38.574,75
   diferencia             3.573,90
     + dividendos           789,53   es ingreso, y en «ganado» cuenta
     − comisiones           197,87   es un coste, y en «ganado» resta
     + efecto divisa      2.982,24   el coste está AL CAMBIO DE HOY (ADR-011) y el efectivo a los tipos de cada día
   ```

   Los dividendos y las comisiones no son un fallo: son ingresos y costes que esa comparación no incluye. La
   **diferencia de verdad es el efecto divisa de 2.982,24 CAD**, que es exactamente lo que la hoja de ruta
   número 2 («desglose del efecto divisa») viene a separar. No se arregla sumando: se presenta aparte.

**Y una decisión que hubo que cambiar sobre la marcha:** se pensaba marcar los tres desdoblamientos como «ya
reflejado». No vale: una venta no se puede anotar sobre una posición que ya no tiene esas acciones, así que
las posiciones hubo que rehacerlas desde las compras y **los desdoblamientos se aplicaron en su fecha**. Solo
tiene sentido marcar cuando se conserva la foto, y entonces no se pueden cargar las ventas.

**Lo que se saltó la auditoría, y se dice:** borrar las 8 posiciones que había fue un `DELETE` por SQL, porque
no hay endpoint para borrar una posición (solo cerrarla, que conserva los lotes y los seguiría contando). Es
el único paso sin rastro en el libro. Se hizo con copia previa verificada
(`respaldos\portafolio-2026-10-18-181215.dump`).

**Lo que se arregló después, ya con la cuenta cargada a la vista:**

- **El informe sumaba monedas.** `PerformanceReader` sumaba los importes **sin convertir**
  (`.Sum(m => m.Amount.Amount)`), al revés que el lector de efectivo: decía «aportado 25.477», que es dólares
  más dólares canadienses. Ahora convierte al tipo del día de cada movimiento, como `CashReader`, y **devuelve
  nulo si a alguno le falta el tipo** en vez de dar una suma a medias. Lo mismo con la ganancia realizada, que
  también se sumaba cruda. Ahora: **aportado 30.476,83 CAD** y **realizada 16.725,99 CAD**.
  - **Tres pruebas nuevas contra SQLite**, y **falsificadas**: se volvió a poner el fallo y fallaron dos.
  - Y un fallo de procedimiento que costó encontrar: al restaurar el fichero con `Move-Item` se conservó la
    fecha vieja, MSBuild no recompiló y la prueba seguía pasando. **Una falsificación que no recompila no
    falsifica nada.**
- **«Ganado» contaba los traspasos.** Ahora se restan, porque no son ganancia: sin eso la cifra decía que se
  había ganado todo lo que se movió de cuenta —**13.205,66 CAD de más**—. Se ve en el desglose de arriba.
- **Las dos fotos de cartera de esa cuenta eran de antes de la carga**, con las posiciones de la foto. Se
  borraron y se pidió una nueva: **un número histórico equivocado es peor que un hueco**.

### Y la otra cuenta: el broker sin historial

Para la cuenta del otro broker **no hacen falta estados de cuenta**. La apertura está hecha justo para eso:
se declara el coste de lo que hay más el efectivo, y la aplicación sigue el rastro desde ahí.

---

## 4. Cómo retomar

Abre una sesión nueva en este repositorio y pega esto:

```
Lee DOCUMENTACION/TODO.md y DOCUMENTACION/12-reconstruccion-de-la-7126.md para ponerte al día.
Vamos a extraer los estados de cuenta de C:\Users\PC\Downloads\Quest y reconstruir la cuenta
RRSP ···126. Empieza mirando un PDF de 2020 y otro de 2025 para ver si el formato cambia, y
enséñame la lista extraída antes de meter nada en la base.
```

**Lo que la sesión nueva necesita saber y encontrará sola:**

- El repositorio es el contexto: código, `TODO.md`, y los documentos `09`, `10`, `11` y este.
- `.env` tiene las credenciales; el administrador es `admin`.
- La receta de `dotnet ef` y de los respaldos está en `DOCUMENTACION/`.
- Las suites de prueba escriben en la base real: hay que limpiar después.
- **Los nombres de las personas no se escriben en el repositorio.** Son `Titular A` y `Titular B`, y las
  cuentas se enmascaran (`RRSP ···126`).

---

## 5. Estado del repositorio al escribir esto

```
Rama:      rediseno-daisyui
Commits:   52 por delante de main
CI:        en verde
Pruebas:   259 dominio + 8 arquitectura + 62 infraestructura + 51 frontend
Base:      7 carteras · 30 posiciones · 72 lotes · 137 movimientos · 26 instrumentos
Datos:     2.559 tipos de cambio USD/CAD (2016-2026) · 21.353 cierres de precios
```

**Aviso para la próxima sesión, y no es menor:** en este equipo `dotnet build` de la solución **puede fallar sin
decir por qué** (`Build FAILED` con cero errores y sin ningún mensaje). La causa comprobada es el aislamiento:
no deja a MSBuild abrir **tuberías con nombre** para sus nodos de trabajo, y con más de uno la compilación no
llega a empezar. Se resuelve con `-m:1`, que ya lleva `probar.cmd`:

```
dotnet build PortaFolioAcciones.slnx -c Debug -m:1
```

**Y si además no hay salida a Internet**, la auditoría de vulnerabilidades de NuGet no puede consultar su
listado y el aviso (`NU1900`) lo convierte en error `TreatWarningsAsErrors`. Entonces hay que añadir
`-p:NuGetAudit=false`. **Se comprobó que es solo eso**: con red, la compilación pasa con la auditoría puesta, así
que la bandera **no** se deja fija — apagaría una comprobación de dependencias que el proyecto quiere tener.


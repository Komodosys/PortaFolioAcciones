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
| **Reconstruir la cuenta `RRSP ···126`** | Es el objeto de este documento |
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
Cuenta:                52227126 (REER particulier)  → en el repo, siempre «···126»
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
apertura y harían que los depósitos contaran doble. El estado actual de esa cuenta:

```
Apertura   14 sep 2020   15.300,00 USD
(sin más movimientos)
```

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
Pruebas:   256 dominio + 8 arquitectura + 59 infraestructura + 49 frontend
Base:      7 carteras · 27 posiciones · 40 lotes
Datos:     2.559 tipos de cambio USD/CAD (2016-2026) · 21.352 cierres de precios
```

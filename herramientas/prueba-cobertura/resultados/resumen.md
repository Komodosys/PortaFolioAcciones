# Resultados de la prueba de cobertura — cartera real

> **Procedencia.** Datos del **2026-10-12**, obtenidos por inspección directa de la fuente, no
> ejecutando `Test-Cobertura.ps1`: el shell de este equipo (Windows PowerShell 5.1) no negocia TLS con
> la fuente, sí la vía usada aquí. Los valores son reales y verificables, y el formato es el mismo que
> produce la herramienta.
>
> Los instrumentos salen de **8 estados de cuenta** (7 cuentas de Questrade y TD, 2 titulares), no de
> una lista escrita a mano. Ver [`listado.csv`](../../listado.csv).

Ejecutada: 2026-10-12
Instrumentos comprobados: **19** (los 18 de los estados de cuenta, más `VEEV`, que estaba en la lista
previa pero **no aparece en ningún estado**)
Umbral de retraso aplicado: 4 días

| Veredicto | Cuántos |
|---|---|
| OK | 15 |
| OK_FONDO_NAV | 4 |
| IDENTIDAD_DUDOSA | 0 |
| NO_ENCONTRADO | 0 |
| SIN_PRECIO | 0 |
| PRECIO_RETRASADO | 0 |
| ERROR | 0 |

## Lectura

**Cobertura: 19 de 19. Cero sin encontrar, cero sin precio, cero errores, cero identidades dudosas.**

Aplicando el criterio fijado de antemano en
[../../DOCUMENTACION/08-prueba-cobertura-instrumentos.md](../../DOCUMENTACION/08-prueba-cobertura-instrumentos.md)
(0–2 sin cubrir → el MVP es casi todo automático), esta cartera cae en el tramo favorable **incluso
contando la categoría de riesgo**: los cuatro fondos mutuos canadienses están cubiertos.

**Veredicto: [ADR-004](../../DOCUMENTACION/decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md)
queda verificado. No hace falta contratar ningún plan de pago para valorar esta cartera a cierre
diario.**

## Cobertura por categoría

| Categoría | Instrumentos | Cubiertos | Nota |
|---|---|---|---|
| Acciones EE. UU. | 9 | 9 | NasdaqGS y NYSE |
| ETF EE. UU. | 2 | 2 | `VOO`, `MGK` |
| Acciones Canadá | 2 | 2 | `GIB-A.TO`, `VBNK.TO` |
| ETF Canadá | 2 | 2 | `VFV.TO`, `HXQ.TO` |
| **Fondos mutuos Canadá** | **4** | **4** | **la categoría de riesgo; resueltos uno a uno** |

## Los cuatro hallazgos que cambian el diseño

### 1. En Canadá un fondo de Fundserv NO tiene ISIN, y su código tampoco sirve para buscar

El código Fundserv (`TDB2624C`) es lo que aparece en el extracto del bróker, pero **buscarlo en la
fuente devuelve CERO resultados**. La fuente indexa los fondos canadienses por un código interno de
estilo Morningstar (`0P0001K312.TO`), y el nombre corto que devuelve es ese mismo código.

Consecuencia: **la equivalencia «código del bróker → identificador de la fuente» no se puede
automatizar. Hay que resolverla una vez por instrumento y guardarla.** Es exactamente el campo
`ProviderKey` que ya preveía [04-modelo-de-datos.md](../../DOCUMENTACION/04-modelo-de-datos.md), pero
ahora deja de ser una precaución teórica y pasa a ser un requisito de la primera versión: sin él, los
fondos no se pueden valorar.

### 2. Las series de un mismo fondo tienen nombres casi idénticos y precios muy distintos

Al buscar «TD Canadian Equity Pool» aparecen dos candidatos:

| Símbolo | Nombre en la fuente |
|---|---|
| `0P0001K312.TO` | TD Canadian Equity Pool **Series W** |
| `0P0001K313.TO` | TD Canadian Equity Pool **Class Series W** |

Elegir por parecido de nombre es una trampa: sus NAV son 22,68 y 28,01. Lo mismo ocurre con el Global
Equity Pool (`0P0001K314.TO` frente a `0P0001K315.TO`).

**Cómo se resolvió, y es el método que debe incorporar la aplicación:** comparar el NAV del candidato
con el precio que imprime el estado de cuenta.

| Código Fundserv | Símbolo elegido | NAV del 2026-01-30 | Precio en el estado | ¿Coincide? |
|---|---|---|---|---|
| TDB2560C | `0P0001K2XD.TO` | 9,180 | 9,180 | ✅ |
| TDB2624C | `0P0001K312.TO` | 19,840 | 19,840 | ✅ |
| TDB2625C | `0P0001K314.TO` | 21,360 | 21,360 | ✅ |
| TDB2626C | `0P0001TKCZ.TO` | 13,830 | 13,830 | ✅ |

Coinciden **al tercer decimal**. Es una verificación objetiva, no una impresión.

### 3. Los instrumentos se renombran

`MGK` figura en el estado como «VANGUARD MEGA CAP GROWTH ETF» y la fuente lo llama «Vanguard
Morningstar Mega Cap Growth ETF». Es el mismo instrumento, pero una comprobación de identidad
demasiado estricta lo habría marcado como dudoso. **La verificación debe avisar, no bloquear**, y debe
comparar por palabras significativas y no por cadena exacta.

### 4. Las posiciones canadienses llevan tres días «de retraso» y están perfectamente al día

Las americanas muestran el cierre de hoy, lunes 12, y las canadienses y los fondos el del viernes 9:
el 12 de octubre de 2026 es el segundo lunes de octubre, **Acción de Gracias en Canadá**, y la TSX está
cerrada. Sin calendario por bolsa la aplicación marcaría como obsoletas unas posiciones que están
correctas.

## Detalle

| Categoría | Entrada | Símbolo | Nombre que devuelve la fuente | Tipo | Moneda | Bolsa | Precio | Fecha | Ver. |
|---|---|---|---|---|---|---|---|---|---|
| Accion US | VEEV | VEEV | Veeva Systems Inc. | EQUITY | USD | NYSE | 295.85 | 2026-10-12 | OK |
| Accion US | MSFT | MSFT | Microsoft Corporation | EQUITY | USD | NasdaqGS | 541.15 | 2026-10-12 | OK |
| Accion US | META | META | Meta Platforms, Inc. | EQUITY | USD | NasdaqGS | 742.96 | 2026-10-12 | OK |
| Accion US | NFLX | NFLX | Netflix, Inc. | EQUITY | USD | NasdaqGS | 70.61 | 2026-10-12 | OK |
| Accion US | ASML | ASML | ASML Holding N.V. | EQUITY | USD | NasdaqGS | 1750.24 | 2026-10-12 | OK |
| Accion US | AMZN | AMZN | Amazon.com, Inc. | EQUITY | USD | NasdaqGS | 259.53 | 2026-10-12 | OK |
| Accion US | TSLA | TSLA | Tesla, Inc. | EQUITY | USD | NasdaqGS | 378.03 | 2026-10-12 | OK |
| Accion US | OXY | OXY | Occidental Petroleum Corporation | EQUITY | USD | NYSE | 61.13 | 2026-10-12 | OK |
| Accion US | PFE | PFE | Pfizer Inc. | EQUITY | USD | NYSE | 28.40 | 2026-10-12 | OK |
| ETF US | VOO | VOO | Vanguard S&P 500 ETF | ETF | USD | NYSEArca | 715.13 | 2026-10-12 | OK |
| ETF US | MGK | MGK | Vanguard Morningstar Mega Cap Growth ETF | ETF | USD | NYSEArca | 94.08 | 2026-10-12 | OK |
| Accion Canada | GIB.A | GIB-A.TO | CGI Inc. | EQUITY | CAD | Toronto | 100.08 | 2026-10-09 | OK |
| Accion Canada | VBNK.TO | VBNK.TO | VersaBank | EQUITY | CAD | Toronto | 31.38 | 2026-10-09 | OK |
| ETF Canada | VFV.TO | VFV.TO | Vanguard S&P 500 Index ETF | ETF | CAD | Toronto | 197.56 | 2026-10-09 | OK |
| ETF Canada | HXQ.TO | HXQ.TO | Global X NASDAQ-100 Index Corporate Class ETF | ETF | CAD | Toronto | 126.84 | 2026-10-09 | OK |
| Fondo Canada | TDB2560C | 0P0001K2XD.TO | TD Fixed Income Pool Series W | MUTUALFUND | CAD | Toronto | 8.84 | 2026-10-09 | OK_FONDO_NAV |
| Fondo Canada | TDB2624C | 0P0001K312.TO | TD Canadian Equity Pool Series W | MUTUALFUND | CAD | Toronto | 22.68 | 2026-10-09 | OK_FONDO_NAV |
| Fondo Canada | TDB2625C | 0P0001K314.TO | TD Global Equity Pool Series W | MUTUALFUND | CAD | Toronto | 24.09 | 2026-10-09 | OK_FONDO_NAV |
| Fondo Canada | TDB2626C | 0P0001TKCZ.TO | TD Tactical Pool W Series | MUTUALFUND | CAD | Toronto | 15.29 | 2026-10-09 | OK_FONDO_NAV |

## Lo que sigue sin estar cubierto por esta prueba

- **La estabilidad en el tiempo.** Esta prueba dice que la fuente cubre los instrumentos *hoy*. No dice
  que siga cubriéndolos dentro de un año. Eso se mitiga con el puerto de precios y la cadena de
  respaldo, no con más pruebas.
- **Los otros miembros de la familia.** Solo hay estados de dos personas, así que solo hay instrumentos
  de esas dos carteras. Si hay más cuentas, faltan estados.
- **Cuentas nuevas.** Si mañana se abre una cuenta con fondos de otra gestora, habrá que repetir la
  resolución a mano — el procedimiento está en
  [08-prueba-cobertura-instrumentos.md](../../DOCUMENTACION/08-prueba-cobertura-instrumentos.md).

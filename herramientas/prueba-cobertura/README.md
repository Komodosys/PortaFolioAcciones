# Prueba de cobertura de instrumentos

Comprueba, instrumento a instrumento, si la fuente de precios candidata los cubre y **con qué
calidad**. No es una prueba de "¿responde el API?": es una prueba de "¿puedo valorar mi cartera con
esto sin producir números falsos?".

- Plan y resultado del reconocimiento previo: [../../DOCUMENTACION/08-prueba-cobertura-instrumentos.md](../../DOCUMENTACION/08-prueba-cobertura-instrumentos.md)
- Decisión de proveedor que esta prueba debe cerrar: [ADR-004](../../DOCUMENTACION/decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md)

## Cómo se ejecuta

1. Edita `instrumentos.csv` y pon **tus instrumentos reales**.
2. Ejecuta:

```powershell
pwsh -File .\Test-Cobertura.ps1
```

**No necesita ninguna clave de API.**

La forma más simple, que ya se ocupa de la política de ejecución y usa PowerShell 7 si lo tienes:

```powershell
.\ejecutar.cmd
```

O directamente:

```powershell
pwsh -ExecutionPolicy Bypass -File .\Test-Cobertura.ps1
```

`-ExecutionPolicy Bypass` afecta **solo a esa ejecución**: no cambia ninguna configuración del
sistema. Ver [Solución de problemas](#solución-de-problemas).

Opciones:

```powershell
pwsh -ExecutionPolicy Bypass -File .\Test-Cobertura.ps1 -Entrada .\instrumentos.csv -Salida .\resultados -DiasRetrasoAceptable 4 -PausaMs 400
```

Deja `resultados/resultados.csv` (para tratar los datos) y `resultados/resumen.md` (para leerlo).

## Rellenar el CSV

| Columna | Qué poner |
|---|---|
| `Categoria` | Libre. Sirve para agrupar en el informe (Accion US, ETF Canada, Fondo Canada…) |
| `Simbolo` | El símbolo **si lo conoces**. Si lo dejas vacío, la prueba intenta resolverlo |
| `Isin` | El ISIN de tu extracto del bróker. **Es la forma más fiable de resolver un símbolo** |
| `NombreEsperado` | El nombre del instrumento. Se usa para resolverlo y, sobre todo, para **verificar la identidad** |
| `Mercado`, `Moneda` | Orientativos; la prueba informa de lo que dice la fuente |
| `Notas` | Libre |

Prioridad de resolución: **símbolo → ISIN → nombre**. La resolución por nombre es difusa y siempre
queda marcada para revisión manual.

> **Rellena `NombreEsperado` siempre, incluso cuando ya sepas el símbolo.** Es lo que permite detectar
> que un símbolo devuelve *otro* instrumento, que es el peor fallo posible: no rompe nada, solo
> produce cifras equivocadas de forma silenciosa.

## Cómo leer los veredictos

| Veredicto | Significa | Qué hacer |
|---|---|---|
| `OK` | Instrumento identificado, precio reciente, historial disponible | Nada |
| `OK_FONDO_NAV` | Fondo mutuo con NAV diario y sin intradía | Esperado. Confirma que va por `ValuationMode = Nav` |
| `NAV_PENDIENTE` | El último NAV todavía no está publicado | Esperado en fondos. El sistema debe usar el cierre anterior **con su fecha** |
| `PRECIO_RETRASADO` | El último cierre real es más antiguo que el umbral | Investigar: festivo, instrumento ilíquido o cobertura pobre |
| `IDENTIDAD_DUDOSA` | El nombre que devuelve la fuente no coincide con el esperado | **Revisar antes de usar.** Puede ser el instrumento equivocado |
| `NO_ENCONTRADO` | La fuente no devuelve ningún candidato | Candidato a valoración manual |
| `SIN_PRECIO` | El instrumento existe pero no trae cierres | Candidato a valoración manual |
| `ERROR` | Fallo de red o de la fuente | Reintentar; si persiste, es un dato sobre la fiabilidad |

## Qué hacer con los resultados

1. **Cuenta los `NO_ENCONTRADO` + `SIN_PRECIO` + `IDENTIDAD_DUDOSA`.** Ese número es el que decide:
   - si son 0–2, el MVP es "casi todo automático" y con valoración manual para las excepciones;
   - si son más de 5, hay que reabrir [ADR-004](../../DOCUMENTACION/decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md)
     y plantearse el plan de pago.
2. **Guarda `resultados.csv`** como evidencia: es lo que justifica la decisión de proveedor.
3. **Anota los símbolos que hayan requerido resolución por nombre o por ISIN.** Confirman que la
   búsqueda de instrumentos es una función obligatoria del MVP, no un adorno.

## Solución de problemas

### «is not digitally signed» / «cannot be loaded»

Es la **política de ejecución de PowerShell**, que bloquea los scripts sin firma digital. **No es un
problema del script ni del CSV.**

Lo que funciona:

```powershell
.\ejecutar.cmd
```

El lanzador añade `-ExecutionPolicy Bypass`, que afecta **solo a esa ejecución** y no cambia ninguna
configuración del sistema. Equivale a:

```powershell
powershell -ExecutionPolicy Bypass -File .\Test-Cobertura.ps1
```

> **Comprobado en este equipo: cambiar la política del usuario a `RemoteSigned` NO basta.** Con
> `Get-ExecutionPolicy -List` mostrando `CurrentUser = RemoteSigned`, el archivo sin firma y sin marca
> de descarga (`Get-Item -Stream *` solo muestra `:$DATA`), la invocación directa
> `.\Test-Cobertura.ps1` sigue fallando. Lo que sí funciona es `-ExecutionPolicy Bypass` **en la
> invocación**. Por eso el camino recomendado es el lanzador.

Para diagnosticar tu caso:

```powershell
Get-ExecutionPolicy -List
Get-Item .\Test-Cobertura.ps1 -Stream *     # ¿hay un flujo Zone.Identifier?
```

Si aparecieran `MachinePolicy` o `UserPolicy` con algún valor, la política la impone el administrador
del equipo y `-ExecutionPolicy Bypass` **no** la saltará. En este equipo esos dos ámbitos están sin
definir. Si el problema persistiera, `Unblock-File .\Test-Cobertura.ps1` cubre el caso de un archivo
marcado como descargado.

### El script dice que no puede conectar con la fuente

Es el handshake TLS. Windows PowerShell 5.1 (edición Desktop) usa .NET Framework y falla con esta
fuente aunque la conexión TCP funcione. Solución: instalarlo y usar PowerShell 7.

```powershell
winget install --id Microsoft.PowerShell
pwsh -ExecutionPolicy Bypass -File .\Test-Cobertura.ps1
```

Antes de empezar, el script hace **una petición de prueba**: si la conexión falla, lo dice una vez
con el diagnóstico y no genera un informe lleno de errores que parecerían falta de cobertura.

### Acentos raros en los informes

El script se guarda en UTF-8 **con marca de orden de bytes** a propósito, para que Windows PowerShell
5.1 lo lea correctamente. Si aun así ves caracteres raros, ejecútalo con PowerShell 7.

## Limitaciones de esta prueba (a propósito)

- Comprueba **una sola fuente**: la candidata gratuita para Canadá. No compara con EODHD porque
  requiere contratarla.
- No comprueba **tipos de cambio**: eso va por Frankfurter y ya está verificado.
- No mide la **estabilidad en el tiempo** (si la fuente se cae o cambia mañana). Eso no se puede
  probar en una tarde; se mitiga con el puerto de precios y la cadena de respaldo.
- La verificación de identidad es **heurística**, por parecido de nombres. Una coincidencia alta no
  garantiza el instrumento correcto: solo descarta los casos evidentes.

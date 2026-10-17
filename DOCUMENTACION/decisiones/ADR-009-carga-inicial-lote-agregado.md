# ADR-009 — Carga inicial con un lote agregado y fecha declarada

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

## Contexto

El [ADR-008](ADR-008-lotes-por-compra.md) decidió modelar **lotes** (una fila por compra) porque la
conversión a CAD se hace lote a lote y eso da un coste exacto. Al llevarlo al caso real apareció el
problema:

- **VEEV**: 23 acciones a un precio medio de 165,5421 USD (3.807,47 USD), compradas en **tres lotes**.
  Y no aparece en ninguno de los 8 estados de cuenta disponibles.
- **La mayoría de las posiciones no tienen historial recuperable.** La sección de actividad de los
  estados cubre solo las operaciones del periodo del estado.

Es decir: exigir lotes reales es exigir un dato que **en su mayor parte no existe**.

### El número que decide

Con el coste fijo (3.807,47 USD), cambiar **la fecha** mueve el coste en CAD así:

| Fecha usada | USD/CAD | Coste en CAD |
|---|---|---|
| Mínimo 2023–2026 (jul-2023) | 1,3216 | 5.031,95 |
| Fecha del estado (ene-2026) | 1,3773 | 5.244,03 |
| Hoy (oct-2026) | 1,4245 | 5.423,74 |
| Máximo 2023–2026 (ene-2025) | 1,4389 | 5.478,57 |

**La elección de fecha mueve el coste hasta 446,62 CAD en una sola posición: un 8,9 %.** Y si solo se
conoce el año, la incertidumbre baja a un 3,8–6,1 %.

Conclusión: **lo que importa no es cuántos lotes se guarden, sino si se sabe la fecha.** Guardar un lote
o tres exige fechas en ambos casos; guardar uno exige *una*.

## Opciones consideradas

1. **Exigir lotes reales** (lectura literal del ADR-008). Conversión exacta. Requiere datos que no
   existen para la mayoría de posiciones, y por tanto bloquea la carga inicial.
2. **Un lote agregado por posición**: cantidad total, precio medio y una fecha declarada.
3. **Sin fecha**: no guardar fecha alguna y mostrar solo el rendimiento en la moneda del activo.

## Decisión

**Opción 2.**

**Refinamiento (2026-10-12, con los estados de todo el año).** Disponiendo de los estados de **enero a
agosto**, la política se aplica **solo a la parte anterior al periodo**. Las compras de 2026 se cargan
como **lotes reales con su fecha exacta** —están en la sección de actividad— y únicamente queda como
lote agregado sin fecha lo comprado antes. En la cartera real, de **30 lotes reconstruidos, 15 tienen
fecha exacta y 15 no**. Es decir: esta decisión ya no describe la carga completa, solo el resto que los
estados no alcanzan.

Y una aclaración importante: **esto no revierte el ADR-008.** El esquema sigue siendo `Position` +
`Lots`; lo que cambia es **la política de carga**: se crea **un único lote agregado** por posición. No
hay que migrar nada cuando se quieran lotes reales: se parte el lote agregado.

Reglas:

- El lote agregado lleva **cantidad total**, **precio medio** (que es lo que publica el bróker) y una
  **fecha obligatoria**.
- La fecha lleva una **marca de exactitud**, que la aplicación debe mostrar:
  - `Exacta` — viene de un registro del bróker o del propio usuario (ejemplo: NFLX, 2026-01-20);
  - `Aproximada` — el usuario la recuerda con holgura («fue en 2024»);
  - `Referencia` — se usa la fecha del estado de cuenta como **base declarada**, no como verdad
    histórica.
- **La aplicación debe marcar visiblemente** las posiciones cuyo coste en CAD se calculó con una fecha
  no exacta. Convertir a un tipo de referencia es legítimo; hacerlo sin decirlo, no.

### Consecuencia que simplifica mucho: el rendimiento en la moneda del activo no necesita fecha

Con solo el precio medio se puede calcular **exactamente** el rendimiento en la moneda del instrumento
(la posición de NFLX lo demuestra: −20,66 % en USD frente a −18,41 % en CAD). Lo que necesita fecha es
únicamente **el rendimiento en CAD**.

Por tanto la aplicación mostrará **dos** cifras, y no una:

| Cifra | Necesita fecha | Exactitud |
|---|---|---|
| Rendimiento en la moneda del activo | **No** | Siempre exacta |
| Rendimiento en CAD | Sí | Exacta si la fecha es exacta; aproximada y **marcada** si no |

Y el **desglose del efecto divisa** solo tiene sentido cuando la fecha es exacta.

**Cuánto aportan realmente los lotes de 2026 (medido el 2026-10-12).** Comparando «todo colapsado en
una fecha de referencia» frente a «lotes de 2026 con su fecha real», sobre cuatro posiciones reales de
la cartera:

| Posición | Diferencia |
|---|---|
| MSFT RRSP-2 | −0,50 % |
| MGK RRSP-2 | −0,63 % |
| VEEV RRSP-2 | +0,79 % |
| ASML RRSP-1 | −0,19 % |

**Menos de un 1 % en todos los casos**, porque el USD/CAD solo se movió un 3,4 % en todo 2026 y las
compras datadas están agrupadas en pocos meses. En cambio, **la fecha del lote anterior a 2026 pesa
hasta un 8,9 %** de ese lote, porque abarca 2023–2025 (de 1,3216 a 1,4389).

Conclusión: el esfuerzo debe ir a **la fecha de los 15 lotes anteriores**, no al detalle de los 15
datados. Los datados se conservan porque no cuestan nada —ya están extraídos y validados— y son
invisibles en la interfaz: la aplicación muestra posiciones con su coste medio, no lotes.

## Consecuencias

- **Positivas:** la carga inicial es viable con los datos que existen; el esquema no cambia y admite
  lotes reales sin migrar; se obtiene una cifra exacta (el rendimiento local) desde el primer día; y la
  aproximación queda visible en lugar de silenciosa.
- **Negativas / coste asumido:** el **rendimiento en CAD es aproximado** para las posiciones cuya fecha
  no sea exacta, y el efecto divisa no se puede desglosar en ellas. Con un 8,9 % de horquilla en el
  peor caso, esto puede mover varios miles de dólares en el consolidado. Es el precio de no tener las
  fechas, no de esta decisión.
- **Camino para eliminar la aproximación, que hay que explorar:** si el bróker permite **exportar el
  historial de operaciones** (CSV de actividades), ese fichero daría los lotes reales con fechas de
  varios años atrás y esta política dejaría de ser necesaria para esas posiciones. **Comprobar con el
  bróker es la acción de más valor pendiente.**
- **Condición de revisión:** si al cargar las posiciones resulta que la mayoría de las fechas son
  exactas, esta política pierde importancia; si resulta que ninguna lo es, habrá que decidir si el
  rendimiento en CAD se muestra como aproximado permanente o se sustituye por el rendimiento local como
  cifra principal.

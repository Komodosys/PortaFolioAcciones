# ADR-010 — Se trabaja con los estados disponibles

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

## Contexto

El 2026-10-12 el usuario cerró la vía de conseguir más datos: **no habrá estados de cuenta anteriores a
2026** ni de las **tres cuentas** que faltan (dos de Titular B en Questrade —`RRSP-3` y `TFSA-3`— y una
en TD —`TD-1`—). Solo se dispone de:

- **8 estados sueltos** (enero y mayo, mezcla de cuentas), y
- **32 estados de 2026** (enero a agosto) de **4 de las 7 cuentas**.

Con eso, las posiciones de las otras tres cuentas quedan a su fecha de estado, y la parte comprada antes
de 2026 no tiene fecha recuperable de ninguna fuente documental.

## Decisión

**Trabajar con lo que hay.** No se persigue más documentación. En concreto:

- Los **15 lotes anteriores a 2026** se cargan con su **coste exacto** (que sí está calculado) y con una
  **fecha declarada por el usuario** si la recuerda, o una **fecha de referencia marcada** si no.
- Las **10 posiciones de las tres cuentas sin estados nuevos** se cargan tal como están, **con su fecha
  de estado visible**, y el usuario puede corregir cantidades a mano cuando lo necesite.
- La aplicación **muestra siempre de cuándo es cada dato** (ya era un requisito) y **marca** las
  posiciones cuyo coste en CAD se convirtió con una fecha no exacta.

## Consecuencias

- **Coste cuantificado de no tener las fechas: hasta 5.435 CAD.** Los 8 lotes anteriores sin fecha y en
  USD suman **46.337,15 USD** de coste; convertidos a CAD van de **61.239,18 a 66.674,53 CAD** según la
  fecha que se use (el USD/CAD se movió de 1,3216 a 1,4389 entre 2023 y 2025). Es decir, un **8,9 % de
  incertidumbre** sobre esa porción del coste consolidado. **Declarar solo el año la reduce a la mitad,
  unos 2.718 CAD**: pasar de «no sé» a «fue en 2024» ya vale la pena, y cuesta un minuto.
  Si el usuario no recuerda ninguna fecha, la incertidumbre se acepta entera y queda marcada.
- **Las 10 posiciones de las tres cuentas pueden estar desactualizadas.** Su cantidad es la del estado
  (enero o mayo de 2026) y puede no reflejar operaciones posteriores. La aplicación debe mostrar la
  fecha de estado y permitir corregir, y **no debe presentarlas como si fueran de hoy**.
- **Refuerza el [ADR-009](ADR-009-carga-inicial-lote-agregado.md):** si una parte del coste en CAD va a
  ser aproximada de forma permanente, la cifra **fiable** es el **rendimiento en la moneda del activo**
  —que es exacto y no necesita fechas— y así debe presentarse. El rendimiento en CAD se muestra
  marcado cuando corresponde, no como si fuera exacto.
- **No es un defecto, es un alcance elegido.** La alternativa —perseguir estados antiguos— tiene un
  coste en tiempo que el usuario ha decidido no pagar, y el sistema queda diseñado para no producir
  números falsos por ello.
- **Condición de revisión:** si en algún momento aparece una **exportación del historial de operaciones**
  del bróker (distinta de los estados), esos lotes podrían fecharse sin migrar nada: basta con declarar
  la fecha del lote agregado.

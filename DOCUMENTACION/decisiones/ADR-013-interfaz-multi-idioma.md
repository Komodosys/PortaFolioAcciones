# ADR-013 — Interfaz multi-idioma (español, francés e inglés)

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

## Contexto

La priorización inicial daba por hecho que la interfaz sería **solo en español**. Al cerrar las preguntas
abiertas, el usuario lo corrigió:

> «NO, LA INTERFAZ SERÁ ESPAÑOL, FRANCÉS O INGLÉS. EL USUARIO PODRÁ CAMBIARLA EN SUS PREFERENCIAS»

La familia vive en Quebec, así que el francés no es un adorno. Y el cambio se hace **en caliente**, desde
las preferencias, sin recompilar ni cambiar de dirección.

## Decisión

**Interfaz multi-idioma con preferencia por usuario, resuelta en tiempo de ejecución.**

Reglas:

- **El idioma es una preferencia del usuario** (`User.PreferredLocale`), no de la instalación.
- **La API no devuelve texto para el usuario.** Devuelve **códigos** y datos; el texto lo pone el frontend.
  Esto incluye los errores de validación y los mensajes de error.
- **Los números y las fechas viajan siempre en crudo.** La API manda `1234.56` y `"2026-10-12"`; el
  formateo lo hace el frontend según el idioma. **Nunca** se envían cantidades ya formateadas.
- La **moneda viaja como código ISO** junto al importe. El frontend decide cómo se presenta; **nunca
  reinterpreta el valor**.
- Los textos de la interfaz se mantienen en un fichero por idioma, con **inglés como respaldo** para lo
  que falte traducido.

## Consecuencias

- **Frontend:** hace falta una librería de traducción **en tiempo de ejecución**. La i18n integrada de
  Angular es **de compilación** (una compilación por idioma), así que **no sirve** para cambiar el idioma
  desde las preferencias. Se usará una librería de runtime (Transloco, o equivalente).
- **Backend:** el dominio y la aplicación **no contienen texto para el usuario**, solo códigos. Es lo
  coherente con la regla de dependencias, y además evita que la lógica de negocio dependa del idioma.
- **Validación:** `DataAnnotations` sigue valiendo como **primer filtro** técnico, pero el mensaje que ve
  la persona **no sale de ahí**: sale del código de error, traducido por el frontend. Es la misma decisión
  de validación que ya se había aceptado, ahora con una consecuencia añadida.
- **Riesgo que esto evita, y que ya nos mordió una vez:** en los estados de cuenta, el mismo número se
  escribe `1,234.56` en inglés y `1 234,56` en francés. Si en algún punto el sistema **reinterpretara** un
  número formateado, volveríamos a tener ese problema. Por eso la regla de «crudo por la API» es
  obligatoria y no una preferencia de estilo.
- **Coste real, que conviene asumir con los ojos abiertos:** cada texto de la interfaz hay que mantenerlo
  **en tres idiomas**. En un proyecto personal es trabajo real. El respaldo en inglés evita que una
  traducción incompleta rompa la aplicación, pero no traduce sola. Recomendación: montar la
  infraestructura desde el primer día —es barato— y **rellenar los textos por bloques**, empezando por
  los que usa la familia.
- **Lo que NO se traduce:** los nombres de los instrumentos (vienen así de la fuente), los identificadores
  técnicos, los mensajes de los registros de diagnóstico, ni los documentos de `DOCUMENTACION/`.
- **Datos por idioma:** el idioma del **contenido** generado (por ejemplo, exportaciones) sigue la
  preferencia del usuario que lo pide, no la del que creó el dato.

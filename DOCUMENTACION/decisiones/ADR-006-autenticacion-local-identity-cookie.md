# ADR-006 — Autenticación local con ASP.NET Core Identity y cookie

- **Estado:** **Aceptado**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

## Contexto

2–5 personas de la misma familia, cada una con su portafolio aislado y sin compartir por defecto. La
restricción del proyecto pide dimensionar autenticación y aislamiento para ese contexto, sin
complejidad empresarial.

El despliegue es **local primero**, con acceso por la red doméstica, y alojamiento contratado más
adelante.

## Opciones consideradas

1. **ASP.NET Core Identity local con autenticación por cookie**, sin registro público: las cuentas las
   crea un administrador.
2. **Login externo (Google/Microsoft).** Menos contraseñas que recordar, pero exige registrar una
   aplicación, declarar URLs de redirección y custodiar secretos. Con acceso por IP de red local los
   flujos OAuth son incómodos, porque el proveedor espera URLs registradas y estables.
3. **Ambos:** cuentas locales ahora y login externo cuando haya dominio público.

## Decisión

**Opción 1: Identity local con cookie, sin registro público.**

Motivos:

- Es una aplicación **privada**: no hay usuarios por captar, y el registro abierto solo añade
  superficie de ataque sin aportar nada.
- El login externo resuelve el problema de recordar contraseñas, que **con cinco personas no es un
  problema**, y a cambio introduce dependencia de un tercero y de conectividad a Internet para poder
  entrar en una aplicación que vive en casa.
- El aislamiento se apoya en el `UserId` del **contexto autenticado**, nunca en un parámetro de la
  URL ni en un valor por defecto del cliente.

Alcance asociado, decidido aquí para no reabrirlo después:

- **Sin registro público** (cuentas creadas por el administrador).
- **Sin confirmación por correo** en el MVP: no hay servidor de correo, y añadirlo solo para
  verificar direcciones de cinco personas conocidas no aporta.
- **Sin 2FA** en el MVP. TOTP queda para v0.2, y pasa a ser recomendable en cuanto la aplicación se
  exponga a Internet.

### Restablecimiento de contraseña — confirmado el 2026-10-12

El usuario eligió la opción que este ADR ya anticipaba: **el administrador genera una contraseña temporal
y la comunica en persona.** Sin servidor de correo. Requisitos que se fijan aquí, porque son los que
hacen que una contraseña temporal sea temporal y no una contraseña permanente que el administrador
conoce:

- La contraseña temporal **se muestra una sola vez**, en el momento de generarla, y se guarda **cifrada**.
  El administrador **no puede volver a verla** después.
- **Caduca** (48 horas). Si no se usa, deja de servir.
- **Obliga a cambiarla en el primer inicio de sesión.** Sin esto, el administrador seguiría conociendo la
  contraseña de todo el mundo de forma indefinida, que es justo lo que una temporal evita.
- El restablecimiento **queda auditado** ([ADR-014](ADR-014-administrador-escritura-con-auditoria.md)):
  es una escritura privilegiada sobre la cuenta de otra persona. Se registra **el hecho**, nunca la
  contraseña.
- Al restablecerse una contraseña, **se invalidan las sesiones abiertas** de esa persona.

## Consecuencias

- **Positivas:** pocas piezas; sin secretos externos que custodiar; funciona sin Internet; coherente
  con el despliegue local actual; el aislamiento queda en un punto único y fácil de probar.
- **Negativas / coste asumido:**
  - Hay que **custodiar contraseñas**. Identity ya aplica hashing y política de complejidad; lo que se
    asume es el soporte al usuario cuando alguien la olvide.
  - Por tanto hay que construir una funcionalidad mínima de **restablecimiento de contraseña por el
    administrador** (sin correo: el administrador genera una temporal y la comunica en persona).
- **Refuerzos obligatorios, no opcionales:**
  - cookie `HttpOnly`, `Secure` y `SameSite`;
  - protección CSRF en las peticiones que modifican estado;
  - bloqueo temporal tras varios intentos fallidos;
  - HTTPS en cuanto la aplicación salga de la red local (ver [ADR-... pendiente] y la sección
    *Despliegue* de [03-arquitectura-y-capas.md](../03-arquitectura-y-capas.md)).
- **Aislamiento entre usuarios — dos barreras, ambas obligatorias:**
  1. un **filtro global de consulta** en EF Core sobre el usuario actual, que evita olvidos;
  2. una **comprobación explícita** al inicio de cada caso de uso, que evita que un filtro mal
     configurado se convierta en una fuga silenciosa.
  Un test de integración debe verificar que el usuario B recibe **404** (no 403) al pedir el
  portafolio del usuario A: un 403 ya confirmaría que el recurso existe.
- **Condición de revisión:** al contratar alojamiento con dominio público, revisar si conviene añadir
  login externo y 2FA (preguntas D2 y D3 de [06-preguntas-abiertas.md](../06-preguntas-abiertas.md)).

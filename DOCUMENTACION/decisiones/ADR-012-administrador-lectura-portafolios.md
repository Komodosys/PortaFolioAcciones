# ADR-012 — El administrador puede ver los portafolios de los demás

- **Estado:** **Sustituido por [ADR-014](ADR-014-administrador-escritura-con-auditoria.md)**

> El alcance de «solo lectura» que fijaba este ADR era **mi interpretación** de la respuesta del usuario
> («sí, debe haber un rol administrador»), no una decisión suya. La corrigió el mismo día: el
> administrador **también escribe**, y cada cambio sobre datos ajenos queda auditado. Se conserva este
> documento porque registra el aislamiento original y el cambio de criterio.
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

## Contexto

El [ADR-006](ADR-006-autenticacion-local-identity-cookie.md) estableció que cada persona tiene su
portafolio **aislado, sin compartir por defecto**, con dos barreras: un filtro global de consulta y una
comprobación explícita en cada caso de uso.

Al cerrar las preguntas abiertas, el usuario respondió a la pregunta **D4** («¿alguien debería poder ver
el portafolio de otro, aunque sea en solo lectura?»):

> «SÍ, DEBE HABER UN ROL ADMINISTRADOR»

Es decir: además de las cuentas familiares, existe un **rol de administrador**, y ese rol **puede ver**
los portafolios de los demás en **solo lectura**.

## Decisión

**El administrador puede leer todos los portafolios; no puede editarlos.**

- La **lectura** de cualquier portafolio está permitida al administrador.
- La **escritura** sigue siendo exclusiva del propietario: el administrador **no** puede añadir, editar
  ni borrar posiciones, lotes o precios manuales de otra persona.
- La interfaz debe **decir en todo momento de quién** es el portafolio que se está viendo, para que nadie
  actúe sobre el equivocado.
- No se añaden permisos por usuario ni tabla de compartición: en el MVP el acceso del administrador es
  **global y de solo lectura**.

## Consecuencias

- **El modelo de aislamiento cambia**: pasa de «nadie ve nada» a **«el propietario, y el administrador en
  solo lectura»**. Es un cambio respecto al ADR-006 y queda registrado aquí.
- **Las dos barreras se adaptan**, no se eliminan:
  - el **filtro global de consulta** debe permitir al administrador ver todo, y seguir ocultando el resto
    a los usuarios normales;
  - la **comprobación explícita** en cada caso de uso debe distinguir lectura de escritura: el
    administrador pasa la primera y **no** la segunda.
- **El test de aislamiento cambia de forma**: el usuario B sigue recibiendo **404** al pedir el portafolio
  del usuario A; el administrador recibe **200**. Y hay que añadir un test de que el administrador recibe
  **403** si intenta modificar algo ajeno.
- **Consideración de confianza, que conviene decir en voz alta:** el administrador ya podía restablecer
  contraseñas ([ADR-006](ADR-006-autenticacion-local-identity-cookie.md)); con esto, además, puede leer
  todo. Es decir, **el administrador tiene acceso efectivo a toda la información de la familia.** Con 2–5
  personas que conviven es una decisión razonable, pero es una decisión consciente y no un descuido.
- **Sin registro de auditoría en el MVP.** No se guarda quién consultó qué. Con cinco personas conocidas
  no aporta y añade una tabla y una escritura por lectura. Queda como posible mejora.
- **Condición de revisión:** si se quisiera que el acceso fuese por portafolio y no global (por ejemplo,
  que el administrador solo vea el de una persona concreta), haría falta una tabla de permisos. Es un
  cambio de modelo, no un ajuste, y conviene tomarlo antes de la Fase 2 si se prevé.

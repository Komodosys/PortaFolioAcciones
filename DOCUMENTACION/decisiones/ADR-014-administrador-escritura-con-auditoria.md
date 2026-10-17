# ADR-014 — El administrador puede modificar datos ajenos, y todo cambio queda auditado

- **Estado:** **Aceptado** — **sustituye al [ADR-012](ADR-012-administrador-lectura-portafolios.md)**
- **Fecha:** 2026-10-12
- **Decide:** usuario (proyecto personal/familiar)

## Contexto

El [ADR-012](ADR-012-administrador-lectura-portafolios.md) fijó que el administrador podía ver los
portafolios de los demás **en solo lectura**. Esa lectura era **mi interpretación** de la respuesta del
usuario («sí, debe haber un rol administrador»), no una decisión suya explícita.

El usuario la corrigió el mismo día:

> «También quiero que el rol administrador modifique datos de otros usuarios, pero que se quede
> journalizado esos cambios.»

Es decir: **el administrador también escribe, y cada escritura queda registrada.**

Esto convierte al administrador en alguien con poder real sobre los datos de toda la familia —puede leer,
puede modificar y puede restablecer contraseñas—, y ese poder necesita una contrapartida.

## Decisión

**El administrador puede leer y modificar los datos de cualquier usuario, y toda modificación que haga
sobre datos ajenos queda registrada de forma permanente.**

En concreto:

1. **Lectura y escritura** sobre portafolios, posiciones, lotes y precios manuales de cualquier persona.
2. **Todo cambio estructural o de datos que un administrador haga sobre datos que no son suyos se
   registra** en un libro de auditoría, con:
   - **quién** lo hizo (el usuario administrador),
   - **cuándo** (marca de tiempo UTC),
   - **qué** entidad y cuál era su propietario,
   - **qué cambió**: el valor **antes** y **después** de cada campo afectado,
   - y un **motivo** opcional que el administrador puede escribir.
3. **El libro es de solo añadir.** La aplicación no ofrece forma de editar ni borrar sus entradas, y
   además una **regla en PostgreSQL rechaza `UPDATE` y `DELETE`** sobre esa tabla. No basta con no
   programar el borrado: si la tabla permite borrarse, el registro no vale como registro.
4. **El administrador tiene una pantalla para consultarlo**, filtrable por persona y por fecha.

**Cómo se implementa, y esto forma parte de la decisión:** el registro lo escribe un **interceptor de
EF Core** en el mismo `SaveChanges` que la propia modificación. **No** se añade una llamada manual en cada
caso de uso: una auditoría que depende de que alguien se acuerde de registrarla no es una auditoría,
porque el día que se olvide no habrá forma de saberlo.

## Consecuencias

- **Positivas:**
  - El administrador puede **corregir un error ajeno** —un dato mal tecleado, una cantidad equivocada—
    sin tener que pedirle la contraseña a nadie.
  - Y hay **rastro de quién lo hizo y qué había antes**, que es lo que hace aceptable ese poder.
- **Negativas / coste asumido:**
  - **El administrador pasa a tener poder efectivo sobre todo:** lectura, escritura y restablecimiento de
    contraseñas. Con 2–5 personas que conviven es razonable, pero es una decisión consciente y no un
    efecto colateral. **La auditoría es la contrapartida, no un adorno.**
  - **La interfaz debe decir siempre en qué cuenta y de quién** se está trabajando. Con permiso de
    escritura, editar el portafolio equivocado deja de ser una molestia y pasa a ser un dato corrupto.
  - Aparece una pantalla y una tabla nuevas que mantener.
- **Trampa que hay que resolver ahora, no después:** el registro de auditoría **no puede tener una clave
  foránea con borrado en cascada hacia la entidad auditada**. Si la posición se borra y el borrado arrastra
  el registro, se pierde justo la prueba de que existió. El libro guarda **tipo e identificador como
  referencia lógica, sin cascada**.
- **Alcance deliberado en el MVP:** se auditan las **escrituras**, no las lecturas. El usuario pidió
  «esos cambios». Auditar cada consulta multiplicaría las filas sin que nadie las lea; se puede añadir
  después sin tocar el modelo.
- **Retención:** se conserva todo, para siempre. Son unas pocas filas al año y ocupan nada.
- **Condición de revisión:** si en algún momento se quiere auditar también la lectura, o que el
  administrador solo pueda escribir en unas cuentas concretas, habría que añadir una tabla de permisos.
  Es un cambio de modelo, y conviene decidirlo antes de la Fase 2.

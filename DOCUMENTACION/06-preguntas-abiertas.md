# 06 — Preguntas abiertas antes de implementar

Cada pregunta lleva: **por qué importa**, **opciones** y **recomendación tentativa**. La columna
*Prioridad* indica si bloquea el arranque del MVP o puede resolverse durante el desarrollo.

---

## Respuestas registradas (2026-10-12)

| Pregunta | Respuesta | Efecto en el diseño |
|---|---|---|
| **C1** — moneda base | **CAD (dólar canadiense)** | Resuelve la duda de FX y la simplifica mucho: el par que importa es **USD/CAD**, disponible gratis y con histórico completo en [Frankfurter](https://frankfurter.dev/). Las posiciones canadienses no necesitan conversión. Redondeo a 2 decimales, sin matices exóticos |
| **B3** — fondos concretos | **Resuelto el 2026-10-12:** los 4 fondos TD localizados en la fuente y verificados por NAV. **Cobertura de la cartera real: 19 de 19** | Comprobado que en Canadá estos fondos **no tienen ISIN** y su código Fundserv no se indexa: la equivalencia con la fuente se resuelve a mano una vez por instrumento. Ver [08](08-prueba-cobertura-instrumentos.md) |
| **B1** — mercados | **EE. UU. y Canadá** | Abre un conflicto real con el presupuesto cero: los planes gratuitos son casi todos solo de EE. UU. → [ADR-004](decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md) |
| **B2** — presupuesto de datos | **0** | Determina [ADR-002](decisiones/ADR-002-estrategia-de-precios-cierre-diario.md): el intradía no cabe en ningún plan gratuito, el cierre diario sí |
| **Proveedor para Canadá** | **Opción A** — fuente gratuita para EE. UU. + Yahoo no oficial para Canadá | Registrado como riesgo aceptado en [ADR-004](decisiones/ADR-004-cobertura-eeuu-canada-con-presupuesto-cero.md): con base CAD, la fuente sin contrato cubre la mayor parte de la cartera |
| **Volumen de la cartera** | **20–60 posiciones** | Confirma que el cierre diario cabe en cualquier plan gratuito (~60 peticiones/día) y que la valoración manual es viable solo como excepción |
| **A1** — renta fija | Solo **fondos y ETF** de renta fija | Elimina del modelo los bonos individuales, el cupón corrido y el precio en % del nominal. El modelo se simplifica de forma notable |
| **E1 / E2** — despliegue | **Local por ahora**; alojamiento contratado más adelante | Ver la sección *Despliegue* en [03-arquitectura-y-capas.md](03-arquitectura-y-capas.md). E2 (exposición a Internet) y E3 (respaldos) siguen abiertas |
| **A5** — splits | Aceptado el ajuste manual implícito al elegir "solo seguimiento" | Conviene confirmarlo explícitamente antes de implementar |
| **D1 / D2 / D5** — autenticación | **ASP.NET Core Identity local + cookie**; las cuentas las crea el administrador | [ADR-006](decisiones/ADR-006-autenticacion-local-identity-cookie.md). Resuelve de paso quién administra: un usuario con rol de administrador |
| **Arquitectura** | 4 proyectos .NET + `web/` Angular, PostgreSQL, Minimal APIs sin mediador | [ADR-001](decisiones/ADR-001-estructura-y-regla-de-dependencias.md), [ADR-005](decisiones/ADR-005-postgresql-desde-el-inicio.md), [ADR-007](decisiones/ADR-007-minimal-apis-sin-mediador.md). **La arquitectura queda cerrada**; ver [07-baseline-tecnologico.md](07-baseline-tecnologico.md) |

**Ya no queda ninguna pregunta bloqueante sin responder y la arquitectura está cerrada.**

**Las preguntas bloqueantes están todas resueltas, incluida B3:** la cobertura de la cartera real se
verificó el 2026-10-12 con **19 de 19 instrumentos cubiertos**, fondos mutuos incluidos (ver
[08](08-prueba-cobertura-instrumentos.md)).

Lo que sigue abierto, y ninguna bloquea el arranque: **C3** (política de FX faltante, con la
recomendación ya escrita en [ADR-003](decisiones/ADR-003-coste-en-moneda-local.md)), **D4** (compartir
portafolios, hoy descartado por defecto), **D3** (2FA, previsto para v0.2) y **E3 / E6** (respaldos y
uso móvil como caso principal).

---

## A. Alcance y producto

| # | Pregunta | Por qué importa | Opciones | Recomendación | Prioridad |
|---|---|---|---|---|---|
| A1 | ¿Los **bonos individuales** entran en el MVP? | Es la decisión que más cambia el modelo de datos: un bono individual necesita valor nominal, cupón y precio en % del nominal; un fondo de bonos es un instrumento normal | (1) Solo fondos y ETF de renta fija. (2) Bonos individuales con **valoración manual**. (3) Bonos individuales con modelo completo (cupones, cupón corrido) | **(2)** si hay bonos: entran con precio manual. El modelo completo arrastra flujos de caja y sale del alcance | **Bloqueante** |
| A2 | ¿Se registra **efectivo/liquidez**? | Afecta al valor total y a la definición de "rendimiento" | (1) No. (2) Sí, como posición manual especial | **(2)**, al final del MVP: es una fila, y sin ella el total del portafolio no cuadra con la realidad | Importante |
| A3 | ¿Se quieren registrar **dividendos y cupones cobrados**? | Hoy está fuera de alcance. Pero si se cobran dividendos, la "ganancia" calculada solo con precio los ignora y el número se queda corto | (1) Fuera. (2) Registrar importes cobrados a mano, sin flujos | **(1)** por ahora, dejando el modelo preparado para añadirlo | Importante |
| A4 | ¿Qué se hace con una **posición vendida**? | Sin transacciones, "vender" es borrar o cerrar | (1) Borrar. (2) Cantidad 0 + `IsClosed`. (3) Archivar | **(2) + (3)**: cerrar y conservar. Borrar destruye el único rastro del coste | Importante |
| A5 | ¿Ajuste **manual de splits** es aceptable en el MVP? | Si no, hace falta una fuente de acciones corporativas desde el principio | (1) Sí, manual. (2) Automático | **(1)**, con un aviso en la posición. El automático es v0.3 | Puede esperar |
| A6 | ¿Hace falta **histórico de rendimiento** desde el primer día (con datos anteriores a la app)? | Es imposible de reconstruir sin transacciones | (1) No, empezar a construir historia desde hoy. (2) Repreciar hacia atrás (aproximación documentada) | **(1)**, y empezar a guardar snapshots en v0.1 (ver [04](04-modelo-de-datos.md)) | **Bloqueante** (por la expectativa, no por el código) |

## B. Datos de mercado

| # | Pregunta | Por qué importa | Opciones | Recomendación | Prioridad |
|---|---|---|---|---|---|
| B1 | **¿Qué bolsas y países** hay realmente en la cartera? | Determina si basta una fuente gratuita US-centric o hace falta cobertura global de pago. Es la pregunta que más influye en el coste | Lista concreta de mercados y monedas | Hacer el inventario **antes** de elegir proveedor | **Bloqueante** |
| B2 | ¿Presupuesto mensual para datos? | Separa el camino "todo gratis con limitaciones" del camino "10–30 USD/mes y sin fricción" | (1) 0. (2) ≤10 USD. (3) ≤30 USD | **(1) al empezar**, con el puerto de precios ya abstraído; decidir el gasto cuando B1 esté respondida | **Bloqueante** |
| B3 | ¿**Fondos mutuos** concretos: de qué gestora y país? | La cobertura de NAV de fondos es el punto más débil de casi todas las fuentes gratuitas | Lista de fondos | **Resuelto el 2026-10-12:** los 4 fondos de la cartera están cubiertos y verificados por NAV | **Resuelto** |
| B4 | ¿Se acepta **valoración manual** para lo que no tenga fuente? | Es la válvula de escape que hace viable el alcance | Sí / No | **Sí**, y conviene diseñarla desde el modelo (ya está en [04](04-modelo-de-datos.md)) | Importante |
| B5 | ¿Hace falta precio **intradía (15 min)** o basta el cierre diario? | Multiplica el consumo de API y el coste; con carteras de largo plazo aporta poco | (1) Solo cierre diario. (2) Cierre + intradía opcional | **(1)** en el MVP. Ver [ADR-002](decisiones/ADR-002-estrategia-de-precios-cierre-diario.md) | **Bloqueante** (define el MVP) |
| B6 | ¿Qué **zona horaria** define "el día" del portafolio? | Un snapshot "del 14" puede ser el 13 en otra zona; los mercados cierran a horas distintas | Zona del usuario / UTC / por mercado | **UTC para almacenar, zona del usuario para mostrar**, y el snapshot diario se cierra con la última zona relevante | Importante |

## C. Divisas

| # | Pregunta | Por qué importa | Opciones | Recomendación | Prioridad |
|---|---|---|---|---|---|
| C1 | ¿**Moneda base** de cada persona? | Todo el cálculo consolidado depende de esto | Una por usuario / una familiar | **Por usuario** (ver [04](04-modelo-de-datos.md)); si coincide, no hay coste extra | **Bloqueante** |
| C2 | ¿Se mostrará la G/P **en la moneda del activo** además de en moneda base? | Ayuda a distinguir "el activo subió" de "la divisa se movió" | (1) Solo moneda base. (2) Base + local | **(2)**: mostrar el retorno local es casi gratis y muy clarificador | Importante |
| C3 | ¿Qué se hace si **falta el FX de la fecha de compra**? | Sin política, el sistema inventa números | (1) Día hábil anterior. (2) Excluir del total con aviso. (3) FX de hoy | **(1)** dentro de una ventana corta; si no existe, **(2)**. Nunca (3) en silencio | **Bloqueante** |
| C4 | ¿Se quiere el **desglose del efecto divisa**? | Responde "¿gané por el activo o por el cambio?" | (1) v0.2. (2) Desde el MVP | **(1)**. Es una capa de cálculo sobre lo básico | Puede esperar |
| C5 | Precisión y **redondeo** por moneda | Afecta a que los totales cuadren con las filas | Política única / por moneda | Redondeo **solo al presentar**, con decimales de la moneda (0 en JPY, 2 habitual, 4 en FX). Ver [04](04-modelo-de-datos.md) | Importante |
| C6 | ¿Se guardan también **tipos de cambio actuales** aparte de los históricos? | Las métricas de hoy usan el FX de hoy; el coste usa el histórico | Ambas cosas | **Sí, ambas**: una tabla `FxRates` con fecha cubre los dos casos y hay que registrar qué fecha se aplicó | Importante |

## D. Identidad, aislamiento y uso familiar

| # | Pregunta | Por qué importa | Opciones | Recomendación | Prioridad |
|---|---|---|---|---|---|
| D1 | ¿Cómo se **crean las cuentas**? | Son 2–5 personas conocidas: el registro abierto es riesgo sin beneficio | (1) El administrador crea usuarios. (2) Registro abierto con invitación | **(1)** | **Bloqueante** |
| D2 | ¿Contraseña local o **login externo** (Google/Microsoft)? | Menos contraseñas que custodiar, pero añade dependencia externa y registro de aplicación | (1) Local. (2) Google/Microsoft. (3) Ambos | **(1)** para empezar: la app es privada y no necesita delegar la identidad | Importante |
| D3 | ¿**2FA**? | Vale más que la app no sea pública que un 2FA a medias | (1) No. (2) TOTP opcional por usuario | **(2)** en v0.2 | Puede esperar |
| D4 | ¿Alguien debería poder **ver el portafolio de otro**, aunque sea en solo lectura? | El requisito dice aislamiento por defecto; pero "es para la familia" | (1) Nunca. (2) Solo lectura explícita y revocable | **(1)** en el MVP, dejando el modelo preparado (una tabla de permisos más adelante). Añadirlo después es fácil; quitarlo, no | Importante |
| D5 | ¿Quién administra (crea usuarios, reinicia contraseñas)? | Define si hace falta panel de administración | (1) Un usuario admin. (2) Todos | **(1)**; un panel mínimo basta | Importante |

## E. Despliegue y operación

| # | Pregunta | Por qué importa | Opciones | Recomendación | Prioridad |
|---|---|---|---|---|---|
| E1 | ¿Dónde se ejecuta? | Define TLS, respaldos, coste y si hay que exponer algo a Internet | (1) Equipo/NAS en casa. (2) VPS pequeño. (3) Cloud gestionado | **(2)** si se quiere acceso desde fuera sin abrir puertos en casa; **(1)** si todos acceden desde la misma red | **Bloqueante** |
| E2 | ¿Acceso **desde fuera de casa**? | Es la diferencia entre un servicio doméstico y un servicio en Internet | Sí / No | Si sí: HTTPS obligatorio, cuentas por invitación, actualizaciones al día | **Bloqueante** |
| E3 | ¿**Respaldos**: dónde, cada cuánto, quién los verifica? | Es la única pérdida de datos irreversible del proyecto | (1) `pg_dump` diario con retención. (2) Copia manual cuando alguien se acuerde | **(1)**, automatizado y con **restauración probada al menos una vez**. Con PostgreSQL ([ADR-005](decisiones/ADR-005-postgresql-desde-el-inicio.md)) el respaldo ya no es copiar un archivo | **Bloqueante** |
| E4 | ¿La app **refresca en segundo plano** o solo al abrirla? | Con cierre diario basta un job diario; sin job, la primera visita del día paga la espera | (1) Job diario + refresco al abrir. (2) Solo al abrir | **(1)**: el job diario hace que la app siempre esté lista | Importante |
| E5 | ¿Idioma de la interfaz? | Afecta a la preparación de textos y formatos | Español / inglés / ambos | **Español** ahora, con los textos centralizados para poder añadir inglés | Puede esperar |
| E6 | ¿Se espera uso desde móvil como caso principal? | Define si la tabla debe ser una vista de tarjetas en pantallas pequeñas | Sí / No | **Sí**: el diseño responsive no es opcional aquí | Importante |

---

## Las cinco decisiones que desbloquean todo lo demás

Si solo se responden cinco preguntas, que sean estas:

1. **B1 — qué mercados y monedas hay de verdad.**
2. **C1 — moneda base de cada persona.**
3. **B2 — presupuesto mensual para datos (0 / ≤10 / ≤30 USD).**
4. **A1 — bonos individuales dentro o fuera.**
5. **E1/E2 — dónde se ejecuta y si es accesible desde Internet.**

Con esas cinco respuestas, el proveedor de precios, el modelo de datos y la estructura de despliegue
quedan casi determinados.

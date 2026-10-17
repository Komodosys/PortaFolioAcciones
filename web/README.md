# Frontend — PortaFolioAcciones

Aplicación **Angular 22** (standalone, zoneless, con señales).

## Comandos

```bash
npm start        # servidor de desarrollo en http://localhost:4200, con proxy a la API
npm run build    # compilación de producción en dist/
npm test         # pruebas con Vitest
```

`npm start` ya incluye el proxy: `/api` se redirige a `http://localhost:8080`, donde escucha la API
(que se levanta con `docker compose up -d` desde la raíz del repositorio). Así el navegador ve un solo
origen y no hay problemas de CORS durante el desarrollo.

## Estructura

```
src/app/
  core/       Servicios globales, sesión, cliente HTTP e idiomas. Nada de aquí conoce una pantalla.
  features/   Una carpeta por funcionalidad. Una no importa de otra.
  shared/     Componentes y utilidades que usan dos o más funcionalidades.
```

## Reglas que no se negocian

- **Aquí no se calcula nada financiero.** La API manda los importes ya calculados; el frontend los
  formatea y los presenta. Si el cálculo se duplicara, dos pantallas podrían dar cifras distintas de lo
  mismo.
- **Los números viajan en crudo.** Se reciben `1234.56` y `"2026-10-12"`, nunca cantidades ya
  formateadas. El formato se decide al presentar, según el idioma. Esto no es un capricho: en los
  estados de cuenta el mismo número se escribe `1,234.56` en inglés y `1 234,56` en francés, y
  reinterpretarlo es justo el error que queremos evitar.
- **La moneda viaja como código ISO** junto al importe. Se presenta según el idioma; nunca se
  reinterpreta el valor.

## Multi-idioma

Español, francés e inglés, **cambiables en caliente** desde las preferencias de la persona
([ADR-013](../DOCUMENTACION/decisiones/ADR-013-interfaz-multi-idioma.md)).

Los textos están en `public/i18n/{es,fr,en}.json`. Si falta una clave, se muestra en inglés: una
traducción incompleta no debe romper la pantalla ni dejar la clave a la vista.

Se usa **Transloco** y no la internacionalización integrada de Angular, porque la integrada es de
compilación (una compilación por idioma) y aquí el idioma es una preferencia que cambia en ejecución.

## Notas de esta versión de Angular

Dos cosas que no funcionan como en versiones anteriores, y que cuestan un rato de encontrar:

- **`withCredentials` ya no existe como opción de `provideHttpClient`.** En Angular 22 se pone como opción
  de la petición, y aquí se hace con un interceptor que clona todas: así deja de depender de que alguien se
  acuerde. Con el proxy de desarrollo y con Caddy, la aplicación y la API comparten origen y el navegador
  mandaría las cookies igual, pero el interceptor **deja escrito el requisito** para el día que la API viva
  en otro origen.
- **Las pruebas se ejecutan con Vitest, y el `TestBed` se reinicia solo entre pruebas.** Ese reinicio lo
  registra la preparación de Angular: no hay que añadirlo a mano, y añadirlo de más estorba.
- **Entre una respuesta y la petición siguiente hay un salto asincrónico.** Al entrar, la petición del
  perfil no se emite hasta que se resuelve la del inicio de sesión. En una prueba, eso significa esperar un
  turno entre responder a una y comprobar la otra; sin esperar, la prueba pide una petición que todavía no
  existe y el fallo que da no se parece a la causa.

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

## Estilo: Tailwind CSS y DaisyUI

La interfaz usa **Tailwind CSS 3.4** y **DaisyUI 4.12**, con los temas `light` y `cupcake`, Montserrat y el
fondo crema `#F5F3EF`. **Son las mismas versiones, temas y tipografía que WinCuentas**, la otra aplicación de
la casa: la coherencia que se busca no es parecerse, es ser el mismo sistema. Cambiar la versión de DaisyUI
cambia los nombres y el aspecto de los componentes, y entonces dos aplicaciones que deberían ser gemelas
empiezan a separarse.

- **No hay SCSS por componente.** El estilo vive en la plantilla con clases de Tailwind. Lo único que no se
  puede poner desde ahí es la clase del elemento anfitrión, y para eso está `host: { class: 'block' }` en el
  componente.
- `src/styles.scss` son las tres directivas de Tailwind, la tipografía y el fondo. Nada más. Las clases
  propias que había —`.cifra`, `.visualmente-oculto`— no hacen falta: Tailwind ya tiene `tabular-nums` y
  `sr-only`.
- El color de la identidad está en `tailwind.config.js` como `crema`, y se usa `bg-crema`. No se repite como
  `bg-[#F5F3EF]` en cada pantalla: el día que cambie tiene que cambiar en un solo sitio.

### Dos trampas de Tailwind que no dan error

Las dos se compilan, pasan las pruebas, y solo se ven en el navegador. `probar.cmd` avisa de la primera.

1. **Un color de texto base y otro condicional en el mismo elemento.** Tailwind ordena sus utilidades por
   **nombre de clase**, no por el orden en que se escriben. Con `text-slate-700` de base y
   `[class.text-emerald-600]` condicional, gana el que quede después en el CSS — y resulta que «emerald» va
   **antes** que «slate». El color condicional no se aplica nunca. **La solución es no ponerlos juntos**: el
   color base va en el padre y se hereda.

2. **Los ficheros se leen con UTF-8 explícito.** `Get-Content -Raw` de PowerShell 5.1 lee como ANSI: si se
   usa para leer y reescribir, **cada acento se convierte en su interpretación cp1252** y `Contraseña` pasa a
   `ContraseÃ±a` sin que nada falle. Hay que usar `[System.IO.File]::ReadAllText($ruta, UTF8)` o la
   herramienta de edición.

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

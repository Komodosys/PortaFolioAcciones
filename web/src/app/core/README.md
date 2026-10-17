# `core` — lo que se carga una sola vez

Servicios de ámbito global, configuración y piezas de infraestructura del frontend: cliente HTTP,
interceptores, guardas de ruta, gestión de la sesión y **la configuración de idiomas**.

Regla: nada de aquí conoce una pantalla concreta. Si algo solo lo usa una pantalla, va en `features`.

Aquí vive `i18n/transloco-loader.ts`, que carga los ficheros de traducción de `public/i18n/`.

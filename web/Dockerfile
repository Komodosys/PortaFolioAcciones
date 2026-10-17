# syntax=docker/dockerfile:1
#
# La aplicacion de Angular, servida por Caddy junto a la API.
#
# **En dos etapas** y no en una: el resultado es una imagen que no lleva Node, ni el codigo fuente, ni las
# dependencias de desarrollo. Solo los ficheros ya construidos y Caddy. Pesa decenas de megas en vez de
# cientos, y no lleva dentro nada que sirva para construir.
#
# El contexto de construccion es la RAIZ del repositorio, no esta carpeta: asi se reutiliza el
# `.dockerignore` de la raiz, que ya excluye `node_modules`, `dist` y —lo que mas importa— los CSV con datos
# personales.

# --- 1. Construir ----------------------------------------------------------------------------
FROM node:24-alpine AS compilar

WORKDIR /origen

# Primero SOLO los ficheros de dependencias. Es lo que hace que Docker reutilice la capa de `npm ci` en
# cada construccion en la que no hayan cambiado: sin esto, instalar las dependencias —que es lo que mas
# tarda— se repetiria entero cada vez que se toca una linea de la aplicacion.
COPY web/package.json web/package-lock.json ./

# `npm ci` y no `npm install`: instala EXACTAMENTE lo que dice el fichero de bloqueo, asi que la imagen se
# construye igual hoy que dentro de un año.
RUN npm ci --no-audit --no-fund

COPY web/ ./

RUN npm run build

# --- 2. Servir ---------------------------------------------------------------------------------
FROM caddy:2-alpine

# El Angular moderno deja los ficheros del navegador en `browser/`, no en la raiz de `dist`.
COPY --from=compilar /origen/dist/portafolio-web/browser /srv

/**
 * Tailwind CSS y DaisyUI.
 *
 * **Las versiones y la configuración son las de WinCuentas**, a propósito: la coherencia visual que se busca
 * no es «parecerse», es ser el mismo sistema. Con otra versión de DaisyUI cambian los nombres y el aspecto
 * de los componentes, y entonces dos aplicaciones que deberían ser gemelas empiezan a separarse.
 *
 * Referencia: `Descartar/win-cuentas/tailwind.config.js` y `Descartar/win-cuentas/src/styles.css`.
 */
module.exports = {
  content: [
    './src/**/*.{html,ts}',
  ],
  theme: {
    fontFamily: {
      sans: ['Montserrat', 'ui-sans-serif', 'system-ui', 'sans-serif'],
      montserrat: ['Montserrat', 'sans-serif'],
    },
    extend: {
      animation: {
        fadeIn: 'fadeIn 0.3s ease-in-out',
      },
      keyframes: {
        fadeIn: {
          '0%': { opacity: 0 },
          '100%': { opacity: 1 },
        },
      },
    },
  },
  plugins: [
    require('daisyui'),
  ],
  daisyui: {
    // Los mismos temas que WinCuentas. `cupcake` es una variante más cálida que encaja con el fondo crema
    // de la aplicación; `light` es la base.
    themes: ['light', 'cupcake'],
  },

  // ------------------------------------------------------------------------------------------------
  // TEMPORAL, mientras dura la migración.
  //
  // `preflight` es el reset de Tailwind: quita márgenes, unifica tamaños de titular y normaliza los
  // controles. Es lo que hace que las utilidades se comporten igual en todas partes, así que al final tiene
  // que estar ACTIVADO.
  //
  // Pero ahora mismo la aplicación todavía se estiliza con su propio SCSS, construido sobre otro reset. Si
  // se activa hoy, todos los componentes sin rediseñar se verán a la vez, y una migración que se pueda
  // revisar por trozos pasaría a ser un salto al vacío.
  //
  // **Al terminar el rediseño hay que borrar esta línea.** Si sigue aquí, la aplicación funcionará pero no
  // será exactamente WinCuentas: sus componentes cuentan con el reset de Tailwind.
  corePlugins: {
    preflight: false,
  },
};

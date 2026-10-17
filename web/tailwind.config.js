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
      // El color de fondo de la aplicación, el mismo crema que WinCuentas. Se declara aquí y no se repite
      // como `bg-[#F5F3EF]` en cada pantalla: es el color de la identidad, y el día que cambie tiene que
      // cambiar en un solo sitio.
      colors: {
        crema: '#F5F3EF',
      },
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

  // El reseteo de Tailwind.
  //
  // **Estuvo desactivado mientras duró la migración**, y a propósito: la aplicación se estilizaba con su
  // propio SCSS construido sobre otro reset distinto, y activarlo entonces habría roto de golpe todas las
  // pantallas sin rediseñar. Ahora que no queda ni un `.scss` de componente, tiene que estar ACTIVADO: es lo
  // que hace que las utilidades se comporten igual en todas partes, y sin él la aplicación funciona pero no
  // es exactamente WinCuentas, cuyos componentes cuentan con este reset.
};

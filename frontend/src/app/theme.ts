import { definePreset } from '@primeuix/themes'
import Aura from '@primeuix/themes/aura'

/** PrimeVue Aura with the garden palette from the demo (see styles.css for the matching CSS tokens). */
export const complotTheme = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#eef6ef',
      100: '#d9ebdb',
      200: '#b5d7b9',
      300: '#8bbd92',
      400: '#5f9d6a',
      500: '#3f7d4e',
      600: '#33683f',
      700: '#2b5535',
      800: '#24452c',
      900: '#1d3824',
      950: '#102016',
    },
    colorScheme: {
      light: {
        surface: {
          0: '#fffdf8',
          50: '#faf6ec',
          100: '#f4f1e8',
          200: '#e4ddcc',
          300: '#d2c8b2',
          400: '#b9a37c',
          500: '#8f8670',
          600: '#6f6a5c',
          700: '#55513f',
          800: '#3b392f',
          900: '#2a2a24',
          950: '#1b1b17',
        },
      },
    },
  },
})

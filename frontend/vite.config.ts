import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig, lazyPlugins } from 'vite-plus'

/** The .NET API (see backend/src/Complot.Api/Properties/launchSettings.json). */
const api = process.env.COMPLOT_API ?? 'http://localhost:5180'

// https://vite.dev/config/
export default defineConfig({
  fmt: {
    singleQuote: true,
    semi: false,
    printWidth: 140,
    ignorePatterns: ['src/shared/api/client.ts', 'src/shared/api/openapi.json'],
  },
  lint: {
    jsPlugins: [{ name: 'vite-plus', specifier: 'vite-plus/oxlint-plugin' }],
    rules: { 'vite-plus/prefer-vite-plus-imports': 'error' },
    ignorePatterns: ['src/shared/api/client.ts'],
    options: { typeAware: true, typeCheck: true },
  },
  plugins: lazyPlugins(() => [vue()]),
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': api,
      '/auth': api,
      '/signin-oidc': api,
      '/health': api,
    },
  },
  test: {
    environment: 'node',
  },
})

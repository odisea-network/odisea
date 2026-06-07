import { defineConfig } from 'vite';
import { resolve } from 'path';

// ESM library build — for future `npm` consumption
export default defineConfig({
  build: {
    lib: {
      entry: resolve(__dirname, 'src/index.ts'),
      name: 'OdiseaComponents',
      formats: ['es'],
      fileName: () => 'odisea-components.es.js',
    },
    outDir: 'dist/v1',
    emptyOutDir: true,
    sourcemap: true,
  },
});

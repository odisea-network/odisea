import { defineConfig } from 'vite';
import { resolve } from 'path';

// Self-contained UMD bundle for <script>-tag embedding on agency websites.
// Lit is bundled in so a single tag is enough.
export default defineConfig({
  build: {
    lib: {
      entry: resolve(__dirname, 'src/index.ts'),
      name: 'OdiseaComponents',
      formats: ['umd'],
      fileName: () => 'odisea-components.umd.js',
    },
    outDir: 'dist',
    emptyOutDir: false,
    sourcemap: true,
  },
});

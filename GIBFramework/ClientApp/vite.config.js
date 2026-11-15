import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  base: '/app/',
  build: {
    outDir: '../wwwroot/app',
    emptyOutDir: true,
    chunkSizeWarningLimit: 1500,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5097',
      '/health': 'http://localhost:5097',
      '/preview': 'http://localhost:5097',
      '/swagger': 'http://localhost:5097',
    },
  },
});

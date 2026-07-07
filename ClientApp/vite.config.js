import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The backend serves the built SPA from TaskManager/wwwroot, so we build straight
// into that folder. In dev we run the Vite dev server (npm run dev) and proxy the
// API to the running .NET app so the browser sees a single origin — this is what
// makes the HttpOnly identity cookie work across the two dev servers.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/account': {
        target: 'http://localhost:5136',
        changeOrigin: true,
      },
      '/api': {
        target: 'http://localhost:5136',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: '../TaskManager/wwwroot',
    emptyOutDir: true,
  },
});

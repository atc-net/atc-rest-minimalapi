import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Started by the Aspire AppHost (AddViteApp + WithReference(api)):
// - PORT is the port Aspire assigned to this app,
// - API_HTTPS / API_HTTP is the address of the Demo.Api resource.
const apiTarget = process.env.API_HTTPS ?? process.env.API_HTTP ?? 'https://localhost:45612';

export default defineConfig({
  plugins: [react()],
  server: {
    port: Number(process.env.PORT) || 5173,
    strictPort: true,
    proxy: {
      // The browser calls /api on this origin; Vite forwards it to Demo.Api (no CORS needed).
      '/api': { target: apiTarget, changeOrigin: true, secure: false },
    },
  },
});

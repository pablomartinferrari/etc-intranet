import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": "http://localhost:5260",
      "/health": "http://localhost:5260",
    },
  },
  build: {
    outDir: "../api/wwwroot",
    emptyOutDir: true,
  },
});

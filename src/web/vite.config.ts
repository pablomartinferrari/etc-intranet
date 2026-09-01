import path from "node:path";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src"),
      "@mf": path.resolve(__dirname, "src/multifamily-lbp"),
    },
  },
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

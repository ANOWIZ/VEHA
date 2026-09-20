import path from "node:path";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { "@": path.resolve(__dirname, "./src") },
  },
  server: {
    port: 5173,
    // Разрешаем доступ по имени сервиса в Docker-сети (dev).
    allowedHosts: ["localhost", "frontend"],
    proxy: {
      // Проксируем API в dev, чтобы фронт ходил на относительный /api.
      "/api": {
        target: process.env.VITE_PROXY_TARGET ?? "http://localhost:8000",
        changeOrigin: true,
      },
    },
  },
});

import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: { proxy: { "/channels": "http://localhost:5057" } },
  build: { outDir: "../TelegramPostsArchive/wwwroot", emptyOutDir: true },
});

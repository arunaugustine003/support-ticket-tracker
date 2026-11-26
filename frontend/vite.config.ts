import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  const { API_PROXY_TARGET } = loadEnv(mode, ".", "API_PROXY_TARGET");
  const backend = API_PROXY_TARGET ?? "http://localhost:8080";

  return {
    plugins: [react()],
    server: {
      proxy: {
        "/api": backend,
        "/swagger": backend,
      },
    },
  };
});

import { defineConfig, devices } from "@playwright/test";

// Sin storageState global: cada caso lo pide explicitamente via
// ayudantes/sesion.mjs (usuarioActivo/verificarSesionExiste), para que el
// mensaje de error sea claro sobre CUAL usuario falta, en vez de un fallo
// generico de Playwright si faltara el archivo.

export default defineConfig({
  testDir: "./casos",
  timeout: 60_000,
  expect: { timeout: 15_000 },
  fullyParallel: false, // los casos comparten estado de negocio (filas reales en Dev)
  retries: 0, // un reintento automatico escondería justo el defecto que este arnes busca
  reporter: [["list"], ["html", { outputFolder: "playwright-report", open: "never" }]],
  outputDir: "test-results",
  use: {
    headless: true, // esta maquina no tiene DISPLAY; el aprobador corre headed solo para guardar-sesion.mjs
    acceptDownloads: true,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
});

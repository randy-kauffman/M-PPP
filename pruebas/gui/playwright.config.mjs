import { defineConfig, devices } from "@playwright/test";

// Sin storageState global: cada caso lo pide explicitamente via
// ayudantes/sesion.mjs (usuarioActivo/verificarSesionExiste), para que el
// mensaje de error sea claro sobre CUAL usuario falta, en vez de un fallo
// generico de Playwright si faltara el archivo.

export default defineConfig({
  testDir: "./casos",
  globalSetup: "./setup/global-setup.mjs",
  // 90s: un caso que revisa los 7 botones puede tener varios "ausente"
  // genuinos (cada uno espera hasta 6s antes de descartarse, ver
  // ayudantes/ribbon.mjs) y eso se acumula.
  timeout: 90_000,
  expect: { timeout: 15_000 },
  fullyParallel: false, // los casos comparten estado de negocio (filas reales en Dev)
  workers: 1, // idem, a nivel de archivos: nunca dos navegadores contra el mismo fixture a la vez
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

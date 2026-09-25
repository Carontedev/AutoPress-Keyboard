# AutoPress validation

## Version 0.4.0-beta.1 — publication-readiness fixes

- Reproduced the rejected-release regression in a test before fixing it. Stop/emergency now retries a pending key-up; a failed retry keeps the recovery state and blocks restart.
- Added UI coverage proving that configuration changes and normal close cannot discard pending recovery; emergency recovery does not restart automation.
- Added actual conflict coverage: a rejected F9 replacement restores the working F8 picker and can start/cancel normally. An unregistered startup window preserves the saved F7 preference.
- Added a constrained 480-DIP-height smoke test that scrolls expanded settings to their bottom. Bounds follow the current monitor and update on DPI/work-area changes; mixed-DPI hardware validation remains manual.
- Packaging tests verify archived executable hashes, successful replacement, preservation of the previous ZIP/checksum when the executable is locked, and rejection of an unsigned stable package.
- No native game keyboard input was sent. Physical-input acceptance, real sleep/lock, extended soak, signing, and owner licensing remain release checklist items.

## Version 0.3.5 — September 24, 2026

- Moved the unlimited setting into the Session limit heading as a themed No limit switch, with state-specific explanatory text. The summary uses the same wording.
- Preserved the checkbox's native keyboard and accessibility semantics underneath the visual template, with a visible keyboard-focus outline.
- UI smoke verifies both thumb positions, the accessible toggle action, explanation changes, retained limits, and settings persistence. Reviewed dark and light expanded screenshots.
- Session logic, saved settings format, native Windows title bar, and compact layout are unchanged.

## Version 0.3.4 — September 24, 2026

- Added the Unlimited checkbox, persistent preference, disabled-but-preserved limit fields, compact summary, and hidden finite progress. Old preferences default to bounded sessions.
- Core requests require explicit Unlimited opt-in when no limits are present; contradictory unlimited-plus-limit requests are rejected.
- Added core tests for repeated output until manual cancellation, cancellation during countdown without output, cleanup on sender failure, and contradictory limits.
- No-input UI smoke covers loading and saving Unlimited, returning to the previous numeric limit, ignoring disabled invalid limit text, and cancellation before keyboard emission.
- Elapsed time uses total hours, so sessions longer than 24 hours do not wrap the hour display.
- Existing hotkey, emergency, shutdown, lock, suspend, disconnect, and error stop paths remain in place. No game input or long-duration soak test was performed.

## Version 0.3.3 — September 24, 2026

- Replaced the repeat-key dropdown with click-to-capture, retaining the existing layout and supported key set.
- Routed-event UI checks cover letters, digits, Space, Tab, Enter, arrows, Home, and function keys, including immediate settings persistence.
- Verified Escape preserves the previous key; losing keyboard focus cancels; reserved shortcut keys, standalone modifiers, and unsupported keys are rejected.
- Verified WM_HOTKEY messages cannot start automation during capture. Preview key-down and key-up are consumed, preventing Space/Enter from activating the button again.
- The existing registered shortcut cannot be changed to the repeat key. Window deactivation cancels capture without adding a focus-stop rule to running automation.
- Tests use local WPF events, not OS keyboard injection. Physical keyboard acceptance and game input remain separate user acceptance checks.

## Version 0.3.2 — September 24, 2026

- Replaced custom macOS-style window controls with a standard Windows title bar, system menu, and native minimize/close controls. Removed custom WindowChrome.
- The content panel remains 560 × 280 logical pixels. The measured overall window is 576 × 319 including the native frame.
- No-input UI smoke verifies native window styles and caption hit testing, the settings button, native minimize-to-tray and restoration, and native close routing. Existing migration, validation, target-selection, and shortcut checks remain enabled.
- Reviewed captures including the native title bar in light and dark themes; PrintWindow captures are not proof of compositor transparency. DWM still reports Desktop Acrylic (3).
- Input generation and session behavior were not changed; no input was sent to user applications.

## Version 0.3.1 — September 24, 2026

- Removed the user-facing monitor window and Test input control; the input receiver is now compiled only into the developer smoke-test project.
- A saved monitor target migrates to Any active window without resetting the user's repeat key, interval, or other preferences. Other saved applications, including unavailable ones, remain selected.
- Updated startup-target guidance and clear stale error feedback when configuration changes.
- Release build and 26 unit tests passed. No-input UI smoke passed, including monitor removal, migration, specific target selection, native caption hit tests, shortcut collision recovery, and cancellation.
- Reviewed the expanded English interface. Compact size remains 560 × 280 logical pixels.
- No input was sent to Roblox or other user applications. Game compatibility remains unverified.

## Version 0.3.0 — September 23, 2026

- Release build and all 26 unit tests passed (18 Core, 8 Windows).
- Published executable identifies as 0.3.0 and passed isolated startup/normal-close verification with exit code 0 and no keyboard input. One idle sample measured 111.8 MiB working set, 51.5 MiB private bytes, and 0 ms CPU over 10 seconds; these are single-machine observations, not guarantees.
- No-input WPF smoke passed twice: native header/title hit tests return HTCAPTION, while the settings button returns HTCLIENT. Actual pointer-driven dragging remains a user acceptance check.
- Application selection retains a specific saved target, even when unavailable. The first option is Any active window; Input Monitor remains selectable.
- Legacy Spanish preferences migrate to English schema version 2 without losing the key, interval, session limit, hotkey, or target. A regression check failed before the migration fix, then passed.
- English light, dark, and expanded screenshots reviewed. Compact panel remains 560 × 280 logical pixels.
- Native Desktop Acrylic reported DWM backdrop 3 with HRESULT 0x00000000.
- Shortcut-conflict recovery, validation, countdown cancellation, and emergency-message handling passed without emitting keyboard input.
- Core tests cover unrestricted start, rejection of an incorrect initial target, and checking a specific target only once. Switching focus does not add a pause or stop condition.
- Real keyboard reception inside Roblox, physical global-hotkey operation, and live focus switching are not proven by these checks. No game input was sent.

Historical validation follows; older performance figures are not measurements of version 0.3.0.

# Validación de AutoPress 0.1.0

## Compatibilidad de entrada 0.2.1

El usuario informó de falta de respuesta en Roblox. Diagnóstico de sólo lectura: la configuración seleccionaba Roblox y W a 100 ms; ambos procesos estaban sin elevación; AutoPress conservaba 28 pulsaciones y el mensaje de parada manual, sin un error de SendInput. La versión anterior enviaba down/up juntos sin duración. Ese hecho está confirmado; que sea la causa exacta dentro de Roblox todavía requiere validación en el juego.

Cambio: emisión por scan code, duración de 40 ms, intervalo medido entre inicios de pulsación y liberación inmediata al cancelar. No se modificó ningún proceso del juego ni su protección.

Pruebas aprobadas: **18 del núcleo y 8 del emisor Windows**. Las nuevas comprueban que down y up quedan separados por la espera, que la cancelación libera la tecla, que un envío rechazado no produce un key-up ajeno y que las teclas de navegación mantienen su marca extendida. El transporte se sustituye en estas pruebas para no enviar entrada al escritorio. La recepción real dentro de Roblox sigue sin confirmarse.

## Actualización visual 0.2.0

Rediseño solicitado por el usuario: panel compacto, rectangular y de cristal. Comprobaciones realizadas:

- Compilación Release sin errores ni advertencias.
- Prueba de interfaz sin emisión de teclas aprobada, incluida apertura/cierre del desplegable, validación, colisión de hotkey conservando F8 y cancelación de emergencia.
- Dimensiones comprobadas: **560 × 280 px lógicos** en estado compacto; el desplegable mantiene el ancho.
- Consulta nativa a DWM: `DWMWA_SYSTEMBACKDROP_TYPE = 3` (Desktop Acrylic), `HRESULT = 0x00000000`, alto contraste desactivado en este equipo.
- Capturas WPF revisadas en claro, oscuro y con ajustes: `artifacts/qa/autopress-light.png`, `autopress-dark.png`, `autopress-settings.png`. Estas capturas verifican los controles y su transparencia, pero no incluyen el escritorio difuminado por el compositor nativo.
- El motor de repetición no cambió. Las limitaciones de validación interactiva descritas más abajo se mantienen.

Las mediciones de rendimiento de la sección original corresponden a la versión 0.1; no se extrapolan al nuevo fondo acrílico.

Fecha: 23 de septiembre de 2026. Entorno: Windows x64, compilación 26200; SDK .NET 10.0.401. No se ha probado dentro de juegos.

## Comprobado

- Compilación Release sin errores ni advertencias, con warnings tratados como errores.
- **16 pruebas del núcleo aprobadas**: límite exacto de pulsaciones, límite de tiempo, parada durante un intervalo largo, cancelación durante la cuenta atrás, inicio duplicado, reinicio, destino inicial incorrecto, validación, fallo del emisor y limpieza.
- Una prueba detectó que un temporizador podía despertar justo antes del límite y emitir una pulsación extra. El programador ahora vuelve a comprobar el instante de la próxima pulsación y la fecha límite después de cada espera; la prueba pasa con el cambio.
- Prueba WPF sin emisión de teclas: apertura real de la ventana, temas claro y oscuro, valores visibles, validación del intervalo, registro de hotkey, colisión de F9 conservando F8, cancelación por botón y por mensaje de emergencia, estructura Win32 INPUT con tamaño correcto y cierre limpio.
- Revisadas visualmente las capturas renderizadas `artifacts/qa/autopress-light.png` y `artifacts/qa/autopress-dark.png`. Se corrigieron contraste, texto de los selectores, contenido de cajas de texto y desbordamiento de la configuración al tamaño inicial.
- Ejecutable autónomo: arranque y cierre normal con código 0, usando ajustes aislados y sin enviar teclas. No necesita el runtime de .NET instalado globalmente.

## Rendimiento

La medición usa `scripts/verify-release.ps1`: inicia la versión Release, espera 10 segundos, mide otros 10 segundos y la cierra. Las cifras son observaciones de este equipo, no garantías para todos los equipos.

La comparación detectó dos costes evitables:

- Comprimir internamente el single-file redujo el EXE de aproximadamente 165 MiB a 72 MiB, pero aumentó la memoria residente de una muestra de aproximadamente 127 MiB a 266 MiB. Se dejó el ejecutable sin esa compresión y se comprime la descarga en ZIP.
- Con renderizado por software, la muestra registró **110,5 MiB de memoria residente y 52,6 MiB de memoria privada**, frente a muestras de 127–158 MiB / 85–103 MiB usando el renderizador predeterminado. Se seleccionó software para esta interfaz pequeña.

La muestra por software registró **0 ms de CPU durante 10 segundos de reposo** y aproximadamente 613 ms hasta `WaitForInputIdle`. Esto último no equivale a una medida exacta de cuándo todos los controles son visibles. Hubo variación entre muestras de arranque con el renderizador predeterminado; una traza posterior mostró el hilo principal esperando mensajes y no identificó un bucle continuo de la aplicación. No se afirma haber aislado todas las causas de esa variación.

El temporizador visual sólo está habilitado durante una sesión; el núcleo espera de forma cancelable y no usa un bucle de espera activa. Las animaciones de tecla se limitan a cuatro por segundo y respetan la opción de animaciones de Windows.

## Pendiente de validación interactiva

La prueba automática de entrada real se detuvo antes de emitir teclas porque no pudo colocar su ventana en primer plano. No se forzó el foco ni se envió input a otra aplicación.

Por ello quedan pendientes:

- Recepción real de pares down/up mediante `SendInput` en el área de prueba.
- Activación/desactivación mediante pulsación física o inyectada de F8 y la combinación completa de emergencia. El registro Win32 y el manejo del mensaje de emergencia sí se comprobaron.
- Continuidad real de entrada al cambiar entre ventanas, funcionamiento con la ventana minimizada, suspensión y bloqueo/desconexión reales de Windows.
- Teclados/distribuciones diferentes, lector de pantalla, escalados 125–200 % y otras máquinas.
- Firma digital, autorización y compatibilidad específica de cada juego. No hay una lista certificada de títulos permitidos ni una garantía anti-ban.

El código de la prueba interactiva está en `tests/AutoPress.Windows.Smoke`. Se puede ejecutar con `scripts/build.ps1 -Smoke` cuando el escritorio esté disponible. Valida el destino antes de comenzar; las ventanas receptoras pertenecen a la propia prueba. La prueba sin teclas se ejecuta con `--render-only`.

## Reproducción

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -Publish
& "$env:LOCALAPPDATA\AutoPressKeyboard\sdk\dotnet.exe" run --project tests/AutoPress.Windows.Smoke -c Release -- --render-only
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-release.ps1
```

Los resultados del núcleo quedan en `tests/AutoPress.Core.Tests/TestResults/core.trx`. Capturas, ajustes aislados y trazas de diagnóstico están en `artifacts/qa`, excluidos de Git. El registro visual verifica composición WPF, no es una captura completa del escritorio ni evidencia de gameplay.

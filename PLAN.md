# Plan de producto: AutoPress Keyboard

> Actualización visual 0.2: la interfaz grande de la propuesta original fue sustituida, a petición del usuario, por un panel de 560 × 280 px con cristal acrílico nativo, botones tipo macOS y ajustes desplegables. El diseño de la sección 6 queda como referencia histórica.

> Estado: primera versión implementada. Consulta `README.md` para el comportamiento entregado y `docs/VALIDATION.md` para la evidencia de pruebas. El plan inicial queda ajustado a hotkey toggle, sin pausa por foco. La selección de aplicación verifica únicamente el destino inicial; no certifica autorización del juego. El catálogo de reglas por juego y la firma pública quedan pendientes.

## 1. Objetivo

Crear una utilidad de escritorio para Windows, ligera y profesional, que repita una sola tecla en contextos autorizados de productividad, QA, accesibilidad y uso offline.

No se presentará como “anti-ban”, “indetectable” ni compatible con un anti-cheat. Una API pública de Windows puede seguir produciendo eventos identificables como inyectados, y las reglas de cada juego prevalecen sobre la técnica utilizada.

La investigación y sus fuentes oficiales están en [`docs/research/autopress-key-research.md`](docs/research/autopress-key-research.md).

## 2. Política de uso

Regla de producto: **si no existe permiso oficial claro, el destino se considera no autorizado**.

| Contexto | Comportamiento del producto |
| --- | --- |
| Productividad, QA y accesibilidad autorizada | Permitido y documentado como caso principal. |
| Aplicación propia o entorno de pruebas | Permitido. |
| Juego offline | Sólo si su licencia o soporte oficial permite macros/automatización. |
| Multijugador o competición | No soportado salvo autorización oficial específica. |
| Steam, Riot, Epic/EAC u otro anti-cheat | No asumir compatibilidad; revisar las reglas del título y mantenerlo bloqueado si son ambiguas. |

Queda fuera del proyecto:

- evasión de anti-cheat, inyección en procesos, drivers o hooks para ocultar eventos;
- intervalos aleatorios o “humanización” orientada a evitar detección;
- lectura o escritura de memoria, DLL injection y manipulación de red;
- modo oculto, renombrado automático u ofuscación con propósito evasivo;
- perfiles preconfigurados para juegos competitivos;
- secuencias de macros con múltiples teclas en el MVP.

## 3. Decisión tecnológica

### Recomendación

**C# + WPF sobre .NET 10 LTS**, arquitectura Windows-only, x64.

Motivos:

- integración directa con las APIs Win32 documentadas;
- una sola pila tecnológica y ninguna interfaz web embebida;
- XAML, estilos, plantillas y animación suficiente para una UI moderna;
- buena mantenibilidad para una utilidad pequeña;
- .NET 10 tiene soporte LTS hasta noviembre de 2028.

SDK .NET 10.0.401 instalado en una carpeta de usuario durante la implementación. La entrega autónoma incluye el runtime para poder ejecutarse sin instalar .NET.

### Alternativas descartadas para el MVP

| Opción | Ventaja | Motivo para no elegirla ahora |
| --- | --- | --- |
| WinUI 3 | Fluent nativo más directo | Más piezas de Windows App SDK y mayor complejidad de despliegue para una utilidad pequeña. |
| Tauri 2 | UI web flexible y multiplataforma | Añade Rust, WebView y una capa de interoperabilidad para una función exclusivamente Windows. |
| Avalonia | Multiplataforma | El proyecto no necesita otras plataformas y la emisión de teclas seguiría siendo específica de Windows. |

### Distribución

Primera entrega:

- `win-x64`, Release y framework-dependent para minimizar la descarga;
- paquete ZIP portable con checksum SHA-256;
- sin permisos de administrador;
- build self-contained opcional si instalar el runtime crea demasiada fricción.

Una versión pública estable debe estar firmada digitalmente. Un binario sin firma se considerará pre-release.

## 4. Alcance del MVP

### Configuración

- elegir una tecla no modificadora;
- intervalo con mínimo seguro y validación visible;
- modo “activar/desactivar” mediante la misma hotkey;
- duración máxima o número máximo de pulsaciones obligatorio;
- atajo global configurable con comportamiento toggle: una pulsación activa y la siguiente desactiva;
- segundo atajo global fijo y claramente visible para parada de emergencia;
- selección explícita de una aplicación destino permitida.

### Ejecución segura

- estados `Detenido`, `Armado`, `Activo` y `Error`;
- cuenta regresiva breve antes de comenzar;
- comprobación del ejecutable seleccionado al finalizar la cuenta atrás; después el foco no limita la emisión;
- el atajo toggle realiza `Detenido → Armado → Activo` y, al pulsarlo otra vez, `Activo → Detenido`;
- cambiar de ventana o perder el foco no pausa ni detiene la sesión;
- mientras esté activo, `SendInput` enviará las pulsaciones a la ventana que tenga el foco en ese momento; la UI debe advertirlo claramente para evitar escritura accidental;
- detener mediante el atajo toggle, la parada de emergencia, el cierre de la app, la suspensión de Windows o al alcanzar el límite configurado;
- pares completos `key down`/`key up` y liberación idempotente al detener;
- error claro si Windows bloquea la entrada por UIPI, sin solicitar elevación automáticamente;
- cero red, cero cuenta y cero telemetría en el MVP.

### Persistencia

- configuración local en `%LocalAppData%\AutoPressKeyboard\settings.json`;
- no guardar texto escrito ni historial de teclas;
- registro local opcional limitado a inicio, parada, motivo y error técnico;
- botón para restaurar valores predeterminados y borrar datos locales.

## 5. Arquitectura

```text
WPF Shell
   │  configura y presenta estado
   ▼
AutomationSession
   ├── Scheduler
   ├── ActivationGate
   ├── KeyboardOutput ──► SendInput adapter
   └── SafetyPolicy

GlobalHotkey adapter ──► AutomationSession.Start/Stop
SettingsStore        ──► configuración local
```

`AutomationSession` será el módulo profundo principal. Su interfaz pública debe ser pequeña:

- `Start(SessionRequest)` valida y arma una sesión completa;
- `Stop(StopReason)` cancela y libera cualquier tecla pendiente;
- un flujo de `SessionState` informa a la UI sin exponer temporizadores ni detalles Win32.

`SessionRequest` contendrá tecla, intervalo, modo, límite y ejecutable declarado. El programador, la máquina de estados, la validación inicial y la limpieza pertenecen a la implementación del módulo.

Seams reales y comprobables:

- `KeyboardOutput`: adapter Win32 en producción y adapter falso en pruebas;
- `ActivationGate`: valida el ejecutable activo sólo al iniciar; el cambio de foco posterior no altera la sesión;
- reloj/programador: tiempo real y reloj virtual para pruebas rápidas;
- `SettingsStore`: JSON local y memoria para pruebas.

Estructura prevista:

```text
src/
  AutoPress.App/          # WPF, view models y composición
  AutoPress.Core/         # sesión, estado, reglas y modelos
  AutoPress.Windows/      # SendInput, hotkeys y ventana activa
tests/
  AutoPress.Core.Tests/
  AutoPress.Windows.Tests/
  AutoPress.TestTarget/   # receptor propio para pruebas integradas
docs/
  research/
```

## 6. Dirección visual

Concepto: **instrumento de precisión**, no panel gamer. La interfaz debe sentirse como una pequeña herramienta fiable.

### Paleta

- lienzo: `#F4F7FB`;
- superficie: `#FFFFFF`;
- tinta: `#172033`;
- azul de acción: `#315EFB`;
- verde activo: `#178A76`;
- ámbar de advertencia: `#C87918`;
- rojo de parada: `#C84444`.

Tipografía: Segoe UI Variable para interfaz y Cascadia Mono para la tecla, el intervalo y el contador.

### Composición

Ventana compacta de dos zonas:

```text
┌──────────────────────────────────────────────┐
│ AutoPress                         Detenido ● │
├───────────────────┬──────────────────────────┤
│                   │ Tecla          [  F  ]   │
│      ┌─────┐      │ Intervalo      [100] ms  │
│      │  F  │      │ Modo           [Toggle]  │
│      └─────┘      │ Destino         [Elegir] │
│  0 pulsaciones    │ Límite          [ 5 min] │
│                   │                          │
│                   │     [ Armar sesión ]     │
├───────────────────┴──────────────────────────┤
│ Parada de emergencia: Ctrl + Alt + F10       │
└──────────────────────────────────────────────┘
```

Elemento distintivo: una tecla física central que baja visualmente cuando se emite una pulsación y un aro que muestra el progreso hacia el límite. La animación se limitará o agrupará para no consumir recursos a frecuencias altas y respetará “Reducir movimiento”.

Requisitos de accesibilidad:

- foco de teclado siempre visible;
- contraste AA como mínimo;
- información de estado mediante texto e icono, nunca sólo color;
- escalado correcto de 100 % a 200 %;
- tema claro y oscuro siguiendo Windows;
- mensajes directos: “Destino sin autorización confirmada” o “Activo: las pulsaciones se enviarán a la ventana que tenga el foco”.

## 7. Plan por fases

### Fase 0 — base del proyecto (medio día)

- instalar y verificar .NET 10 SDK;
- inicializar Git, `.gitignore`, solución y proyectos;
- fijar Windows 10/11 x64 como plataforma soportada;
- registrar la política de uso y las exclusiones en README.

### Fase 1 — núcleo determinista (2 días)

- definir modelos, estados y errores;
- implementar `AutomationSession` con cancelación;
- crear reloj y emisores falsos;
- cubrir transiciones, límites y limpieza con pruebas unitarias.

### Fase 2 — adapters de Windows (1–2 días)

- implementar `SendInput` con P/Invoke seguro;
- implementar `RegisterHotKey` con `MOD_NOREPEAT`;
- identificar el ejecutable en primer plano;
- manejar colisiones de atajos, UIPI, cierre y suspensión.

### Fase 3 — interfaz moderna (2 días)

- construir la ventana, tokens, temas y controles;
- implementar selector de tecla y destino;
- enlazar estados y errores;
- añadir bandeja del sistema, contador y animación contenida.

### Fase 4 — controles de cumplimiento (1 día)

- aplicar allowlist local y bloqueo por defecto;
- mostrar evidencia/estado del destino: permitido, prohibido o no confirmado;
- añadir avisos y confirmaciones sin permitir excepciones silenciosas.

### Fase 5 — QA y publicación (2 días)

- ejecutar pruebas unitarias e integradas contra `AutoPress.TestTarget`;
- probar teclado español/inglés, hotkeys ocupadas, cambios de foco durante la ejecución, suspensión y cierre forzado;
- medir CPU, memoria, arranque y estabilidad del intervalo en Release;
- generar ZIP, checksum, notas de versión y guía de uso responsable.

Estimación total del MVP: **7–10 días de desarrollo**, sin contar la obtención de un certificado de firma.

## 8. Verificación

### Automatizada

- máquina de estados y validación de solicitudes;
- inicio/parada concurrente e idempotente;
- límite por tiempo y por repeticiones;
- cambios de foco durante una sesión sin alterar su estado ni sus contadores;
- liberación de tecla ante cancelación y excepciones;
- lectura/escritura y migración de configuración;
- analyzers, formato y `dotnet test` en Release.

### Integrada y manual

- usar exclusivamente la aplicación receptora propia, no un juego, para verificar pulsaciones;
- comprobar el atajo de emergencia con la ventana minimizada;
- cerrar desde la bandeja y desde el Administrador de tareas durante una sesión;
- suspender/reanudar Windows y cambiar de usuario;
- validar escalado, lector de pantalla, tema oscuro y movimiento reducido;
- repetir en una cuenta estándar sin privilegios administrativos.

## 9. Criterios de aceptación

- nunca queda una tecla lógicamente presionada después de cualquier ruta normal de parada;
- la parada de emergencia se procesa en menos de 250 ms en el banco de pruebas local;
- el estado sólo cambia mediante las hotkeys, los límites configurados, la suspensión, el cierre o un error fatal;
- la interfaz advierte permanentemente que, mientras esté activo, la ventana enfocada recibirá las pulsaciones;
- el proceso permanece bloqueado en reposo, sin bucle de polling agresivo;
- no requiere administrador, driver, hook global ni acceso a otro proceso;
- no realiza conexiones de red ni recopila texto o telemetría;
- todas las afirmaciones de rendimiento se basan en mediciones Release publicadas;
- ningún juego se marca como compatible sin una fuente oficial vigente;
- documentación y UI no contienen promesas de “anti-ban” o indetectabilidad.

## 10. Decisiones para iniciar la implementación

Valores recomendados si no se solicitan cambios:

- nombre provisional: **AutoPress Keyboard**;
- Windows 10/11 x64;
- una sola tecla por sesión;
- intervalo mínimo inicial de 50 ms;
- duración máxima predeterminada de 5 minutos;
- la pérdida de foco no pausa ni detiene la sesión; la hotkey toggle es el control normal de activación;
- sin inicio automático con Windows;
- interfaz en español, preparada para localización posterior.

Antes de habilitar un juego concreto se necesitará su nombre exacto, plataforma/launcher, servidor o modalidad y un enlace oficial que autorice esa automatización.

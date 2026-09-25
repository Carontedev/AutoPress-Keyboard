# Investigación: auto-presionador de teclas ligero y profesional

Fecha de consulta: **2026-09-23**.

## Conclusión

No es posible prometer que un auto-presionador sea “anti-ban” o tenga riesgo cero. Steam, Riot y Epic prohíben distintas formas de macros, bots o automatización. Además, Windows identifica como inyectados los eventos generados por su API pública.

El producto debería orientarse a productividad, pruebas de software, accesibilidad y contextos expresamente autorizados. No debe anunciarse como seguro para juegos competitivos ni incluir técnicas de evasión.

## Políticas oficiales

### Steam y Valve

El [Steam Subscriber Agreement](https://store.steampowered.com/subscriber_agreement/), actualizado el 10 de septiembre de 2026, establece en la sección 4.C que no se pueden usar scripts, bots, macros u otros sistemas no controlados por una persona para interactuar con contenido o servicios de Steam.

La sección 4.D permite a Valve restringir o terminar cuentas o suscripciones por actividad que constituya automatización. Los juegos alojados por terceros pueden imponer requisitos adicionales.

**Decisión conservadora:** no ofrecer Steam como caso de uso permitido.

### Riot, League of Legends y VALORANT

Los [Términos de Riot](https://www.riotgames.com/en/terms-of-service) prohíben programas de terceros no autorizados, incluidos scripts, bots, trainers y programas de automatización, que interactúen con sus servicios.

La guía [Third Party Applications](https://support-leagueoflegends.riotgames.com/hc/en-us/articles/225266848-Third-Party-Applications) considera una ventaja medible que una aplicación ejecute acciones en nombre del jugador, como botting o scripting. También indica que las sanciones pueden llegar a la suspensión permanente.

Riot describe [Vanguard](https://support-valorant.riotgames.com/hc/en-us/articles/360046160933-What-is-Vanguard) como software de seguridad compuesto por un cliente y un driver en modo kernel. La documentación pública consultada no declara seguro ningún auto-presionador.

**Decisión conservadora:** no soportar productos de Riot sin autorización escrita y específica.

### Epic, Fortnite y Easy Anti-Cheat

Los [Términos de Epic](https://legal.epicgames.com/epicgames/tos), aplicables entre otros productos a Fortnite, prohíben software o servicios bot que automaticen el uso de productos licenciados. También incluyen entre los cheats los bots y scripts no aprobados que produzcan una ventaja injusta.

El soporte oficial de [Easy Anti-Cheat](https://www.easy.ac/support/articles) explica que puede bloquear software que interactúe con un juego de manera similar a técnicas de cheat, incluido software de automatización. El publisher o desarrollador determina las sanciones y es el primer punto de contacto.

La documentación de [Epic Online Services Anti-Cheat](https://dev.epicgames.com/docs/game-services/anti-cheat) confirma que su finalidad es prevenir y detectar trampas, pero no publica una autorización universal para macros.

**Decisión conservadora:** no soportar Fortnite ni otros juegos protegidos por EAC sin permiso oficial del publisher.

### Qué depende de cada juego

No existe una regla universal para juegos offline, servidores privados o títulos sin anti-cheat. Cada licencia, servidor y competición puede añadir condiciones. Si no hay permiso oficial claro, el estado debe ser **no confirmado** y el programa no debe habilitar ese perfil por defecto.

## APIs de Windows

### Emisión de teclas

[`SendInput`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) es la API pública de Windows para sintetizar pulsaciones de teclado y otros eventos de entrada.

Límites documentados:

- Está sujeta a UIPI: sólo puede inyectar entrada en aplicaciones con nivel de integridad igual o inferior.
- No restablece el estado actual del teclado; teclas físicamente pulsadas pueden interferir.
- El programa debe mantener pares `key down` y `key up`, y liberar cualquier tecla pendiente al detenerse o cerrarse.
- Microsoft no garantiza que la inyección funcione en todos los tipos de aplicaciones.

La estructura [`KBDLLHOOKSTRUCT`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct) expone `LLKHF_INJECTED` y `LLKHF_LOWER_IL_INJECTED`. Por tanto, la entrada generada no debe presentarse como indistinguible de un teclado físico.

### Atajos globales

[`RegisterHotKey`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) permite registrar un atajo de sistema. `MOD_NOREPEAT` evita activaciones repetidas por la repetición física de la tecla.

Para el MVP conviene usar dos atajos configurables: iniciar/detener y parada de emergencia. Si una combinación está reservada u ocupada, la interfaz debe mostrar el error y solicitar otra.

## Tecnología de interfaz

### Recomendación: C# + WPF + .NET 10 LTS

[WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/) es un framework de UI de .NET exclusivo de Windows. Ofrece XAML, estilos, plantillas y renderizado acelerado por hardware. Para esta utilidad permite integrar Win32 desde C# sin alojar una interfaz web.

En septiembre de 2026 no conviene iniciar el proyecto en .NET 8. La [política oficial de soporte de .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) indica:

- .NET 8: mantenimiento hasta el 10 de noviembre de 2026.
- .NET 10 LTS: soporte activo hasta el 14 de noviembre de 2028.

La recomendación es usar **.NET 10 LTS**.

### Alternativas

| Opción | Ventaja | Tradeoff |
| --- | --- | --- |
| WPF + .NET 10 | Integración directa con C#/Win32; XAML y plantillas; sólo Windows. | El aspecto moderno debe diseñarse. |
| [WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/) | Framework nativo recomendado por Microsoft para nuevas apps Windows; Fluent Design. | Añade Windows App SDK y más complejidad de despliegue. |
| [Avalonia](https://docs.avaloniaui.net/docs/welcome) | UI .NET multiplataforma con motor de renderizado propio. | Su ventaja principal no aporta mucho a una herramienta dependiente de Win32. |
| [Tauri 2](https://v2.tauri.app/start/) | Reutiliza el webview del sistema y puede producir aplicaciones pequeñas. | Requiere frontend web, Rust y un puente para Win32. |

### Distribución

Según la [documentación de publicación de .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/):

- `framework-dependent` ocupa menos, pero exige un runtime compatible instalado;
- `self-contained` incluye el runtime y es más grande, pero no exige instalación previa.

Ambos pueden publicarse en [un solo archivo](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview). La primera versión debería medir tamaño, memoria y arranque en Release antes de anunciarse como “ligera”.

## Alcance recomendado del MVP

1. Selección de una tecla, intervalo, modo de activación y límite de tiempo o repeticiones.
2. Estados visibles: `DETENIDO`, `ARMADO` y `ACTIVO`.
3. Atajo global toggle: una pulsación activa y la siguiente desactiva; otro atajo independiente funciona como parada de emergencia.
4. Cuenta atrás breve antes de comenzar.
5. Pares down/up en una llamada y liberación de cualquier envío parcial en las rutas normales de parada. Una terminación forzada no permite garantizar limpieza. Perder el foco no detiene la sesión, según el requisito posterior del usuario.
6. Lista positiva de aplicaciones objetivo para productividad y QA.
7. El cambio de foco no modifica el estado. Mientras esté activo, Windows dirige `SendInput` a la ventana enfocada, por lo que la interfaz debe advertir el riesgo de escritura accidental.
8. Perfiles locales, sin nube ni telemetría por defecto.
9. Aviso visible de uso responsable y ausencia de garantía anti-ban.

## Fuera de alcance

- aleatorización o “humanización” para aparentar entrada física;
- drivers, acceso al kernel o dispositivos virtuales;
- lectura o escritura de memoria de otros procesos;
- inyección de DLL, overlays o manipulación de paquetes;
- detección, desactivación o bypass de anti-cheat;
- ocultación del proceso u ofuscación orientada a evadir detección;
- perfiles preconfigurados para juegos competitivos.

## Plan de trabajo

### Fase 0 — política

- Adoptar la regla “desconocido = no autorizado”.
- Mantener una tabla por juego con fuente oficial, fecha y estado.
- No aceptar foros o ausencia previa de sanciones como prueba de permiso.

### Fase 1 — núcleo

- Separar máquina de estados, temporizador y emisor Win32.
- Implementar cancelación inmediata y limpieza idempotente de `key up`.
- Ejecutar sin elevación; si UIPI bloquea el destino, mostrar un error.

### Fase 2 — interfaz

- Crear una ventana compacta WPF/.NET 10 con selector de tecla, intervalo, modo, límite y control principal.
- Añadir temas claro/oscuro, contraste accesible, foco visible y animación discreta.
- Añadir icono de bandeja con estado y parada rápida.

### Fase 3 — cumplimiento

- Validar la aplicación declarada y la lista positiva al activar; no vigilar el foco ni cambiar el estado después de iniciado.
- Detener al cambiar de ventana o bloquear la sesión.
- Mantener bloqueados por defecto Steam, Riot y Epic competitivos.
- Exigir evidencia oficial antes de cambiar cualquier bloqueo.

### Fase 4 — validación

- Probar transiciones, límites, cancelación y liberación de teclas.
- Realizar pruebas manuales en Notepad y una aplicación propia de QA.
- Medir CPU y memoria en reposo, activo, minimizado y bandeja.
- Verificar suspensión, reanudación, cierre inesperado y atajos ocupados.

### Fase 5 — entrega

- Publicar Release `win-x64` con versión y changelog.
- Firmar el ejecutable si se distribuye públicamente.
- Proporcionar instalación y desinstalación limpias.
- Publicar una política de privacidad que confirme almacenamiento local y ausencia de captura de texto.

## Criterios de aceptación

- Ninguna promesa de “indetectable”, “anti-ban” o “100 % seguro”.
- Ningún mecanismo de evasión o interferencia con anti-cheat.
- La parada de emergencia funciona con la ventana minimizada.
- Nunca queda una tecla lógicamente pulsada al detener o cerrar.
- No existe polling agresivo ni consumo sostenido apreciable en reposo.
- Todo uso en juegos tiene evidencia oficial vigente o permanece no soportado.

## Fuentes primarias

- [Steam Subscriber Agreement](https://store.steampowered.com/subscriber_agreement/)
- [Riot Games Terms of Service](https://www.riotgames.com/en/terms-of-service)
- [Riot Third Party Applications](https://support-leagueoflegends.riotgames.com/hc/en-us/articles/225266848-Third-Party-Applications)
- [Riot Vanguard](https://support-valorant.riotgames.com/hc/en-us/articles/360046160933-What-is-Vanguard)
- [Epic Games Terms of Service](https://legal.epicgames.com/epicgames/tos)
- [Easy Anti-Cheat Support](https://www.easy.ac/support/articles)
- [Microsoft SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
- [Microsoft KBDLLHOOKSTRUCT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct)
- [Microsoft RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
- [Microsoft WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
- [Microsoft WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
- [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

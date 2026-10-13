# VALDRIO — incidencia recurrente de saltos al desplazar páginas WPF
Fecha: 2026-10-13 · Rama: `fix/ux-consolidated-legacy-open-20261013` (PR #44)

## Diagnóstico sobre el código real
La intervención anterior (`StableScrolling.cs`, documentada en
`docs/evidencias/t11-resultados-scroll-20261011/README.md`) ya evitaba
parte de los bloqueos de rueda y fijaba la altura de la ayuda contextual.
No obstante, mantenía tres fallos de enrutamiento:

1. `active` representa **el último DataGrid pulsado o enfocado**, no la tabla
   que está bajo el puntero actual. El manejador desviaba la rueda a dicha
   tabla incluso sobre cuadros, gráficos o regiones ajenas de la misma página.
   Resultado: el desplazamiento aparentemente se detenía y después se reanudaba
   al cambiar de contexto, percibiéndose como saltos.
2. Los nuevos diagramas primario y secundario utilizan un `ScrollViewer`
   **solo horizontal** (`VerticalScrollBarVisibility="Disabled"`). Una rueda
   vertical sobre el gráfico se dirigía al visor interno, que no puede
   desplazarse verticalmente; no llegaba a la página.
3. Se admitía como destino cualquier visor privado con `Auto`, incluso con
   rango efectivo de desplazamiento **cero**. WPF genera visores internos
   de plantillas de entrada, tablas y controles compuestos. Absorber la rueda
   en uno de ellos bloquea el documento que sí puede desplazarse.

No se cambia la posición automáticamente para compensar `ScrollChanged`;
esa alternativa puede ocultar saltos y luchar contra navegación y foco.
Tampoco se desactiva globalmente `RequestBringIntoView`, necesario para
navegación accesible por teclado. Un foco programático en otras operaciones
sigue siendo una causa potencial distinta que requeriría un caso reproducible.

## Referencias externas contrastadas

- Proyecto oficial de WPF, incidencia #8353 (2023, abierta):
  https://github.com/dotnet/wpf/issues/8353 — los visores anidados
  consumen la rueda aunque no tengan rango útil.
- Proyecto oficial de WPF, incidencia #9203:
  https://github.com/dotnet/wpf/issues/9203 — documentación del problema
  de sensibilidad y del delta de la rueda en el comportamiento heredado.
- Microsoft Learn, `ScrollViewer.CanContentScroll`:
  https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.scrollviewer.cancontentscroll
  — la unidad de offset puede ser física o lógica; no asumir que el mismo
  incremento sirve para todos los controles.
- Foro Stack Overflow, `RequestBringIntoView`:
  https://stackoverflow.com/questions/8384237/stop-wpf-scrollviewer-automatically-scrolling-to-perceived-content
  — el foco puede iniciar desplazamientos automáticos, pero suprimirlos
  sin discriminar rompería navegación accesible.
- Referencia interna: `docs/evidencias/t11-resultados-scroll-20261011/README.md`.

## Corrección aplicada

`src/Valdrio.Desktop/StableScrolling.cs`:
- El visor de página se determina **por origen real del evento y eje**,
  ignorando visores internos de DataGrid/TextBox.
- El visor de una tabla activa se utiliza solo si el evento procede
  **de esa misma tabla**, nunca por el mero recuerdo de foco/clic.
- Al desplazarse verticalmente sobre un gráfico con visor horizontal,
  se asciende al visor vertical de la página; con Shift se respeta
  el scroll horizontal del gráfico.
- Los visores con eje deshabilitado o con rango de scroll cero
  no interceptan una página desplazable.
- Se mantiene el delta fraccional para touchpad, la configuración
  Windows de líneas por paso y el límite en extremos. Una tabla activada
  sigue consumiendo la rueda en sus bordes y no mueve la página.
- No se toca el ledger, el solver, los importes, los datos de negocio ni
  la representación de los arcos.

`src/Valdrio.Desktop/MainWindow.ResultsScrollSmoke.cs`:
- Añadidas tres regresiones que deben ejecutarse en Windows en ES/EN:
  (1) tabla activa + rueda fuera de ella,
  (2) tabla activa + rueda sobre otra tabla,
  (3) rueda vertical sobre grafo cuyo visor solo admite horizontal.
- Se conservan las pruebas anteriores de delta 30, rueda dentro de
  tabla, extremos, TextBox, ayuda fija, DPI/layout y resultados.

## Verificación y aceptación necesaria

- Revisión de estructura y de rutas del código: realizada.
- Confirmación de que el workflow de esta rama solo tiene
  `workflow_dispatch`: realizada para `.github/workflows/windows-ci.yml`.
- **Compilación .NET 10, smoke WPF, pruebas MSTest en Windows y dispositivo
  físico: PENDIENTES**; el entorno de edición conectado no tiene SDK
  .NET ni escritorio Windows. No presentar este parche como aceptación
  real ni como solución certificada para todos los ratones.
- Ejecutar en Windows local, sin GitHub-hosted runners:
  `dotnet build Valdrio.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false`
  y el smoke existente `--results-scroll-only` sobre copia descartable de
  proyecto sintético, además del conjunto normal de MSTest.
- Aceptación manual: rueda y touchpad de alta resolución, cambio de foco
  al pasar entre tablas y gráficos, sidebar, importador, claves, primario,
  secundario, ventana estrecha, zoom de 125/150/200 %, idiomas ES/EN y
  combinaciones Shift+rueda. Evaluar si persiste desplazamiento inducido
  por `RequestBringIntoView` al cambiar foco sin girar la rueda.

## Presupuesto
No se lanza GitHub Actions ni se cambia la configuración de facturación.
Gasto de runners provocado por esta intervención: **0 USD**.
PR #44 continúa en borrador; no fusionar con `main` antes de la
validación Windows real.

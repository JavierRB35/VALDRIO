# Composición de repartos — 15/10/2026

Petición del propietario: facilitar el cierre al terminar de revisar el reparto, situar las acciones abajo y mejorar simplicidad, usabilidad y estética; fusión autorizada al finalizar.

## Resultado

- Primario y secundario comparten una barra inferior fija con estado, descarte, cálculo/cancelación (secundario) y cerrar/reabrir. El cierre es la acción principal. La barra queda fuera del desplazamiento del documento y desaparece en otras fases.
- Las acciones de claves quedan debajo de la biblioteca; guardar, debajo de los destinos y su validación. La asignación por lotes queda después de la tabla de cobertura.
- Resumen de costes principales y orígenes se presentan verticalmente, con todo el ancho disponible. Los importes del resumen y sus totales comparten proporciones; las tablas de detalle conservan desplazamiento horizontal cuando es necesario.
- Se conservan nombres de controles, comandos, eventos y bindings de columnas de la pantalla original, comprobados por comparación automática. No se cambian cálculos, validaciones de cierre, persistencia ni permisos. La barra utiliza los mismos controles y conserva el contexto y estado habilitado del panel.
- La directriz anterior de cierre arriba se sustituye por la decisión explícita del propietario. ES/EN y estilos globales de VALDRIO conservados.

## Integración

Base de integración: `main@4aa747c`. Se trasladan únicamente los cambios de presentación T12 (`6424e81`) y composición (`9c006b0`), más ajustes de aceptación. No se incorpora T14 ni su corrección local `5144989`. El conflicto de traducciones se resuelve aplicando únicamente las claves modificadas por T12, preservando las de main.

La evidencia anterior de T12 corresponde a una base distinta. Este documento registra la validación de la integración actual y no convierte sus pruebas históricas en resultados del HEAD nuevo.

## Reproducción local

Restaurar desde caché, compilar Release y ejecutar la suite MSTest. Para crear la empresa ficticia, definir `VALDRIO_T12_DEMO` con una ruta nueva y ejecutar `T12_6500FlowsPersistAndHistoricalReadDoesNotRecalculate`.

Abrir una copia desechable mediante `Valdrio.Desktop.exe <copia.valdrio> --allocation-layout-only --capture-dir=<carpeta>`. La prueba utiliza servicios y botones reales: cálculo de 6.500 EUR, cierre/reapertura, selección de tablas, posición fija al desplazar, foco de teclado, herencia del bloqueo del panel y ventana de 960 px, en español e inglés.

El modo `--cost-flow-only` verifica por separado los resúmenes y gráficos T12. Las capturas usan datos ficticios. No se modifica ningún fichero de empresa del usuario.

GitHub Actions: ningún workflow ejecutado. El único workflow conserva activación exclusivamente manual.

## Resultado de aceptación

- Compilación Release de la solución: 0 errores y 0 advertencias.
- Suite completa sobre la integración: **695/695** correctas, 0 fallidas, 0 omitidas, 5 min 54 s. Recibo local `tests/Valdrio.Tests/TestResults/allocation-ui.trx`.
- Interacción WPF de repartos: **41/41** controles superados. [Recibo](layout-checks.json).
- Informes y gráficos T12: **66/66** controles superados. [Recibo](t12-checks.json).
- Revisión visual real de ambas pantallas, ES/EN, anchos 1.600 y 960 px. Corregidos el ancho del encabezado «Include», la alineación de importes y la repetición del estado en el pie.
- Conservados todos los nombres, comandos, eventos Click y bindings de columnas originales. No se declara certificación exhaustiva de accesibilidad o DPI multimonitor.

[Primario](primario.png) · [Secundario](secundario.png) · [Ventana estrecha en inglés](secundario-estrecho-en.png)

# T17 — composición y cantidades: evidencia local

Fecha: 20/10/2026. Rama `feat/t17-compositions-20261020`, desde main `e956771`.

## Alcance comprobado

Catálogo de productos, servicios, semielaborados y materiales; composiciones por unidad, lote iniciado y orden; conversión explícita; exceso de entrada/rendimiento; redondeo discreto; publicación inmutable; versiones fijadas de semielaborados; bloqueo de ciclos. Página integrada ES/EN y siete preguntas/respuestas con cantidades reales de la previsualización. La selección de otra versión invalida resultados hasta abrirla; el guardado normal y la publicación trabajan en segundo plano con protección del archivo y de la navegación.

Revisión metodológica y límites: [NIC 2 y decisión D06](../../tareas/T17.md). No hay precios, importes ni valoración contable en esta entrega. T18 deberá aplicar los criterios económicos correspondientes y prevenir doble imputación. T19 incorporará consumos reales. No se ha modificado el cálculo económico T15/T16.

## Ejemplos reproducibles

1. Industrial: 120 conjuntos, lote de 50, material de 2 kg/unidad con 10 % de entrada adicional, preparación de 2 h/lote iniciado. Resultado: **264 kg y 6 h**, tres lotes. La versión 2 cambia a 3 kg; al reabrir la versión 1 conserva 2 kg y sus resultados.
2. Servicio: cuatro intervenciones, 2 h/orden más 0,5 h/intervención. Resultado: **4 h**, con dos líneas de 2 h. Una ejecución de previsualización equivale a una orden.
3. Pruebas de cálculo: rendimiento del 80 % sobre 240 kg base requiere 300 kg; conversión explícita 90 minutos a aproximadamente 1,5 h; semielaborados expandidos sin sumar simultáneamente el padre y sus componentes finales.

Los lotes y preparaciones de semielaborados se calculan por línea de uso; no se consolidan órdenes de fabricación ni se optimizan lotes entre líneas. La previsualización no registra una orden, una salida de almacén ni un consumo real.

## Verificación ejecutada

- Compilación local de la solución y de la pantalla: cero errores y advertencias.
- Regresión inicial: **745/745**. Regresión ampliada: **748/748**, 5 min 10 s, sin fallos ni omisiones. Incluye nueve pruebas T17.
- Tras mover los totales físicos al motor de cálculo y ajustar la selección de versiones: **24/24** pruebas focalizadas T17/localización. Compilación posterior de la pantalla sin errores ni advertencias.
- **33 comprobaciones WPF** de publicación, guardado en segundo plano, protección de salida, cancelación, descarte, versiones anteriores, dos recetas y ES/EN. Resultado detallado en [composition-checks.json](composition-checks.json).
- Escalado de contenido 125 %, 150 % y 200 %: se comprueba acceso a respuestas por desplazamiento. Esto no sustituye una matriz física completa de monitores/DPI ni la aceptación de comprensión por el analista.
- Inspección visual de capturas reales; sin ventanas funcionales flotantes. El catálogo y las composiciones se editan en tablas.

La primera ejecución detectó una propiedad ausente en el modelo de migración y se corrigió antes de la regresión. Se corrigió también un bloqueo de limpieza de una conexión SQLite usada únicamente por una prueba de manipulación. Ninguno de estos fallos se acepta como resultado final.

## AURORA y tiempos

Origen: `entregables/AURORA-final/AURORA_Industrial_y_Servicios.valdrio`, estructura Industrial con tres principales y dos auxiliares y análisis industrial completo. Se trabaja con copia byte a byte estable y upgrade a formato 12. La comprobación verifica SHA-256 del original antes y después; no escribe sobre la empresa del usuario.

Hash original: `756977CF952456620CEEB657B1A836A2C2668746C6CB42B3746282F4B794DE7F`.

Preparación y primer render de T17 con la empresa ya abierta: **106 ms ES / 64 ms EN** en la última prueba de guardado. Son dos mediciones locales, no percentiles ni garantía en otros equipos. Excluyen apertura completa de empresa, upgrade y validación de escritura.

## Reproducción

```text
dotnet build Valdrio.slnx --no-restore -m:1 -v quiet
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj --no-build --no-restore -m:1
Valdrio.Desktop.exe --composition-checks-only --capture-dir=<directorio-nuevo> <AURORA-original>
Valdrio.Desktop.exe --composition-view-checks-only --capture-dir=<otro-directorio> <copia-preparada>
```

Los archivos `.valdrio`, binarios y resultados completos de pruebas permanecen en los directorios locales ignorados; no se distribuye una nueva instalación local. Workflows únicamente manuales. GitHub Actions no ejecutado: **0 USD**.

## Revisión final de la pantalla

**15 comprobaciones adicionales** sobre las recetas persistidas: versión publicada de solo lectura, borrador editable, invalidación al cambiar selección, imposibilidad de calcular la receta anterior sin abrir la nueva selección, servicio sin lotes no presentado como preparación por lotes y archivo idéntico antes/después de cambiar idioma y previsualizar. Véase [view-checks.json](view-checks.json).

Capturas finales: [composición ES](composition-es.png), [respuestas ES](answers-es.png), [respuestas EN](answers-en.png), [servicio ES](service-es.png), [servicio EN](service-en.png).

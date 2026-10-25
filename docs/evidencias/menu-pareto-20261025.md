# VALDRIO: menú y Pareto

Para ejecutar: abre `app/Valdrio.Desktop.exe` y conserva el resto de archivos de esa carpeta. Requiere .NET Desktop Runtime 10, disponible en este equipo.

Base vigente: `8ae6d90`, rama `codex/menu-pareto`.

## Coherencia del menú

La base vigente agrupa siete destinos distintos dentro de «Cálculo de costes», y al desplegar una fase se cierran las otras. Actividad, servicios, tarifas, composiciones y resultados quedan subordinados a una agrupación demasiado amplia.

Se promueven los subgrupos existentes a cinco fases del mismo nivel:

- Proyecto: Proyecto y Estructura de costes.
- Datos del análisis: Análisis y periodo, Importar saldos y Preparar cuentas.
- Repartos de costes: Reparto primario y Reparto secundario.
- Actividad y tarifas: Actividad y capacidades, Servicios auxiliares y Tarifas de centros.
- Productos y resultados: Productos y servicios y Resultados y trazabilidad.

Cada grupo se puede plegar de forma independiente. La fase de destino se abre al cambiar de fase, incluidos los módulos integrados de composiciones y auxiliares. Preferencias queda fuera del recorrido económico. Se conserva la decisión vigente de no numerar el menú, ya que incluye tareas reutilizables y no una secuencia obligatoria única. Se conservan bloqueos, cierre/reapertura, estado activo, navegación atrás/continuar y ayudas; el primario bloqueado también explica su requisito.

## Gráficos

Un Pareto junto a los resultados de cada reparto, más una tabla desplegable con importes y porcentajes. Barras por coste descendente y línea de porcentaje acumulado. Primario: costes asignados agrupados por nexo. Secundario: coste final de cada principal, incluyendo primario y auxiliares, sin sumar de nuevo las transferencias internas.

Se usan importes numéricos del resultado, sin modificar el motor ni persistir datos nuevos. Los empates se ordenan por etiqueta y no se fusionan nexos con la misma etiqueta. Los porcentajes usan el total neto. Los abonos se conservan con signo; la línea puede superar 100 % antes de terminar en 100 %. Si el total es cero o negativo, solo se representan los costes y se explica la ausencia de porcentaje. Sin resultados, se muestra una indicación para completar/calcular el reparto.

Textos en español e inglés; importes y porcentajes con el formato regional elegido. Desplazamiento horizontal para muchos nexos; datos exactos accesibles en tabla.

## Preservación

Se inventariaron nombres de controles, comandos, eventos Click, estados IsEnabled y enlaces ItemsSource/SelectedItem de las dos vistas modificadas frente a HEAD. No se ha eliminado ninguno: `evidencia/preservacion-base-actual.json`. El menú conserva sus handlers y requisitos. Los gráficos consumen las colecciones existentes; no sustituyen tablas ni explicaciones.

No se han modificado `sources/`, workflows ni facturación. Sin GitHub Actions. Entrega local; no publicada en GitHub.

## Verificación

La implementación inicial se preparó en una copia antigua (`0a1dd6e`); sus 655 pruebas pasaron, pero su esquema 9 no abre la demostración actual. Se trasladó la implementación mediante integración de tres vías a una copia local de la rama vigente `8ae6d90`, conservando las funciones posteriores. Solo la nueva copia `valdrio-menu-pareto` constituye la entrega. Los intentos previos se conservan como diagnóstico histórico.

- Compilación Release correcta: 0 errores y 0 advertencias.
- Regresión de la base vigente: 765 pruebas correctas, 0 fallos, 0 omitidas. Incluye cuatro pruebas nuevas de Pareto: orden/acumulado, abonos, total no positivo y etiquetas duplicadas/empates. TRX en `evidencia/tests-actual/`.
- 25 comprobaciones WPF correctas, también ejecutadas desde la carpeta `app/` entregada: cinco grupos independientes, ubicación activa, resultados reales guardados, un punto por nexo, español/inglés, tabla de valores que permanece abierta y acepta foco de teclado, navegación secundaria a 760×620. Evidencia final en `evidencia/entrega/checks.json`.
- Capturas de ambos gráficos y pantallas en ES/EN: `evidencia/entrega/`. Se revisaron visualmente los gráficos, la página secundaria y la ventana compacta.
- Datos utilizados: copia del análisis «02 · Referencia T15: 60 horas reales» de AURORA. No se modificó la empresa original.
- La ventana compacta usa el desplazamiento existente; estas pruebas no certifican todos los monitores físicos, DPI, lectores de pantalla ni la comprensión mediante estudio con usuarios.

Comandos, desde `valdrio-menu-pareto`:

```powershell
dotnet build Valdrio.slnx -c Release --no-restore --nologo -m:1 -nr:false
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore
# Usar una copia de la demostración, no un archivo de trabajo del usuario:
.\src\Valdrio.Desktop\bin\Release\net10.0-windows\Valdrio.Desktop.exe C:\ruta\demo.valdrio --capture-dir=C:\ruta\evidencia --pareto-only
```

Las dependencias se restauraron desde la caché local. No se ejecutó CI remoto.

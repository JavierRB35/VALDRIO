# T11: composición primaria y pantalla secundaria — validación local 2026-10-09

## Alcance y base

Repositorio JavierRB35/VALDRIO. Base remota comprobada: `e55090ff319aa49c9f11d309d4e6e73a26472bc7`, rama `feature/t11-reparto-secundario-cierre`, PR #35. Trabajo preparado en `codex/t11-composicion-y-navegacion`. No se fusiona main. No se ejecuta GitHub Actions: coste provocado 0 USD.

## Decisiones metodológicas implementadas

- Paso 6, **Reparto primario**: composición por cuenta/recurso. Valor inicial 100 % fijo; admite 100 % variable o mezcla de porcentajes que sumen 100 %. La edición en lote conserva importes, destinos y otras propiedades del recurso.
- El primario produce componentes separados fijo y variable. El redondeo conserva el importe total de cada cuenta y de cada componente entre destinos. Se cubren importes negativos y porcentajes muy pequeños.
- Paso 7, **Reparto secundario y cierre**: página independiente. Las claves nuevas pertenecen automáticamente a esta fase y la biblioteca muestra sus claves. El secundario conserva la composición recibida; no ofrece reclasificación. Mantiene cálculo recíproco, explicación, conciliación y cierre.
- Los nodos no muestran selector fijo/variable. Los nodos existentes del análisis pueden modificarse mientras su primario esté abierto; el cierre bloquea la edición también en persistencia. Los cambios se registran por análisis, conservando la estructura publicada y los estados históricos. Crear/eliminar nodos requiere una nueva versión de estructura para preservar referencias existentes.
- Las cuentas que ya contienen varias partes conservan su edición detallada; no se sobrescriben en lote.
- Compatibilidad: los campos nuevos opcionales se omiten del JSON cuando son nulos. Se conserva la clasificación histórica para lectura y trazabilidad.

## Defectos corregidos

1. Recurso de traducción ausente `Resources.ClassifyBatch`, origen de la excepción mostrada por el usuario; también se incorpora la etiqueta de la nueva operación de edición de nodos.
2. Composición fija/variable ausente del flujo primario y clasificación presentada en nodos.
3. Pantalla secundaria no diferenciada en navegación.
4. El solver agrupaba por parte sin admitir sus dos componentes; ahora conserva ambas naturalezas y concilia por parte original.
5. Explicación secundaria ambigua cuando la misma parte aporta fijo y variable al mismo destino.
6. Cambiar idioma descartaba el cálculo secundario y deshabilitaba su cierre. La actualización de presentación conserva el resultado sin recalcular.
7. La automatización visual desconocía la nueva página: se incorpora su navegación real.

## Comandos reproducibles (PowerShell, raíz del repositorio)

```powershell
dotnet build Valdrio.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false --verbosity minimal
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=full-final.trx' --results-directory artifacts/t11-methodology
$captureDir = Join-Path $PWD 'artifacts/t11-methodology/wpf-final'
$p = Start-Process .\src\Valdrio.Desktop\bin\Release\net10.0-windows\Valdrio.Desktop.exe -ArgumentList @("--capture-dir=$captureDir", '--methodology', '.\samples\t01-smoke.valdrio-dev.json') -WindowStyle Hidden -Wait -PassThru
$p.ExitCode
git diff --check
```

Los tests y el recorrido WPF se ejecutan fuera del aislamiento de Codex, en este Windows local. El aislamiento impedía la conexión localhost de vstest. No se utilizan datos reales: el recorrido crea proyectos sintéticos.

## Resultados y evidencias

- Compilación final: 0 errores, 0 advertencias (`artifacts/t11-methodology/build-final.log`).
- Suite intermedia: 619/619 pruebas correctas (`full-suite.trx`).
- Recorrido nativo intermedio: salida 0 y 27 comprobaciones metodológicas correctas (`wpf3/methodology/interaction.json`), además del recorrido existente de importación, recursos, claves y cálculo.
- Capturas ES/EN: composición 70/30; edición de descripción del pool; dos filas finales 700 fijo y 300 variable; cierre y reapertura verificados.
- Recorrido WPF final: salida 0; 461 comprobaciones en 14 secciones, todas correctas, incluidas 27 específicas de metodología (`wpf-final/**/interaction.json`). Capturas finales revisadas visualmente.
- Suite final: **621/621 correctas**, 0 fallidas y 0 omitidas, duración 3 min 26 s (`full-final.trx`, `tests-full-final.log`), incluidos los nuevos casos de redondeo positivo/negativo.

## Incidencias de la validación

- Primer vstest aislado: abortó por comunicación con testhost; repetición local sin aislamiento correcta.
- Primera captura: ruta relativa incompatible con una aserción del recorrido previo; se repitió con ruta absoluta.
- Primer recorrido metodológico: detectó pérdida del resultado al cambiar idioma; corregida y repetida con éxito.
- Dos casos de redondeo inicialmente usaban importes CSV inferiores al mínimo admitido por el importador. Se corrigió la preparación del test para usar ±1 y un porcentaje variable de 0,000001 %, manteniendo el caso económico de un quantum.
- Una recompilación coincidió con la aplicación de aceptación abierta y Windows bloqueó sus DLL. Se esperó al cierre y se recompiló: 0 errores/advertencias.

Los logs, TRX, bases sintéticas y capturas están bajo `artifacts/t11-methodology` (no versionados). Este documento registra los comandos y los resultados; los tests y el recorrido de aceptación sí se versionan.

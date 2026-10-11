# Consulta de repartos y desplazamiento estable — 2026-10-11

Base comprobada: `4fd7ca569045c4c91f45f0c4b40e33b9e083a1b8`, coincidente con
`origin/feature/t11-reparto-secundario-cierre` tras fetch. Rama local de trabajo:
`codex/t11-resultados-scroll`. Entrega sobre la rama funcional de PR #35, sin
fusionar main. Árbol inicialmente limpio. Referencias leídas: AGENTS local y de
origin/main, sistema visual V1 y referencia T08, USABILIDAD, T08-CUENTA-01 e issue #28.

## Inventario y cambios autorizados

Se conservan biblioteca de claves, apertura con botón/doble clic/Enter, guardado,
reglas, composición, cobertura, cierre/reapertura, cálculo y cancelación,
conciliación, circulación bruta, viabilidad, localización y trazabilidad.

- Antes, `LinesGrid.IsEnabled` dependía del permiso de editar; un cierre impedía
  incluso desplazar la tabla. Ahora el grid sigue activo y solo los editores se
  bloquean: casillas deshabilitadas y porcentajes de solo lectura seleccionables.
- Primario presenta cuenta con descripción, nodo destino, importe adjudicado y
  comportamiento. La regla aplicada se conserva en la ayuda de la celda.
- Secundario presenta nodo de origen, principal destino, importe y comportamiento.
  El resumen anterior muestra por principal el coste que conserva del primario,
  el recibido de auxiliares y el total. Incluye principales activos con coste cero.
- La selección muestra inmediatamente su cuenta/importe de origen, porcentaje
  efectivo, importe adjudicado y claves. Se elimina el botón redundante. La fila
  conserva el resaltado amarillo cuando el foco pasa al detalle. También se
  conserva al cambiar preferencias de presentación. Los importes brutos y ajustes
  del hecho final permanecen en la ayuda del importe del detalle.
- Las cabeceras numéricas se alinean con sus cifras. La viabilidad explica la
  existencia de salida hacia principales; pivote y factor de circulación quedan
  en la ayuda técnica, limitados a cuatro decimales y aclarando que no son coste nuevo.

## Exactitud económica

No se modifica el solver ni el documento publicado. `SecondaryResultPresentation`
agrupa los enlaces explicativos por origen para cada hecho final ya adjudicado.
Conserva exactamente su importe: atribuye el residuo numérico/redondeo a un origen
auxiliar con desempate estable por mayor aportación/ID, y usa `BalancedRounder`
con el quantum del estudio. Los costes ya asignados a principales se conservan.
La proyección es de consulta y nunca se persiste como un segundo reparto.

Las filas se agrupan por origen/destino/comportamiento. Fijo y variable continúan
separados; los abonos mantienen el signo. La circulación bruta no se suma a los
costes finales. El detalle explica el porcentaje efectivo a través de todas las
rutas, por lo que puede diferir del porcentaje de una ruta directa.

## Desplazamiento

Se encontraron dos causas en la interfaz: ScrollViewers de tablas dentro de la
página, y ayuda contextual inferior de altura variable actualizada por MouseEnter.

El comportamiento común de ventanas dirige la rueda a la página hasta que el
usuario entra en una tabla con clic o teclado. Llegar a un extremo de la tabla
consume el gesto sin desplazar la página. Clic/foco fuera devuelve la rueda a la
página. La barra lateral y desplegables abiertos conservan su contexto. Los combos
cerrados no cambian una elección por pasar la rueda. Se conservan las barras,
el teclado y la virtualización; la tabla usa desplazamiento por píxeles y acepta
deltas pequeños. Shift+rueda permite desplazamiento horizontal.

La ayuda inferior ocupa 48 unidades estables, con el texto completo en su ayuda,
para que los cambios de texto no redimensionen el viewport durante el scroll.

## Comandos y resultados locales

Desde la raíz del repositorio:

```powershell
git fetch origin feature/t11-reparto-secundario-cierre
git rev-parse HEAD origin/feature/t11-reparto-secundario-cierre
dotnet build Valdrio.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v minimal
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~T08|FullyQualifiedName~T09|FullyQualifiedName~T10|FullyQualifiedName~T11|FullyQualifiedName~Allocation|FullyQualifiedName~Reciprocal|FullyQualifiedName~Localization|FullyQualifiedName~PrimaryWorkspace|FullyQualifiedName~Composition" --logger "trx;LogFileName=results-scroll-final.trx" --results-directory artifacts/results-scroll
```

Compilación final: **0 errores, 0 advertencias**. Pruebas finales: **88 correctas,
0 fallidas, 0 omitidas**, 56 segundos. Incluyen tres regresiones nuevas de la
atribución por origen: positivos, abonos, ceros, redondeo, conservación por hecho,
conservación de coste en principal y orden de enlaces independiente.

Prueba WPF en una copia descartable de la empresa ficticia, sin modificar el
archivo de Descargas:

```powershell
$demoPath = Join-Path (Get-Location) 'artifacts/results-scroll/demo-final.valdrio'
$capturePath = Join-Path (Get-Location) 'artifacts/results-scroll/wpf-final'
$process = Start-Process -FilePath 'src/Valdrio.Desktop/bin/Release/net10.0-windows/Valdrio.Desktop.exe' -ArgumentList @($demoPath, '--results-scroll-only', "--capture-dir=$capturePath") -WindowStyle Hidden -PassThru
$process.WaitForExit()
$process.ExitCode
```

**Salida 0; 61 comprobaciones correctas**. ViewModels, servicios, publicación
SQLite y controles WPF reales. Eventos de rueda WPF, delta pequeño, activación de
tabla, extremos sin transferencia, salida de tabla y estabilidad del viewport al
cambiar la ayuda. Selección automática y conservación al leer el detalle. ES/EN,
anchos 960/1600 y escalado de layout 125/150/200 %. Publicación cerrada y cálculo
abierto presentan el mismo desglose por principal: total **146.900 EUR**.

Límite de la comprobación: eventos WPF sintéticos y escalado de layout; no simula
el firmware de todos los ratones/touchpads ni cambia el DPI del sistema operativo.

Incidencias de ejecución resueltas: fetch aislado sin red; MSBuild paralelo devolvió
fallo sin diagnósticos, sustituido por ejecución serial; se añadió el nuevo fichero
de proyección a los sources enlazados del proyecto de pruebas y se corrigió una
comparación de referencia en la comprobación nativa. Vstest aislado agotó 90 s al
conectar con testhost, sin ejecutar aserciones; se repitió localmente fuera del
aislamiento y pasó. Registros iniciales y TRX completos en `artifacts/results-scroll*`.

GitHub Actions: **no ejecutado, 0 USD**. Workflows existentes exclusivamente
`workflow_dispatch`; no se modifica facturación ni disparadores.

## Evidencias conservadas

- [Comprobaciones WPF](interaction.json)
- [Compilación](build.log) y [pruebas](tests.log)
- [Resumen y aportaciones, español](es-secondary.png)
- [Detalle automático y selección](es-explanation.png)
- [Conciliación alineada](es-reconciliation.png)
- [Viabilidad comprensible](es-feasibility.png)
- [Resumen, inglés](en-secondary.png)

Se compararon las capturas con la referencia T08: se conserva sidebar verde,
tarjetas claras, tipografía, tablas y selección amarilla del sistema visual.

# T11 — claves, ordenación y viabilidad automática

Fecha: 2026-10-11. Base: `5d9d047a338eea000f43381f5dd552244aea9d28`.
Rama local: `codex/t11-claves-viabilidad`. Destino: `feature/t11-reparto-secundario-cierre`, PR #35.

## Cambios solicitados

- Claves de primario y secundario: nombre, criterio y guardar juntos; las tablas
  ocupan el resto de la altura disponible, calculada desde el viewport. En la
  ventana de 1600 × 1000 la tabla de destinos pasa de 190 a unos 378 píxeles
  y muestra los seis nodos completos del ejemplo. Con criterio por unidades se
  conserva el selector de unidad. Las claves cerradas siguen siendo consultables.
- Asignar claves a cuentas: orden ascendente/descendente en cuenta, importe,
  clave y estado; el importe se ordena por decimal, no por texto localizado.
- Eliminada la tabla de conciliación por recurso de la interfaz secundaria.
  Permanecen los controles económicos y los datos de conciliación del motor.
- Viabilidad derivada del resultado real o de la publicación cerrada, sin botón
  ni una segunda resolución del grafo. Un error muestra diagnóstico y estado
  rojos, elimina resultados anteriores y no presupone que cualquier fallo sea
  un grafo inviable. Reabrir/cambiar configuración invalida el resultado anterior.
- Cerrar/reabrir secundario al comienzo de la pantalla, con la misma composición
  y posición que el botón del primario.

## Validación local Windows

```powershell
dotnet build Valdrio.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~T08|FullyQualifiedName~T09|FullyQualifiedName~T10|FullyQualifiedName~T11|FullyQualifiedName~Allocation|FullyQualifiedName~Reciprocal|FullyQualifiedName~Localization|FullyQualifiedName~PrimaryWorkspace|FullyQualifiedName~Composition" --logger "trx;LogFileName=keys-viability.trx" --results-directory artifacts/keys-viability
```

Compilación: **0 errores, 0 advertencias**. Pruebas: **88 correctas, 0 fallidas**.
Logs completos adjuntos. Testhost ejecutado fuera del sandbox para permitir su
conexión local. Sin instalación ni ejecución de servicios remotos.

Aceptación WPF sobre una copia desechable del fichero de empresa inventada:

```powershell
$dest = Join-Path (Get-Location) 'artifacts/keys-viability/wpf-v2'
$demo = Join-Path (Get-Location) 'artifacts/keys-viability/demo-v2.valdrio'
Copy-Item -LiteralPath 'C:/Users/cjrui/Downloads/VALDRIO_Empresa_Demo_Industrial_2026-10.valdrio' -Destination $demo
$p = Start-Process -FilePath 'src/Valdrio.Desktop/bin/Release/net10.0-windows/Valdrio.Desktop.exe' -ArgumentList @($demo,'--results-scroll-only',"--capture-dir=$dest") -WindowStyle Hidden -PassThru
$p.WaitForExit()
```

Salida 0, **91 comprobaciones correctas** en `interaction.json`: español/inglés,
ventanas de 960 y 1600 píxeles, escalado de layout 125/150/200 %, lectura de claves
protegidas, altura de destinos, cada cabecera en ambos sentidos, abonos negativos,
posición y funcionamiento del botón de reapertura, cierre tras recálculo,
publicación reabierta y cálculo editable con total 146900 EUR idéntico, viabilidad
automática, explicación y selección, propiedad del scroll y estabilidad de ayuda.
Se crean claves y reglas mediante los comandos reales para formar un circuito
auxiliar cerrado; el motor falla y la UI muestra el diagnóstico en rojo.

Las interacciones usan eventos WPF y comandos reales, con OnClick de cabecera
invocado por reflexión; no son pulsaciones de ratón físicas. El escalado de layout
no sustituye una prueba en monitores de diferente DPI. Se han inspeccionado las
capturas de claves, resultados y error. La empresa original de Descargas no cambia.

## Incidencias durante la comprobación

- Un primer intento de build usó `Valdrio.sln`: MSB1009. El repositorio utiliza
  `Valdrio.slnx`; el comando correcto terminó satisfactoriamente.
- El primer ensayo WPF pidió Invoke al AutomationPeer de la cabecera, que no
  expone ese patrón: NullReferenceException en el arnés. Se corrigió el arnés
  para invocar OnClick de DataGridColumnHeader. La ejecución completa posterior
  terminó con las 91 comprobaciones correctas.

GitHub Actions: **no ejecutado; coste generado 0 USD**. Workflow manual conservado.
No se fusiona main.

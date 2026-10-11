# Edición de composición en tabla — 2026-10-11

Base: `5b2ca278e8cc4d00f869da98342687ad66a5b53e`, rama remota
`feature/t11-reparto-secundario-cierre` (PR #35). Rama de trabajo:
`codex/t11-edicion-en-tabla`.

## Solicitud aplicada

- Comportamiento editable en la fila de cada cuenta mediante Fijo / Variable / Mixto.
- Porcentajes fijo y variable en celdas de la misma fila, visibles y editables solo
  para Mixto; Fijo usa 100/0 y Variable 0/100 automáticamente.
- Un único botón Guardar cambios; comienza desactivado, se activa al editar,
  se desactiva tras guardar o al restituir los valores originales.
- Los datos inválidos permanecen editables y no se escriben. La navegación admite
  guardar, descartar o cancelar; refrescar presentación conserva cambios pendientes.
- Eliminados del panel los porcentajes externos y acciones de clasificación que
  duplicaban la edición por fila. Se conserva el detalle de cuentas ya divididas.
- Preferencia permanente del propietario registrada en AGENTS.md y USABILIDAD.md:
  editar en tablas siempre que sea posible, sin formularios externos duplicados.

## Captura KEY_INCOMPLETE

La captura muestra una clave porcentual con un destino y suma cero. El dominio
rechaza correctamente su publicación. El editor ahora detecta esta condición antes
de llamar a persistencia: Guardar clave queda deshabilitado hasta que los destinos
sumen 100 y aparece una explicación localizada. No se relaja la regla del dominio.
La comprobación nativa anterior intentaba pulsar Guardar con 80 %; se actualiza para
comprobar el botón deshabilitado y el mensaje de corrección.

## Validación local

```powershell
dotnet build Valdrio.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false --verbosity minimal
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=table-edit.trx' --results-directory artifacts/table-edit
$captureDir = Join-Path $PWD 'artifacts/table-edit/wpf-final'
$p = Start-Process .\src\Valdrio.Desktop\bin\Release\net10.0-windows\Valdrio.Desktop.exe -ArgumentList @("--capture-dir=$captureDir", '--methodology', '.\samples\t01-smoke.valdrio-dev.json') -WindowStyle Hidden -Wait -PassThru
$p.ExitCode
git diff --check
```

Ejecución Windows local con datos sintéticos. GitHub Actions: 0 USD. Sin fusión a main.
Los logs y capturas se conservan en artifacts/table-edit y artifacts/table-edit-*.log.
Compilación final: 0 errores y 0 advertencias. Suite completa: 623/623 correctas, 0 omitidas (3 min 27 s).
Repetición T11 final: 13/13 correctas. Recorrido WPF final: salida 0, 470 comprobaciones correctas (incluidas 36 metodológicas). Captura de composición revisada: combo y porcentajes en fila, botón Guardar desactivado después del guardado. Se comprobó ocultación en Fijo/Variable, reactivación al editar y desactivación al restituir valores originales.

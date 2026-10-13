# VALDRIO — consolidación UX y apertura segura de archivos anteriores
Fecha: 2026-10-13

## Base y alcance

- Base: PR #43, commit `309a0799c562ec08f7d4b515c1f9ee55ae141b57`, derivado de `main@7be4189`.
- Rama consolidada: `fix/ux-consolidated-legacy-open-20261013`.
- No se han fusionado #40, #41, #42 ni #43 en `main`.
- Se retienen las correcciones del PR #43: importes monetarios conciliados sin cambiar el ledger, resultados visibles, recuperación de fases, claves, navegación y textos ES/EN. **Estas correcciones siguen pendientes de validación WPF sobre esta rama**.
- Se incorpora funcionalmente la asignación múltiple del PR #42, **no se integra su rama ni sus workflows de GitHub Actions**.

## Incidencia comunicada por el usuario: fallo al abrir .valdrio de una versión anterior

Contrato del servicio existente: `SqliteCompanyFileService.Open` valida el esquema *antes* de abrir en escritura. Para un esquema anterior (1–8 frente al 9 actual) devuelve `FILE_UPGRADE_REQUIRED`. La actualización existente requiere seleccionar el origen y una **copia nueva** como destino; nunca debe modificarse automáticamente el original.

Cambios:
1. `CompanyFileViewModel.OpenPath` controla además los errores al coordinar editores pendientes, fuera del bloque `Run`, y presenta un diagnóstico sin dejar escapar una excepción del controlador de UI.
2. Se ofrece un aviso de apertura visible en el entorno WPF normal. El diagnóstico de versión anterior conserva su texto específico y no se confunde con un fallo general.
3. Si la carga de los paneles falla después de crear una sesión candidata, se restaura la sesión previa y se libera la candidata; no se descarta el proyecto que estaba abierto.
4. Se muestra una acción de actualización explícita junto al aviso. La acción emplea la migración a una copia nueva ya existente, no una conversión silenciosa.
5. En el smoke WPF se añade un caso de apertura de una **copia desechable** con metadatos de versión 8 y comprobación de aviso, sesión previa e integridad del origen.
6. En MSTest se añaden casos de formato anterior y error de coordinación de cambios pendientes.

**Límite**: la copia con metadatos rebajados comprueba rechazo preventivo y seguridad de UI, **no** demuestra que todas las bases históricas reales migren correctamente. No se dispone todavía del archivo concreto que provocó la rotura ni de su stack trace. Si el fallo persiste hay que recopilarlo y reproducirlo sobre una copia, sin tocar el original.

## Asignación múltiple (P2)

- Selección múltiple de filas de cobertura por Ctrl/Mayús; elección de clave primaria **publicada**.
- Vista previa de seleccionadas/aplicables/omitidas e importe según la moneda del estudio, con confirmación antes del guardado.
- Asigna **solo partes pendientes** que no tengan clave, cobertura previa, asignaciones manuales o absolutas, ni una regla activa exacta/de prefijo aplicable. La operación se guarda mediante un solo `ChangeAllocations`.
- Protección por reevaluación de elegibilidad y no-escritura en repeticiones vacías.
- Pruebas MSTest para mezcla de pendiente/manual, idempotencia y regla exacta activa.
- No sustituye al editor avanzado ni modifica historiales económicos.

## Navegación adicional

- Acción «Volver a claves y reglas» desde «Detalle de cuentas», respetando cambios pendientes y conservando controles de editor coherentes al regresar.
- Acceso visible para actualizar archivo antiguo sin buscar en un panel avanzado.
- Comprobación estructural de que los textos ES/EN nuevos tienen idénticas firmas de parámetros y existen los handlers/bindings.

## Validación

**Realizada**: revisión estática del código leído en GitHub, incorporación de nuevas pruebas, comprobación ES/EN de nuevos recursos y firmas, comparación de ficheros/cambios y verificación de que la rama nace de #43 y no incorpora workflows one-shot.

**No realizada**: compilación .NET en Windows de esta rama, ejecución MSTest/WPF, inspección en 780/960/1440/1600 px a distintos DPI y reproducción del fallo sobre una empresa histórica genuina. La compilación Windows comunicada por el propietario corresponde a una compilación sin SHA de esta rama y no sirve como aceptación de los nuevos commits.

### Validación Windows solicitada al propietario

```powershell
git fetch origin
git switch fix/ux-consolidated-legacy-open-20261013
git pull --ff-only origin fix/ux-consolidated-legacy-open-20261013
dotnet restore Valdrio.slnx
dotnet build Valdrio.slnx -c Release --no-restore
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build
```

- Ejecución de pruebas nuevas: filtrar `PreviousFormatFileNotifiesUserWithoutReplacingCompanyOrChangingSource`, `FailureInPendingEditCoordinationCannotCrashCompanyOpen`, `BatchAssignsOnlyPendingPartsAndNeverOverwritesManualCoverage`, `BatchSkipsRowsCoveredByActiveExactRule`.
- Abrir desde el selector una **copia** del archivo real anterior. Debe mostrar un aviso útil sin finalizar VALDRIO; también se debe poder seguir trabajando con el proyecto previo o iniciar otro.
- Comprobar manualmente la actualización a un archivo nuevo con ruta distinta (solo si el origen es una versión legible reconocida), conservar la copia original, cerrar/reabrir la nueva y comprobar sus datos.
- Recorrido completo Horizonte: 5.500,00 € y 2 × 2.750,00 € visuales; subtotales conciliados. Repetir T13 servicios 17.500 € e industria 36.000 €.
- UI ES/EN, 780/960/1440/1600 px y 100/125/150/200 % DPI; botón de actualización, navegación de regreso y vista previa del lote.
- El rediseño adaptable integral y T12/historial deben verificarse por separado; siguen sin darse por cerrados.

## Seguridad económica

- No se ha ejecutado GitHub Actions ni modificado facturación. Gasto inducido: **0 USD**.
- Objetivo de ejecución: Windows local o runner self-hosted Windows. No ejecutar workflows GitHub-hosted sin autorización expresa.
- **No fusionar en main** hasta resultado satisfactorio y evidencia del recorrido real.

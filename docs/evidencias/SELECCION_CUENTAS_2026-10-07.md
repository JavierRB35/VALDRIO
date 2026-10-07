# T06 — Selección explícita de cuentas

Base: `8909e886bdcce7b1cb1ee5828ba8aa6b0a71d2f6`, rama
`codex/local-tests-and-workspace`. `main` remoto comprobado en `5b66e3c`.
Evidencia local con fuentes sintéticas, no aceptación de una fuente productiva.

## Resultado

Después de validar las columnas, el asistente permite incluir todas las cuentas
(comportamiento anterior) o desmarcar esa opción y elegir cuentas individualmente.
La lista incluye todas las cuentas validadas y conserva ceros iniciales, Unicode y
comparación ordinal. No se seleccionan cuentas por país o prefijos de numeración.
El propietario limita las fuentes de costes a pérdidas y ganancias.

Cambiar modo o casilla cancela una preparación en curso, descarta la preparación y
revoca su aprobación. Una lista explícita vacía deshabilita preparar. Releer/cambiar
archivo o mapeo elimina las marcas; mantiene el modo explícito para evitar volver a
importar todo inadvertidamente. Cambiar idioma/región conserva la selección y las
huellas. Los importes visibles se reformatean sin interpretarlos de nuevo.

Se reutilizan `ImportAccountSelection.ExplicitList`, perfil v1, preparado v1 y archivo
v4. No cambian esquemas ni migraciones históricas. La selección de la UI se ordena
ordinalmente antes de congelarla. PrepareImport usa un HashSet ordinal para evitar
búsquedas lineales repetidas en listas extensas, conservando la misma semántica.
Guardar conserva todas las filas/originales, incluidas las excluidas; recursos y
enlaces se crean exclusivamente para las filas elegidas. Los errores del archivo
siguen bloqueando su validación: excluir cuentas no oculta importes inválidos.

## Verificación ejecutada

- SDK .NET 10.0.401; solución Release completa, incluido WPF: 0 avisos y 0 errores.
- Pruebas dirigidas de confirmación: 41/41 correctas.
- Suite Release completa: 395/395 correctas, sin fallos ni omitidas.
- Cuatro regresiones nuevas: persistencia/reapertura/snapshot con exclusión de una
  cuenta de 9000 y conservación de cero/abono; invalidación y aislamiento de marcas
  antiguas; selección después de las primeras 50 filas y comparación ordinal;
  bloqueo ante una fuente inválida.
- Recorrido WPF real: empresa 28, modelos 53, estudios 73, recuperación 10,
  columnas 13, confirmación 26, Debe/Haber 22, editor FX 12; todos correctos.
- Las cinco comprobaciones nuevas de confirmación operan casillas reales y verifican
  selección vacía, inclusión/exclusión, revocación de aprobación, conciliación y
  conservación de evidencia excluida. Se conserva el recorrido de reapertura/backup.
- Runner sobre el snapshot creado por WPF: `status=verified`, dos recursos,
  tres filas leídas, dos seleccionadas, una excluida y neto 1250,5 EUR.
- Manifests de modelos, estudios, confirmación y Debe/Haber: SHA-256 comprobados.
- Captura `import-confirmation/accounts-es.png` revisada visualmente: casillas,
  cuentas, importes y selección visibles. ES con formato en-US conserva la elección.
- Verificador Windows actualizado para 26 comprobaciones y tres capturas de
  confirmación, incluidos los nombres esperados; sintaxis PowerShell verificada.

Artifacts locales: `artifacts/account-selection-2026-10-07`.
TRX: `tests/Valdrio.Tests/TestResults/account-selection-release.trx`.
SHA-256 del TRX: `261c170bbed66de0f466fed33475544543f9522039af226759c91cf6e1243409`.
El build Debug intentado no pudo copiar DLL bloqueadas por la aplicación abierta
y Visual Studio. Se mantuvo esa sesión abierta y se verificó Release por separado.
No se atribuye una compilación Debug correcta a esta entrega. No se ejecutó el
verificador Windows completo (publicación/recibo de checkout limpio) ni se revalidó
CI remota, UTC/Madrid o el corpus canónico en este turno.

## Continuidad

Esta subentrega completa la selección de cuentas en el asistente. T06 sigue abierta.
La agrupación requiere ampliar la relación persistente entre recurso y filas y
resolver descripciones distintas explícitamente; el formato actual mantiene una
fila por recurso. Siguen pendientes sustitución de lotes, diferencias comparables,
muestra representativa de pérdidas y ganancias y aceptación global. T06A/T07 después.

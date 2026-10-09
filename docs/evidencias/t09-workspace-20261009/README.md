# Correcciones del espacio de trabajo — 9 de octubre de 2026

Rama: `feature/t09-primary-allocation-20261008`, PR #33. Validación local en Windows,
sin ejecutar GitHub Actions. Datos sintéticos; no se incluye ningún archivo de empresa.

## Resultado

- Compilación Release de `Valdrio.slnx`: correcta, cero errores y advertencias.
- Suite completa `Valdrio.Tests`: **572/572**, ninguna omitida (3 min 16 s).
- Recorridos nativos WPF de recursos, claves/distribución y edición: correctos.
- Se mantiene el enlace de `AllocationWorkspaceViewModel.Primary.cs` en Tests,
  que resuelve el CS0103 de `RebuildPrimary`; Desktop compila la clase parcial completa.

## Comprobaciones de regresión

Preparación muestra únicamente importes y correcciones. Se comprobaron 19 cuentas,
ES/EN y anchos 960/1257/1600, con encabezados acotados y filas visibles. Se probó
escalado del contenido WPF al 125/150/200 %; no equivale a cambiar el DPI del sistema.

El recorrido guarda una clave al responder Sí, sale y vuelve sin nuevos avisos,
publica una regla para las 19 cuentas sin elegir un pool, cierra y reabre el archivo
con los 19 resultados. Las pruebas unitarias comprueban también formulario válido
aplicado al salir y formulario inválido conservado sin escritura.

La medición nativa incluye actualización del binding y espera a Dispatcher idle:
37 pulsaciones, media **1,52 ms**, máximo **8,76 ms**. Es evidencia para esta muestra
sintética, no una garantía para cualquier volumen o equipo. La prueba de edición
con 200 cuentas verifica que escribir no reconstruye cobertura ni listas de selectores.

El motor se prueba con correcciones, reglas, asignaciones manuales/absolutas,
cuentas sin regla, conciliación con pendientes de suma cero, reapertura y
compatibilidad de serialización/hashes de capturas históricas.

## Evidencia visual

- [Preparar cuentas](es-accounts-1257.png)
- [Claves y destinos](es-keys-19-accounts.png)
- [Resultados y reglas](es-results-19-accounts.png)
- [Comprobaciones y medición](interaction.json)
- [Decisión metodológica y conservación funcional](../../directrices/PREPARACION_Y_DISTRIBUCION_2026-10-09.md)

Reproducción de las pruebas: `dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release`.
El recorrido nativo admite `Valdrio.Desktop.exe <copia-desechable.valdrio>
--capture-dir=<directorio> --workspace-fixes-only`; requiere una copia de un proyecto
sintético con un modelo publicado, nodos principales/auxiliares y unidades.
No debe ejecutarse sobre un proyecto de usuario: el recorrido genera datos de prueba.

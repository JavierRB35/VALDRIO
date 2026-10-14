# T13 ↔ T12 — registro de aceptación local Windows (14/10/2026)

## Alcance y revisión

- Repositorio: `JavierRB35/VALDRIO`; PR **#53** (`feature/t13-t12-integration-20261014`).
- **SHA de código realmente probado**: `534cc6bd0cc2c1b547e4e26e6950427394fdf0ba`.
- La ejecución se hizo en Windows local mediante `pwsh -NoProfile -File .github/scripts/Verify-T13-T12.ps1`, con `-EvidenceDirectory` apuntando a `artifacts/t13-t12-20261014-194151`.
- Esta acta proviene de la **salida de consola comunicada por el propietario**; no se han leído físicamente el TRX ni las capturas en este entorno. Las evidencias permanecen en el equipo Windows y no forman parte de Git.
- GitHub Actions: **sin ejecutar** y sin autorizar gasto; `main` no se ha modificado.

## Resultado comunicado por el propietario

| Puerta | Resultado |
| --- | --- |
| Oráculo independiente T13: Servicios | **OK**, 6 cuentas, 2 principales, 2 auxiliares recíprocos, **17.500 EUR** |
| Oráculo independiente T13: Industrial | **OK**, 7 cuentas, 3 principales, 2 auxiliares recíprocos, **36.000 EUR** |
| Casos negativos del oráculo | **4/4**: porcentajes negativos, bucle cerrado, importe esperado incorrecto, código de cuenta duplicado |
| Restauración y compilación Release .NET 10 Windows | **Correctas**, todos los proyectos incluidos Desktop, Tests, Application e Infrastructure |
| Integración T13 parametrizada con T12 | **7/7 correctas**, 0 fallidas, 0 omitidas, 25,1 s; TRX `tests/Valdrio.Tests/TestResults/t13-t12-integration.trx` |
| Publicación WPF win-x64 | **Correcta**, salida `artifacts/desktop` |
| Smoke T13 Servicios (ES/EN) | **46/46 comprobaciones**; **8 PNG** |
| Smoke T13 Industrial (ES/EN) | **46/46 comprobaciones**; **8 PNG** |
| Resultado del script local | **`T13/T12 ACCEPTANCE PASSED`** para SHA `534cc6bd...` |

Las pruebas T13 incluyen la migración **9→10 en copia** de ambos archivos SQLite de referencia y conservación de su huella, el cierre T11, las ejecuciones T12 de las dos empresas, la ausencia de doble cómputo, la reapertura del histórico inmutable y los casos de costes directos/excluidos y agrupaciones. El smoke WPF cubre presentación de gráficos primario y secundario/final en ES/EN, selección, importes y porcentajes, teclado, ventana estrecha y no duplicación al recalcular la misma revisión.

## Evidencias conservadas localmente

- `artifacts/t13-t12-20261014-194151/generated/Service Company/services.valdrio`, `completed-t12-run.json`.
- `artifacts/t13-t12-20261014-194151/generated/Industrial Company/industrial.valdrio`, `completed-t12-run.json`.
- `artifacts/t13-t12-20261014-194151/visual-services/` y `visual-industrial/`, con cuatro capturas por idioma para cada empresa.
- TRX `tests/Valdrio.Tests/TestResults/t13-t12-integration.trx`, en el equipo Windows. 
- Las dos bases `Synthetic Projects/*/*.valdrio` versionadas continúan en **esquema 9** intencionalmente como fixtures de migración: no se han sustituido silenciosamente por los archivos generados.

## Validaciones adicionales necesarias para fusionar

**Aceptación específica T13–T12 superada; PR #53 aún NO fusionable por cierre incompleto.** Ejecutar sobre la misma revisión de código (solo se admiten commits posteriores de documentación) la suite global y el verificador Windows `Verify-Windows.ps1`, que debe confirmar **al menos 694 pruebas MSTest** previstas con los dos casos nuevos, cobertura general WPF y recibos de integridad. Revisar las capturas gráficas si se requiere aprobación visual humana. Registrar SHA y resultados, y pedir autorización de fusión antes de pasar la PR a lista para revisión.

No se afirma que los dos escenarios cubran todas las funcionalidades opcionales T13; esta acta certifica exclusivamente la integración parametrizada T13–T12 y las puertas ejecutadas que se han comunicado.

## Incidencia adicional: selección obsoleta de DataGrid durante el smoke WPF general (14/10/2026)

Tras superar la aceptación T13–T12 específica (7/7 MSTest y 92 checks WPF), el propietario ejecutó `Verify-Windows.ps1` sobre el SHA de código `534cc6bd...` (acta documental en `c0014bce...`). El WPF general se detuvo antes del recibo final de recursos. La excepción completa, leída de `company-failure.txt`, comienza con:

```
System.ArgumentNullException: Value cannot be null. (Parameter 'item')
   at System.Windows.Automation.Peers.DataGridCellItemAutomationPeer..ctor(Object item, DataGridColumn dataGridColumn)
   at System.Windows.Automation.Peers.DataGridItemAutomationPeer.GetOrCreateCellItemPeer(...)
   at System.Windows.Automation.Peers.DataGridAutomationPeer.RaiseAutomationCellSelectedEvent(...)
   at System.Windows.Controls.DataGrid.ChangingSelectedCellsHelper.Dispose()
```

El fallo se propagó desde `GuidedUi.Reveal(SaveDistributionButton)` vía `MainWindow.RevealWorkflowElement → UpdateLayout()`. `ResourceWorkspaceViewModel.RebuildRows` llamaba a `Rows.Clear()` y `Parts.Clear()` mientras `ResourceWorkspacePanel` contiene tres tablas virtualizadas vinculadas a estas listas. Al reconstruir, WPF puede retener un `DataGridCellInfo` cuyo elemento quedó nulo y fallar al generar los eventos de accesibilidad. La secuencia también puede alcanzarse al cambiar preset de idioma y orientación en este smoke.

**Corrección propuesta, pendiente de Windows:** introducir un evento `RowsRebuilding` antes de sustituir las colecciones; `ResourceWorkspacePanel` limpia sincrónicamente `SelectedCells` y `CurrentCell` en las tres tablas; `RebuildRows` conserva los identificadores de recurso/parte/ajuste **antes** de notificar para restaurar las selecciones lógicas después. No se desactiva accesibilidad, virtualización, controles ni cálculos. Requiere reproducir el smoke WPF general al menos dos veces y después repetir la aceptación global y evaluar resultados. Hasta entonces PR #53 sigue en borrador y **NO SE FUSIONA**.

## Cierre final y autorización de fusión — 14/10/2026

**Estado: aceptación técnica T13↔T12 COMPLETADA. El propietario autorizó expresamente fusionar si procede**, después de comunicar la aceptación integral Windows satisfactoria. La mención a pruebas pendientes en las secciones anteriores es histórica y queda sustituida por este cierre.

- **SHA del código y scripts validado por Windows:** `4cd3586074c86dd4432400ef9bc1f8d610417a96`.
- **Integración específica T13 con T12**: oráculo independiente Servicios **17.500 EUR** e Industrial **36.000 EUR** más 4 casos negativos; **7/7 MSTest** T13, sin fallos ni omisiones; **46/46 comprobaciones WPF ES/EN por empresa** y **8 capturas por empresa** (16 en total). Esta aceptación se ejecutó previamente en `534cc6bd0cc2c1b547e4e26e6950427394fdf0ba`.
- **Corrección de selección nula de DataGrid** en `4cd3586074c86dd4432400ef9bc1f8d610417a96`: compilación y publicación Windows correctamente concluidas; **dos ejecuciones del smoke WPF general consecutivas** con salida 0 y **49/49 controles de recursos T07** en cada una. No se reprodujo la excepción `DataGridCellItemAutomationPeer(item=null)`.
- **Aceptación integral Windows definitiva**: `.github/scripts/Verify-Windows.ps1 -EvidenceDirectory artifacts/t13-t12-final-20261014-200501`, con salida informada literalmente `Windows acceptance passed for source commit 4cd3586074c86dd4432400ef9bc1f8d610417a96.` y `ACEPTACIÓN INTEGRAL T13-T12 SUPERADA`.
- **Evidencias locales**: `artifacts/t13-t12-20261014-194151` (T13/12 específica), `artifacts/t13-t12-final-20261014-200501` (suite integral), `artifacts/t13-wpf-regresion-*` (doble regresión del DataGrid); TRX de T13 en `tests/Valdrio.Tests/TestResults/t13-t12-integration.trx`. Los ficheros de evidencia se han generado en el Windows del propietario, **no están versionados ni inspeccionados directamente en este entorno**.
- **Total exacto de MSTest globales**: el fragmento de consola compartido recoge el resultado final del verificador, pero no el total de `valdrio-tests.trx`. Se declara la **aceptación integral satisfactoria** sin inventar un recuento global.
- **Artefactos de referencia T13**: los dos `.valdrio` versionados de esquema **9** quedan intactos para comprobar las migraciones; los ejemplos esquema **10** con T12 terminado se generan de forma optativa en carpetas ignoradas.
- **Alcance**: se acredita la compatibilidad T13↔T12 para las dos empresas y los casos de regresión incorporados; esto no certifica cada funcionalidad opcional T13 ni sustituye la inspección visual humana detallada de los PNG, que no ha sido aportada.
- **Costes**: GitHub Actions no se ha ejecutado durante esta integración; 0 USD de gasto autorizado o provocado por la fase. La fusión no debe disparar workflow hospedado, ya que `.github/workflows/windows-ci.yml` está limitado a `workflow_dispatch`.

**Decisión:** apto para integrar en `main`, sujeto a confirmación del estado de GitHub y de que el commit siguiente sea documental. La rama de trabajo no se eliminará sin comprobar primero la fusión.

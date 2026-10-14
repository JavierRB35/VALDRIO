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

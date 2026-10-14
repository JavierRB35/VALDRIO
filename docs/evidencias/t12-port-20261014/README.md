# T12 — Portabilidad sobre main (14/10/2026)

## Base comprobada

- Repositorio: `JavierRB35/VALDRIO`
- Destino de trabajo: `feature/t12-runs-history-20261014` (rama separada; `main` sin modificar).
- Base: `6d746139c4d2be35aa0b4f0a14871b6bcb03ddd5`.
- Fuente funcional: PR #36, head histórico `8f3d24976502b4d7909268eee0e922cfbeed3229`. PR cerrada sin fusionar.
- La comparación con `main` mostró 251 commits adelantados en `main` y 2 commits exclusivos de T12.

## Cambios portados

- Contratos: `Application/CalculationRuns.cs`, `ICompanyFileSession.Runs`.
- Persistencia: migración v10, `RunDbContext`, servicio de ejecución y lectura, validación y cierre atómico.
- Presentación: `CostFlowPanel`, `CostFlowViewModel`, smoke y wiring de App/Company.
- UI vigente: navegación acordeón preservada, incorporando T12 como tercer paso de «Repartos y cálculo».
- Recursos: 54 claves nuevas ES y 54 EN sin duplicar claves anteriores.
- Tests: `T12RunTests.cs` y expectativas de migraciones actualizadas.

Los hunks originales en infraestructura y pruebas se aplicaron por coincidencia textual exacta sobre archivos actuales; el wiring visual se adaptó manualmente a las tres fases presentes. No se ha realizado una validación semántica en compilador.

## Resultados de verificación de esta rama

- GitHub: rama y commits comprobados.
- Inspección de cambios y adaptaciones: realizada.
- Compilación C#/.NET 10: **NO EJECUTADA** (SDK y Windows no disponibles aquí).
- Tests MSTest: **NO EJECUTADOS**.
- WPF ES/EN y screenshots: **NO EJECUTADOS**.
- GitHub Actions: **0 ejecuciones solicitadas**, sin cambios en workflows, runners o facturación.
- El resultado se considera **candidato no aceptado** hasta verificación nativa y revisión funcional.

## Evidencia histórica que no se extrapola

[PR #36](https://github.com/JavierRB35/VALDRIO/pull/36) registra validación local en otra revisión (634/634, 9/9 y 28/28); logs y capturas originales:
[docs/evidencias/t12-flujos-20261011](https://github.com/JavierRB35/VALDRIO/tree/8f3d24976502b4d7909268eee0e922cfbeed3229/docs/evidencias/t12-flujos-20261011).

## Criterio de entrega antes de fusionar

Ejecutar en Windows local `dotnet build`, suite completa, escenarios migrados y smoke WPF; resolver regresiones por los cambios T13, PR #47–#50, rutas y localización. Registrar hash de commit probado, resultados, errores reales, capturas y conformidad económica. No usar runners hosted sin consentimiento separado.

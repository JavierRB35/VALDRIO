# Continuidad local — 2026-10-06

Base GitHub/local: 5b66e3c1c452548044585d0580590274ec20c49b. Rama codex/local-tests-and-workspace.
Sin PR abiertas al iniciar. No se han disparado Actions, hecho push ni fusionado cambios.

## Cambios comprobados

- Runner junto a los tests y con su misma configuración; Debug ya no busca Release.
- Empresa abierta destacada; el borrador de una nueva empresa no cambia esa identidad.
- Detalles técnicos y ejemplos plegables; título de espacio de trabajo; textos EN/ES.
- Saldo íntegro al cierre como modalidad explícita y aprobada, 160→160, conservada al
  guardar/reabrir; idempotencia sin duplicación. Cambio de modalidad invalida preparación y aprobación.
- La modalidad histórica de cierre sin interpretación y la diferencia de acumulados permanecen bloqueadas.
- Verificador Windows actualizado de 24 a 28 checks de empresa y de 18 a 21 de importación:
  se añaden comprobaciones, no se eliminan expectativas anteriores.

## Ejecución local

SDK .NET 10.0.401. dotnet build Valdrio.slnx --configuration Debug/Release --no-restore:
ambas compilaciones completas, incluido WPF, sin avisos ni errores.
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj --configuration Debug/Release --no-build --no-restore,
con logger TRX específico. Primera pasada detectó una expectativa del título antiguo y otra
de tipo de excepción en el test nuevo; se corrigieron y las suites finales son las siguientes.
- Debug: 391/391, sin fallos ni omitidas. TRX SHA-256: 9ef860b231438b0b4ba6040caaacabc556f0e5dcb788e0df306b7873c28b3787.
- Release: 391/391, sin fallos ni omitidas. TRX SHA-256: c07ae14335f7fb078693e9166524ae283c03eebf77eb8f04c053cf45611dd858.

## Interacción WPF real

- company-file/interaction.json: 28 correctas.
- fx-editor-interaction.json: 12 correctas.
- import-columns/interaction.json: 13 correctas.
- import-confirmation/interaction.json: 21 correctas.
- import-debit-credit/interaction.json: 22 correctas.
- models/interaction.json: 53 correctas.
- studies/interaction.json: 73 correctas.
- studies/recovery.json: 10 correctas.

Aplicación Release ejecutada con --capture-dir y preferencias aisladas, salida 0.
Artifacts en artifacts/continuity-closing. Se verificaron los SHA-256 de cada archivo
de los manifests de modelos, estudios, confirmación y Debe/Haber.
Se conservan capturas EN/ES, regionales y pseudo/RTL, navegación de teclado y FX.
La revisión visual confirma que el nombre de empresa está visible y no hay recortes
en el resumen de importación revisado.

Esta es evidencia del árbol local, no un recibo de aceptación de un commit limpio:
no se ejecutó Verify-Windows completo (requiere checkout limpio y publish/manifest).
No se repitió la matriz UTC/Madrid ni el corpus matemático, no afectados por las fórmulas
de esta entrega. La carpeta .vs preexistente se conserva sin incluirla en los cambios.

## Continuidad

D03: el propietario identifica saldos de cierre y elige saldo completo (100→160 da 160).
Muestra real aún pendiente. Ver ADR-0008 y registro de decisiones.
T06 sigue abierta para agrupación, sustitución, diferencias comparables y aceptación global;
T06A y T07 continúan después. La autodetección de formatos numéricos no forma parte de esta entrega.
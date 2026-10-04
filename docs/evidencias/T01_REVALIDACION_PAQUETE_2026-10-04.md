# Evidencia T01 — revalidación contra paquete v0.4

Fecha: 2026-10-04  
Rama: `t00/reconcile-package-v0.4`

## Motivo

La primera T01 se implementó antes de reconciliar la ficha `tareas/T01.md` del paquete v0.4. La comparación detectó tres carencias:

1. textos WPF hardcodeados y sin validación bilingüe;
2. Runner compilado pero no ejecutado en CI;
3. artefacto `win-x64` framework-dependent.

Se corrigieron sin implementar las preferencias dinámicas de T01A.

## Ejecución de referencia

- Workflow: `windows-ci`
- Run ID: `37217066778`
- Commit: `f59feb8d709720efd1f323d47f3fa5583dea4fb7`
- Resultado: **success**

URL:
https://github.com/JavierRB35/VALDRIO/actions/runs/37217066778

## Resultado comprobado

- Build: **success**.
- Tests: **4 passed, 0 failed, 0 skipped**.
- Nuevas pruebas: catálogos inglés/español con mismas claves y placeholders; claves obligatorias no vacías.
- Runner: ejecutado correctamente desde CLI de desarrollo.
- Publish: `win-x64 --self-contained true`.
- WPF smoke: proceso vivo 6 segundos tras abrir el fixture.
- Artefacto: `valdrio-t01-win-x64`.
- Artifact ID: `11308518710`.
- Tamaño informado por GitHub: `68,358,723 bytes`.

Extractos:

```text
Build succeeded.
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4
VALDRIO Runner scaffold. Economic snapshot execution is not implemented in T01.
WPF launch smoke passed. Process remained alive for 6 seconds.
```

## Conformidad con T01

Implementado y verificado:

- módulos Domain, Calculation, Application, Infrastructure, Localization, Reporting, Desktop, Runner y Tests;
- SDK/paquetes fijados;
- CI Windows;
- shell VALDRIO;
- fixture accesible/cargado;
- recursos neutral inglés + español preparados para T01A;
- validación de claves/placeholders;
- Runner ejecutable;
- artefacto Windows autocontenido.

No implementado en T01:

- cambio dinámico de idioma/región/zona;
- preferencias por usuario;
- pseudotraducción/RTL;
- Money/FX;
- parsing internacional;
- persistencia productiva.

Esos puntos pertenecen a tareas posteriores, principalmente T01A/T02A/T06A.

## Límite de evidencia

El smoke automatizado demuestra arranque del proceso WPF. No sustituye una revisión visual humana ni una aceptación de UX.

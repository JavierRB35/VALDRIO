# Integración y organización de ramas — 2026-10-08

Candidato validado: `60503282761564deeb12e9a1248189c02ace9093`.
Base main conservada: `82770779478a71c6ed7edc89a86be02fbb0188a7`.
Base funcional: `8bcd47ee07886805fd904af9363650352f5ab13d`.

## Integración

La PR #22 reúne T06/T06A, T07, usabilidad, tema visual V1 y correcciones de auditoría.
Orden histórico: #25/#26 hacia T07; #24 hacia local-tests-and-workspace;
#27, #29 y #30 hacia local-tests-and-workspace; reconciliación con main y PR #22 hacia main.
Solo AGENTS.md y README.md tuvieron conflictos. Se conserva íntegro AGENTS.md de
la rama funcional, que incluye las directrices de main, y README combina la obligación
visual con el alcance real de V1. Diseño, referencia T08 y T08-CUENTA-01 son idénticos
a main. No se modificó código de negocio durante la reconciliación.

Se corrigió el verificador Windows: ventanas auxiliares ocultas y recuentos exactos
actualizados para las suites WPF ya ampliadas (modelos 57, columnas 16, recursos 44,
recorrido 20, auditoría 27, estudios 74). Ninguna comprobación se eliminó.

## Validación local

- Release: cero errores y advertencias; 544/544 pruebas, cero omisiones.
- 57 fuentes canónicas y 39 referencias matemáticas verificadas.
- Guards y runners T02/T02A pasados.
- Arranque WPF y recuperación de preferencias inválidas pasados.
- 425 comprobaciones WPF pasadas, incluidas 59 de shell visual; capturas revisadas.
- Snapshots, manifest de modelos/estudios/importaciones y hashes verificados.
- Recibo: [windows-verification.json](integracion-main/windows-verification.json).
- Ejecución: Verify-Windows.ps1 completa; tras corregir solo el verificador,
  PrepareBuildManifestOnly y UseExistingBuild reutilizaron binarios y TRX comprobados.
- Este documento posterior al candidato no altera código ni binarios.

## Organización

`main` es la base de integración. `codex/local-tests-and-workspace` conserva el
historial funcional y la reconciliación. Las ramas de PR cerradas se mantienen como
referencias históricas: no se reintroducen commits antiguos solo porque una fusión
por squash no produzca relación de ancestro. No se eliminan ni fuerzan ramas.

Única PR abierta al revisar: #22. Las ramas históricas sin PR abierta no se presentan
como trabajo nuevo pendiente ni se integran indiscriminadamente. El inventario
adjunto conserva sus SHA y la relación de ancestro con el candidato.

La copia local `valdrio-ux`, en `codex/t08-allocation-keys`, contiene modificaciones
T08 sin confirmar. Está preservada y no se incluye en esta integración: necesita
su propio cierre y validación antes de integrarse. La aceptación de este candidato
no acredita esos cambios locales.

## Pendientes y coste

T08-CUENTA-01 / issue #28 continúa obligatorio y pendiente de evidencia completa.
V2–V6, D03 productivo, catálogo monetario completo y fases económicas posteriores
mantienen sus pendientes. V1 no acredita conformidad visual integral.

No se ejecutó ni solicitó ningún workflow. windows-ci queda exclusivamente manual;
los commits de reconciliación/publicación incluyen [skip ci].

[Inventario de ramas](integracion-main/ramas.md).

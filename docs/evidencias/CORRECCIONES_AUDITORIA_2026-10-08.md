# Correcciones de auditoría — 2026-10-08

## Alcance y trazabilidad

Correcciones autorizadas por el propietario tras la auditoría del repositorio.
Base funcional: `codex/local-tests-and-workspace@dc53b55aa5806f45e14c2fb64d6b7b5c43e9a7d2`,
PR #22. Directrices contrastadas con `main@82770779478a71c6ed7edc89a86be02fbb0188a7`:
AGENTS, usabilidad, ADR-0001, T06/T06A/T07, sistema visual V1 y T08-CUENTA-01.
Rama de corrección: `fix/auditoria-20261008`, propuesta sobre la rama activa.

Commit que contiene las fuentes comprobadas: `d32a8ed83bad8a9750aea4efeb3e2ba6a0a691bc`.
La prueba local precede a su publicación; los hashes de fuentes, binarios y recibos
permiten contrastar el contenido. [validation.json](audit-corrections/validation.json)
registra resultados y límites; [tests.json](audit-corrections/tests.json) conserva
los 544 resultados extraídos del TRX, sin presentarlos como aceptación WPF.

## Matriz de hallazgos

| Hallazgo | Corrección y referencias | Estado de aceptación |
|---|---|---|
| A01 · alto | `ImportOriginOverlap` en Application; comprobación dentro de `ConfirmImport` antes de escribir. Detecta registros/celdas repetidos entre lotes activos, con independencia de selección, aprobación y FX. Conserva las selecciones disjuntas, columnas financieras distintas y sustitución identificada. | Reproducción .NET/SQLite y cinco regresiones correctas. |
| A02 · alto | `ResourceCorrectionCommand` valida revisión y estado autoritativos; `ResourceCorrectionRules` calcula delta/partes. El ViewModel conserva parsing, interacción y guardado automático. | 50 → 20 produce −30; repetir 20 no escribe; revisión obsoleta rechazada; originales y clasificación conservados. |
| A03 · alto | Scanner compartido en `.github/scripts/Get-ProductionMessageKeys.ps1`, usado por validación local y workflow. Su regresión excluye prefijos `StartsWith` y mantiene una clave real deliberadamente ausente. | PowerShell ejecutado: 345 claves de producción cubiertas en ambos idiomas. Autoprueba integrada en ambos recorridos. |
| A04 · alto | Directriz vigente de main incorporada en `docs/directrices/T08_CUENTAS_DESCRIPCION.md` y AGENTS. | Dependencia funcional abierta en issue #28; no se declara implementada. |
| M01 · medio | `MainWindow.xaml`: grupo de redondeo en la fila 4 válida. Smoke nativo ampliado para abrir herramientas ES/EN y probar el contenedor interior a escala 100/125/150/200 %. | XAML y compilación correctos; ejecución y revisión visual Windows pendientes. |
| M02 · medio | Tabla de Recursos: columna Estado de asignación y contador basado en `HasPendingParts`, conservando el resumen monetario. | Cero original y pendientes +100/−100 siguen visibles como pendientes; exclusión explícita de cero queda completada. Regresión ES/EN correcta. |
| M03 · medio | `ResourceWorkspaceViewModel.Revalue.cs`: construye tipos, elecciones y preview en variables locales antes de publicar la nueva fase. | El desbordamiento conserva edición, motivo y archivo; corregir el tipo permite recuperar la preview. |
| M04 · medio | `StudyWorkspaceViewModel.CurrentModelDescription` resuelve la versión del estudio; Guidance la usa fuera del editor. | Propiedad probada contra estudio guardado, independiente del selector. Recorrido WPF con editor B y estudio/modelo A pendiente. |
| M05 · medio | Encabezado vigente de `ESTADO_PROYECTO.md`, ficha T06A y AGENTS reconciliados; cifras antiguas conservadas como históricas. Alcance de PR #22 actualizado al publicar. | Evidencia propia del candidato; las aceptaciones históricas no se extrapolan. |
| M06 · medio | Cantidades de la tabla de Recursos heredan `Text.Number` y alineación derecha; se conservan columnas, controles y eventos. | Mejora incremental. Migración completa V2–V6 y aceptación visual pendientes. |
| B01 · bajo | Retirada `CurrencyOptions`, sin consumidores. Las nuevas pruebas de corrección económica usan Application/Calculation. | Depuración parcial: APIs históricas de ajustes aún usadas por pruebas/smoke y revaloración se conservan hasta migrar esos consumidores. |

## Reproducción económica y alcance del control de origen

Antes de añadir el control se ejecutó el servicio real con SQLite: importar 280
y añadir después los mismos registros junto con 78,35 produjo **638,35**, contando
280 dos veces. El test esperaba rechazo y falló; su salida y hash quedan en el recibo.
Con la corrección el segundo append falla con `IMP_SOURCE_ROWS_REUSED` sin cambiar
el archivo. Importar solo el registro restante o sustituir el lote identificado
produce **358,35**, conservando historia y reapertura.

La identidad controlada combina hash del archivo, hoja, tipo/dialecto de origen,
columnas financieras/código, periodo y papel actual/comparativo. Un tipo FX distinto
no transforma las mismas celdas en otro origen. Las columnas financieras diferentes
siguen siendo valores distintos; actual y comparativo mantienen su procedencia
separada. La protección evita nuevas confirmaciones solapadas: **no reescribe ni
repara automáticamente archivos históricos** que ya tengan doble cómputo, y no
detecta archivos de bytes distintos con contenido económico equivalente.

## Comprobaciones ejecutadas

- SDK fijado en `global.json`: .NET 10.0.401; entorno Linux. Suite Release:
  **544 ejecutadas, 544 correctas, 0 fallos y 0 omitidas**, incluidas diez nuevas
  regresiones económicas/de estado. Recibo original: `audit-corrections-final.trx`.
- Desktop con destino `net10.0-windows`, `EnableWindowsTargeting=true`:
  **0 errores y 0 advertencias**. Es compilación cruzada, no ejecución de WPF.
- PowerShell 7.6.6: autoprueba del scanner, cobertura de 345 claves,
  992 recursos por idioma, placeholders coincidentes y tres scripts sin errores
  de sintaxis.
- XML/XAML, nombres de controles y límites estáticos de filas comprobados;
  controles con nombre conservados. Guards de tipos económicos y tiempo ambiente
  correctos; `git diff --check` correcto.
- 325 archivos disponibles sin cambios cotejados con sus blobs del HEAD remoto.
  La publicación parte del árbol Git real y conserva los demás archivos/binarios.
- GitHub Actions permanece con `workflow_dispatch` exclusivamente. No se ejecuta
  ningún workflow por esta entrega.

Los comandos de prueba y los hashes se registran en `validation.json`. Se limita
el paralelismo de MSBuild en este host sin cambiar la configuración del repositorio.

## Puertas de aceptación pendientes

1. Ejecutar la aceptación local de Windows en un checkout limpio del candidato:
   `pwsh -File .github/scripts/Verify-Windows.ps1 -EvidenceDirectory artifacts/audit-corrections-windows`.
   Revisar las capturas de herramientas abiertas, columna de estado/alineación,
   teclado, ES/EN, ventana estrecha y escalas. La ampliación de layout no acredita
   cambio entre monitores con DPI distintos.
2. Comprobar el contexto con dos modelos distintos y el error/recuperación de
   revaloración en la aplicación WPF real. Los tests portátiles no prueban el árbol
   visual ni sus bindings en ejecución.
3. Resolver captura semántica de descripción, política de ausencias/conflictos,
   persistencia, reapertura y consumo antes de cerrar T08-CUENTA-01 (issue #28).
4. Completar V2–V6 según el plan oficial y migrar los consumidores históricos antes
   de retirar las APIs restantes del formulario antiguo. D03 productivo y catálogo
   monetario completo mantienen sus pendientes oficiales.

La rama mejora los defectos reproducidos y su trazabilidad. Permanece como candidato
de integración; no acredita conformidad visual integral, cierre de T08 ni validación
de políticas económicas futuras.

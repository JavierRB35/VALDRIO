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

## Incidencia nueva: apertura de archivo anterior (14/10/2026)

El usuario detectó que Visual Studio pausaba en el lanzamiento de `FILE_UPGRADE_REQUIRED` al abrir un proyecto antiguo. La excepción ya se capturaba en `CompanyFileViewModel.Run`, pero el caso normal pasaba por un `throw` antes de ofrecer la actualización.

Se ha incorporado `ICompanyFileService.RequiresUpgrade` y su lectura SQLite en modo solo lectura, sin escribir ni abrir el archivo con esquema actual. La interfaz anticipa la situación, conserva la ruta elegida y ofrece actualizar a una copia nueva sin volver a seleccionar el origen. El título del aviso se distingue de errores auténticos (ES/EN). El comando de actualización conserva la ruta original y exige destino nuevo; cancelar no altera el origen.

Prueba nueva: `OpeningGenuineOldFileOffersUpgradeToNewCopyWithoutReSelectingSource`, sobre fixture histórica v3, con verificación de hash del origen, cancelación, migración a versión 10, reapertura y ausencia de segundo selector del origen. Se amplía también el smoke WPF para comprobar que aparece la acción. **Pruebas nuevas aún no ejecutadas en Windows**; no se presume superada la aceptación por revisión estática.

## Incidencia: código de centro duplicado durante preparación (14/10/2026)

La captura del usuario muestra `MODEL_DUPLICATE` en `SqliteCompanyFileService.Session.CreateCenter` cuando existe el código de un centro físico. El repositorio ya capturaba `DomainRuleException` en `ModelWorkspaceViewModel.Run`, pero el depurador se detenía en el `throw` original. Se ha añadido comprobación previa de código de centro repetido en el ViewModel, comparando literalmente con todos los centros guardados, incluso desactivados, y excluyendo el ID del propio centro editado. La pantalla muestra ayuda de corrección junto al código y deshabilita guardar mientras coincida. El servicio SQLite mantiene la validación final contra carreras de concurrencia.

Se incorpora `DuplicatePhysicalCenterCodeIsShownBeforeStorageAndUniqueCodeCanBeSaved` para probar código repetido al crear y editar, desbloqueo con uno nuevo, persistencia y no-escritura en caso duplicado. **No se modifica ni restringe la selección de cuentas a importar:** varias líneas con la misma cuenta siguen agrupándose según el contrato de importación. La relación causal concreta entre el flujo de importación del usuario y `CreateCenter` todavía debe reproducirse en WPF.

Sin compilación ni pruebas Windows todavía.

## Claridad del importe efectivo y gráficos T12 (14/10/2026)

Conforme a las capturas del propietario, la fase «Preparar cuentas» destaca solo la columna «Importe efectivo (base de reparto)», con superficie verde suave y cifra seminegrita, conservando selección, formato y edición de correcciones. La misma jerarquía distingue el subtotal efectivo. Los mensajes ES/EN explican importado convertido + ajustes manuales + ajustes por reglas. **No se modifica el libro mayor, los resultados o la mecánica económica de distribución**.

T12 separa ahora el antiguo gráfico de cuatro etapas con lienzo rígido 1100 × 1350 en **primario** (cuenta → centro) y **secundario/final** (centro → circulación auxiliar → principales). Se usan los importes persistidos, mostrando solo en el gráfico dinero con dos decimales y porcentajes con dos decimales; tablas e historial conservan los importes completos. La disposición envuelve nodos por filas conforme al ancho disponible y permite desplazamiento al faltar espacio. Al seleccionar un nodo o transferencia, la selección ilumina las rutas upstream/downstream por ambos gráficos, usando un recorrido de grafos con conjunto de visitados para ciclos recíprocos; las transferencias internas siguen tratándose como brutas no aditivas. Selección y foco por teclado permanecen activos.

Se amplía el smoke WPF de T12 para verificar ambas vistas, estado de ruta iluminada, porcentajes, moneda, cambio de idioma y ancho estrecho. **Ejecución WPF, build .NET y pruebas de regresión aún pendientes.**

## Evitar ejecuciones duplicadas e historial (14/10/2026)

Al pulsar Calcular en T12, el ViewModel realiza primero una consulta **solo lectura** y verifica que existe un resultado `Completed` para la **misma revisión congelada del estudio** y la versión vigente de algoritmo. Si existe, selecciona ese resultado y presenta «No hay cambios» sin capturar otro snapshot, ejecutar el solver o publicar otra ejecución. Se preservan los intentos `Failed`, `Cancelled` y `Started`; no sirven como resultados a reutilizar. Los resultados de revisiones previas tampoco bloquean un cálculo nuevo. Esta comprobación conservadora se limita a la identidad de revisión/algoritmo: no deduce equivalencia entre revisiones distintas.

El propietario solicitó además poder eliminar ejecuciones a voluntad. **No se ha autorizado ni implementado un borrado físico**: el formato v10 protege con triggers las ejecuciones e historial. La opción propuesta es archivar/ocultar de la lista normal con recuperación y auditoría, lo que exigiría persistencia de visibilidad separada (sin mutar resultados); requiere aprobación específica del diseño de retención antes de cambiar el esquema.

Se actualiza el smoke WPF para esperar la reutilización sin duplicado, y se añade prueba T12 de reconocimiento de revisión completada y ausencia de captura al consultar. Validación .NET/Windows pendiente.

## Criterio de entrega antes de fusionar

Ejecutar en Windows local `dotnet build`, suite completa, escenarios migrados y smoke WPF; resolver regresiones por los cambios T13, PR #47–#50, rutas y localización. Registrar hash de commit probado, resultados, errores reales, capturas y conformidad económica. No usar runners hosted sin consentimiento separado.

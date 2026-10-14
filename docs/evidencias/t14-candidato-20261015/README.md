# T14 — Entrega candidata parcial, 15/10/2026

**Rama:** `feat/t14-physical-observations-20261015`. **Base:** `main` tras el PR #54 de las 16 decisiones T14.1. **Estado:** candidato de T14.1 + captura manual inicial T14.2; **no certificado, no fusionar hasta validar localmente en Windows y completar fases pendientes**.

## Código propuesto (no equivale a aceptación)

- **Application:** observaciones físicas tipadas, `null` diferente de cero, fuentes y calidad normalizadas, confirmación solo con normal y real presentes, cero normal justificado, excepción real>normal y normal=0 con real>0, razón obligatoria para corregir confirmado; calculadora física pura de calendario y promedios sobre períodos expresamente comparables.
- **Infrastructure:** archivo SQLite de empresa **v11** con migración sobre copia desde archivos históricos, registro de cada versión **completa e inmutable** por estudio/modelo/pool/período y auditoría trazada en la misma transacción. Controles de integridad de JSON/hash y cadena de auditoría, versión física esperada, revisión de estudio esperada, rechazo de escrituras de estudio cerrado, anulación sin sustitución por cero. No modificar `Study.Revision` al guardar solo horas.
- **Desktop:** primer diálogo WPF T14 accesible desde el estudio guardado, con tabla por nodo principal, unidad, normal, real, utilización y coste terminal **T12 vigente** de solo lectura; procedencia/calidad, guardar borrador, confirmar, anular, revisar historial y mensajes ES/EN. **Pantalla todavía candidata**: exige prueba real con teclado, datos, DPI, UX y estilos de VALDRIO.
- **Pruebas redactadas:** versionado, corrección de confirmado, conflicto, anulación y reemplazo posterior, archivo reabierto, estudio cerrado, cero normal, exceso de actividad, borrador incompleto, reversión de transacción, contexto de período cambiado, comparación/calendario y preservación de ejecución/hashes económicos T12. Las aserciones de esquema histórico (v10) se actualizan a v11 sin modificar resultados matemáticos esperados de T12.

## Pendiente antes de afirmar «T14 completa»

1. **T14.1:** compilación .NET 10/EF Core y prueba real de migraciones desde archivos v10 y antiguos; contrastar toda la suite de regresión, rendimiento, restricciones y serialización; definir la lectura **verificada** de coste T12 económicamente compatible tras cierres/reaperturas, sin falsificar vigencia.
2. **T14.2:** afinar UX con edición y validación directa en tabla, indicadores/ayuda contextual detallada, permisos, estados y revisión WPF accesible ES/EN; comparación con referencia visual obligatoria, flujos de cancelación y cambios sin guardar.
3. **T14.3:** asistentes visuales completos **histórico / calendario / cifra declarada**, parámetros con evidencia reproducible, períodos anómalos revisados, jornadas equivalentes, referencias de estudios anteriores y capacidades heredadas T07 para adopción expresamente revisada.
4. **T14.4:** importación **física** CSV/XLSX independiente de T06 contable, previsualización de períodos/unidades y duplicados, conservar/sustituir con auditoría, sin sumar solapamientos ni duplicar total maestro; pruebas de casos internacionales.
5. **Aceptación:** recorrido Windows manual con datos reales y suites `dotnet restore`, `dotnet test Valdrio.slnx -c Release` (y compilación WPF), captura y reporte de resultados antes de cualquier fusión. Cumplir la ficha canónica T14, CM08, casos C15/C16/C18/C19/C31 y las 16 decisiones. No alterar D04/T15.

## Evidencia efectiva y limitaciones

- Se revisaron directamente los contratos existentes `ICompanyFileSession`, `IRunStore`, modelos publicados, migraciones v1–v10, `RunHistory`, tests existentes, localización y directrices de diseño/usabilidad.
- Se revisó la configuración de `.github/workflows/windows-ci.yml`: solo admite `workflow_dispatch`. Los cambios de esta rama usan `[skip ci]`; **no se ejecutó GitHub Actions**, no se solicitó trabajo facturable ni se cambió el workflow. Coste autorizado: **0 USD**.
- En el entorno disponible **no están instalados `dotnet`, compilador C# ni MSBuild**; no existen resultados de ejecución C#, MSTest o interfaz WPF atribuibles a este candidato. Las pruebas están **escritas**, no ejecutadas. No confundir inspección estática con compilación.
- No se ha modificado la especificación canónica `specification/v0.4/`, ni las políticas económicas D04, ni se ha fusionado el código con `main`.

**Criterio de continuación:** validar localmente antes de dar por aprobada la migración. En caso de fallo, preservar intactos los archivos `.valdrio` originales y corregir primero en esta rama de código. No fusionar por comodidad o para sortear un test.

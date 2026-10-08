# Promoción de T08 y consolidación de ramas — 2026-10-08

El propietario autoriza revisar todas las ramas y llevar los avances completos a
main. Base principal `79087a588ceb82cc4f4bfc1ca9cc03045655068d`; origen workspace
`3cf853a76d7ba2364f7d40f5c4dafdc07466e609`.

Se sincronizaron explícitamente todas las referencias remotas, porque el fetch
configurado solo incluía workspace y la rama de seis problemas de usabilidad.
La consulta a GitHub devuelve 33 ramas y ninguna PR abierta antes de la promoción.
Las ramas activas coinciden con entregas fusionadas; no hay avances posteriores a
sus integraciones. Las referencias históricas se conservan, sin borrar trabajo.

## Ramas e integraciones

- T08: PR #31, fusionada en workspace; recibos posteriores exclusivamente documentales.
- Auditoría actual: PR #30; diseño visual: PR #29; seis problemas de usabilidad: PR #27.
- T07 y auditoría de usabilidad: PR #24/#26; editor: PR #25. La referencia del editor
  apunta al propio commit de integración #25 y está incluida en la entrega #24.
- T06 y preparaciones de importación: PR #16–#21; auditoría de cierre: PR #17;
  estudios/modelos/empresa: PR #12–#14; todas sus referencias están integradas.
- T00–T02A y revisiones R01–R07: PR #1–#10; sus ramas conservan cabezas históricas
  anteriores a las fusiones squash. No es correcto repetirlas porque no sean ancestros.
- `audit-2026-10-05-a01-a06` es la variante histórica de auditoría: el cierre
  posterior #11 incorpora A01–A06 con regresiones y documentación de resolución.
  Se conserva como referencia, sin reemplazar la implementación auditada posterior.
- `t02a/fx-rates` y `t02a/fx-rates-final` son etapas previas de T02A.3; la entrega
  completa #10 incluye Runner, editor y controles de teclado, con correcciones
  posteriores #11. Su implementación antigua no constituye una fase pendiente.
- PR #23 fue cerrada sin fusionar; su rama remota ya no existe. El diseño aprobado
  vigente es V1 (#29), conservado junto con su plan de migración.

## Validación de la promoción

La fusión local sobre main se resuelve automáticamente. El árbol resultante es
idéntico a workspace antes de añadir este registro. Se comprueba igualdad exacta
de `src`, `tests`, `.github`, `specification`, AGENTS y documentación de diseño
con el candidato aceptado. No hay modificaciones funcionales de integración.

Se reutiliza la aceptación local completa del mismo código ejecutable:
[recibo](t08-workspace/verification.json), commit `c1e6877`, 561/561 pruebas,
467 comprobaciones WPF, 57 fuentes y 39 referencias matemáticas; Runner, snapshots
y manifests verificados. Los cambios posteriores son documentación/recibos.
No se repite la suite sin cambios de código ni fallos pendientes.

GitHub Actions permanece exclusivamente manual; no se ejecuta ningún workflow.
T08 incluye claves versionadas, reglas, cobertura, nombres/procedencia y snapshots.
Las fases posteriores, decisiones económicas propuestas y adaptación visual V2–V6
siguen su planificación; no son cambios desarrollados que queden fuera de main.

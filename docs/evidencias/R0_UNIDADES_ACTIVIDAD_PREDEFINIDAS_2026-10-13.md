# R0 — Catálogo de unidades de actividad (13/10/2026)

- 20 unidades iniciales: códigos internos ingleses invariantes, dimensión, símbolo, escala, discreción y traducciones ES/EN en recursos.
- Los proyectos nuevos creados desde el formulario WPF instalan las unidades usando `AddUnits` (auditoría incluida), sin cambiar el esquema SQLite ni los hashes económicos de modelos publicados.
- Proyectos existentes: acción explícita para incorporar solo códigos ausentes. No se cambia ni renombra una unidad previa, incluso si coincide el código con un preset; los proyectos históricos no se modifican al abrirlos.
- Los selectores de modelos y nodos traducen nombres de presets según la preferencia del usuario. Unidades propias conservan su código. Se mantiene selección explícita por nodo y no hay conversiones automáticas ni nuevas tarifas.
- Pruebas MSTest añadidas sobre catálogo, ES/EN, idempotencia, SQLite y auditoría. Smoke WPF adaptado a 20 unidades iniciales más una propia.
- **Pendiente:** compilación .NET 10 y prueba WPF real en Windows (idiomas, teclado, DPI, selector, persistencia y regresión); no se certifica la aceptación por revisión estática.
- GitHub Actions solo `workflow_dispatch`. No se ha lanzado ningún workflow ni autorizado consumo facturable.

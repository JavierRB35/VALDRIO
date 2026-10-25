# Revisión documental y fusión T18 — 25/10/2026

El propietario solicita revisar la documentación y fusionar en Git. Referencia funcional revisada: `928b263`, sobre `main@dd1f6f7`.

Se corrigen README, estado, inventario funcional, matriz de comprobación y ayuda ES/EN para describir los cinco grupos del menú, los Pareto de ambos repartos, las órdenes estimadas y sus versiones congeladas. Se distinguen el subtotal parcial de Productos y servicios y la valoración de materiales y actividad en Órdenes estimadas. Se actualizan el catálogo de ayuda a 17 vistas y el formato de archivo a 13, mediante copia. T19 y T32 siguen pendientes. Los cortes históricos mantienen sus fechas y resultados.

Validación de esta revisión: `python scripts/verify_help.py` verifica 17 vistas y 17 XAML; las 4 pruebas `ContextHelpCatalogTests` pasan con los documentos empaquetados. `git diff --check` sin incidencias. No se cambia código, pruebas ni workflows, por lo que se conserva la evidencia económica previa de 776/776 pruebas y 25/25 comprobaciones WPF; no se declara una nueva ejecución completa de ellas.

Fusión autorizada en main y publicación en GitHub. Sin ejecución de GitHub Actions: 0 USD.

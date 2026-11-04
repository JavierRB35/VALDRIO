# VALDRIO

> **Entrega T18:** órdenes estimadas con aceptación inmutable, historial y comparación, junto al menú organizado y los Pareto de ambos repartos. [Guía de órdenes](docs/help/16-ordenes-estimadas.md) · [Validación local](docs/evidencias/t18-ordenes-20261025.md).

Aplicación de escritorio para Windows (WPF/.NET 10) que guía al analista desde el proyecto y el modelo de costes hasta los repartos, la actividad/capacidad, los productos y las ejecuciones económicas. Los proyectos se guardan en archivos `.valdrio`.

> **Referencia funcional:** `928b263` (25/10/2026). El [inventario funcional](docs/revision/REALIDAD_FUNCIONAL_2026-10-25.md) distingue funciones disponibles y límites; la [matriz de comprobación](docs/revision/PLAN_VERIFICACION_MANUAL_2026-10-25.md) conserva pendientes manuales sin sustituirlos por pruebas automáticas.

## Navegación disponible

El menú tiene cinco grupos desplegables independientes. El acceso a una pantalla no significa que sus datos estén completos.

| Grupo | Pantallas |
| --- | --- |
| **Proyecto** | Proyecto · Estructura de costes |
| **Datos del análisis** | Análisis y periodo · Importar saldos · Preparar cuentas |
| **Repartos de costes** | Reparto primario · Reparto secundario |
| **Actividad y tarifas** | Actividad y capacidades · Tarifas de centros |
| **Productos y resultados** | Productos y servicios · Órdenes estimadas · Resultados y trazabilidad (incluye informe auxiliar) |
| **Preferencias** (acceso separado) | Idioma · formato regional · zona horaria |

### Qué puede hacer el analista

- Crear, abrir, copiar, restaurar y actualizar proyectos; definir unidades y mantener modelos de centros, nodos y pools mediante borradores y versiones publicadas.
- Crear análisis por período, escenario y modelo; importar saldos CSV/XLSX con mapeo, revisión y confirmación; preparar cuentas, clasificar destinos y aplicar ajustes reutilizables de forma explícita.
- Definir claves y reglas de reparto primario y secundario, revisar cobertura, resolver prestaciones recíprocas, calcular y consultar ejecuciones persistidas e históricas.
- Consultar el Pareto de cada reparto: barras por nodo de mayor a menor coste y línea de porcentaje acumulado.
- Aceptar órdenes estimadas con materiales y actividad, conservar sus fuentes congeladas y comparar versiones sin registrar consumo real.
- Registrar/importar actividad y capacidad física, estimar capacidad normal, evaluar servicios auxiliares y consultar tarifas de gestión por centro.
- Mantener el catálogo de productos, materiales y servicios; importar **Referencia, Nombre, Unidad de medida y Coste unitario** desde CSV/TSV; crear composiciones con unidades, lotes, rendimiento y semielaborados; estimar subtotales físicos valorados.

La reimportación de catálogo actualiza nombre y coste de referencias existentes sin cambiar su identidad, unidad ni tipo; agrega referencias nuevas con el tipo seleccionado. La ayuda de **Productos y servicios** explica el procedimiento y los límites económicos.

## Límites que conviene tener presentes

- El coste T18 es una estimación de gestión con precios unitarios de referencia en la moneda predeterminada del proyecto. No es consumo real ni valoración contable de inventario o producción en curso.
- **Productos y servicios** muestra un subtotal de materiales que puede ser parcial. **Órdenes estimadas** incluye materiales y actividad, y bloquea la aceptación si falta un precio o tarifa. No se confunde ausencia con cero.
- Las órdenes aceptadas se guardan en formato 13. La actualización de archivos anteriores crea una copia; conserva el original para versiones antiguas de la aplicación.
- El cierre de los repartos no es un cierre integral del período. El cálculo recíproco no implica que haya métodos ABC/TDABC.
- El registro de consumos reales y desviaciones de T19 y la valoración contable de T32 no forman parte de estas pantallas.

## Ayuda en la aplicación

Las guías completas están disponibles localmente en español e inglés mediante **Ayuda (F1)**. La microayuda de campos aparece al situar el foco o el puntero sobre un control; **Tab conserva su función normal de pasar al siguiente control**. Consulta el [índice y arquitectura de ayuda](docs/help/README.md). Las 17 vistas están registradas en [`help-map.json`](docs/help/help-map.json).

## Desarrollo y validación local

Requisitos: Windows y .NET SDK **10.0.401** (véase [`global.json`](global.json)).

```powershell
dotnet restore Valdrio.slnx
dotnet build Valdrio.slnx -c Release --no-restore
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore
dotnet run --project src/Valdrio.Desktop -c Release
python scripts/verify_help.py
```

No ejecutar pruebas con archivos reales de usuario. Los escenarios WPF ampliados están en [Verify-Windows.ps1](.github/scripts/Verify-Windows.ps1). Las pruebas y evidencias fechadas deben leerse junto con el SHA al que corresponden; no certifican automáticamente un SHA posterior.

### Coste de GitHub Actions

Objetivo habitual: **0 USD**. El límite de seguridad absoluto es 20 USD y el gasto sin autorización explícita es 0 USD. Los workflows permanecen manuales (`workflow_dispatch`); no los ejecutes sin autorización del propietario. Prioriza validación local o runners Windows self-hosted.

## Documentación del proyecto

- [Inventario funcional del código actual](docs/revision/REALIDAD_FUNCIONAL_2026-10-25.md) · [estado e historia](ESTADO_PROYECTO.md).
- [Ayuda de usuario ES/EN](docs/help/README.md) · [plan de comprobación manual actual](docs/revision/PLAN_VERIFICACION_MANUAL_2026-10-25.md).
- [Directriz de usabilidad](docs/directrices/USABILIDAD.md) · [sistema visual aprobado](docs/diseno/SISTEMA_VISUAL_VALDRIO_V1.md).
- [Tareas y decisiones](docs/tareas) · [ADR](docs/adr) · [evidencias](docs/evidencias) · [auditorías](docs/auditorias).
- [Especificación v0.4](specification/v0.4): contratos y hoja de ruta; no debe leerse como descripción de funciones ya integradas.
- [Acuerdos funcionales 2026-11-04](docs/decisiones/ACUERDOS_COSTES_DIRECTOS_MARGENES_2026-11-04.md): adenda aprobada a la hoja de ruta para nodos, MOD, materiales y márgenes; **pendiente de implementación**, sin alterar la v0.4 ni los códigos T.

Los informes históricos conservan su fecha y SHA como evidencia de esa entrega. Para conocer el comportamiento actual, empieza por el inventario funcional y las guías de pantalla.

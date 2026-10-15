# VALDRIO

**Aplicación local de escritorio para Windows (WPF/.NET 10)** orientada a proyectos de cálculo y gestión de costes. El archivo de trabajo es `.valdrio`; se conservan estructura, análisis, importaciones, preparación, repartos y ejecuciones según el estado/versión de cada proyecto.

> **Estado documental actualizado el 15/10/2026.** Esta portada describe la implementación inspeccionada en `main@8b6c89e47d69d53bb5adad2fe0a2b215b4d68a95`. «Existe en el código» no equivale a «ha sido aceptada interactivamente en esta revisión». Para comportamiento, pruebas y límites, consultar [inventario de la aplicación real](docs/revision/REALIDAD_FUNCIONAL_2026-10-15.md) y [matriz de aceptación Windows](docs/revision/PLAN_VERIFICACION_MANUAL_2026-10-15.md).

## Recorrido disponible en la aplicación

El menú real consta de **tres fases operativas desplegables** y ocho pasos (más Preferencias). Se avanza cuando se cumplen los requisitos de proyecto/análisis/importación/cierre:

| Fase | Pasos de la interfaz |
|---|---|
| **Proyecto y estructura** | **1. Proyecto** · **2. Diseñar estructura** |
| **Análisis y datos económicos** | **3. Periodo / análisis** · **4. Importar saldos** · **5. Preparar cuentas** |
| **Repartos y cálculo** | **6. Reparto primario** · **7. Reparto secundario y cierre** · **8. Flujos y ejecuciones** |
| Fuera del proceso | **Preferencias** (idioma, región, zona horaria y herramientas de desarrollo) |

### Qué hace realmente cada etapa

1. **Proyecto:** crear/abrir archivo de proyecto, abrir **copia** del ejemplo, copiar/restaurar/actualizar archivo y gestionar unidades de actividad.
2. **Diseñar estructura:** centros, nodos, pools y unidades; borradores, validación, publicación y versiones de la estructura.
3. **Periodo/análisis:** análisis por modelo, fechas, moneda y escenario; selección/reapertura, instantáneas, estados y revisión.
4. **Importar saldos:** asistente CSV/XLSX, mapeo de cuentas/descripciones e importes (columna o Debe/Haber), interpretación, selección, agrupación, revisión y confirmación.
5. **Preparar cuentas:** correcciones manuales en tabla, clasificación, partes/destinos, exclusiones, costes directos y reglas de ajustes extracontables **guardables y aplicables expresamente por análisis** (con vista previa, confirmación y reversión según estado).
6. **Reparto primario:** biblioteca de claves versionadas, reglas por cuenta/prefijo, asignación y cobertura, composición, reparto, resultados explicativos y cierre/reapertura.
7. **Reparto secundario y cierre:** claves para auxiliares, prestaciones recíprocas, diagnóstico de viabilidad, cálculo y resultados conciliados por principal; circulación bruta no aditiva y cierre/reapertura.
8. **Flujos y ejecuciones (T12):** crear/publicar una ejecución de costes, consultar histórico vigente/anterior, gráficos por etapas y detalle de flujos y conciliaciones sin recalcular al consultar.

El **cierre primario/secundario** no es un cierre integral del período. El cálculo recíproco disponible no significa que existan ABC, TDABC ni selector de métodos. Los campos de «capacidad» que puedan figurar en estructuras/recursos **no implementan por sí mismos** tarifas u ociosidad.

## Estado de desarrollo y límites

- **Integradas en el código de `main`:** bases T00–T11 (proyecto, estructura, importación, recursos, claves y repartos), ejecuciones persistidas/historial **T12**, ejemplos de verificación **T13** y editor de ajustes reutilizables. La existencia se ha comprobado sobre archivos de producción, no únicamente fichas de tareas.
- **Verificación histórica:** constan pruebas y recorridos WPF Windows en [auditoría integral](docs/auditorias/2026-10-14/VALIDACION.md), [ergonomía visual](docs/auditorias/2026-10-14-visual/VALIDACION.md), [T12](docs/tareas/T12.md), [T13](docs/tareas/T13.md) y [reglas](docs/evidencias/2026-10-14-ajustes-reutilizables/INFORME.md). Sus resultados corresponden a **commits/escenarios documentados**, no son un pase nuevo del HEAD.
- **En desarrollo, no disponible en `main` de este corte:** T14 (observaciones físicas y capacidad, [PR #55 en borrador](https://github.com/JavierRB35/VALDRIO/pull/55)). Tampoco se anuncian como disponibles T15+ (tarifas/absorción, producción y objetos, rentabilidad, escenarios, informes comerciales, presupuestos, ABC/TDABC).
- **Pendiente para el futuro manual:** recorrido manual Windows sobre el binario final, mensajes exactos, casos negativos, resultados representativos, accesibilidad y escalado. Véase [matriz con estado por caso](docs/revision/PLAN_VERIFICACION_MANUAL_2026-10-15.md).

## Ejemplos de comprobación económica

Se incluyen dos proyectos ficticios de aceptación, `Synthetic Projects/Service Company/` y `Synthetic Projects/Industrial Company/`, con resultados de referencia **17.500 EUR** y **36.000 EUR**, respectivamente. Los archivos `.valdrio` originales se conservan para probar reapertura/migración; no se deben modificar. Véase [T13](docs/tareas/T13.md) para expectativas independientes, signos, auxiliares recíprocos y ejecución histórica T12 sobre **copias generadas**.

## Desarrollo y verificación local

Requisitos de ejecución: **Windows y .NET SDK 10.0.401** según [global.json](global.json); consulta las dependencias de [Valdrio.slnx](Valdrio.slnx). El frontend está en `src/Valdrio.Desktop`, el motor económico en `Application`/`Calculation`, la persistencia en `Infrastructure`, el modelo en `Domain` y las vistas de reporting en `Reporting`.

```powershell
dotnet restore Valdrio.slnx
dotnet build Valdrio.slnx -c Release --no-restore
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore
# Ejecución interactiva (Windows):
dotnet run --project src/Valdrio.Desktop -c Release
```

La verificación más amplia y los escenarios WPF se describen en [Verify-Windows.ps1](.github/scripts/Verify-Windows.ps1) y [matriz de aceptación](docs/revision/PLAN_VERIFICACION_MANUAL_2026-10-15.md). Puede necesitarse un directorio de evidencias nuevo y el árbol limpio; **no ejecutar tests con archivos reales del usuario**.

### Política GitHub Actions

**Objetivo: 0 USD; gasto sin autorización expresa: 0 USD; límite de seguridad absoluto: 20 USD.** El único workflow `windows-ci.yml` usa actualmente `workflow_dispatch` y un runner alojado Windows: no lo lanzar sin autorización. Priorizar PC Windows local o runner Windows self-hosted. Las modificaciones exclusivamente documentales no justifican compilaciones remotas. Esta actualización documental no ha lanzado workflows ni consumido GitHub Actions.

## Documentación por finalidad

- **Realidad funcional observable:** [inventario actual](docs/revision/REALIDAD_FUNCIONAL_2026-10-15.md) y [protocolo de validación del futuro manual](docs/revision/PLAN_VERIFICACION_MANUAL_2026-10-15.md).
- **Estado e historia del proyecto:** [ESTADO_PROYECTO.md](ESTADO_PROYECTO.md); sus capítulos antiguos son registros de fases pasadas, **no** una descripción vigente alternativa.
- **Instrucciones para agentes/desarrollo:** [AGENTS.md](AGENTS.md) y [usabilidad](docs/directrices/USABILIDAD.md).
- **Diseño vigente como directriz:** [sistema visual V1](docs/diseno/SISTEMA_VISUAL_VALDRIO_V1.md) y [plan de migración](docs/diseno/PLAN_MIGRACION_FRONTEND.md); el estilo aprobado no garantiza aceptación visual completa.
- **Decisiones técnicas y trazabilidad:** [ADR](docs/adr), [fichas de tareas](docs/tareas), [evidencias fechadas](docs/evidencias), [auditorías](docs/auditorias).
- **Visión de producto y contratos de referencia:** [especificación v0.4](specification/v0.4), [directrices T14](docs/directrices/T14_INTEGRACION_Y_HOJA_DE_RUTA.md).

**Convención:** conservar los históricos de desarrollo como fuentes con fecha y SHA. La documentación de «cómo se usa hoy» debe seguir el inventario y el binario aceptado, no un `README` de T05/T07/T08 ni una maqueta futura.

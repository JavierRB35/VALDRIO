# Evidencia T12 — presentación, 2026-10-15

Base funcional local `85ef4cf`. Rama `fix/t12-presentation-20261015`.

## Dependencias previas

La base tenía errores ajenos a la presentación: faltaba `using System.IO` en PhysicalImportEditor y la auditoría de actualización de formato no aceptaba el paso 10 → 11 de T14. Se corrigieron en el commit local independiente `5144989`. La corrección de auditoría coincide con el trabajo local observado en T14; no se modificó su copia ni se incorporaron sus demás cambios pendientes. Se preservan las comprobaciones de integridad, incluyendo continuidad y IDs de migración.

## Inventario de preservación

Se conservaron los 36 bindings originales de CostFlowPanel: comandos de cálculo, cancelación y refresco; ejecución seleccionada; estado y diagnósticos; detalle y selección; conciliación y datos de ambos gráficos. La comparación automática de bindings no detectó ausencias. Se añadieron resumen y navegación por nodo. No se modificaron Calculate, PrepareRun, PublishRun ni las fórmulas de reparto.

## Validación local

- SDK .NET 10.0.401. Restauración desde caché local; sin descargas requeridas.
- Compilación Release de toda la solución: 0 errores y 0 advertencias.
- Pruebas focalizadas T12/T13: **16/16**, incluyendo el nuevo puente por nodo, publicación/historia y ejemplos sintéticos.
- WPF: **66 comprobaciones** en ES/EN, con ocho capturas. Ver [recibo](wpf-checks.json).
- WPF conserva la ejecución publicada byte a byte al consultar; comprueba resumen, total, selección de nodo, selección de transferencia con Intro, foco, etiquetas opcionales y acceso en ventana de 960 px.
- Comprobación de layout con escala 1,25 / 1,5 / 2,0. No equivale a una certificación de DPI físico multimonitor ni a una auditoría exhaustiva de accesibilidad.
- Las pruebas requieren comunicación local entre testhost y el ejecutor; se ejecutaron fuera del aislamiento después de que ese enlace agotara el tiempo de espera dentro de él. Todo el cálculo y la evidencia son locales.
- GitHub Actions: ninguna ejecución. No se publicaron ramas ni PR.

Suite completa: **705/706**, 0 omitidas, 5 min 55 s. Falló `T14_DraftCanKeepPartialSourceWithoutClaimingConfirmation`: esperaba `ACT_INCOMPLETE` y recibió `ACT_EVIDENCE_REQUIRED`. Los archivos `PhysicalActivity.cs` y `T14PhysicalActivityTests.cs` son idénticos a la base `85ef4cf`; el problema es el orden de validación en T14, que ya tiene una corrección en su copia de trabajo paralela. No se incorporó esa corrección funcional a este cambio visual. **No se declara la suite global en verde.** Recibo local: `tests/Valdrio.Tests/TestResults/t12-full.trx`.

## Capturas reales

La captura anterior conserva el layout original; las dependencias de apertura de T14 ya estaban corregidas y la microayuda de gráficos ya usa el texto nuevo. No es una captura binaria intacta del commit base.

[Antes](antes.png) · [Después](despues.png) · [Diagrama secundario](secundario.png)

Las capturas se generaron con el ViewModel y los servicios reales sobre copias desechables del ejemplo de 6.500 EUR; no se alteró un archivo del usuario. El diagrama secundario diferencia coste bruto de auxiliares y final en principales.

## Reproducción

```powershell
dotnet restore Valdrio.slnx --source C:/Users/cjrui/.nuget/packages -p:NuGetAudit=false --disable-parallel -m:1 -nr:false
dotnet build Valdrio.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --filter 'Name~T12_|Name~T13_'
```

Para generar el ejemplo, definir `VALDRIO_T12_DEMO` con una ruta nueva y ejecutar `T12_6500FlowsPersistAndHistoricalReadDoesNotRecalculate`. Abrir una copia con `Valdrio.Desktop.exe <copia.valdrio> --cost-flow-only --capture-dir=<carpeta> --preferences-path=<archivo-temporal>`.

La prueba manual de integración T13 mantiene su comprobación exacta de recuento, actualizada de 46 a 52 al añadir tres aserciones por idioma. No se cambian los disparadores de workflows.

## Límites de aceptación

No hay exportación nueva, comparación de ejecuciones ni atribución por cuenta de transferencias recíprocas brutas. El informe completo y criterios pendientes están en [la revisión](../../diseno/REVISION_T12_PRESENTACION_20261015.md). La proyección de saldos no debe confundirse con el cálculo de tarifas o absorción de fases posteriores.

## Empresas T13

Aceptación WPF adicional: **52/52 servicios** (17.500 EUR, dos principales) y **52/52 industrial** (36.000 EUR, tres principales), ES/EN y ventana estrecha. Recibos `services-checks.json` e `industrial-checks.json`. Se revisaron visualmente el resumen de servicios y el diagrama industrial; capturas completas en `artifacts/t12-visual/visual-services` y `visual-industrial`.

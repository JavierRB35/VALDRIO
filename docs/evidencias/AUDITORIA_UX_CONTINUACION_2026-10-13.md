# VALDRIO — continuación de auditoría UX (13/10/2026)

**Objeto.** Continuación, sin sustituir, de:
- `VALDRIO_Auditoria_Recorrido_Usuario_2026-10-12.md` (recorrido real de Café y Obrador Horizonte SL);
- `VALDRIO_T13_Informe_Inspeccion_Visual.md` (servicios/industria).

**Base auditada:** `main@7be41898208d7e452c560d7f5d09c5cb68e589e7`. Al comenzar, `main` era idéntica a esa base. **Rama de implementación aislada:** `audit/ux-financial-results-20261013`. No se ha modificado `main`.

## Resultado previo acreditado

- Recorrido de empresa vacía → centros/nodos → modelo publicado → estudio → CSV confirmado → preparación → primario → secundario → cierre de archivo → reapertura completado.
- Caso Horizonte: 5 cuentas, **5.500,00 EUR**; el motor conserva el total aunque muestra 2.750,00000002 y 2.749,99999998 por nodo en la interfaz.
- T13: **74 verificaciones** de los ejemplos de servicios e industria completadas en la auditoría del 12/10; 46 capturas. Son evidencias **anteriores a esta rama**, no validaciones nuevas.
- T12 no quedó revalidada en la base examinada.

## Matriz de hallazgos y desarrollo

| ID | Prioridad | Evidencia / problema | Cambio en esta rama | Estado real |
| --- | --- | --- | --- | --- |
| UX-01 | P1 | Decimales contables excesivos, líneas visibles no conciliadas al céntimo | Proyección determinista a `ReportQuantum` en tabla primaria, por centro/nodo, secundario, desglose principal/auxiliar y explicación; totales visuales se calculan desde los importes presentados | **Código implementado; validación Windows pendiente** |
| UX-02 | P1 | Resultados detrás de formularios inhabilitados | Cierre del primario y cálculo del secundario revelan resultados; al entrar en fases cerradas, navegación abre la sección de resultados | **Código implementado; UX visual pendiente** |
| UX-03 | P2 | `Guardar clave` cortado en anchos reducidos | Botón trasladado a segunda fila adaptable, conservando comando | **Mitigación parcial**: pendiente comprobar tablas y 780/960/1440 px a distintos DPI |
| UX-04 | P2 | Secundario inaccesible tras cargar estudio hasta visitar primario | Carga explícita del snapshot de repartos al completar `LoadCommand` del estudio y antes de refrescar navegación | **Código implementado; regresión WPF pendiente** |
| UX-05 | P2 | 15 clics de guardar/aplicar reglas de cinco cuentas | Sin nuevo asistente. Ya existe asignación directa por cuenta; falta selección múltiple, validación y confirmación en una sola operación | **Pendiente** |
| UX-06 | P2 | `Detalle de cuentas` bloquea edición sin explicación | Mensaje contextual ES/EN indica cerrar el detalle para volver a claves | **Mitigación informativa**; pendiente mejorar navegación contextual |
| UX-07 | P3 | Confirmación obligatoria de significado de saldo poco visible | Texto localizado existente `InterpretationPrompt` mostrado junto al selector inicial del significado del saldo; sigue habiendo confirmación expresa posterior | **Mejora parcial; probar ambos diseños de importación** |
| UX-08 | P3 | Clave seleccionada con editor vacío/rojo tras reapertura | Al abrir fase cerrada, selección y lectura de clave guardada sin desplazar foco fuera de resultados | **Código implementado; prueba visual pendiente** |
| T13-TEXTO | menor | «Correcciones se guarda...» | «Las correcciones se guardan...» | **Cadena corregida; verificar en pantalla** |

**Importante:** «código implementado» no equivale a defecto resuelto en una versión entregable. Sin compilación y ejecución WPF en Windows no procede cerrar definitivamente ninguno de estos ítems.

## Detalle técnico de la presentación monetaria

El motor conserva su precisión de libro mayor; **no se modifican** `PrimaryAllocationService`, `SecondaryAllocationService`, `BalancedRounder`, históricos, SQLite ni importes publicados.

- `AllocationCurrencyDisplay.Project`: `CurrencyProjector` existente, IDs de desempate estables e intervalo monetario (`ReportQuantum`) de la divisa del estudio.
- `ProjectBreakdown`: proyección determinista de cada principal; los tres importes mostrados (directo + recibido = final) concilian y las sumas de columnas coinciden.
- `ProjectToTarget`: líneas de contribución y líneas explicativas proyectadas a subtotales previamente mostrados para impedir divergencias en cascada. El ajuste es **exclusivamente de presentación**.
- Los records mantienen campos de importe original para lógica/sorting/pruebas económicas y campos `Presented...` para texto financiero.
- Para Horizonte se espera mostrar **2.750,00 + 2.750,00 = 5.500,00 EUR** por nodos, con desglose visible consistente al céntimo.

## Archivos afectados por esta continuación

- `src/Valdrio.Desktop/AllocationCurrencyDisplay.cs` (nuevo).
- `src/Valdrio.Desktop/AllocationWorkspaceViewModel.cs`, `.Primary.cs`, `.Secondary.cs`.
- `src/Valdrio.Desktop/AllocationWorkspacePanel.xaml`, `.xaml.cs`.
- `src/Valdrio.Desktop/MainWindow.Guidance.cs`, `MainWindow.ResultsScrollSmoke.cs`.
- `src/Valdrio.Desktop/StudyWorkspaceViewModel.cs`, `App.xaml.cs`.
- `src/Valdrio.Desktop/ImportColumnMappingWindow.xaml`.
- `src/Valdrio.Localization/Resources/Strings.resx`, `Strings.es.resx`.
- `tests/Valdrio.Tests/AllocationCurrencyDisplayTests.cs` (nuevo) y `Valdrio.Tests.csproj` (registro explícito del proyecto).

## Puertas de aceptación pendientes — ejecutar localmente en Windows o runner self-hosted autorizado

1. **Compilar** `dotnet build Valdrio.slnx -c Release` con .NET 10 y restauración local de paquetes.
2. **Pruebas** `dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release` (incluidas nuevas pruebas de tercios/5.500 EUR/JYP/saldos negativos), sin usar runners GitHub-hosted.
3. **Smoke WPF** sobre copias desechables de `services.valdrio`, `industrial.valdrio` y `Horizonte.valdrio` si está disponible. Validar cierre/reapertura, selección de clave, acceso directo a secundaria, tablas y explicaciones, importación.
4. **Cuadre visual**: suma de filas = pie de tabla, subtotales directo + auxiliar = total y secundario = recursos efectivos a precisión de moneda. Revisar EUR y divisas de 0/3 decimales; importes negativos y 1/3.
5. **Pantallas** ES/EN, anchos 780/960/1440/1600 px, DPI 100/125/150/200 %, teclado/lector de pantalla, editor visible y desplazamiento accesible.
6. **Regresión funcional 1:1** de claves, reglas, guardado, historial y datos publicados. La vista de resultados es de consulta, no modifica la precisión interna.
7. Volver a reproducir **el recorrido de Horizonte completo** a partir de empresa vacía, guardando nuevas capturas y resultado.
8. Revalidar separadamente T12/historial cuando su rama o funcionalidad esté disponible.

**Validación realizada en esta continuación:** lectura de ambos informes, comparación `main` vs SHA auditado y revisión de estructura de código, contratos de proyecto, reglas de presentación e integración de cambios. **No se ejecutó compilación, suite .NET ni interfaz WPF**: el entorno disponible no tiene `dotnet` ni Windows. Los tests añadidos están escritos pero **no ejecutados**.

## Trabajos que siguen abiertos

- **Lote primario sin repetición**: propuesta funcional: seleccionar varias cuentas en la tabla, escoger una clave publicada, previsualizar cobertura/efecto sobre asignaciones manuales, confirmar una operación atómica, con Undo/errores y pruebas de no pisar reglas existentes; conservar modo avanzado. No implementado por requerir decisión UX y prueba funcional.
- **Rediseño adaptable integral**: conservar todos los comandos/bindings, reorganizar tablas y editor para 780–1440 px, exponer scroll horizontal cuando sea inevitable. Solo se mitigó el botón.
- **Separación de «Detalle de cuentas»**: se añadió explicación de bloqueo; aún conviene navegación de regreso explícita sin desactivar inesperadamente el editor.
- **Aprobación del significado del saldo**: el mensaje es ahora más temprano, pero confirmar que los usuarios encuentran y comprenden la aprobación en revisión.
- **Validación con personas** sin guía por automatizador: recorrer el proyecto desde cero y observar dudas y errores antes de concluir que es intuitivo.

## Seguridad, costes y promoción

- `main` no modificada ni fusionada; entrega en rama aislada.
- **GitHub Actions: 0 ejecuciones provocadas**, sin autorización para gastos ni cambios de facturación. La configuración existente es `workflow_dispatch` manual con runner GitHub-hosted, que **no se ejecutó**.
- No promocionar la rama a `main` sin compilar, pasar pruebas y verificar las pantallas WPF reales.
- Las capturas/estadísticas del 12/10 corresponden a la base antigua y no deben presentarse como pruebas de estas correcciones.

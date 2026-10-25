# Implementación del diagnóstico UX de VALDRIO

Fecha: 25-10-2026. Base: `e198d2873396e505b96b692d0169b6f4ba644822`.
Rama local: `codex/ux-diagnostico-integral`.

La instrucción posterior del propietario autoriza implementar el diagnóstico completo sin aprobaciones intermedias. La propuesta original se conserva en `diagnostico.md` como referencia histórica; sus afirmaciones sobre no haber ejecutado la aplicación corresponden a esa revisión original.

## Alcance implementado

Se modifica presentación, orientación, lectura y confirmaciones. Se conservan comandos, cálculos, datos, reglas de cierre, versionado, importadores y exportaciones. No hay cambios en Domain, Calculation, Application, Infrastructure ni workflows. `sources/` permanece intacto. No se han ejecutado GitHub Actions ni operaciones facturables.

| Hallazgo | Implementación o capacidad existente preservada |
|---|---|
| UX01 | Ruta por tareas y títulos sin numeración global parcial; se mantienen requisitos de acceso. |
| UX02 | Contexto visible de proyecto, estructura, análisis, periodo, moneda y estado; ajuste de línea. |
| UX03 | Destinos de navegación y ayudas unificados con Reparto primario y Resultados y trazabilidad. |
| UX04 | Decisiones explícitas de guardar, descartar y seguir editando; cierre y Escape conservadores. |
| UX05 | Inicio explica la preparación previa del proyecto para importar contabilidad. |
| UX06 | Copia, restauración y actualización muestran origen y destino antes de escribir. |
| UX07 | Grupo de costes sustituye a pool en textos de interfaz; ejemplo de centro, nodo y unidad. |
| UX08 | Código obligatorio antes de descripción; edición de centros y nodos en la tabla con guardado existente. |
| UX09 | Ayudas distinguen guardado del nodo, estructura reutilizable y nodos del análisis. |
| UX10 | Magnitudes admitidas time/count/batch explicadas y acción activar/desactivar según estado. |
| UX11 | Mensaje neutral y enlace a los cálculos guardados; se elimina la afirmación de que no hay cálculos. |
| UX12 | Puntos de control identificados como entradas, sin cálculo de costes. |
| UX13 | Acciones de abrir periodo y crear otro periodo diferenciadas en sus textos. |
| UX14 | Ejemplos de movimientos, saldos y diferencia de acumulados, con resumen persistente. |
| UX15 | Ayudas de signos/formato y cuenta numérica; se conserva la vista previa del importador. |
| UX16 | Tarjeta persistente de añadir o sustituir lote en el análisis destino. |
| UX17 | Confirmación con nombre del análisis y siguiente tarea; se conserva procedencia. |
| UX18 | Cuentas y conciliación antes de ajustes reutilizables; ficha de regla abierta si tiene cambios. |
| UX19 | Corrección manual total y guardado al finalizar celda explícitos; motor e historial conservados. |
| UX20 | Textos específicos de definición, revisión, aplicación y deshacer; comparación existente preservada. |
| UX21 | Retorno a claves y explicación de tratamiento especial de cuentas conservados y aclarados. |
| UX22 | Recargar datos guardados diferenciado de reiniciar preparación; confirmaciones existentes conservadas. |
| UX23 | Botón de guardar clave disponible o borrador incompleto según las mismas reglas existentes. |
| UX24 | Ejemplos de pesos y porcentaje junto al criterio; sin reinterpretar valores introducidos. |
| UX25 | Explicación de guardar regla, aplicar y excepción manual; asignaciones existentes preservadas. |
| UX26 | Comportamiento del coste diferenciado de composiciones de productos; ejemplo fijo/variable. |
| UX27 | Resumen desplegable previo al cierre con base, cobertura, clasificación y restricciones existentes. |
| UX28 | Adoptar y aplicar reglas seleccionadas, con límites de reutilización expresos. |
| UX29 | Nombre mostrado de cuenta y conservación de código, saldo y asignación explicados. |
| UX30 | Detalles secundarios de liquidación, gráfico y explicación inicialmente plegados; errores de viabilidad visibles. |
| UX31 | Transferencias internas y coste final distinguidos en los textos y ámbitos de totales. |
| UX32 | Todos los componentes como presentación; identificador interno all preservado. |
| UX33 | Guardar/confirmar identifican fila; contador de otras filas pendientes. |
| UX34 | Definiciones de normal, real y utilización; cero y ausencia siguen siendo valores diferentes. |
| UX35 | Confirmación de propuesta trasladada a la fila sin guardar al adoptar capacidad. |
| UX36 | Contrato de importación visible y plantilla CSV con códigos/unidades/fechas reales y cantidades vacías. |
| UX37 | Historial integrado consultable y copiable con columnas separadas y motivos completos. |
| UX38 | Acciones de auxiliares indican comprobar, guardar y calcular/guardar resultados; siguiente tarea visible. |
| UX39 | Estado de preparación por servicio y total de cada grupo frente a actividad real, sin sumar grupos. |
| UX40 | Ayuda CSV distingue códigos de nodo auxiliar/destino de códigos de centro. |
| UX41 | Referencias a tareas técnicas sustituidas por destinos visibles y límites de esta versión. |
| UX42 | Resumen de tarifa, coste, normal, real y unidad; conciliación parcial persistente; siete respuestas conservadas. |
| UX43 | Exportaciones identificadas como JSON y archivo auxiliar con nombre económico. |
| UX44 | Guardado de catálogo indica que incluye la composición pendiente según la operación existente. |
| UX45 | Conversión de unidad y ejemplos de consumo/merma explicados sin alterar fórmulas. |
| UX46 | Ámbito físico y límites de valoración/consumo real de composiciones explícitos. |
| UX47 | Estado, borrador, publicación y guardado en composiciones; confirmación de publicación con contexto. |
| UX48 | Calcular con entradas actuales separado de consultar ejecución guardada. |
| UX49 | Ámbito de conciliación aclarado junto al resultado; ninguna suma económica modificada. |
| UX50 | Cinco pestañas, tablas exactas, selección y gráficos existentes conservados. |
| UX51 | Nombres comprensibles de idiomas, regiones y zonas; muestra de formato; aviso de diagnóstico técnico. |
| UX52 | Lector completo de ayuda accesible mediante botón, sin expandir el pie y con retorno de foco. |

## Verificación reproducible

- Compilación local Release de `Valdrio.slnx`, sin advertencias ni errores.
- Regresión de 761 pruebas: 761 correctas, 0 fallos, 0 omitidas; TRX en `tests/ux-regression-final.trx`.
- Nuevas pruebas de historial físico y plantilla: lectura sin escritura, cantidades separadas, motivos, códigos/periodos reales y mediciones vacías no aplicables.
- Comprobador WPF `--ux-acceptance`: recorrido guiado, ES/EN, contexto, navegación en 1280×720, 1366×768, 1024×720 y 760×620; decisiones guardar/descartar/cerrar; guardado de centros y nodos en tabla, preferencias legibles, lector de ayuda con retorno de foco y plantilla sin mutación.
- WPF de composiciones: versiones, publicación, conservación de originales, cancelación, cantidades y escalado de contenido 125/150/200 %.
- WPF de tarifas, servicios auxiliares y asistente de capacidad: recorridos y presentación en ES/EN.
- `control-comparison.json`: ningún Command ni Click preexistente eliminado de las vistas XAML modificadas. Es una comprobación estática complementaria; no sustituye las pruebas de interacción.

Comandos usados (PowerShell, desde el repositorio):

```powershell
dotnet build Valdrio.slnx -c Release --no-restore --nologo -m:1 -nr:false
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore
# Capturas con datos sintéticos, sin persistir preferencias personales:
.\src\Valdrio.Desktop\bin\Release\net10.0-windows\Valdrio.Desktop.exe --capture-dir=C:\ruta-sin-espacios\evidencia --ux-acceptance
```

La restauración inicial utilizó paquetes locales y `-p:NuGetAudit=false` porque no había acceso al servicio NuGet. No se cambió la política del repositorio. La ejecución de vstest requirió salir del aislamiento para la conexión local con testhost, sin servicios remotos.

## Límites de aceptación

Los requisitos R01–R10 combinan pruebas de software y comprensión humana. Hay evidencia local de tamaños de ventana, ES/EN, cancelación, versiones, guardado y cálculos. El escalado de contenido WPF no equivale a certificar todos los monitores físicos y DPI de Windows. No se ha realizado una observación con analistas (R08), ni se afirma una prueba exhaustiva de todas las combinaciones de tamaño, 500 cuentas y tecnologías de asistencia. La mejora de comprensión humana debe validarse con usuarios reales.

Los archivos de empresa empleados por los comprobadores son copias o datos sintéticos. Los intentos previos y capturas se conservan localmente; el fixture antiguo de auxiliares no contenía el análisis esperado por su comprobador y se sustituyó por un fixture generado por la prueba de integración vigente.

La entrega es local. No se ha publicado una rama ni activado CI remoto.

## Panel de reanudación del analista

Seguimiento del 25-10-2026 tras revisar la utilidad visible de la entrega. La portada del proyecto ahora reúne cuatro tarjetas de trabajo: estructura de costes, periodo de análisis, saldos importados y preparación de cuentas. Cada tarjeta muestra el estado y el dato disponible, explica el requisito del siguiente paso y ofrece una acción directa. «Continuar» abre revisión de cuentas, importación o selección/creación del periodo según lo guardado; permanece disponible con una estructura publicada para empezar un periodo.

La prueba WPF `--ux-acceptance` pasó 47 comprobaciones, incluidas las cuatro etapas visibles, sus estados y explicaciones, el requisito de importación, la navegación de «Continuar» y la actualización inmediata de ayuda al enfocar dos campos seguidos. La captura del panel a 1280×860, la ayuda enfocada y el resultado están en `resume-panel/project-resume.png`, `resume-panel/field-help-focused.png` y `resume-panel/ux-acceptance.json`.

## Aplicación entregada

Carpeta local `../entregables/VALDRIO-UX-20261025` respecto del repositorio. Ejecutar `Valdrio.Desktop.exe` conservando los archivos y subcarpetas acompañantes. Es la compilación Release dependiente de .NET Desktop Runtime 10 para Windows, disponible en este equipo. Se verificó nuevamente el recorrido `--ux-acceptance` desde esta carpeta, con resultado correcto.

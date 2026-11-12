# Calculadora e indicador global — 2026-11-12

## Base y objetivo

Repositorio real: `C:/Users/cjrui/source/repos/VALDRIO`, HEAD inicial `2da1541`.
Árbol limpio antes de editar. Se creó `fix/capacity-global-activity`; durante el
trabajo hubo un checkout externo a `main`, en el mismo HEAD. Durante la implementación no se realizaron
commits, push, fusiones ni ejecuciones de GitHub Actions. Las fuentes sincronizadas
permanecen intactas.

Se leyó íntegramente el protocolo v1.0, 39 páginas, disponible en las fuentes del
proyecto como `PROTOCOLO DE DESARROLLO ASISTIDO POR INTELIGENCIA ARTIFICIAL_.pdf`.
Se consultaron AGENTS, el sistema visual V1, USABILIDAD/COR-02 y la directriz de
cuentas T08. COR-02 ya exigía la misma barra global verde: no era necesario crear
otra barra o cambiar el diseño.

Aceptación: guardar capacidad normal y actividad real, cerrar la calculadora solo
tras confirmación correcta y volver a la tabla; barra animada durante la espera;
una sola escritura ante doble pulsación; conservar datos ante error. La auditoría
transversal añade cobertura donde faltaba y conserva los contratos económicos.
Recuperación: cambios limitados a escritorio, pruebas y documentación, sin cambios
de esquema, migraciones nuevas ni modificaciones del motor de cálculo.

## Auditoría de pantallas y operaciones

Inventario estático de los puntos de entrada de UI, métodos asíncronos, trabajo de
almacenamiento/cálculo, estados ocupados y conexión con `Company.IsProcessing`.
Las pruebas dinámicas cubren recorridos representativos; no se afirma ejecución
manual de cada operación de la tabla.

| Superficie | Operaciones y cobertura |
| --- | --- |
| Proyecto/empresa/unidades | Crear, abrir, ejemplo, cerrar, unidades, duplicar, copia, restauración y actualización: `Run` existente y trabajo fuera del dispatcher. Se conservan cancelación y errores. |
| Estructura/nodos/mapa | `Run` existente en cargas, guardados, validación y publicación; exportación PNG conectada al estado global y codificación/escritura fuera del dispatcher. |
| Análisis/periodo | `Run` existente; añadida cobertura a la lectura del destino de importación. |
| Importación contable | `ImportColumns.IsBusy` ya se propaga a estudio/empresa: lectura, validación, preparación, confirmación y cancelación. Sin cambios de columnas, cuentas o reglas económicas. |
| Recursos y repartos | Conservados los estados encadenados de recursos, primario y secundario, incluidas correcciones, reglas, cálculo y publicación. |
| Informes de costes | Añadido ámbito a carga/actualización, bloqueo de cargas repetidas, CSV asíncrono e impresión asíncrona. |
| Actividad/capacidad | Carga inicial, cambio de análisis, guardado y anulación asíncronos; historial en memoria para evitar lecturas desde bindings. |
| Calculadora | Guardado asíncrono, prevención de doble envío y salida al éxito; borrador permanece; validaciones y errores conservan la edición. |
| Importación física | Archivo, vista previa, persistencia y plantilla con cobertura global; análisis y persistencia fuera del dispatcher. |
| Tarifas | Carga/recarga y exportación asíncronas, reutilizando una sola lectura del historial de ejecuciones. |
| Auxiliares | Carga, guardado, reapertura, validación, cálculo e importación/exportación conectados a la barra común. |
| Productos/composiciones | Carga, importación, vista previa y guardado; eliminada la lectura síncrona duplicada tras guardar. La salida con cambios pendientes espera el guardado sin bloquear. |
| Presupuestos | Ámbitos acumulativos en carga, vista previa y aceptación; lectura de modelos fuera del dispatcher. |
| Reutilizar repartos | Carga de orígenes, vista previa y adopción asíncronas en la página integrada. |
| Preferencias, ayuda y diálogos | Sin flujo asíncrono de negocio descubierto. Las decisiones del usuario, bindings, edición local y ayudas breves no se presentan como procesos de fondo. |

`BeginActivity` es acumulativo y su liberación es idempotente; se combina con los
estados ocupados existentes. `RunActivityAsync` bloquea solo la página en operación.
Se conserva la barra de 4 px y sus textos/localización existentes. Los métodos
síncronos de compatibilidad se conservan para consumidores y pruebas aisladas;
los recorridos de producción modificados llaman a las variantes asíncronas.

La navegación integrada y el cierre de la aplicación esperan la resolución de
composiciones pendientes. El cierre durante una escritura se rechaza hasta acabar;
no se cancela una persistencia que carece de cancelación segura.

## Verificación ejecutada

- `dotnet build src/Valdrio.Desktop/Valdrio.Desktop.csproj --no-restore -c Release -v quiet`:
  correcta, cero errores y advertencias. La primera salida Debug estaba bloqueada
  por una instancia abierta; se usó Release sin cerrar esa instancia.
- `dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj --no-restore -c Release --filter
  'FullyQualifiedName~T14|FullyQualifiedName~T15|FullyQualifiedName~T16|FullyQualifiedName~T17|FullyQualifiedName~T18|FullyQualifiedName~AllocationReuse|FullyQualifiedName~CompanyFile|FullyQualifiedName~CostFlow' -v quiet`:
  **102 correctas, 0 fallidas, 0 omitidas**. Incluye las nuevas regresiones de guardado
  lento, doble invocación, fallo de escritura, reintento, validación y persistencia.
- WPF real, `--activity-feedback-only`, sobre una copia actualizada del ejemplo:
  ES/EN, entrada por bindings y foco, borrador sin cierre, validación sin pérdida,
  bloqueo SQLite deliberado, dispatcher operativo durante la espera, barra visible,
  salida al éxito, una sola versión y recarga de ambos valores. Comprobados ámbitos
  solapados, liberación repetida y cancelación. Evidencia inicial completa de 27
  comprobaciones en `artifacts/activity-feedback-v2/activity-checks.json`.
- Ejecución ampliada: guardado de una composición al salir, carga de tarifas y
  auxiliares superadas antes de detectar un error de serialización XPS. Corregido
  usando el overload de documento fijo, y repetida únicamente esa prueba con
  `--report-feedback-only`: archivo XPS reabierto y actividad desactivada, registrado
  en `artifacts/activity-report-verified/report-check.txt`.
- Inspección visual de capturas ES/EN de guardado y retorno. Los fotogramas de
  guardado muestran el segmento verde en posiciones diferentes y los controles
  deshabilitados. Capturas en `artifacts/activity-feedback-final/`.
- Los recorridos visuales existentes esperan ahora la inicialización asíncrona de
  las páginas; no se utiliza únicamente un ciclo de render como señal de carga.
- `git diff --check`: sin errores. Diff revisado, sin cambios en cálculos, dominio,
  infraestructura, localización, dependencias o fuentes del proyecto.

Límite real: la salida asíncrona de impresión se verificó con XPS local; no se ha
impreso en una impresora física. No se ha ejecutado la suite global ni todos los
recorridos visuales históricos, al no ser necesarios para estos riesgos.

## Integración autorizada

El propietario solicitó fusionar el 2026-11-12. Se comprobó que `main` y
`origin/main` seguían en `2da1541`, sin cambios posteriores en el código revisado.
Se registran los 39 archivos de esta entrega en `fix/capacity-global-activity` y
se integran localmente en `main` mediante avance directo, sin conflictos. Se
reutilizan las verificaciones anteriores; no se publica al remoto ni se ejecuta CI.

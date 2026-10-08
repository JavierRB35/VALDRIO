# VALDRIO — Directrices ejecutables para T06: sumas y saldos verificables

Actualización 2026-10-07: estas directrices conservan el plan original. ADR-0009
materializa la selección de registros y agrupación por código con formato v5.
El propietario ha indicado explícitamente que no se debe usar la descripción para
agrupar. Esa decisión sustituye la exigencia de resolver descripciones distintas
que figura en el plan histórico. No modificar las fuentes canónicas v0.4.

Fecha: 2026-10-05. Destinatarios: equipo y LLM ejecutor. T06 no está implementada. Leer T05 y su evidencia antes de empezar; este documento no aprueba D03 ni las propuestas D04–D07.

## 1. Resultado y puerta D03

El usuario abre un estudio y prepara un CSV/XLSX mediante perfil explícito. Ve cuenta/descripción originales, importe interpretado, modalidad, fechas, moneda, duplicados/errores y conciliaciones. Preparar/cancelar no cambia entradas ni revisión. Confirmar identifica el estudio/revisión y guarda lote completo, filas, enlaces de origen y recursos en una transacción; un fallo deja el estado previo.

**Antes de importar definitivamente una fuente del propietario, resolver D03 con un archivo representativo y un ejemplo aprobado.** No deducir la modalidad de la palabra «saldo», el país, el idioma, las cuentas 6/7 ni el signo de una columna. Se puede construir y probar el adaptador con fixtures sintéticas, manteniendo el perfil real pendiente. No anunciar el perfil productivo aceptado hasta documentar su interpretación.

Información que obtener del propietario con su muestra, preferiblemente sin nombres o datos sensibles innecesarios:

| Pregunta de negocio | Evidencia que registrar |
|---|---|
| ¿El importe corresponde al periodo, a un cierre o a un acumulado? | Dos filas y resultado esperado; modalidad seleccionada |
| ¿Hay saldo firmado o columnas Debe/Haber? | Convención y un gasto positivo/abono o reversión negativo |
| ¿Qué intervalo y ejercicio cubre? | Fechas y, si hay diferencia de acumulados, archivo comparativo y corte fiscal |
| ¿Qué moneda tiene cada fila? | Moneda fija declarada o columna; funcional del estudio diferenciada |
| ¿Se importan todas las cuentas o una selección? | Selector explícito; ningún filtro automático por plan contable nacional |
| ¿Puede repetirse una cuenta, con distintos conceptos/monedas? | Rechazar o agrupar con procedencia, y elección de descripción |
| ¿Qué significa una cuenta ausente o un saldo cero? | Missing separado de cero; regla de ausencia y tratamiento visible |
| ¿Una reimportación añade o sustituye? | Identificador de lote sustituido; nunca borrar todo por abrir pantalla |

Ejemplo C01 que debe entenderse: enero acumulado 100 y febrero acumulado 160 producen febrero 60 **solo** con acumulados comparables y modalidad aprobada. Como importe de periodo, 160 sigue siendo 160. ClosingBalance es evidencia de cierre, no coste mensual automáticamente.

## 2. Base y lectura

Leer ESTADO_PROYECTO, ADR-0002/0003/0004, evidencia/ficha T05, decisiones vigentes, prompt maestro v0.4, fichas T06/T06A, CM01/CM15/CM16 y documento internacional. Mantener las fuentes canónicas y sus hashes. Revisar main/PRs/instrucciones antes de editar; conservar cambios ajenos.

Mantener .NET 10/C#/WPF/MVVM/SQLite local, un editor y puertos tipados. T05 ofrece Study, revisión independiente, modelo publicado congelado, metadatos/vigencia de moneda, estado Open/Closed, capture/history y dos hashes. El archivo v3 es el nuevo formato histórico; su mapping/migración/snapshot no se reescriben para incluir importación. La fixture genuina `tests/Valdrio.Tests/Fixtures/t05-v3.json` conserva cinco fotos contextuales y auditoría del recorrido WPF identificado en sus metadatos; verificar SHA al descomprimir y usarla en F20/F21 sin reconstruirla con el mapping nuevo.

No reutilizar el parser del editor decimal como adaptador contable de manera implícita. `DecimalEditSession` interpreta una edición visual; el perfil de un archivo debe conservarse independientemente de preferencias. Extraer/reutilizar una lógica pura solo con un contrato y pruebas que demuestren su ámbito de origen.

## 3. Subfases y salida

| Bloque | Trabajo acotado | Puerta |
|---|---|---|
| T06.0 | Perfil de negocio/D03 y contrato técnico del staging | Ejemplo aprobado o declaración explícita de modo fixture; modalidades separadas |
| T06.1 | Adaptadores CSV/XLSX y perfil versionado | Filas originales/ubicación/tipos conservados, ninguna escritura de estudio |
| T06.2 | PrepareImport, diagnósticos y conciliaciones | Igual origen/perfil produce igual interpretación; errores nunca cero |
| T06.3 | Formato v4, lotes/filas/enlaces/recursos, ConfirmImport | Migración real, scope/revisión/idempotencia/rollback e importación íntegra |
| T06.4 | Evolución de snapshots/lectores y puente funcional soportado | V1 contextual conservado; schema nuevo para recursos; ninguna falsa preparación completa |
| T06.5 | Asistente WPF, runner y evidencia | Archivo→hoja/columnas→perfil→preview→confirmar, reapertura/cancelación/backup |
| T06.6 | Cierre y directrices T06A | Alcance FX y decisiones pendientes visibles; CI del último commit |

Cada bloque compila y tiene evidencia. Si solo se completa el staging de una fuente, registrar esa subentrega; no presentar T06 completa ni reemplazar contratos por TODOs que devuelvan datos inventados.

## 4. Perfil de origen

Definir `SourceParseProfileVersion` inmutable con ID/versión y payload schema. Incluye formato/hoja, fila de cabecera y datos, mapeo de columnas, encoding/delimitador/quote para CSV, decimal/group separator y tamaños de grupos admitidos, DatePattern, modalidad/fechas, convención de signos, moneda fija/columna, duplicados, ceros y selección de cuentas. Identificar el ejemplo y estado de aprobación del perfil de negocio.

La CultureTag puede documentar el origen; los tokens efectivos y reglas están congelados y no dependen de overrides del equipo. `1,234` y `01/02/2026` sin perfil inequívoco bloquean IMP_AMBIGUOUS_FORMAT. Un nombre de país no completa un perfil. La UI inglesa con formato de pantalla en-US puede importar un archivo es-ES con coma decimal sin modificar ese perfil.

Cuenta y descripción son texto ordinal. Conservar `00123`, Unicode, comillas/delimitadores dentro de campos y espacios significativos según política. Una cuenta numérica en XLSX puede haber perdido ceros: no reconstruirlos sin una regla explícita del perfil. No convertir códigos mediante ToUpper de cultura actual.

Distinguir campo ausente, vacío, cero, inválido y celda de error. Debe/Haber necesita la fórmula y convención aprobadas; un campo vacío no equivale a cero automáticamente. Un signo negativo, paréntesis, trailing-minus o signo explícito solo se acepta si el perfil lo declara. Validar agrupación; no Replace global de coma/punto. NBSP/narrow-NBSP se aceptan exclusivamente si se declaran.

## 5. Adaptadores y original

`IImportAdapter` recibe stream/ruta autorizada, perfil y cancelación; devuelve filas de origen tipadas para preparación, no Resource ni entidades EF. CSV debe manejar delimitadores dentro de quoted fields, comillas escapadas, saltos de línea y encoding/BOM declarado. No usar Split por línea/coma como parser definitivo.

Elegir la dependencia XLSX mínima después de verificar documentación oficial, versiones/licencia y API que conserve valor bruto y tipo de celda. Es un lector de archivo; no requiere Excel instalado. No añadir Excel COM, macros o recálculo oculto. Fórmula solo con valor cached utilizable, preservando fórmula y evidencia; sin cached o con error bloquea. Distinguir número, texto, fecha serial y vacío.

Los valores autorizados se convierten a decimal con validación de escala/magnitud. Evitar el puente double→decimal de una librería como supuesto de exactitud; obtener el lexical bruto cuando sea posible y declarar la precisión realmente disponible. No prometer recuperar dígitos ya perdidos por Excel. Una notación exponencial propia de celda numérica requiere parsing explícito de origen; la salida canónica sigue decimal invariante sin exponente.

Las fechas seriales XLSX necesitan sistema 1900/1904 declarado y caso serial 60 inválido; no convertirlas en UTC. Un texto se interpreta con DatePattern del perfil. Un periodo contable no cambia por zona horaria. Cuantizar la moneda visual no debe alterar importes internos.

Conservar FileHash, nombre, hoja, fila/columna y tipos/valores brutos necesarios. Si se guarda archivo original, documentar su almacenamiento/hash/copia; si solo se guardan celdas extraídas, no afirmar que se podrá reconstruir el workbook byte por byte.

## 6. Staging y PrepareImport

Definir `PreparedImport` con ID, StudyId/CompanyId, InputRevision, esquema, FileHash, hash de perfil, modalidad, fechas, política de selección, filas interpretadas, diagnósticos, totales por moneda/signo y huella de la preparación completa. Los valores tienen Money explícito y BusinessDate. Los errores llevan Code/MessageKey/argumentos/ubicación y severidad de bloqueo, separados del texto traducido.

PrepareImport lee los bytes/perfil y el contexto aplicable del estudio; no actualiza Study.Revision, recursos ni audit de mutación económica. Permitir staging local identificado si se necesita reanudar, separado de entradas confirmadas. Guardar preparación no crea un ImportBatch Confirmed. Modificar archivo/perfil/fechas invalida el preparado anterior y produce uno nuevo; no editar silenciosamente sus filas finales.

Reglas de duplicados: Reject por defecto; AggregateWithProvenance es una elección explícita. Agrupar conserva **cada fila**, su original y aportación funcional. Descripciones distintas exigen elección de destino, sin tomar la primera arbitrariamente. Agrupación multimoneda muestra originales por moneda; no produce un OriginalMoney nominal único de varias divisas.

PeriodAmount utiliza el importe aprobado del periodo. ClosingBalance permanece evidencia. CumulativeExpenseDifference alinea dos fuentes por cuenta y ámbito aprobados; una ausencia queda Missing, no cero. Identificar cuentas aptas, periodos comparables y corte de ejercicio; no restar acumulados de ejercicios diferentes por conveniencia. No filtrar 6/7 universalmente.

Conciliaciones de preparación: número de filas leídas/ignoradas por regla/invalidas, importes por moneda y signo, aportaciones seleccionadas, agregados y evidencia excluida. Mostrar por qué cada fila genera recurso o queda fuera. No sumar monedas nominalmente ni considerar una neta global cero prueba de corrección por fila.

## 7. Moneda funcional y alcance de T06A

La moneda funcional viene del Study, no del archivo de empresa ni de la región. Identidad fuente=funcional conserva importe y puede identificarse con conversión 1. Para otras monedas hacen falta RateSet aplicado, elección explícita por original, fecha/fuente/orientación y quantum de conversión.

T06 puede entregar confirmación de fuentes en la moneda funcional y staging multimoneda bloqueado cuando falte el puente. **No anunciar confirmación internacional completa hasta T06A.** Una UI de ejemplo de cotización T02A no suministra automáticamente tipos a los datos reales. No usar su quote de demostración o un valor vigente sin solicitud.

T06A debe persistir FX originals/applications/versiones junto al lote y habilitar la conversión. Tipos faltantes u orientación inválida bloquean antes de modificar estudio. Preparar con RateSet nuevo no sustituye originales/conversiones históricas. `RevalueStudy` transaccional seguirá exigiendo integración con particiones/consumos de T07 y publicación de T12; no cerrar A07/I14 con mocks.

## 8. Persistencia v4 y ConfirmImport

Añadir contexto/mapping/migración/snapshot v4 propios. Registrar rutas conocidas desde v3 y, si se admiten, v1/v2 vía cadena; migrar a nueva copia. Evolucionar `CompanyFile` para el nuevo paso 3→4, conservando las revisiones y handlers anteriores. T05 hoy limita la cadena a dos pasos; ampliarlo de forma explícita y probada, no borrar eventos o volver a Revision=0.

Tablas mínimas: ImportBatches, ImportedRows, ResourceSourceLinks y Resources. UUIDs y scope por empresa+estudio+lote; FK compuestas, índices estructurales y sin cascadas que borren históricos. Importes/porcentajes/tasas como TEXT canónico, no REAL. Los originales confirmados son inmutables. Un recurso conserva enlaces; su efectivo funcional proviene de los originales convertidos, sin añadir ajustes/partes ficticios de T07.

ConfirmImport incluye PreparedImportId/huella, CompanyId/StudyId, ExpectedRevision, RequestId, actor y modo. Verifica en transacción: estudio abierto, revisión/contexto iguales al preparado, aprobación de perfil, cero bloqueos, integridad/hash del staging y ausencia de cambios de bytes/perfil. Un objeto de UI mutable no es fuente autorizada del commit.

Modo inicial recomendado: AppendBatch. ReplaceIdentifiedBatch requiere identificar el lote y explicar qué dependencias conserva/sustituye; si no cabe, posponer ese modo con diagnóstico. No implementarlo mediante DELETE de todos los recursos. Nueva revisión no debe duplicar dinero de una importación previa inadvertidamente.

Guardar lote, filas, recursos/enlaces, Study.Revision+1 y auditoría de importación en una única transacción. T05 replay de Study conoce Create/UpdateContext/ChangeState; añadir un handler tipado de ConfirmImport que vincule lote/recursos y describa la nueva revisión. No disfrazar el evento de UpdateStudyContext ni aceptar operaciones desconocidas.

RequestId debe proporcionar idempotencia: replay idéntico devuelve el mismo lote; datos distintos fallan. FileHash+perfil+modalidad+fechas detectan repetición con un aviso/bloqueo explícito, aparte de RequestId. Una nueva solicitud no debería duplicar recursos de un mismo lote por doble click.

Fallos tras escritura real/cancelación precommit dejan estudio/lotes/recursos/audit intactos. Comprobar otra conexión. Después de commit, informar del lote confirmado aunque se solicite cancelar el refresco. Históricos anteriores siguen consultables; CurrentRunId continúa null hasta T12.

Con recursos presentes, cambiar moneda en UpdateStudyContext no puede cambiar solo el código. Bloquearlo con operación necesaria de revaloración/escenario, o implementar la revaloración completa en su tarea. Cambiar periodo/modelo exige revalidar compatibilidad con recursos importados y diagnósticos; no mantener un prepared import como válido frente a otra revisión.

## 9. Evolución de inputs y hashes

`valdrio-study-input-v1` es contextual y exige arrays económicos vacíos. No modificarlo para tolerar recursos bajo el mismo nombre. Introducir esquema nuevo con recursos/enlaces/originales/conversiones/perfil/totales según módulos efectivamente disponibles; conservar deserialización y hashes del v1 histórico.

El getter `StudySnapshotReceipt.Input` actual solo lee v1: diseñar dispatch por esquema hacia DTOs tipados antes de guardar payloads nuevos. No usar string/object/JsonElement libre como contrato de cálculo, ni reinterpretar v1 aplicando defaults v2. Replay/audit e interfaces de consulta deben soportar ambos sin regenerar fotos históricas.

Extender la proyección económica campo a campo: cantidades/importes autorizados, monedas/tipos/políticas, selección y reglas con efecto económico. Procedencia (archivo/hoja/fila/raw/profile) entra en FullSnapshotHash; solo lo que altera interpretación entra también en EconomicInputHash. Si se quiere que formatos equivalentes produzcan huellas económicas iguales, definir IDs estables de los recursos equivalentes y probarlos; no prometerlo cuando cada importación genera UUIDs distintos por diseño.

Mantener política decimal completa, definiciones congeladas, orden ordinal, campos obligatorios, round-trip estricto y rechazo de duplicados/ámbito/unidad/moneda inválidos. Snapshot con recursos importados aún no está listo para repartir si faltan particiones, claves, actividad o políticas. Los diagnósticos evolucionan según datos reales; no desaparecer todos al confirmar un lote.

## 10. UI y runner

Asistente con navegación conservable: fuente/hoja → columnas/perfil → modalidad/fechas/moneda → preview → confirmación. Permitir corregir configuración y rehacer preview; una fila inválida permanece visible. Mostrar importes originales y funcionales separados, totales por moneda y efecto esperado de añadir/sustituir. La elección de idioma no cambia el perfil.

Cancelar mantiene el estudio y resultados previos. Trabajo de archivo/DB fuera del hilo WPF, cancelación observable y editores coordinados. Textos EN/ES con ubicación de errores/acción; fechas tipadas o editor con cultura capturada. Pseudo/RTL/formatos regionales se ensayan sobre el flujo completo.

Runner recibe preparado/input/receipt versionado y produce salida canónica verificable, incluyendo reconciliaciones del ámbito ya implementado. Comparar con la misma preparación persistida/WPF; no una fixture distinta con valores coincidentes. No declarar cálculo de costes o conservación terminal antes de T08–T12.

## 11. Matriz mínima F01–F24

| ID | Caso y prueba requerida |
|---|---|
| F01 | Preparar/cancelar deja bytes/entradas/revisión/audit sin cambios económicos |
| F02 | Archivo/perfil válido produce preview y confirmación idénticos por fila |
| F03 | Error en fila intermedia: diagnóstico y cero filas del lote confirmadas |
| F04 | PeriodAmount 160 frente a diferencia 160−100=60 y cierre separado |
| F05 | Acumulados no comparables/ejercicios distintos/cuenta ausente bloqueados |
| F06 | Debe/Haber, negativos/abonos y ceros según perfil aprobado |
| F07 | Decimal ambiguo, agrupación inválida, exponentes/límites/underflow no se convierten en cero |
| F08 | Cuenta 00123/Unicode y XLSX cuenta numérica con política explícita |
| F09 | Duplicado rechazado; agrupación elegida conserva todas las filas y descripciones resueltas |
| F10 | CSV quotes, delimitador dentro de campo, multiline, BOM/encoding/NBSP declarado |
| F11 | XLSX número/texto/formula cached/sin cached/error/fechas 1900–1904–serial60 |
| F12 | Original distinto de funcional sin FX: preview explicable y confirmación bloqueada |
| F13 | Moneda fuente=funcional conserva Money; ninguna región sustituye el ISO |
| F14 | Preparado con revisión/contexto obsoletos rechazado sin writes |
| F15 | Doble click/replay RequestId exacto mismo lote; RequestId reutilizado con otros datos rechazado |
| F16 | FileHash+perfil repetido identificado sin duplicar recursos inadvertidamente |
| F17 | Fallo tras primera escritura CreateBatch/Rows/Links/Resources hace rollback integral |
| F18 | Cancelar precommit revierte; cancelación posterior conserva confirmación |
| F19 | Scope/FK compuestas no permiten filas/enlaces de otra empresa/estudio/lote |
| F20 | v3→v4 en copia, v1/v2 genuinos si se admiten, procedencia/audit/hash conservados |
| F21 | Snapshot v1 histórico sigue idéntico; esquema nuevo estricto y recursos reales congelados |
| F22 | UI inglesa/es-ES y española/en-US, de-DE/en-GB, UTC/Madrid, pseudo/RTL conservan interpretación |
| F23 | WPF confirma→cierra→reabre→consulta y backup/restauración; runner mismo preparado/input |
| F24 | CI del commit final: suite anterior+nueva, corpus/SHA intactos, artifacts, evidencia y decisiones |

## 12. Prompt ejecutable

> Ejecuta T06 en VALDRIO desde main y CI comprobados. Lee estado, ADR-0004, evidencia T05, decisiones, CM01/CM15/CM16, ficha T06/T06A y directrices actuales. Conserva fuentes canónicas y trabajo ajeno.
>
> Resuelve D03 con muestra y ejemplo aprobado antes de confirmar una fuente real; mientras tanto puedes implementar adaptadores con fixtures identificadas. No inferir signos/modalidad/filtros/moneda por cultura o cuentas nacionales.
>
> Implementa perfil versionado y adaptadores CSV/XLSX que conserven fila/hoja/celda/tipo/original. PrepareImport es independiente de la UI y no modifica estudio. Valida ambiguos, duplicados, ausencias, límites, fechas y monedas; cada error tiene ubicación y nunca se sustituye por cero.
>
> Añade v4/migración nueva y upgrade consistente a copia. ConfirmImport comprueba preparado/hash/contexto/revisión/aprobación, guarda lote+filas+enlaces+recursos+Study.Revision+audit juntos y soporta replay definido. Prueba rollback tras escrituras reales y reapertura independiente. No borrar todos los recursos ni usar REAL/Excel COM.
>
> Evoluciona Study replay y lector/codec de snapshots con esquema nuevo para recursos. Conserva v1 contextual y hashes anteriores. FunctionalCurrencyCode es del estudio; fuentes de otra divisa bloquean si falta puente confirmado T06A. No usar el quote de la UI demo ni cerrar RevalueStudy/A07 mediante mocks.
>
> Integra el asistente y runner con entradas reales comunes, EN/ES/regiones/zonas independientes, cancelación y conservación histórica. Completa F01–F24 por su alcance, documenta límites FX y políticas pendientes, cierra únicamente con CI aprobada del último commit y prepara las directrices T06A. Si falta D03, registra la subentrega fixture/staging sin afirmar aceptación productiva.

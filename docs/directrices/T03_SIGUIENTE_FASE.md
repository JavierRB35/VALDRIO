# VALDRIO — Directrices para la siguiente fase de desarrollo
Fecha: 5 de octubre de 2026. Versión 1.0.
Destinatarios: equipo de desarrollo y LLM que ejecutará las tareas.
Próxima entrega: **T03 — archivo de empresa y SQLite real**.

## 1. Objetivo y situación de partida

La siguiente fase debe convertir los fundamentos de VALDRIO en una aplicación que crea, abre, cierra y vuelve a abrir un archivo local de empresa sin cambiar su identidad, precisión ni datos. Debe impedir una segunda edición simultánea y recuperarse de errores sin guardar agregados incompletos.

La base sigue siendo .NET 10, C#, WPF/MVVM y SQLite mediante Infrastructure. Domain y Calculation permanecen independientes de WPF, Entity Framework, Localization, SQL e Internet. Application orquesta casos de uso tipados. Es una aplicación de escritorio local y de un editor por archivo.

Se ha revisado el commit original `14b6501a9fe38cff700d8e772347a63101997dfc`. Las correcciones A01–A06 se han implementado en la entrega que acompaña este documento. Leer [la evidencia de cierre](../evidencias/AUDITORIA_CIERRE_2026-10-05.md) para conocer el commit comprobado, las pruebas y los límites de esa comprobación.

Hay primitivas monetarias, temporales, de identidad y precisión, redondeo balanceado, proyección monetaria y FX probado. **Todavía no hay archivo de empresa SQLite productivo, importación contable ni motor completo de costes.** Las pantallas actuales y los identificadores `ui-demo` son ejemplos de desarrollo. No tratarlos como gestión persistente de un estudio.

T03 no debe construir el modelo económico completo. Su resultado es una base persistente fiable sobre la que después se desarrollan T04, T05, T06, T06A y T07.

## 2. Fuentes que debe leer el equipo

En este orden:

1. [ESTADO_PROYECTO](../../ESTADO_PROYECTO.md) y [decisiones vigentes](../decisiones/DECISIONES_PENDIENTES.md).
2. [Prompt maestro v0.4](../../specification/v0.4/VALDRIO_Prompt_Desarrollo_v0_4.md).
3. [Ficha canónica T03](../../specification/v0.4/tareas/T03.md).
4. [Datos y contratos de aplicación](../../specification/v0.4/especificacion/04_Datos_y_Contratos_de_Aplicacion.md).
5. [Internacionalización](../../specification/v0.4/especificacion/05_Internacionalizacion_y_Extension.md).
6. Código y pruebas de las correcciones, especialmente CanonicalSnapshotJson, CanonicalValueJsonConverters, FxConversionHash, FxConverter y FrozenList.

La especificación v0.4 conserva sus fuentes y huellas originales. Este documento concreta su ejecución; no sustituye sus contratos ni acepta políticas económicas. Si se necesita cambiar un contrato, registrar el motivo, el impacto y las pruebas antes de implementarlo. No editar referencias o manifiestos para esconder un incumplimiento.

## 3. Decisiones y límites que se conservan

| Tema | Regla vigente | Consecuencia para esta fase |
|---|---|---|
| D01 | R1 para gestión; valoración contable de existencias en T32 | No implementar inventario contable en T03 |
| D02 | Un editor por archivo, uso local | Bloqueo durante toda la sesión de edición |
| Moneda | La empresa propone; cada estudio fija su moneda funcional | Guardar DefaultFunctionalCurrencyCode, sin imponerla a futuros estudios |
| D03 | Pendiente de archivo contable representativo | No decidir modalidad ni signos del importador; no bloquea T03–T05 |
| D04–D07 | Propuestas de capacidad, auxiliares, lotes y márgenes | No activarlas por defecto ni declararlas aprobadas |
| D08/D09 | Protección, recuperación comercial y licencias pendientes antes de T24 | Recuperación técnica básica ahora; no prometer cifrado ni introducir validación online |
| D10/D11 | Políticas de autorreparto y cobertura aún propuestas | No convertirlas en decisiones económicas dentro del esquema |
| D12 | Módulos avanzados posteriores | No añadir TDABC, estándares o simulación en esta entrega |

La finalidad de reducir mantenimiento no elimina pruebas, migraciones, copias ni compatibilidad. La arquitectura debe permitir evolucionar módulos sin introducir servidores o servicios operativos innecesarios.

## 4. Contratos técnicos que ya deben respetarse

### Identidad y reconstrucción

- Generar una identidad de empresa al crear el archivo y conservarla al reabrir, copiar y restaurar.
- Nuevas entidades persistentes usan UUID canónicos y ámbitos explícitos. No regenerar IDs al cargar.
- UnitId, EntityId, FxRateId y destinos de redondeo se validan en sus fronteras. No admitir un valor por defecto como identidad válida.
- Códigos de negocio, incluidos futuros códigos de cuenta como `00123`, son texto. No convertirlos en números.
- La carga de datos inválidos produce un diagnóstico de archivo/datos. No sustituirlos por cero, fecha mínima, moneda predeterminada o un UUID nuevo.

### Decimal y moneda

- Guardar importes, cantidades y tasas como TEXT canónico y JSON string.
- Aplicar CanonicalDecimal.Format/Parse y las validaciones de la política persistida.
- Mantener la política completa y su versión. No recuperar solo el nombre y sustituir sus parámetros por los defaults actuales.
- PDEC-1-proposed tiene importes 8, cantidades 12, porcentajes 18, magnitud inicial 1e12 y corrección automática máxima 1e-8.
- No sumar, comparar u ordenar cantidades monetarias mediante REAL, float, double o CAST a REAL en SQLite. Filtrar por ámbito/IDs y calcular con valores tipados en C#.
- Money siempre conserva moneda. El catálogo monetario actual es un fixture, no un catálogo internacional completo de producto.
- Para vistas monetarias de producto, usar los metadatos congelados de la moneda y el overload tipado del formateador. La vista debe conservar al menos sus unidades menores; una proyección conciliada sigue siendo distinta del ledger.

### Tiempo e internacionalización

- Fechas de negocio como DateOnly/BusinessDate ISO. Auditoría como UtcInstant, obtenido mediante IClock.
- Idioma de interfaz, cultura de formato, zona de presentación, perfil de origen y moneda son dimensiones independientes.
- Textos y diagnósticos nuevos tienen recursos completos EN/ES. No introducir mensajes de negocio literales en ViewModels.
- Diagnóstico con Code, MessageKey y argumentos tipados. La traducción se realiza al presentar.
- Mantener es-ES, en-US, en-GB y de-DE en la verificación. Pseudolocalización/RTL verifica composición visual; no equivale a ofrecer una traducción adicional.
- La cultura capturada de una edición permanece hasta confirmar o cancelar; un cambio de región no reinterpreta texto pendiente.
- Una fecha técnica ISO debe anunciar su formato. Una fecha de presentación usa las preferencias seleccionadas.

### Serialización, huellas e inmutabilidad

- CanonicalSnapshotJson es la base versionada de valores, con esquema `valdrio-values-v1`; no es todavía el contrato completo de RunInput de T05.
- Reconstruir mediante converters/factories validados. No usar JsonSerializer predeterminado como contrato de almacenamiento.
- No aceptar campos ausentes, duplicados, desconocidos, valores inválidos o versiones futuras mediante defaults silenciosos.
- En T05 definir DTO y validación de agregado del snapshot económico y completo, además de esta base. Ordenar por IDs estables las colecciones cuyo orden no tenga significado.
- FX se adjudica a escala 8 desde el producto exacto de coeficientes enteros. ConvertedRaw puede ser aproximado; original y quote conservan la reconstrucción exacta y el ajuste conserva el puente.
- Original no nulo que pasa a cero debe conservar FX_AMOUNT_BELOW_QUANTUM y su procedencia.
- Las huellas nuevas FX usan `fx-v2`. Persistir junto a cada huella su esquema de hash. No etiquetar una huella fx-v1 como v2.
- No hay datos SQLite productivos antiguos que migrar en esta entrega. Si en el futuro se leen huellas antiguas, distinguir versiones y aplicar una migración explícita.
- Usar copias defensivas/colecciones inmutables en DTO de resultados y snapshots. IReadOnlyList sobre un array compartido no es garantía suficiente.

## 5. Diseño de persistencia y archivo

Implementar el acceso a SQLite en Infrastructure. Definir puertos y DTO en Application. La composición de Desktop conecta implementaciones. Los ViewModels no reciben DbContext, conexiones SQLite ni entidades rastreadas por EF.

El esquema inicial debe cubrir exclusivamente lo necesario para T03:

| Agregado/dato | Contenido mínimo | Invariante |
|---|---|---|
| FileMetadata | Identificador de formato, FileSchemaVersion y metadatos de compatibilidad | Reconocer inequívocamente un archivo VALDRIO |
| Company | Id, Name, DefaultFunctionalCurrencyCode, CreatedAtUtc | Una empresa por archivo |
| UnitDefinition | Id, ámbito, Code, Dimension, Symbol, DecimalScale, IsDiscrete | Unidad válida, código único en el ámbito acordado |
| Política de precisión | Versión y parámetros completos aplicados al archivo/valores | Reapertura sin cambiar precisión |
| AuditEvent | Entidad, operación, antes/después, actor local, UTC y motivo cuando aplique | Se confirma con la operación correspondiente |
| Historial de migraciones | Migraciones EF y versión del formato coherentes | No interpretar una versión desconocida como actual |

Los nombres físicos pueden adaptarse a la estructura del repositorio. Registrar el diseño en un ADR breve y en la migración inicial. No crear todas las tablas futuras vacías por adelantado.

Reforzar en SQLite identidad, unicidad y FK. La regla de una empresa debe tener una restricción física comprobable, por ejemplo un slot único con valor 1. Los ámbitos futuros necesitan claves compuestas cuando una FK simple permitiría cruzar modelos.

Configurar foreign_keys en cada conexión y comprobar que está activo. Elegir y documentar journal mode y política de busy timeout; no depender de defaults del equipo. WAL requiere copias coherentes y manejo de sus archivos asociados. SQLite necesita transacciones cortas y operaciones serializadas; un DbContext no se comparte entre hilos.

Revision es long y la genera la aplicación. FileSchemaVersion, versión del modelo, revisión del estudio y versión del algoritmo representan conceptos distintos. No usar SQL Server rowversion ni un único contador global como sustituto de las revisiones de los agregados.

## 6. Entregas pequeñas y secuenciales

Las siguientes tareas descomponen las tres subentregas de la ficha T03. Cada bloque deja código compilable y evidencia. No declarar T03 terminada hasta cerrar el flujo completo.

### T03.1a — Contratos y decisiones físicas

Definir CreateCompanyFile, OpenCompanyFile, CloseCompanyFile, sus DTO, modos de apertura, resultado tipado y dueño de la sesión. Especificar:

- creación en ruta nueva y comportamiento si existe;
- compatibilidad conocida, futura, archivo ajeno y archivo corrupto;
- propiedad y liberación del bloqueo;
- límite de transacción;
- forma de expresar error y si hubo cambios;
- ubicación separada de datos, preferencias y programa.

Diseñar adapters de valores canónicos, sin inventar fórmulas económicas. Entregar ADR, diagrama pequeño de dependencias si ayuda y contrato de diagnósticos EN/ES.

**Aceptación.** Application no depende de EF/WPF y los contratos no exponen entidades rastreadas. La decisión física es revisable antes de construir UI.

### T03.1b — Esquema inicial y mappings

Crear migración inicial, DbContext interno y converters de valores. Hacer explícito TEXT para decimales y códigos, formato de UUID/fechas, nulabilidad y restricciones.

Probar los converters con máximos, negativos, cantidades de 12 decimales, quote de 18 y Unicode. Cuando esos valores aún no pertenezcan al esquema productivo de empresa, usar un fixture de integración aislado para el mapping; no presentar una tabla de prueba como importación implementada.

**Aceptación.** SQLite real sobre disco, inspección de typeof/valor guardado y reapertura por un contexto nuevo. La estructura mínima existe y los valores no pasan por REAL.

### T03.2a — Crear y reabrir

Crear primero en una ruta temporal única, inicializar esquema y datos completos, validar y cerrar; promover a la ruta solicitada sin sobrescribir. Un archivo preexistente se conserva. Si falla, eliminar únicamente el temporal propio.

Al abrir, comprobar formato/esquema antes de escribir o ejecutar migraciones. Cargar la identidad existente y validar tipos. Una empresa inválida no se repara automáticamente mediante defaults.

**Aceptación.** Crear → cerrar todas las conexiones → volver a abrir produce la misma identidad, moneda sugerida, unidades, precisión y fechas. Añadir una comprobación entre procesos, además de los contextos de una sola prueba.

### T03.2b — Bloqueo y compatibilidad

Mantener un bloqueo de edición durante toda la vida de la sesión, no solo durante SaveChanges. Un segundo proceso debe recibir un diagnóstico y no convertirse en otro editor. Usar un mecanismo de exclusión del sistema operativo y tratar de forma coherente rutas normalizadas/alias admitidos; un booleano persistido IsLocked no sirve.

La finalización normal o caída del proceso libera el bloqueo del sistema. No exigir que el propietario quite manualmente una bandera obsoleta. La modalidad de lectura concurrente, si se ofrece, se documenta y se prueba.

Para esquema más nuevo, permitir solo una lectura realmente compatible o bloquear apertura. No ejecutar migrations sobre una versión desconocida. Probar el rechazo sin modificar el archivo.

**Aceptación.** Dos procesos, cierre normal, terminación forzada y nueva apertura. No se anuncia colaboración en carpetas de red ni sincronización viva en servicios de nube.

### T03.3a — Transacciones, fallo y recuperación básica

Crear una operación de integración con al menos dos escrituras relacionadas y auditoría. Inyectar un fallo después de la primera escritura y antes del commit. Verificar mediante una conexión independiente que no quedan cambios parciales.

Preparar una copia consistente de un archivo abierto mediante el mecanismo de backup de SQLite o una estrategia equivalente comprobada. No copiar solo el .db mientras WAL conserva cambios. Validar la copia y restaurarla a una ruta nueva, preservando el anterior.

Migraciones conocidas se prueban con fixtures de sus versiones reales. No inventar compatibilidad con versiones que nunca existieron. Antes de una migración de datos existente, disponer de copia validada y recuperación probada.

**Aceptación.** Fallo parcial, cancelación, fallo de creación y copia con cambios ya confirmados en WAL. Reapertura y lectura demuestran el estado, no solo la llamada a un mock.

### T03.3b — Flujo WPF funcional

Entregar Inicio → Crear empresa → Elegir ruta nueva/moneda predeterminada → Abrir → Consultar identidad/compatibilidad → Cerrar → Reabrir.

Mantener el archivo activo si falla la apertura de otro. Evitar que un error destruya el contexto de trabajo anterior. Mostrar acción correctiva y si hubo cambios.

La UI se limita a entrada, navegación y presentación. Los casos de uso comparten implementación con tests/runner. Las operaciones de disco no bloquean innecesariamente el hilo de interfaz y tienen cancelación con semántica definida.

**Aceptación.** Recorrido automatizado Windows mediante controles/comandos reales, con evidencia del archivo creado y reabierto. Capturas son complemento, no prueba de escritura o rollback.

### T03.3c — Cierre y continuidad

Ejecutar CI sobre el commit final; comprobar build sin advertencias, suite existente y tests nuevos, guards, referencias, publish y recorridos WPF.

Actualizar estado, ficha de trabajo y evidencia con commit, run, comandos, resultados, limitaciones y siguiente bloque. Entregar archivo de ejemplo generado por el código y sin datos de terceros.

**Aceptación.** T03 puede demostrarse desde un clon nuevo. No queda un TODO en una garantía declarada como terminada.

## 7. Matriz de aceptación que debe acompañar T03

| ID | Prueba | Evidencia requerida |
|---|---|---|
| F01 | Crear, cerrar, reabrir y comprobar identidad | SQLite en disco y proceso/contexto nuevo |
| F02 | Ruta existente | Bytes y datos del archivo anterior intactos |
| F03 | Decimales límite y negativos | TEXT exacto, valor tipado idéntico, sin REAL |
| F04 | Texto 00123 y Unicode | Conservar código y símbolos; AccountCode real se probará además en T06 |
| F05 | Unidad/ID/moneda/política inválidos al cargar | Diagnóstico; sin defaults ni escritura de reparación |
| F06 | Segunda empresa, FK y códigos duplicados | Restricción SQLite realmente ejecutada |
| F07 | Dos editores en procesos distintos | Solo uno entra en edición |
| F08 | Cerrar y terminar forzadamente propietario | El siguiente editor abre sin quitar banderas manuales |
| F09 | Versión de archivo futura | Bloqueo/lectura compatible explicitada; archivo conservado |
| F10 | Archivo ajeno, incompleto o corrupto | Error comprensible; empresa activa anterior conservada |
| F11 | Fallo después de primera escritura | Ningún dato parcial ni auditoría huérfana |
| F12 | Cancelación antes de commit | Estado previo confirmado al reabrir |
| F13 | Copia con WAL activo | Incluye últimos cambios confirmados y se abre |
| F14 | Restauración y copia dañada | Ruta nueva; original intacto; copia dañada rechazada |
| F15 | EN + es-ES + USD; ES + en-US + EUR; de-DE | Presentación cambia; valores, IDs y política no |
| F16 | Zona de presentación distinta | BusinessDate y UTC almacenado permanecen iguales |
| F17 | Crear/abrir/cerrar por WPF | Controles/comandos reales y resultado SQLite comprobado |
| F18 | Clon limpio y CI final | Pruebas, guards, TRX, commit/run y artifact de ejemplo |

En F03, el fixture de mapping no prueba un importador inexistente. En F05, probar también el dato corrupto insertado directamente por el fixture, para verificar la frontera de lectura. En F11, un mock con CommitCount=0 no demuestra rollback SQLite.

## 8. Diagnósticos y gestión del error

Definir códigos estables como FILE_ALREADY_EXISTS, FILE_IN_USE, FILE_SCHEMA_NEWER, FILE_INVALID_FORMAT, FILE_DATA_INVALID, FILE_WRITE_FAILED, FILE_BACKUP_INVALID y FILE_OPERATION_CANCELLED. Estos nombres son una propuesta técnica; comprobar colisiones con el catálogo antes de registrarlos.

Cada diagnóstico indica la operación, si hubo commit y qué puede hacer el usuario. Errores esperados se devuelven estructurados; excepciones técnicas se registran aparte. No capturar todo y continuar con una base vacía ni crear una empresa nueva al fallar OpenCompanyFile.

El registro técnico no contiene claves de licencia ni incorpora automáticamente sumas y saldos completos. La auditoría de negocio es distinta del log técnico y participa en la transacción del cambio.

## 9. Revaloración A07 y tareas posteriores

El contrato actual de RevalueStudy prueba la orquestación, conserva candidatos inmutables y usa revisiones long. Sus puertos todavía no tienen implementación persistente productiva. **T03 prueba la infraestructura de transacción; no puede certificar la revaloración completa sin estudios, recursos y partes.**

Asignar explícitamente las obligaciones restantes:

| Hito | Obligación |
|---|---|
| T03 | Transacciones reales, lectura validada, bloqueo, reapertura, copia y rollback |
| T04 | Maestros/centros, modelo borrador/publicado y ámbitos; publicación inmutable y duplicación |
| T05 | Study.Revision long, lectura coherente, DTO RunInput y hashes económico/completo, con políticas/versiones congeladas |
| T06 | Importación preparada sin cambios y confirmación atómica, con D03 resuelta mediante archivo representativo |
| T06A | Perfil internacional y puente por origen/moneda/tipo; conversions y esquemas de hash persistidos |
| T07 | Ajustes/particiones, revalidación por recurso y revaloración persistente completa |

Al integrar A07:

1. Cargar originales, ajustes y cobertura autorizados desde el repositorio. No confiar únicamente en Money enviado por la UI.
2. Validar estudio, ámbito, revisión esperada, IDs únicos, conjunto completo y políticas/moneda compatibles.
3. Reconvertir originales y deltas que deban cambiar; no reutilizar importes viejos bajo la etiqueta del RateSet nuevo.
4. Revalidar particiones y consumos con las políticas vigentes; no aprobar automáticamente un cambio económico.
5. Confirmar cambios, revisión y AuditEvent en una única transacción con comparación de revisión.
6. Probar revisión obsoleta, fallo tras primera escritura, falta de cobertura y reapertura.
7. Mantener snapshots y resultados anteriores intactos. No marcar como actual un resultado de otra revisión.

Esas pruebas deben usar SQLite real y una conexión independiente que compruebe el estado. Conservar la distinción entre resultado histórico válido y resultado actual.

## 10. Evidencia y trabajo mediante LLM

Cada sesión entrega una sola subentrega principal, su prueba relevante y continuidad. Puede resolver por sí misma decisiones técnicas reversibles dentro del alcance.

Antes de editar, comprobar commit, rama, estado y cambios de otros. Trabajar en una rama propia. Leer instrucciones del repositorio si existen. No sobrescribir trabajo ajeno ni forzar referencias Git.

Para cada contrato importante, registrar requisito → código → prueba → resultado. Los 39 casos del verificador racional son referencias independientes; no equivalen a 39 recorridos completos del motor C#.

La evidencia mínima contiene:

- commit exacto y enlace a CI;
- comandos ejecutados y resultado, sin atribuir ejecuciones locales si solo se ha usado CI;
- número de pruebas, fallos y omitidas;
- casos nuevos que fallan antes y pasan después cuando corrigen un defecto;
- archivo/fixture, restricciones y fallos observados;
- acciones WPF verificadas, separadas de capturas/revisión visual;
- límites actuales y tareas siguientes;
- decisiones económicas que siguen pendientes.

Mantener TRX y artifacts comprobables. No aumentar tolerancias, quitar una prueba válida ni cambiar el esperado para hacer pasar el código. Si no hay ejecución local disponible, la CI de Windows es una vía de verificación; un build no sustituye las pruebas de comportamiento.

## 11. Prompt de inicio para el LLM de la siguiente fase

> Trabaja en JavierRB35/VALDRIO. La siguiente tarea es T03, archivo de empresa y SQLite real. Lee ESTADO_PROYECTO, DECISIONES_PENDIENTES, el prompt maestro v0.4, la ficha T03, los contratos de datos e internacionalización y estas directrices.
>
> Verifica primero la rama, el commit y la CI real. Conserva las correcciones de la auditoría y su suite de regresión. Identifica la primera subentrega T03 pendiente; no declares terminada una funcionalidad que solo tenga puertos o mocks.
>
> Mantén .NET/C#/WPF y SQLite local, un editor por archivo, Domain/Calculation puros y Application con contratos tipados. Guarda decimales como TEXT canónico, conserva política completa/versionada, UUID, DateOnly y UTC. No uses REAL para cálculo económico.
>
> Comienza por T03.1a y T03.1b. Después completa creación/reapertura, bloqueo/compatibilidad, transacciones/copias, UI y cierre en ese orden. Puedes dividir sesiones, pero cada bloque queda compilable y con continuidad. No construyas importación o cálculo de costes en T03.
>
> Implementa código, integración y las comprobaciones de F01–F18 que correspondan al bloque. Usa SQLite real sobre disco y dos procesos para el bloqueo. Inyecta fallo después de una escritura; demuestra rollback mediante reapertura. Protege archivos existentes y restaura copias a una ruta nueva.
>
> Externaliza textos EN/ES, captura la cultura de edición, separa idioma/región/zona/moneda y entrega diagnósticos concretos. Usa los metadatos monetarios para presentación; no conviertas una vista redondeada en ledger.
>
> No cambies D03 ni aceptes D04–D07 por conveniencia. No añadas SaaS, colaboración simultánea, licencias online o cifrado prometido sin una decisión aplicable. D03 se necesitará antes de T06; no bloquea esta fase.
>
> Ejecuta verificaciones pertinentes, deja CI del commit final y actualiza ESTADO_PROYECTO, ficha y evidencia. El cierre debe mostrar un flujo real crear/cerrar/reabrir y sus límites. Entrega la siguiente subentrega exacta si la sesión termina antes de cerrar T03.

## 12. Criterio de autorización para avanzar

T03 se cierra cuando el usuario puede gestionar un archivo local estable y la matriz demuestra precisión, integridad, compatibilidad y recuperación. Después comenzar T04, sin repetir los fundamentos ni introducir otra arquitectura.

Las pruebas de T03 certifican la gestión del archivo. Reciprocidad, subactividad, objetos de coste, fabricación y márgenes recibirán sus pruebas económicas y decisiones en las tareas previstas. Mantener esa frontera al explicar el avance al propietario.

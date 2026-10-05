# VALDRIO — Directrices para T04: modelos versionados y centros

Fecha: 2026-10-05. Destinatarios: equipo y LLM de desarrollo. Estado: hoja de ejecución; T04 todavía no está implementada.

## 1. Resultado del siguiente bloque

El usuario configura un modelo pequeño desde WPF: registra centros físicos, crea un borrador, añade nodos principales/auxiliares y sus unidades, publica una versión estructural inmutable y crea un duplicado editable. Puede desactivar un maestro sin perder la explicación de una versión histórica.

Ejemplo obligatorio: centro físico `PRENSA-001` con nodo `SETUP` y driver lote, y nodo `PROCESO` con driver hora. Otro centro físico `MANT-001` tiene un nodo auxiliar con driver hora. La función producción/mantenimiento es independiente del papel principal/auxiliar. Un pool por nodo en R1; no mezclar horas y lotes ni asignar el mismo coste entero a varios pools.

T04 entrega configuración estructural. No entrega tarifas, reciprocidad, reparto, importación, actividad ni costes de productos. Publicar aquí no certifica que el modelo esté preparado para todas las ejecuciones futuras: las tareas de claves, reglas y capacidad añaden sus validaciones explícitas.

## 2. Base que debe conservarse

Leer ESTADO_PROYECTO, evidencia T03, ADR-0002, decisiones, prompt maestro, ficha canónica T04, contratos de datos y documento internacional. No editar las fuentes v0.4 ni sus huellas. Comprobar commit y CI antes de trabajar; conservar toda la suite existente.

Stack .NET 10/C#/WPF/MVVM, SQLite local y un editor por archivo. Domain/Calculation puros. Puertos de Application, transacciones en Infrastructure, UI sin DbContext ni SQL. Decimales TEXT canónico, UUID y UTC mediante IClock. Preferencias de idioma/región/zona fuera del archivo de empresa.

D03 necesita un archivo contable antes de T06, pero no bloquea T04/T05. D04–D07 siguen propuestas. No elegir `NormalizedManagement`, `OwnerOnceCapacity`, lotes o márgenes como defaults económicos aprobados. Tampoco activar autorreparto/cobertura definitivos por conveniencia del editor. Cifrado/licencias quedan en sus puertas.

## 3. Resolver primero la evolución del archivo

T03 reconoce exclusivamente esquema 1 y nunca migra al abrir. T04 requiere **esquema 2 y una actualización segura**; agregar tablas al archivo v1 sin cambiar versión no es aceptable.

1. Añadir registro de formatos soportados y migraciones conocidas. Detectar metadata y versión con lectura mínima anterior a usar el DbContext vigente.
2. Ofrecer una acción de actualizar compatible. Mantener fuente v1 intacta: generar copia SQLite coherente en destino nuevo, migrar el temporal/copia propia a v2, validar, cerrar y promover sin sobrescribir. Reusar la reserva/bloqueo de T03; no abrir una ventana sin propietario.
3. Conservar CompanyId, unidades, política completa, auditoría y procedencia de actualización. Registrar versión origen/destino y migración. La aplicación antigua debe rechazar v2 sin alterar bytes.
4. Un fallo/cancelación durante la migración no altera el origen ni publica un destino parcial. Versiones futuras/ajenas se rechazan antes de writes. No usar EnsureCreated ni suprimir PendingModelChangesWarning.
5. No editar `V1Model` ni `InitialCompanyFile`; usar mapping v2 y nueva migración/snapshot. Ensayar desde un archivo creado por el código T03 original, además de bases nuevas.
6. No ignorar ni eliminar automáticamente un `__EFMigrationsLock` de un archivo ajeno. Migrar temporales propios limita la recuperación al proceso que los creó; registrar su eliminación si falla.

El validador T03 concilia exclusivamente operaciones CreateCompany/AddUnits con Company.Revision. **No agregar eventos de modelos a ese contador**. Evolucionar AuditEvent con AggregateType, AggregateId, AggregateRevision, esquema de payload y operación/transaction ID cuando haga falta. Los eventos v1 pasan al ámbito Company sin reinterpretar su contenido. Validar cada familia de payload con un handler tipado y conservar lectura histórica. La revisión del modelo no cambia al cambiar preferencias ni por crear otro modelo.

## 4. Contratos y entidades

| Entidad | Campos y ámbito | Invariante |
|---|---|---|
| PhysicalCenter | Id, CompanyId, Code TEXT, Name, IsActive, Revision | Código único por empresa; desactivar no borra históricos |
| ModelVersion | Id, CompanyId, VersionNumber, Status, ParentVersionId, Description, Revision long, PublishedAtUtc, referencia de política | Estados explícitos; publicado inmutable; padre de la misma empresa |
| CalculationNode | Id, ModelVersionId, PhysicalCenterId, Code, Role, FunctionTags, IsActive | Código único dentro de la versión; Principal o Auxiliary, sin Mixed |
| CostPoolDefinition | Id, ModelVersionId, NodeId, DriverUnitId, Description, CostLayer, estado de política de capacidad | Exactamente un pool por nodo activo al publicar; unidad válida |
| Publicación | Payload canónico, SchemaVersion, fecha/actor/revisión y huella estructural | Congela definiciones necesarias para explicar el histórico |
| Auditoría | Agregado, revisión, operación, antes/después, UTC y actor | Confirmada en la misma transacción que el cambio |

Determinar en ADR el ámbito de VersionNumber y la identidad lógica de un modelo. Si se añade un identificador de familia, justificarlo, impedir cruces de empresa y probarlo; no usar el número de versión como UUID. ParentVersionId y linaje no sustituyen los IDs propios de cada nueva versión.

Las políticas metodológicas todavía no materializadas deben estar explícitamente pendientes, con su ID/versión/estado. Separar validación estructural de preparación económica: el motor futuro bloqueará una política requerida no resuelta. El editor no calcula ni presume su resultado. CostLayer es una clasificación; no acepta D07 por elegir una etiqueta.

Maestros pueden evolucionar. Congelar en la publicación códigos/nombres/definiciones de unidades y centros aplicados, además de IDs. Desactivar o renombrar un maestro no debe cambiar lo que muestra una versión ya publicada. Evitar que el histórico dependa de un JOIN a la descripción vigente.

## 5. Operaciones y transacciones

Puertos tipados: CreatePhysicalCenter, ChangePhysicalCenter/DeactivatePhysicalCenter, CreateModelDraft, SaveModelDraft, ValidateModel, PublishModel, CloneModel y consultas defensivas. Cada comando de edición incluye ExpectedRevision y ámbito, no DTO EF ni un grafo que la UI pueda reasignar a otra empresa.

- Validar en Application/domain y reforzar IDs/ámbitos/unicidad en SQLite. Una FK por UUID individual no impide un pool de A que apunte a un nodo de B: usar claves alternativas y FK compuestas por ModelVersionId/NodeId. Unit y PhysicalCenter deben pertenecer a la empresa de la versión.
- Cambiar un borrador confirma su agregado, revisión y auditoría en una sola transacción. Revisión obsoleta o error revierte todo.
- PublishModel vuelve a validar desde persistencia en una lectura coherente; no confía en un resultado ValidateModel viejo. Confirma estado, snapshot congelado y auditoría juntos. Reintentos tienen una respuesta definida, sin publicaciones/IDs duplicados.
- Toda ruta de escritura rechaza un publicado: UI, casos de uso y repositorio. Reforzar campos/datos congelados en persistencia o diseñar almacenamiento insert-only para publicación. La protección no puede depender solo del botón deshabilitado.
- CloneModel admite un publicado, crea borrador nuevo, IDs nuevos para sus nodos/pools y remapea todas sus referencias internas. Conserva ParentVersionId y linaje; el maestro físico puede seguir siendo el mismo. Un cambio en el clon no modifica el original.
- Desactivación de maestros impide nuevas selecciones según el estado, pero conserva referencias/snapshots existentes. No usar cascade delete para borrar versiones o hechos históricos.

## 6. Subentregas y puertas

| Bloque | Entrega | Puerta de salida |
|---|---|---|
| T04.0 | ADR, esquema v2 y copia/actualización segura | V1 intacta; v2 nueva abre; fallo/versión futura probados |
| T04.1 | Centros físicos y unidades seleccionables | Código texto/Unicode, unicidad y ámbito reales; WPF guardar/reabrir |
| T04.2 | Borrador, nodos, pools, papel y función | Ejemplo SETUP/PROCESO de distintas unidades; FK de otro modelo falla |
| T04.3 | Validación y publicación estructural | Diagnósticos claros; snapshot reproducible/inmutable; revisión obsoleta bloqueada |
| T04.4 | Duplicación y desactivación | Clon editable con IDs/remapeo propios; histórico idéntico tras cambiar maestros |
| T04.5 | WPF, internacionalización, CI y evidencia | Flujo completo con botones reales y comprobación independiente SQLite |

Cada bloque debe compilar y dejar pruebas/continuidad. No declarar T04 terminada con entidades sueltas o mocks. No agregar APIs futuras vacías para aparentar avance. Si se divide en sesiones, registrar el próximo bloque y las garantías pendientes.

## 7. Editor WPF

Desde el archivo activo, ofrecer Maestros y Modelos. Mostrar claramente Borrador/Publicado, revisión, padre y alcance de validación. El editor permite seleccionar centro, papel, función y unidad como dimensiones independientes. Los códigos de negocio conservan ceros y Unicode.

Guardar, Validar, Publicar, Duplicar y Desactivar usan comandos de Application. Mostrar errores junto a la entidad/campo con Code/MessageKey y argumentos tipados. Publicado se consulta; editar ofrece crear un borrador. La pérdida de ediciones pendientes debe tener una elección clara de guardar/descartar/cancelar.

Disco fuera del hilo WPF; cancelación antes de commit. No cerrar ni sustituir la empresa activa por fallo. Textos nuevos EN/ES, accesibilidad, teclado, expansión/pseudo y RTL de ensayo. Formato es-ES/en-US/de-DE/en-GB independiente de idioma, moneda y zona; no guardar fechas traducidas o enum como etiqueta de interfaz.

## 8. Matriz de pruebas mínimas

| ID | Caso | Resultado comprobable |
|---|---|---|
| M01 | Actualizar archivo v1 a destino nuevo | IDs/política/unidades/auditoría iguales; origen SHA intacto |
| M02 | Fallar/cancelar entre pasos de migración | Ningún destino publicado; origen reabre en T03 |
| M03 | Versión futura o formato ajeno | Rechazo antes de write/migración; bytes intactos |
| M04 | Centro 00123/Unicode y código repetido | Texto intacto al reabrir; duplicado rechazado físicamente |
| M05 | SETUP lote y PROCESO hora | Pools distintos sin sumar unidades incompatibles |
| M06 | Papel auxiliar + función producción/comercial | Dimensiones independientes; Mixed inválido |
| M07 | Pool apunta a nodo de otra versión/empresa | Domain y FK compuesta rechazan el cruce |
| M08 | Unidad inexistente/inactiva o pool ausente/duplicado | Diagnóstico de publicación; ningún publicado parcial |
| M09 | Publicar y modificar por UI/puerto/persistencia | Toda ruta normal rechaza; snapshot original idéntico |
| M10 | Cambiar un maestro tras publicar | Descripción y unidades históricas permanecen congeladas |
| M11 | Duplicar y editar nodos/pools | IDs nuevos, referencias remapeadas, padre conservado, original idéntico |
| M12 | Fallo tras primera escritura al guardar/publicar/clonar | Datos, revisiones y auditoría previos tras reapertura independiente |
| M13 | ExpectedRevision viejo, repetir publicar o clonar | Conflicto/reintento explícito; sin efectos dobles |
| M14 | Desactivar centro con referencia histórica | Histórico íntegro; selección nueva según regla documentada |
| M15 | Idioma/región/zona y otro orden de UI | Snapshot estructural/IDs/políticas iguales; solo presentación cambia |
| M16 | Ciclo WPF crear→guardar→publicar→duplicar→reabrir | Botones reales; archivo leído con sesión independiente |
| M17 | Backup/restauración después de actualizar | Modelo publicado/clon/auditoría conservados; origen intacto |
| M18 | Clon limpio y CI final | Suite previa+nueva, guards, TRX, artifact y evidencia del commit |

No usar bases solo in-memory para M01/M02/M07/M12/M17. Inyectar fallo después de SaveChanges real y comprobar desde otra conexión. Una captura no prueba un commit. Una aserción sobre un mock no prueba rollback.

## 9. Cierre y siguiente fase

Actualizar ESTADO_PROYECTO, ficha T04, ADR/esquema y evidencia con commit/run, pruebas reales, ejemplo y límites. Conservar las 39 referencias matemáticas y corpus canónico sin cambios. Generar archivo sintético v2 con el ejemplo de centros/modelos; distribuirlo como artifact con manifest de formato y no como datos de terceros.

Tras T04, T05 implementará estudios, períodos/escenarios, Study.Revision long, snapshots completos y hashes económico/completo. El hash estructural de T04 **no sustituye** EconomicInputHash. T06 necesitará D03 y la muestra contable. RevalueStudy se integrará sobre originales/particiones persistentes en sus tareas posteriores.

## 10. Prompt listo para el LLM ejecutor

> Ejecuta T04 en JavierRB35/VALDRIO. Lee el estado, evidencia T03, ADR-0002, decisiones, prompt maestro v0.4, ficha canónica T04, contratos de datos/internacionalización y estas directrices. Comprueba rama, commit, CI y trabajo ajeno.
>
> Empieza por T04.0: registra diseño v2 y actualiza copias a un destino nuevo sin modificar v1. No edites la migración histórica ni migres formatos desconocidos. Evoluciona auditoría por agregado, conservando Company.Revision separada de ModelVersion.Revision y futuras Study.Revision.
>
> Implementa centros físicos, borradores, nodos Principal/Auxiliary, funciones independientes y un pool homogéneo por nodo con unidad. Usa el ejemplo PRENSA SETUP/lote y PROCESO/hora, más MANT auxiliar/hora. Refuerza ámbitos en Domain y FK compuestas SQLite.
>
> Entrega validación estructural, publicación atómica/inmutable, duplicación con IDs propios/remapeo y desactivación sin pérdida histórica. Congela las definiciones aplicadas. La publicación estructural no aprueba políticas económicas pendientes ni declara cálculo disponible.
>
> Integra editor WPF con comandos reales y puertos de Application; preserva el archivo activo al fallar. Externaliza EN/ES, respeta idioma/región/zona/moneda y captura de ediciones. No introduzcas SQL/WPF/DbContext en Domain o Calculation.
>
> Completa los bloques T04.0–T04.5 y verifica M01–M18 con SQLite en disco, fallo tras primera escritura, reapertura independiente y recorridos WPF. No cambies esperados/corpus para ocultar errores. D03 y D04–D07 conservan su estado.
>
> Cierra con CI del commit final, artifact de ejemplo, estado/evidencia actualizados y directrices T05. Si una sesión no termina la fase, registra qué bloque está probado y cuál sigue; no presentes el plan o un puerto vacío como trabajo terminado.

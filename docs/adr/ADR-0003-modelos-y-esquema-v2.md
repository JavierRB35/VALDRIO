# ADR-0003 — Formato v2 y modelos versionados

Fecha: 2026-10-05. Alcance T04. Decisiones técnicas; D03–D12 conservan sus estados.

## 1. Contextos históricos y evolución segura

El archivo mantiene `VALDRIO-COMPANY` y pasa de esquema 1 a 2. El registro del servicio conoce únicamente estas dos versiones y las migraciones `202610050001_InitialCompanyFile` y `202610050002_VersionedModels`. La detección mínima de FileMetadata precede al modelo EF vigente; versiones futuras o formatos ajenos se rechazan en lectura.

`CompanyDbContext`, `V1Model`, `InitialCompanyFile` y su snapshot permanecen congelados y sin cambios. `ModelDbContext`, `V2Model` y `ModelDbContextSnapshot` describen v2. Al crear, solo un temporal propio recibe las dos migraciones en orden: el contexto histórico crea v1 y el vigente aplica v2. Cada snapshot coincide con su modelo; no se suprime PendingModelChangesWarning. Las versiones posteriores deberán tener mappings/snapshots propios y mantener los históricos congelados.

Abrir v1 devuelve FILE_UPGRADE_REQUIRED. `Upgrade` adquiere propiedad del origen y reserva un destino nuevo. La API SQLite BackupDatabase copia a un temporal del destino; se migra exclusivamente esa copia, se actualiza metadata y registra procedencia, se valida y se promueve sin sobrescritura. El origen conserva los mismos bytes, ID, política, unidades y eventos. El handle de destino pasa a la sesión sin liberación intermedia. Cancelación/fallo limpia solo temporales propios; no publica un archivo parcial. Si hay un lock EF no vacío en la copia, se rechaza: no se borra automáticamente un lock ajeno. Las copias/restauraciones de T03 siguen usando la misma API consistente y un destino nuevo.

El fixture `tests/Valdrio.Tests/Fixtures/t03-v1.json` contiene comprimido un archivo sintético realmente producido por WPF T03, commit `17c166e51bd41cb38477c32cc62bdd789d7815ba`, CI 37314810801. La prueba verifica SHA antes de usarlo. No se crea un v1 falso con el mapping v2. El servicio T03 original se conserva como referencia de prueba (solo namespace adaptado): reabre el origen tras los fallos y rechaza v2 sin escribir. También se comprueba su modelo congelado.

## 2. Ámbitos e identidad de versiones

Un `ModelFamily` identifica el modelo lógico dentro de una empresa. Cada `ModelVersion` tiene UUID propio y VersionNumber único y creciente dentro de la familia. Crear un modelo inicia familia/número 1. Duplicar un publicado crea el siguiente número en una transacción y conserva ParentVersionId; pueden existir varias ramas de borradores, con números únicos. ParentVersionId identifica el antecedente concreto; no sustituye UUIDs ni revisión. Solo se duplica un publicado.

Centros físicos son maestros de empresa, con código TEXT BINARY único, nombre, IsActive y Revision long. Cada nodo tiene código único en su versión, papel Principal/Auxiliary y etiquetas de función independientes y ordenadas ordinalmente. No hay papel Mixed. Cada pool pertenece a un nodo de la misma versión; el índice único impide dos pools por nodo. Publicar exige un pool por nodo activo y al menos un principal activo. Lote y hora son unidades diferentes; el editor no suma sus cantidades.

FK compuestas refuerzan empresa+maestro, empresa+modelo y modelo+nodo. No existe borrado en cascada de históricos. Los códigos preservan Unicode, mayúsculas/minúsculas y ceros: la unicidad es ordinal, no una traducción regional. No se renombran ni convierten automáticamente unidades físicas.

## 3. Políticas y publicación

Método y capacidad contienen ID, versión y estado Pending explícitos. T04 rechaza una referencia Accepted porque aún no existe el caso de uso que materializa una política económica aprobada. CostLayer clasifica (Unclassified/Fixed/Variable/Mixed), sin calcular ni aprobar tratamiento de capacidad o perímetros. D04–D07, D10 y D11 siguen en sus puertas.

Guardar valida identidad, referencias, unicidad y nuevas selecciones; un borrador puede carecer de pools y por ello no publicarse. Validar devuelve diagnósticos estructurales por entidad. Publicar vuelve a leer/validar en transacción y congela `PublishedModelStructure`: estructura, centros físicos aplicados con sus nombres/códigos y definiciones completas de unidades. No consulta maestros vigentes para explicar ese histórico. Desactivar centros/unidades preserva referencias existentes, impide nuevas selecciones y bloquea publicación si un nodo activo depende de ellos. Un clon conserva esas referencias y pide resolverlas antes de publicar.

Una publicación guarda esquema de payload, JSON canónico y SHA-256 estructural. Orden de nodos/pools/etiquetas es ordinal por ID/código; idioma, región, zona y preferencias no intervienen. Fecha/actor de publicación pertenecen a la envoltura/auditoría; no al hash estructural. Este hash **no es EconomicInputHash** ni certifica que haya recursos, claves o capacidad suficientes para calcular.

Publicado: UI de solo lectura, rechazo en puertos y triggers de integridad sobre versión, nodos, pools y publicación. Estos triggers no contienen fórmulas económicas. El snapshot se inserta antes del cambio de estado dentro de la misma transacción. SQL exige publicación/pools/principal al pasar a Published y bloquea después update/delete/insert sobre el grafo histórico. Cambiar maestros no altera el JSON ni la huella publicados.

## 4. Revisiones, auditoría y reintentos

AuditEvents conserva columnas/payloads v1 y añade AggregateType, AggregateId, PayloadSchema y TransactionId. Revision es la revisión de ese agregado. La migración asigna a los eventos existentes ámbito Company y TransactionId=Id sin modificar sus JSON. Company.Revision sigue contando solo CreateCompany/AddUnits; centros y ModelVersion tienen secuencias propias; UnitStates tiene su revisión y eventos desde el primer cambio de actividad. El upgrade tiene un único evento CompanyFile. T05 añadirá Study, sin reutilizar ninguno de esos contadores.

Cada comando guarda cambios/revisión/auditoría en una sola transacción. Lectura valida handlers tipados y secuencia antes/después contra el estado actual. Un error no se convierte en defaults ni reparación automática. El actor local-user representa una sesión local, no autenticación ni firma; hashes/auditoría detectan incoherencia, no constituyen protección criptográfica frente a un atacante con acceso al archivo.

ExpectedRevision obsoleto produce conflicto. Repetir publicación produce rechazo de publicado, sin otro evento. CloneModel exige RequestId: reintentar el mismo comando devuelve el mismo clon (en su estado actual); reutilizarlo con distinto origen/actor/descripción falla. Una solicitud nueva permite un nuevo clon. Todas sus IDs de nodo/pool cambian y se remapean; los maestros físicos y unidades mantienen su identidad de empresa.

## 5. Integración y límites

ICompanyFileSession.Models expone IModelWorkspace con la misma vida, modo y puerta de serialización del archivo. La UI no recibe DbContext, conexiones ni filas EF. IO en Task.Run, listas defensivas y catálogos EN/ES. El panel distingue aplicar una edición local, guardar borrador, validar y publicar. Antes de abandonar una empresa/modelo con cambios ofrece guardar/descartar/cancelar. Una cancelación tardía posterior al commit conserva el resultado confirmado.

R1 sigue siendo Windows local con un único editor. No se implementa reparto, actividad, tarifa, importación, estudios o ejecución. El catálogo de cuatro monedas sigue siendo un ejemplo de desarrollo. Cifrado/licencias/distribución comercial pendientes de T24; no se presenta la build de desarrollo como entrega comercial.

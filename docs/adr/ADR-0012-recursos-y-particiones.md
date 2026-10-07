# ADR-0012 — Recursos persistidos, particiones y preparación guiada

Fecha: 2026-10-07. Estado: implementado en la rama T07. Decisión técnica; no acepta
las propuestas económicas D04/D05/D07/D11.

## Problema y solución

T06A conserva importaciones y conversiones, pero faltaba preparar sus recursos para
repartirlos. Se integra el núcleo de reglas T07 existente con puertos de aplicación,
persistencia SQLite y editor WPF. La UI no calcula importes ni elige criterios económicos.

`ResourceOrigins` identifica establemente cada cuenta dentro del estudio y cada fila
de cada lote. Agrupa por código ordinal, no por descripción; los originales siguen
separados por fila y moneda. En comparativos, la fila anterior se conserva como
contribución negativa con su conversión aprobada. Solo cuentan los lotes vigentes.

`ResourcePreparationRules` comprueba efectivo = original convertido + ajustes y
efectivo = directo + pools + excluido + pendiente por recurso. No basta el neto global.
Se exige al menos una parte incluso para un recurso de cero. IDs de origen y rutas
impiden reutilizar el mismo origen como recurso directo y como otro pool completo.
Una factura duplicada en archivos externos sigue requiriendo control de la fuente;
no se promete deduplicación por similitud de contenido.

`ResourceWorkspaceRules` añade ámbito de empresa/estudio/modelo, existencia de objetos,
pools activos, unidades publicadas y propietario de capacidad compatible. Fijo y
variable necesitan horizonte, unidad y rango; comportamiento, capa y ruta son
independientes. Ausente no equivale a cero. La cobertura incompleta se conserva como
pendiente explícito; los bloqueos definitivos corresponden a fases posteriores.

## Escritura y auditoría

El formato de empresa v8 añade `ResourceChanges`, un registro inmutable de comandos
y recibos con estado anterior, revisión confirmada y ledger resultante. Las vistas
normalizadas de ajustes/partes/asignaciones se derivan del ledger tipado. Una única
transacción escribe cambio, eventos de auditoría y revisión del estudio. Un error,
cancelación previa al commit o revisión obsoleta no deja datos parciales.

RequestId repetido con payload idéntico devuelve el recibo existente; cambiar su
payload se rechaza. No se reutilizan IDs de ajuste ni tras un reinicio. Cada ajuste
lleva un AuditEvent real; la apertura reproduce el historial y valida los enlaces,
revisiones y recibos. Esta comprobación detecta inconsistencias, no es firma digital
ni protección frente a un atacante que reescriba todo el archivo.

Los ajustes nuevos conservan el catálogo monetario congelado. La reversión utiliza
el negativo exacto del original con su conversión, no el tipo actual. Revalue exige
selección de tipo para cada valor extranjero, reconstruye fuentes y deltas y requiere
particiones conciliadas; no distribuye diferencias automáticamente. Mantiene los
snapshots previos, las fuentes importadas y el historial de tipos anteriores.

Modificar importaciones con fuentes distintas para un recurso preparado falla antes
de escribir. El usuario puede reiniciar la preparación con motivo y confirmación;
el historial sigue siendo reproducible. Importar cuentas nuevas independientes las
añade como pendientes. Un estudio cerrado o sesión de solo lectura no permite cambios.

## Snapshots y versiones

`valdrio-study-input-v6` congela contexto, estructura publicada, historial de lotes y
cambios T07. La deserialización reconstruye y valida el resultado. El hash completo
incluye procedencia y motivos; la proyección económica excluye nombres descriptivos
de objetos y motivos, conservando códigos, importes, FX, rutas y clasificación.
Idioma/región/zona de presentación no alteran el ledger ni el hash económico.

Los lectores v1–v5 se conservan. La actualización v1–v7 escribe en una copia v8;
la fixture v7 procede de la aceptación anterior y verifica bytes originales,
importaciones, snapshots e historial. No se cambian las fuentes canónicas.

## Interfaz y alcance

Empresa → Estructura → Estudio → Importación → Recursos. Recursos tiene cuatro pasos
internos: cuenta, ajustes, partes y revisión/guardado, con un único avance principal.
Los campos numéricos conservan la región de edición; cambiarla requiere confirmación
explícita antes de interpretar el texto. Los formularios pendientes bloquean el avance
con una explicación; volver atrás no los descarta. Las opciones avanzadas no sustituyen
el recorrido habitual. Ayuda contextual disponible con ratón, teclado y automatización.

Se mantienen los servicios anteriores y la separación Domain/Calculation/Application/
Infrastructure/Desktop. El catálogo de objetos es el mínimo de T07 para asignación;
las recetas, consumos, tarifas, repartos y políticas de capacidad siguen en sus fases.

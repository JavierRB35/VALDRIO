# ADR-0002 — Archivo de empresa v1

Fecha: 2026-10-05. Alcance técnico T03; no decide políticas económicas.

## Contratos y esquema

Application expone `ICompanyFileService`, `ICompanyFileSession` y snapshots defensivos. Infrastructure implementa EF Core/SQLite; Desktop compone el servicio e invoca los casos de uso fuera del hilo visual. Domain y Calculation no reciben persistencia.

Formato `VALDRIO-COMPANY`, esquema 1, migración `202610050001_InitialCompanyFile`. Solo se migra el temporal propio durante la creación. Abrir valida en lectura antes de admitir edición. Una versión distinta se rechaza sin migrar. Al introducir v2 se requiere un protocolo explícito con copia previa, migración ensayada y recuperación; T03 no promete migrar formatos todavía inexistentes.

Tablas: FileMetadata, Companies, PrecisionPolicies, Units, AuditEvents y los historiales internos EF. Una empresa por slot único 1; política con FK única a empresa; unidades con FK y código BINARY único por empresa. IDs UUID canónicos, códigos de negocio TEXT, UTC canónico. La moneda predeterminada solo sugiere la de estudios futuros.

`Company.Revision` es long y cuenta modificaciones del agregado de empresa, con comparación de revisión. **No sustituye Study.Revision o ModelVersion**. Cada escritura de unidades incluye revisión y auditoría en la misma transacción. El actor `local-user` identifica una sesión local; no representa autenticación ni una firma.

`V1Model` es el mapping histórico congelado, usado por la migración y el snapshot EF. No modificarlo al desarrollar v2. Crear su sucesor, una migración nueva y un snapshot nuevo; no reutilizar las convenciones de los DTO actuales para reinterpretar una migración histórica.

## Precisión y reconstrucción

`CanonicalDecimalConverter` persiste decimal como TEXT invariante, sin REAL. El esquema inicial guarda los parámetros decimales completos de la política; un fixture EF independiente prueba máximos, negativos, cantidades y tasas. Este fixture **no implementa importación ni tablas de recursos**.

La lectura reconstruye IDs, moneda, unidades, política y UTC, verifica integridad/FK/historial, y concilia auditoría con revisión y unidades. JSON de auditoría usa `valdrio-values-v1`, validación tipada y secuencia de antes/después. Datos inválidos generan diagnóstico, sin defaults ni reparación escrita. Esto detecta incoherencia, no equivale a protección frente a manipulación maliciosa.

## Conexiones y bloqueo

Conexiones cortas, sin pooling, `foreign_keys=ON`, `busy_timeout=2000`, `synchronous=FULL`. Producción edita en `journal_mode=DELETE`: rollback journal, adecuado al único editor y transacciones breves. No cambia silenciosamente el diario de un archivo abierto.

Un handle exclusivo de `<archivo>.lock` se mantiene hasta cerrar la sesión. La adquisición reintenta conflictos de compartición durante un máximo de 2 segundos, con cancelación, para tolerar la liberación diferida de handles tras cerrar un proceso; un propietario vivo sigue bloqueando. No se elimina ese sidecar al salir: eliminarlo introduce carreras. El OS libera el handle al terminar el proceso; la presencia del sidecar no significa que siga ocupado. Windows normaliza ruta física/caso/nombre corto, rechaza enlaces duros y reparse points, UNC y unidades de red. Las pruebas Linux son portables; el contrato de producto y la comprobación de identidad física son Windows. No usar enlaces duros en Linux ni carpetas con sincronización activa. No se puede detectar toda carpeta de sincronización de terceros; no se anuncia como colaboración soportada.

Una lectura independiente puede coexistir con el editor. La UI entrega edición y conserva la sesión anterior si falla crear/abrir/restaurar. Los comandos se serializan; cerrar la ventana durante una operación solicita cancelación y mantiene la ventana hasta finalizar.

## Creación, cancelación y recuperación

Creación y copia: temporal único en la carpeta destino → transacción/copia → cierre → validación → promoción mediante Move sin sobrescritura. Solo se limpian temporales propios. Un error de escrituras revierte unidades, revisión y auditoría. Cancelar se comprueba antes de confirmar o promover; después de ese punto el resultado es completado. No se borra un archivo confirmado para simular una cancelación tardía.

Copias mediante `SqliteConnection.BackupDatabase`, incluyendo commits aún en WAL en la prueba F13. El destino se cierra, valida y promueve como archivo independiente con diario DELETE. Restaurar siempre exige un destino nuevo; conserva origen, identidad e históricos. No se usa File.Copy sobre una base abierta.

D08 sigue pendiente: no hay cifrado, calendario de copias ni recuperación comercial. Un archivo inválido se rechaza; no se promete reparar corrupción. Cifrado y recuperación se decidirán antes de T24.

## Referencias técnicas

- Microsoft: [Backup de SQLite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup).
- Microsoft: [Limitaciones del proveedor EF SQLite](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations).
- SQLite: [rollback journal y WAL](https://www.sqlite.org/wal.html). La versión nativa realmente ejecutada se registra en TRX; en la comprobación local fue 3.53.3. El modo de producto no requiere WAL.

# Reparto primario: decisión del propietario, 9 de octubre de 2026

Ámbito autorizado: misma rama `feature/t09-primary-allocation-20261008`, PR #33.
Esta decisión sustituye en esta pantalla la preservación 1:1 de publicación,
selector de fase y controles de reparto secundario. No autoriza modificar main.

## Inventario y cambios aprobados

| Antes | Después |
| --- | --- |
| Biblioteca con abrir, publicar, crear versión | Abrir con doble clic, Intro o botón; guardar clave sin paso de publicación |
| Clave publicada inmutable | Nombre editable; composición editable si nunca se ha utilizado |
| Cuentas a nodos / auxiliares a nodos | Solo cuentas a nodos; los nodos auxiliares siguen siendo destinos primarios válidos |
| Comprobación en tarjeta separada | Total y validación junto a los destinos, antes de guardar |
| Cobertura de solo lectura | Desplegable por cuenta/parte con claves primarias completas del modelo del proyecto |
| Guardar regla aplicaba inmediatamente | Guardar sin aplicar; Aplicar actualiza el reparto y persiste la regla aplicada |
| Campos de regla en varias filas y prioridad | Selector, cuenta/prefijo y clave en una línea adaptable al ancho |
| Pestaña de reparto de auxiliares | Retirada de esta pantalla; se conservan datos e historial existentes |
| Volver a reglas automáticas | Quitar asignación manual, explicando su efecto sobre esa parte |
| Descripción de cuentas | Aclarar nombres de cuentas: resolver nombres importados en conflicto sin cambiar códigos/importes |
| Una misma advertencia por cada pendiente | Cada motivo se muestra una vez; las filas identifican las cuentas afectadas |

Se mantienen edición regional, ES/EN, solo lectura/estudio cerrado, revisión
optimista, importes absolutos, exclusiones/directos en opciones de cuenta,
procedencia, avisos al salir, cancelación, conciliación, hashes y capturas históricas.
Los códigos contables conservan su identidad textual y descripción.

## Reglas de uso

- Una clave guardada completa queda lista para asignar. Los borradores históricos
  pueden abrirse y completarse; no se ofrecen en el desplegable hasta ser válidos.
- Se considera usada una clave referenciada por una asignación manual o regla
  aplicada en cualquier análisis del archivo, incluido su historial. Guardar una
  regla pendiente por sí solo no bloquea la composición de la clave.
- Renombrar conserva identidad y composición. Una revisión nueva queda auditada;
  las capturas y los registros previos conservan el nombre que tenían al crearse.
- Editar y guardar una regla ya aplicada conserva su versión activa y almacena una
  modificación pendiente. Aplicar sustituye la regla original, incluso si cambia
  el selector, y actualiza el reparto mostrado. La modificación pendiente se guarda
  como regla inactiva con referencia opcional a su original, sin migración SQLite.
- Aplicar una regla sustituye otras del mismo selector. La asignación manual tiene
  preferencia; después la cuenta exacta y el prefijo más específico. Las prioridades
  históricas se conservan en los datos, pero no se piden al usuario.
- Elegir una clave en una fila guarda su asignación manual y recalcula. Aplicar
  reglas posteriormente respeta esa decisión. Quitar la asignación manual permite
  que las reglas actúen de nuevo sobre esa parte; no cambia la pestaña visible.

La validación del historial compara cada referencia con su revisión guardada,
no con el nombre vigente. La persistencia vuelve a comprobar el uso de la clave
antes de aceptar cambios económicos; la restricción no depende solo del botón.

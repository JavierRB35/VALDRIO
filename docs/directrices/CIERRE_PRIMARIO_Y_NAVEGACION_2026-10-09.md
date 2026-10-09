# Cierre del reparto primario y navegación

Petición del propietario del 9 de octubre de 2026. Misma rama de PR #33.

## Cambios de interfaz

- Eliminar clave: disponible para claves guardadas sin uso. La persistencia rechaza
  la eliminación si hay referencias en reglas, asignaciones, versiones derivadas o
  historial. La eliminación queda auditada; la clave deja de aparecer en la biblioteca
  y no puede reutilizarse su identificador. No se destruyen eventos anteriores.
- La tabla de asignación de cuentas aumenta de 200 a 420 unidades WPF. Sus filas
  conservan identidad y orden durante el guardado: se actualizan los valores de la
  fila, sin vaciar ni reconstruir la colección. La selección y el desplazamiento
  se comprueban con el control nativo, además de una regresión con 30 cuentas.
- La biblioteca reserva 120 unidades a Estado, sin saltos de línea; las etiquetas
  son «Lista» e «Incompleta». Se amplía el ancho mínimo del panel.
- Biblioteca, definición, cuentas, reglas y resultados se pueden plegar
  independientemente. Reglas y resultados empiezan plegados. Se conservan las
  acciones, navegación por teclado, formatos regionales y traducciones ES/EN.

## Cierre persistente

«Cerrar reparto primario» requiere cuentas importadas y un reparto completo y
conciliado. El cierre es un estado de esta fase, independiente del cierre general
del análisis; no publica una ejecución definitiva de cálculo.

Una vez cerrado:

- La pantalla 5 no permite correcciones ni cambios en recursos.
- La pantalla 6 conserva la consulta y permite reabrir, pero no cambiar asignaciones.
- El servicio rechaza nuevas importaciones y mutaciones económicas aunque procedan
  de un cliente con una pantalla anterior todavía abierta.
- Reabrir es una operación explícita separada; no se permite combinarla con cambios
  de reglas/importes en la misma petición. Tras reabrir, vuelve a habilitarse la 5.
- Cierre y reapertura quedan en el historial de configuración con revisión y auditoría.
  El estado se conserva al cerrar y volver a abrir el archivo.

La configuración y las claves incorporan indicadores opcionales, omitidos cuando
son falsos. Los datos históricos sin estos campos mantienen su serialización.
No se cambia el esquema de SQLite ni se reescriben capturas históricas.

## Inventario conservado

Asignación mediante tabla y por partes, importes absolutos, guardar/aplicar reglas,
renombrado de claves usadas, edición de claves sin uso, ajuste de descripciones,
validación numérica, procedencia, selección/cancelación y lectura de estudios cerrados.
Las nuevas restricciones económicas corresponden al cierre solicitado expresamente.

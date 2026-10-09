# Preparación de cuentas y distribución a nodos

Decisión del propietario, 2026-10-09: **«Aplicar el flujo 5 → 6»**, en la rama
`feature/t09-primary-allocation-20261008`, PR #33. Sustituye la exigencia de elegir
un pool en Preparar cuentas antes de utilizar las claves. No autoriza fusionar main.

## Recorrido aprobado

1. **Preparar cuentas** conserva código y descripción, saldo importado, conversión,
   correcciones extracontables, importe efectivo y conciliación. La corrección total
   se guarda al confirmar la celda; Intro/Tab/salida confirman y Escape cancela.
2. **Claves y reglas** recibe los importes efectivos sin exigir un destino previo.
   Las claves publicadas reparten entre nodos por porcentajes, unidades de driver
   o partes iguales. Se mantienen las prioridades, reglas exactas/prefijos,
   asignaciones manuales y cantidades absolutas.
3. Desglose, imputación directa a objetos, exclusiones, clasificación y capacidad,
   así como revalorización/reset, siguen accesibles en las opciones de cuenta de
   la pantalla 6. Se preserva lo ya guardado. No hay un segundo reparto en la 5.

## Contraste metodológico y terminología

La [Resolución IGAE de 28 de julio de 2011](https://www.boe.es/buscar/act.php?id=BOE-A-2011-13514),
apartados segundo, séptimo y noveno, distingue elementos de coste, centros,
actividades y criterios de reparto. Las claves relacionan costes con sus destinos
cuando no existe asociación directa. Es una referencia oficial de contabilidad
analítica pública, no una obligación general para cualquier empresa privada.

La [Resolución ICAC de 14 de abril de 2015](https://www.boe.es/buscar/act.php?id=BOE-A-2015-4394),
norma cuarta, distingue costes indirectos fijos y variables y su relación con
la capacidad normal a efectos de coste de producción. Su finalidad de valoración
no se confunde con cualquier análisis interno de coste completo.

**Decisión de producto derivada de ese contraste:** los ajustes preparan el importe;
los destinos y criterios corresponden a distribución. El usuario no debe elegir
un destino provisional que la clave reemplazará después.

En el código actual, el nodo identifica centro, papel y funciones; su pool define
la unidad de actividad y un campo histórico `CostLayer` (fijo/variable/mixto).
La clasificación del recurso usa otro concepto: `ResourceCostLayer`
(producción/comercial/estructura). No son intercambiables. No se copia fijo/variable
desde el nodo a todos sus costes, ni se convierte esa etiqueta en una capa funcional:
un centro puede recibir componentes de distinto comportamiento. La unidad de cada
destino se muestra desde el modelo publicado. Clasificar un coste no es requisito
para preparar su importe ni para previsualizar el reparto primario; la validación
metodológica de capacidad y absorción conserva sus requisitos posteriores.

## Conservación y compatibilidad

- Los `Pending` del libro de recursos pasan a ser elegibles para claves, sin crear
  un pool artificial ni modificar el libro al previsualizar.
- Solo las partes repartidas íntegramente pasan de pendiente a pools en la
  conciliación del resultado. Si falta una regla o hay ambigüedad, siguen pendientes.
- Se mantienen identidad, procedencia, signos, precisión, redondeo, tratamiento
  de asignaciones obsoletas y exclusiones; no se duplican costes directos/excluidos.
- Las nuevas capturas fijan `allocation-preparation-t09-v2`. Las históricas sin
  `IncludeUnroutedAccounts` conservan la política v1, su serialización y sus hashes.
- No se modifica el esquema SQLite ni se reescriben archivos históricos.

## Inventario funcional de la corrección de interfaz

Se conservan edición numérica regional, selección por teclado, cuentas con código
y descripción, correcciones idempotentes, estados de solo lectura/estudio cerrado,
publicación/versionado de claves, validación de totales, reglas de prioridad,
excepciones, nombres/procedencia, guardado/cancelación y reapertura. Los controles
especiales cambian de pantalla, no se eliminan.

Las tablas tienen mínimos de ancho, encabezados acotados y desplazamiento horizontal.
Los importes no se parten entre líneas; el texto de nombres/destinos se adapta al ancho.
El editor conserva las mismas listas de selectores mientras se escribe y notifica
solo el estado del formulario. La cobertura se recalcula al cambiar datos guardados,
no por cada pulsación. «Sí» al salir aplica los formularios válidos y guarda; una
validación fallida conserva la edición y muestra su motivo. El cierre incluye T08.

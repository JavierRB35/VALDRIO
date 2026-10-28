# Auditoría funcional: desarrollo y validación

Referencia: «VALDRIO · Auditoría funcional real», suministrada por el propietario. Base de trabajo: `a654e46`; rama `feat/functional-audit`. La petición autoriza implementar los ajustes y fusionar al terminar. La rentabilidad real de productos/clientes (RF-26) queda fuera del alcance económico actual, tal como establece la auditoría.

## Inventario conservado

- Preparación: importación y procedencia, correcciones, distribución previa, clasificación, historial, guardado y navegación. La clasificación utiliza el mismo estado y comandos que el reparto, no una copia independiente.
- Primario/secundario: biblioteca, creación/versión/borrado de claves, criterios, destinos, reglas, excepciones, descripciones de cuentas, asignaciones, resultados, conciliación, cierres/reapertura y operaciones del pie. Los controles existentes se recolocan en tareas; las explicaciones ampliables siguen disponibles.
- Resultados: cálculo/cancelación/actualización, selección de ejecuciones guardadas, flujos y selección de aportaciones, tablas de nodos/centros, detalle, conciliación, zoom y ampliación. Se añade resumen, concentración, consulta primaria sin guardar una ejecución y exportación de negocio.
- Actividad: observaciones normales/reales, calidad y fuentes, justificaciones de cero/excepción/corrección, capacidad teórica/práctica, importación física, borrador, confirmación, anulación e historial. El editor integrado conserva los tres métodos de propuesta. La tabla principal sirve para consulta y selección.
- Modelo: edición de centros/nodos, unidades existentes y alta opcional de unidad, borradores/publicaciones/versiones. Se conserva el bloqueo de modificación de estructuras publicadas.
- Presupuestos: composición, cantidad, base económica seleccionada, cálculo, aceptación/versionado, comparación e historial congelado. La necesidad física se muestra aunque falle la valoración económica.

## Correspondencia funcional

| Requisitos | Implementación y comprobación |
|---|---|
| RF-01 | Propagación de revisión confirmada desde servicios auxiliares y actualización al navegar; se preservan formularios pendientes. |
| RF-02–03 | Franja de contexto fuera del desplazamiento y del modo ampliado. Resultados tras secundario, antes de actividad. Consulta primaria independiente del cálculo secundario. |
| RF-04 | Preparación por importes, clasificación y comprobaciones; primario por asignaciones, claves y comprobaciones; secundario añade método/alcance y capacidad/servicios. Sin pestañas de reglas anidadas; pie de acciones conservado. |
| RF-05 | Pendientes de preparación identificados como previos a claves. En informes: número, positivos, abonos, neto y absoluto. Las cuentas sin importe distinguen asignación definida/pendiente. |
| RF-06 | Explicación y acceso a reapertura secundaria desde primario; revisión del cierre ampliable. |
| RF-07–08 | Clasificación compartida con preparación y conservación del estado sin revisar; mixto con complemento fijo. Ayuda diferenciada entre porcentaje cerrado y peso normalizado. Los borradores incompletos no se publican. |
| RF-09–10 | Capacidad y dependencias accesibles antes del cálculo; cobertura evaluada explícita. Preparación incompleta separada de fallo técnico, con cuentas e importes identificados y primario consultable. |
| RF-11–12 | Resumen sin sumar transferencias internas como coste nuevo. Concentración por nodo/centro, filtro por nombre/papel y umbral editable; base positiva y abonos separados, curva suprimida si neto no positivo. |
| RF-13–15 | Puente primario + recibido − cedido + ajustes = final; crecimiento indefinido con base cero. Detalle de cuentas, claves, importación y ajustes; aportaciones seleccionadas diferenciadas de totales globales. Conciliación no implica cobertura ni evaluación física. |
| RF-16–17 | Editor unificado, fuente y descripción normales únicas, propuesta automática y adopción sin narrativa. La confirmación conserva sus requisitos de fuente. |
| RF-18 | Generación idempotente de claves primarias directas publicadas al 100 %, equivalencia por destino y sin asignación automática de cuentas. |
| RF-19–20 | Borrado auditado de centros sin dependencias; bloqueo con referencias actuales/históricas. Descripción del nodo primero, edición a un clic y selección de texto; alta de unidad sigue bajo petición. |
| RF-21 | Antes de confirmar se presenta base importada activa, importe sustituido, incorporación y resultado; ajustes separados y advertencia sobre históricos. Se conserva el resumen de positivos/negativos y filas. |
| RF-22 | Tarifas con importes monetarios legibles y precisión de tarifa diferenciada; componentes, denominadores, fuentes y bloqueos por nodo/cuenta. |
| RF-23 | CSV UTF-8 con protección frente a fórmulas y documento paginado para impresión/PDF. Contexto y vigencia en cada página. Filtro de concentración y conciliación global claramente distinguidos. |
| RF-24 | Textos de negocio, fechas en zona configurada, entrada a cabecera y feedback de guardado de actividad sin color de error. |
| RF-25–26 | Necesidad física conservada en presupuesto incompleto, sin coste cero ni aceptación; informes del reparto separados de productos/presupuestos y sin inventar rentabilidad realizada. |

## Validación local

Las pruebas se ejecutan contra servicios reales y archivos de ejemplo/copias desechables. No se modifican archivos sincronizados de `sources/`, ni datos de trabajo del propietario.

- Compilación de escritorio: sin errores ni advertencias.
- Regresión completa y repeticiones específicas: resultados exactos en `resumen-pruebas.json`.
- `--functional-audit-only`: clasificación ES/EN, claves idempotentes sin reasignar, primario sin ejecución guardada; caso 17.500 y posteriores importes +1.000/+600/−100; asignación de mantenimiento; parcial 19.000/18.100 con pendientes +1.000/−100; histórico y exportación filtrada; capacidad 1.000/actividad 800 y presupuesto incompleto con necesidad física 20.
- `--allocation-only`: acciones WPF reales de claves/reglas, borrador de porcentaje incompleto, guardado/aplicación, descripciones y teclado; idiomas español/inglés y pseudo-RTL. El arnés se adapta a la biblioteca preexistente del ejemplo sin eliminarla.
- `--cost-flow-only`: flujos recíprocos, conciliación, históricos sin recalcular, selección, teclado y tamaños de ventana.
- `--capacity-checks-only`: cálculo automático 810, rechazo de parada inválida, adopción, teclado y ventana estrecha ES/EN.
- `--estimate-only`: presupuestos completos, aceptación y versiones congeladas, comparación y rechazo de separador decimal incorrecto en ES/EN.

La cifra exacta de DELIVERY del ejemplo es 11.478,57142857; se compara a dos decimales con los 11.478,57 del documento. CUSTOMER_SUCCESS corresponde a 6.021,42857143. No se altera el cálculo para igualar su presentación.

Los registros nativos y capturas seleccionadas de esta carpeta constituyen la evidencia reproducible. El arnés adicional genera el CSV y el mismo documento paginado que utiliza la impresión, y verifica la marca histórica en todas sus páginas.

## Límites operativos

- La salida PDF utiliza el diálogo de impresión de Windows; elegir una impresora PDF (por ejemplo, Microsoft Print to PDF). La prueba automática valida paginación/contenido renderizado, no todos los controladores de impresora disponibles.
- La conciliación y los puentes exportados cubren todo el análisis; el filtro se aplica a la concentración. Ambos alcances se rotulan en el informe.
- El borrado deja evidencia auditada. Los archivos con centros eliminados deben abrirse con esta versión o una posterior; versiones anteriores pueden rechazar la nueva operación del historial.
- Las pruebas cubren casos concretos, no certifican universalmente cualquier conjunto de datos, dispositivo, escala o impresora.
- GitHub Actions: no ejecutado; gasto autorizado y realizado por esta tarea: 0 USD. Los workflows conservan activación manual.

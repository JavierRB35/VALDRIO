# Decisiones pendientes

Fecha: 2026-10-04

Regla de estado: todo lo siguiente permanece **PROPUESTA / PENDIENTE** salvo aceptación explícita posterior del propietario. Los ejemplos sirven para decidir y para futuras pruebas; **no convierten la propuesta en política aprobada**.

## Registro

| ID | Estado | Propuesta actual | Momento límite |
|---|---|---|---|
| D01 Finalidad | PENDIENTE | Gestión de costes y pricing en R1; valoración contable de inventario separada en T32 | Antes de adelantar inventario |
| D02 Colaboración | PENDIENTE | Un editor local por archivo; copias sin fusión automática | Antes de T03 si se requiere simultaneidad |
| D03 Fuente contable | PENDIENTE | Perfil explícito para signos, saldo de periodo/acumulado e ingresos | Antes de T06 |
| D04 Capacidad | PENDIENTE | Política analítica normalizada; fijo por capacidad normal y variable por consumo/actividad real | Antes de T15 |
| D05 Auxiliares | PENDIENTE | Medir servicios/capacidad propios antes de aplicar OwnerOnceCapacity; si faltan datos, diagnosticar | Antes de T16 |
| D06 Recetas y lotes | PENDIENTE | Distinguir por unidad, lote iniciado y orden; confirmar lote parcial, setup, mermas e indivisibles | Antes de T17-T18 |
| D07 Perímetro de márgenes | PENDIENTE | Separar producción, comercial y estructura y mostrar perímetro de coste | Antes de T20 |
| D08 Cifrado y recuperación | PENDIENTE | No prometer cifrado hasta elegir proveedor, credenciales y recuperación | Antes de T24 con archivos reales |
| D09 Venta | PENDIENTE | Licencia perpetua por versión y actualización separada; proveedor/dispositivo/usuario por decidir | Antes de T24 comercial |

## Ejemplos de entrada/salida para decisiones económicas

### D01 - Finalidad e inventario

**Entrada de ejemplo:** una orden con coste de gestión calculado de 420 y unidades que permanecen en existencias al cierre.

**Salida bajo la propuesta actual R1:** VALDRIO puede conservar/explicar el coste de gestión de la orden, pero **no publica por ello una valoración contable de existencias**. Adelantar esa valoración implica replanificar T32 y definir perímetro productivo, movimientos, producción en curso y marco aplicable.

Estado del ejemplo: **PROPUESTA DE ALCANCE**.

### D03 - Modalidad de fuente contable

**Entrada de ejemplo:** una cuenta presenta 1.000 de acumulado en enero y 1.600 de acumulado en febrero.

- Si el perfil declara **acumulados/diferencia**, el importe de febrero sería 600.
- Si el origen declara **saldo del periodo**, un valor 1.600 significa 1.600 del periodo y no debe transformarse automáticamente en 600.

**Salida esperada:** la preparación de importación muestra la modalidad elegida; si no se puede determinar sin ambigüedad, bloquea la confirmación. La política de signos e ingresos sigue pendiente de validación con un archivo real.

Estado del ejemplo: **PROPUESTA PARA DECIDIR D03**.

### D04 - Capacidad

Ejemplo ya definido en la especificación propuesta:

**Entrada:** fijo 1.000; variable 600; capacidad normal 100 h; actividad real 60 h; consumos por objetos 40 h y 20 h.

**Salida bajo NormalizedManagement propuesta:**
- tarifa fija: 10/h;
- tarifa variable: 10/h;
- objeto A (40 h): 800;
- objeto B (20 h): 400;
- fijo no absorbido: 400;
- variable sin asignar: 0;
- conciliación: 1.600 = 800 + 400 + 400.

Estado del ejemplo: **PROPUESTA ECONÓMICA, NO ACEPTADA**.

### D05 - Capacidad de auxiliares

**Entrada de referencia:** auxiliares con servicios efectivamente medidos y capacidad propia identificable. La referencia C20 del prompt conserva un total de 1.600.

**Salida bajo OwnerOnceCapacity propuesta:** 1.600 = 800 aplicados + 500 de variación de A + 300 de variación de B, evaluando cada origen de capacidad una sola vez.

**Salida alternativa cuando faltan cantidades de servicio/capacidad:** diagnóstico `capacidad auxiliar no evaluada`; no se inventa ociosidad.

Estado del ejemplo: **PROPUESTA ECONÓMICA, NO ACEPTADA**.

### D06 - Recetas y lotes

Ejemplo ya definido en el prompt:

**Entrada:** lote 50 unidades; orden 120 unidades; setup 2 h por lote iniciado; proceso 0,1 h por unidad; tarifas 30/h y 20/h.

**Salida propuesta:**
- lotes iniciados: 3;
- setup: 6 h;
- proceso: 12 h;
- coste: 6×30 + 12×20 = 420;
- coste unitario: 3,5.

Pendiente confirmar con órdenes reales cómo tratar lote parcial, mermas y piezas indivisibles.

Estado del ejemplo: **PROPUESTA ECONÓMICA, NO ACEPTADA**.

### D07 - Perímetro de margen

Ejemplo ya definido en el prompt:

**Entrada:** venta 200; coste variable 120; fijo aplicado 30.

**Salida:**
- margen: 50 = 25 % sobre ventas;
- recargo sobre coste: 50/150 = 1/3;
- contribución: 80 = 40 % sobre ventas.

La cifra solo es definitiva cuando se acepte qué capas de producción, comercial y estructura forman el perímetro y dónde quedan costes sin objeto.

Estado del ejemplo: **PROPUESTA ECONÓMICA, NO ACEPTADA**.

## Decisiones no económicas

D02, D08 y D09 afectan arquitectura, operación o comercialización. Se mantienen pendientes, pero no se les atribuye una salida económica calculada mientras no exista un contrato específico que la defina.

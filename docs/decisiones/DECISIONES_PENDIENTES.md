# Decisiones de producto y negocio

Fecha de reconciliación: 2026-10-04  
Fuente de autoridad: `especificacion/03_Decisiones_y_Diccionario.md` del paquete VALDRIO v0.4.

Regla: **Propuesta** puede utilizarse en demostración si queda identificada, pero debe aceptarse su ejemplo antes de convertirse en regla económica definitiva. Este registro no transforma propuestas en decisiones aceptadas.

## Registro D01-D12

| ID | Estado fuente | Propuesta / supuesto | Resolución necesaria |
|---|---|---|---|
| D01 Finalidad | PROPUESTA | R1 orientada a gestión, capacidad, pricing y rentabilidad. Inventario contable en T32. | Confirmar finalidad y si T32 se adelanta. |
| D02 Colaboración | SUPUESTO | Un editor local por archivo; sin edición simultánea de red. | Confirmar antes del diseño definitivo de T03 si existe trabajo concurrente. |
| D03 Fuente contable | PENDIENTE DE EJEMPLO | Perfil explícito, signos y saldo de periodo. `CumulativeExpenseDifference` solo con comparables. | Archivo de muestra y periodo explicado antes de T06 de producción. |
| D04 Capacidad | PROPUESTA | `NormalizedManagement`: fijo propio sobre normal y variable observado sobre real. | Aceptar C15-C19 y cómo se fija normal antes de T15. |
| D05 Auxiliares | PROPUESTA | `OwnerOnceCapacity` para auxiliares medidos; `ThroughFlow` deja capacidad auxiliar sin evaluar. | Aceptar C20 y listar servicios medidos antes de T16. |
| D06 Recetas y lotes | PROPUESTA | Setup por lote iniciado completo; unidades continuas sin redondeo a pieza; merma explícita. | Dos órdenes reales antes de T17-T18 definitivos. |
| D07 Perímetro de márgenes | PROPUESTA | Coste productivo y capas comercial/estructura separadas y visibles. | Aceptar perímetros y costes sin objeto antes de T20. |
| D08 Protección y recuperación | PENDIENTE DE OPERACIÓN | No prometer cifrado por defecto. | Necesidad, uso entre equipos y recuperación antes de T24 comercial. |
| D09 Venta/licencia | PENDIENTE COMERCIAL | Licencia perpetua para versiones adquiridas; actualizaciones con vigencia separada; uso offline. | Proveedor, condiciones y soporte antes de T24 comercial. |
| D10 Autorreparto | PROPUESTA | Autorreparto prohibido por defecto; habilitable por versión con diagnóstico. | Revisar si existen servicios a sí mismo. No bloquea modelos sin diagonal. |
| D11 Cobertura definitiva | PROPUESTA | Ejecución definitiva exige cobertura completa; borrador/provisional conserva pendientes visibles. | Confirmar utilidad de publicación provisional. |
| D12 Módulos avanzados | PENDIENTE AVANZADO | Estándares, TDABC y simulación requieren datos/supuestos propios. | Definir antes de T28-T31; no afecta R0/R1. |

## Ejemplos para revisión del propietario

### D03 — interpretación de saldos, referencia C01

**Entrada:** acumulado anterior 100; acumulado actual 160; modalidad `CumulativeExpenseDifference`.

**Salida de referencia:** importe del periodo = **60**.

Si 160 se declara como `PeriodAmount`, el importe del periodo es 160. El programa no debe convertirlo en 60 por intuición.

Estado: **ejemplo de interpretación; D03 sigue pendiente de un archivo real y convención de signos**.

### D04 — capacidad, referencias C15 y C18

C15:

**Entrada:** fijo 1.000; variable 600; normal 100; real 60; objetos 40 y 20.

**Salida propuesta `NormalizedManagement`:**
- tarifa fija = 10;
- tarifa variable = 10;
- O1 = 800;
- O2 = 400;
- variación de capacidad = 400;
- actividad sin objeto = 0;
- fuente = 1.600.

C18 mantiene real 60 pero solo 40 asociados a objeto:

**Salida:** objeto = 800; variación de capacidad = 400; **actividad sin objeto = 400**; fuente = 1.600.

Estado: **PROPUESTA ECONÓMICA**.

### D05 — auxiliar por propietario, referencia C20

**Entrada:** A fijo 1.000, normal 100, real 50; A presta 10 a B y 40 a P. B fijo 600, normal 100, real 50; B presta 50 a P.

**Salida propuesta:** P recibe 800; capacidad A = 500; capacidad B = 300; conciliación **1.600 = 800 + 500 + 300**.

El coste de A recibido por B no vuelve a evaluarse como capacidad propia de B.

Estado: **PROPUESTA ECONÓMICA**.

### D06 — lote iniciado, referencia C22

**Entrada:** Q=120; lote=50; setup=2 h/lote; proceso=0,1 h/unidad; tarifas 30 y 20.

**Salida propuesta:** 3 lotes; setup 6 h = 180; proceso 12 h = 240; total **420**; unitario **3,5**.

Estado: **PROPUESTA ECONÓMICA**.

### D07 — margen y contribución, referencia C23

**Entrada:** venta 200; variable 120; fijo aplicado 30.

**Salida:** coste 150; margen 50 = 25 % sobre ventas; recargo = 1/3; contribución 80 = 40 % sobre ventas.

Estado: **PROPUESTA ECONÓMICA** hasta aceptar el perímetro.

## Reglas internacionales no económicas que no requieren decisión de negocio

- I01: idioma y formato regional son independientes.
- I02: cultura `en-US` no convierte EUR en USD.
- I04: `1,234` / `01/02/2026` sin perfil explícito bloquea importación con `IMP_AMBIGUOUS_FORMAT`.
- I13: sumar 10 EUR + 10 USD sin FX debe producir `MONEY_CURRENCY_MISMATCH`, no 20.

Estas reglas están especificadas; solo deben declararse implementadas cuando existan y se ejecuten sus pruebas correspondientes.

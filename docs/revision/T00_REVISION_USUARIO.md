# T00 — revisión del propietario

Objetivo: validar que el flujo y las propuestas se entienden **sin convertir automáticamente las propuestas económicas en reglas aceptadas**.

## 1. Flujo del estudio que propone VALDRIO

1. Se crea un archivo local de empresa con una moneda funcional.
2. Se crea un modelo y un estudio para un periodo/escenario.
3. Una importación se **prepara y previsualiza** con perfil explícito; solo `ConfirmImport` altera entradas.
4. Los recursos conservan origen, ajustes, partes y rutas; no se duplican entre directo/pool/exclusión/pendiente.
5. El cálculo recibe un snapshot tipado, sin DbContext, CultureInfo actual ni ViewModels.
6. Solo una ejecución completada y compatible con la revisión vigente se publica como resultado actual.
7. Cada importe publicado debe poder explicarse hasta su origen.
8. Capacidad, órdenes y márgenes se añaden con políticas y perímetros visibles, no con fórmulas ocultas en UI/SQL.

## 2. Interpretación internacional

Estas combinaciones son válidas y no deben confundirse:

- interfaz inglesa + formato `es-ES` + moneda USD;
- interfaz española + formato `en-US` + moneda EUR;
- zona `Europe/Madrid` sin cambiar periodo contable;
- archivo de origen con un perfil distinto del formato de pantalla.

Ejemplos fuente:

- I01: USD 1234.50 con formato `es-ES` se muestra `1.234,50 USD`; cambiar idioma no cambia importe/moneda.
- I02: EUR 1234.50 con formato `en-US` se muestra `1,234.50 EUR`; no aparece USD por la cultura.
- I04: `1,234` y `01/02/2026` sin perfil explícito son ambiguos y deben bloquearse.
- I13: 10 EUR + 10 USD sin FX no produce 20.

## 3. Ejemplos económicos que siguen siendo propuestas

### Capacidad principal — C15/C18

C15 propone que 1.000 fijo + 600 variable, normal 100 y real 60 produzcan tarifas 10/10, objetos 800 y 400, y 400 de variación de capacidad.

C18 distingue además **actividad observada sin objeto**: con real 60 y solo 40 consumidos por objeto, quedan 400 de actividad sin objeto además de 400 de capacidad.

### Auxiliares — C20

1.600 de coste fuente termina como 800 en P + 500 capacidad de A + 300 capacidad de B. El servicio recibido no vuelve a evaluarse como capacidad propia del receptor.

### Lotes — C22

Orden 120, lote 50, setup 2 h/lote, proceso 0,1 h/unidad: 3 lotes; 6 h setup + 12 h proceso; coste 180 + 240 = **420**; unitario **3,5**.

### Márgenes — C23

Venta 200, variable 120, fijo aplicado 30: coste 150; margen 50/25 % ventas; recargo 1/3; contribución 80/40 % ventas.

## 4. Qué NO se pide decidir ahora

- D08 protección/cifrado y recuperación: antes de T24 comercial.
- D09 proveedor/licencia/soporte: antes de T24 comercial.
- D12 estándares/TDABC/simulación: antes de T28-T31.
- D04-D07 pueden seguir como propuestas hasta sus tareas de producción.

## 5. Puntos que sí necesitan respuesta antes de sus puertas

- D01: finalidad de gestión R1 e inventario T32 opcional/adelantado.
- D02: si realmente habrá edición simultánea entre equipos antes de T03.
- D03: archivo contable real, modalidad y signos antes de T06.
- D04-D07: aceptación de ejemplos/perímetros en las tareas indicadas.

La revisión de T00 puede cerrarse confirmando que este flujo y estas distinciones se comprenden; **no implica aceptar D04-D07 como reglas definitivas hoy**.

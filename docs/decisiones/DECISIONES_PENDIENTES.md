# Decisiones de producto y negocio

Fecha de actualización: 2026-10-04  
Fuente base: `especificacion/03_Decisiones_y_Diccionario.md` del paquete VALDRIO v0.4.  
Aceptación del propietario registrada el 2026-10-04.

## Registro D01-D12

| ID | Estado vigente | Decisión / propuesta | Próxima puerta |
|---|---|---|---|
| D01 Finalidad | **ACEPTADA** | R1 se centra en gestión de costes, capacidad, productos, órdenes y rentabilidad. Valoración contable de existencias y producción en curso queda en T32. | T32; no adelantar salvo nueva decisión. |
| D02 Colaboración | **ACEPTADA** | Primera versión: un editor por archivo, funcionamiento local, sin edición simultánea desde equipos distintos. Arquitectura desacoplada. | Revisar solo si cambia el modo de colaboración. |
| D03 Fuente contable | **PENDIENTE DE EJEMPLO** | Perfil explícito para modalidad del saldo, signos, periodo e interpretación. No inferir por idioma ni convenciones españolas. | Antes de importación definitiva/T06, con archivo representativo. |
| D04 Capacidad | **PROPUESTA** | `NormalizedManagement`: fijo propio sobre normal y variable observado sobre real. | Antes de T15: datos, fórmula, resultado y alternativas. |
| D05 Auxiliares | **PROPUESTA** | `OwnerOnceCapacity` para auxiliares medidos; `ThroughFlow` deja capacidad auxiliar sin evaluar. | Antes de T16: datos, fórmula, resultado y alternativas. |
| D06 Recetas y lotes | **PROPUESTA** | Setup por lote iniciado completo; unidades continuas sin redondeo a pieza; merma explícita. | Antes de T17-T18: datos, fórmula, resultado y alternativas. |
| D07 Perímetro de márgenes | **PROPUESTA** | Coste productivo y capas comercial/estructura separadas y visibles. | Antes de T20: datos, fórmula, resultado y alternativas. |
| D08 Protección y recuperación | **PENDIENTE DE OPERACIÓN** | No prometer cifrado por defecto. | Hito previsto T24; no bloquea trabajo independiente actual. |
| D09 Venta/licencia | **PENDIENTE COMERCIAL** | Licencia perpetua para versiones adquiridas; actualizaciones con vigencia separada; uso offline. | Hito previsto T24; no bloquea trabajo independiente actual. |
| D10 Autorreparto | **PROPUESTA** | Autorreparto prohibido por defecto; habilitable por versión con diagnóstico. | Revisar si aparecen servicios a sí mismo. |
| D11 Cobertura definitiva | **PROPUESTA** | Ejecución definitiva exige cobertura completa; borrador/provisional conserva pendientes visibles. | Revisar antes de publicación definitiva. |
| D12 Módulos avanzados | **PENDIENTE AVANZADO** | Estándares, TDABC y simulación requieren datos/supuestos propios. | T28-T31; no bloquea R0/R1 actual. |

## Separación monetaria aceptada como requisito de producto

- La **empresa propone una moneda predeterminada**.
- Cada **estudio fija su propia moneda funcional**.
- Los importes originales pueden conservar monedas distintas.
- Toda conversión es explícita.
- Los tipos de cambio utilizados quedan congelados por ejecución.
- Idioma, región o país no determinan la moneda funcional ni el tipo aplicado.

Esta precisión es coherente con A11/CM16 y sustituye cualquier redacción anterior que atribuyera una única moneda funcional al archivo de empresa.

## Ejemplos que siguen siendo propuestas

### D03 — C01

Acumulado anterior 100 y actual 160 con `CumulativeExpenseDifference` produce 60. Con `PeriodAmount`, 160 sigue siendo 160. La modalidad y los signos se decidirán con archivo real.

### D04 — C15/C18

C15: fijo 1.000, variable 600, normal 100, real 60 y objetos 40/20 → propuesta 800 + 400 + 400 capacidad.  
C18: si solo 40 de actividad real están asociados a objeto, aparece además actividad sin objeto.

### D05 — C20

Coste fuente 1.600 → propuesta 800 a P + 500 capacidad A + 300 capacidad B, sin reevaluar como capacidad propia el coste recibido.

### D06 — C22

Q=120, lote=50, setup 2 h/lote, proceso 0,1 h/u → propuesta 3 lotes, 6 h setup, 12 h proceso, coste 420, unitario 3,5.

### D07 — C23

Venta 200, variable 120, fijo aplicado 30 → propuesta coste 150, margen 50/25 %, recargo 1/3 y contribución 80/40 %.

Comprender estos ejemplos **no** equivale a aceptar D04-D07.

## Regla de continuidad económica

Antes de convertir D04-D07 en reglas definitivas, la tarea correspondiente debe presentar al propietario:

1. datos necesarios;
2. fórmula propuesta;
3. resultado esperado;
4. alternativas razonables y sus consecuencias.

No se altera un resultado esperado para facilitar la implementación.

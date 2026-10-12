# ADR-0016 — Saldos de importación en la moneda del análisis

Fecha: 2026-10-12. Decisión explícita del propietario. Aplica al asistente de
importación de saldos de VALDRIO, sin sustituir las reglas de valoración del ERP.

## Contexto

Los balances de cuentas suministrados a un análisis de costes deben venir
ya contabilizados en la moneda de la empresa/análisis. Permitir introducir
divisas y tipos manuales por registro traslada al usuario una decisión contable
peligrosa, aumenta la probabilidad de error y mezcla la normalización contable
con la asignación de costes.

## Decisión

1. El asistente importa todos los importes como moneda funcional del análisis.
   No ofrece selector editable de moneda de origen, columna de divisa, RateSet,
   tipo de cambio, fecha del tipo ni asignación de cotización por registro.
2. La interfaz informa de la moneda que utilizará y exige que el archivo haya
   sido normalizado antes de importarlo. No presenta tipos ni conversiones.
3. El formato de los números (separador decimal/miles) y la semántica del saldo
   (movimientos, cierre o diferencia de acumulados) siguen siendo decisiones
   independientes de la moneda y se conservan.
4. Cuando el archivo no declara moneda, VALDRIO no puede detectar si un importe
   fue contabilizado en otra divisa; esto se resuelve en origen y mediante la
   revisión/aprobación del usuario, no inventando una conversión.
5. Persistencia, lectura histórica, snapshots, huellas y restauración de
   importaciones internacionales ya existentes permanecen intactos. El motor
   heredado T06A se conserva exclusivamente para compatibilidad y pruebas
   técnicas; su UI de configuración ya no se ofrece a usuarios.

Esta decisión prima sobre la capacidad interactiva anterior del ADR-0011.
No se modifica el comportamiento histórico de T07 ni se introduce un módulo
de consolidación de sociedades. La comprobación de compilación Windows y del
recorrido WPF ES/EN sigue siendo requisito antes de fusionar.

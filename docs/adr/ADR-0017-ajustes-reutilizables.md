# ADR-0017 — Fichas reutilizables de ajustes extracontables

Fecha: 2026-10-14. Estado: implementación candidata, pendiente de revisión del propietario.
Base: main@8d7ce5c4a661c9bee1e396feef1a422f72f73bfe.

## Decisión económica y ubicación

El propietario amplía CM02/T07: sustituir explícitamente un conjunto de cuentas por un coste analítico identificable, con aplicación y reversión voluntarias. Se incorpora en el paso 5, **Preparar cuentas**, antes del reparto; no añade fases ni sustituye la corrección manual existente.

Cada ficha pertenece al proyecto/empresa. Permite alta, modificación, duplicación y retirada, con nombre, cuenta exacta o prefijo terminado en `*`, código analítico, modalidad, valor y motivo. Los códigos se comparan como texto, conservando ceros iniciales. La selección muestra código y descripción disponible. Una ficha guardada en un periodo está disponible en otros análisis, pero nunca se ejecuta automáticamente.

Modalidades: importe fijo, porcentaje del saldo efectivo seleccionado o saldo efectivo más un importe firmado. La base incluye correcciones manuales previas. Se opera en la moneda funcional y con la precisión del análisis. No se incorporan fórmulas arbitrarias, calendarios automáticos ni conversión monetaria implícita.

Ejemplo: dos cuentas 681* con 600 y 400 reciben compensaciones de -600 y -400; un nuevo recurso `AJ-AMORT-TEC`, denominado Amortización técnica, recibe 500. El original importado sigue siendo 1000, el ajuste neto es -500 y el coste efectivo es 500. El registro analítico tiene procedencia propia, sin inventar filas contables importadas.

## Aplicación, conflictos y reversión

- Aplicar muestra las cuentas reales, su saldo, compensación y el coste de destino; confirmar es una segunda acción explícita. Cancelar no escribe ajustes.
- Dos sustituciones activas no pueden compensar una misma cuenta del mismo análisis. El conflicto se detecta en la previsualización y vuelve a validarse al escribir.
- Se comprueban revisión del análisis y versión de ficha. Una previsualización desactualizada no se aplica silenciosamente.
- Las cuentas compensadas quedan con componentes a cero. El nuevo coste queda pendiente de preparación y utiliza las claves normales del modelo; no hereda destinos silenciosamente.
- Solo los recursos neutralizados por estas operaciones se excluyen de la exigencia de repartir importes cero. No se cambia el tratamiento general de cuentas de saldo cero.
- Deshacer conserva el historial, revierte exactamente las compensaciones y el coste generado, y restaura la preparación previa. Las correcciones monetarias independientes posteriores se conservan como diferencias pendientes. Los cambios de preparación posteriores requieren revisión: la confirmación informa de ello.
- Es posible deshacer tras calcular mientras el cálculo permanezca abierto. Las validaciones existentes bloquean modificaciones tras el cierre; primero debe reabrirse. Los resultados afectados requieren recálculo y los snapshots históricos permanecen intactos.
- Retirar una ficha no borra aplicaciones existentes: se mantiene accesible para deshacer. Modificarla crea una versión; no modifica las aplicaciones ya efectuadas. Reaplicar tras deshacer puede reutilizar el recurso analítico neutralizado de esa ficha, sin duplicar coste.

## Persistencia, separación de capas y compatibilidad

El motor puro de Application construye la operación y sus deltas deterministas. Desktop solo recoge datos y presenta confirmaciones. Infrastructure usa la transacción, revisión, auditoría y replay de comandos existentes. El catálogo se reconstruye a partir de las versiones guardadas en el historial de todos los análisis de la empresa. Los identificadores de los efectos se derivan de la solicitud, no del reloj ni del orden de las filas.

Los nuevos campos opcionales se omiten cuando son nulos para preservar representaciones canónicas anteriores. No se modifica el esquema SQLite ni se reescriben snapshots. Esta versión lee archivos anteriores. **No se promete que una versión antigua pueda abrir archivos que ya contengan estas nuevas operaciones**: los lectores anteriores no conocen el nuevo contrato y su validación canónica puede rechazarlos. Debe utilizarse una versión con soporte de fichas para esos archivos.

El catálogo se lee del historial completo: no se ha realizado una prueba de rendimiento con miles de versiones de fichas. Una proyección indexada futura deberá mantener el historial como fuente verificable.

## Validación y límites

Ver [informe y evidencias](../evidencias/2026-10-14-ajustes-reutilizables/INFORME.md). Las pruebas automatizadas y la inspección visual no sustituyen la aceptación del propietario ni demuestran ausencia absoluta de regresiones. No se modifican workflows ni se ejecuta GitHub Actions.

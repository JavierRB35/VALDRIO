# T06 — Registros seleccionados y agrupación por código

Base `d1ff15928acf51bd79fb82032fb9a65c9cf56ef9`, rama `codex/local-tests-and-workspace`.
Decisiones y formatos: [ADR-0009](../adr/ADR-0009-registros-y-agrupacion-por-codigo.md).
Evidencia local; no cierre integral T06 ni aceptación CI del commit final.

## Resultado visible

El selector admite códigos de cuenta, agrupación o gasto desde la columna elegida.
Muestra todas las filas válidas con casillas individuales y botones Seleccionar todos /
Deseleccionar todos. Se puede incluir solo una de dos filas del mismo código. Las filas
elegidas se suman por código, ignorando descripciones conforme a la decisión expresa
del propietario. Se muestran grupos, registros de origen e importes antes de confirmar.

La primera validación selecciona todos los registros. Cambiar selección revoca aprobación
y preparación. Una selección parcial no se reutiliza tras cambiar/releer el origen.
Siguen bloqueando los importes inválidos, moneda sin puente, falta de aprobación y
estudios cerrados/no guardados; excluir filas no transforma errores en ceros.

## Pruebas ejecutadas

- Build Release de la solución completa, WPF incluido: 0 errores y 0 advertencias.
- Suite final: **407/407** correctas, sin omitidas. TRX:
  `tests/Valdrio.Tests/TestResults/grouping-release-final.trx`.
  SHA-256 `23411e3b332de0fc3dff12a0340177a44cda447af94579fc2753ccde9432bcfd`.
- Doce casos nuevos: suma con descripciones diferentes y procedencia; fila única
  de un código repetido; grupo cero con aportaciones opuestas; selección desconocida /
  código incoherente; límites de magnitud por grupo; todos/ninguno/parcial/excepción;
  migración v4 genuina; cuatro fallos/cancelaciones transaccionales; detección de enlace
  perdido después de intentar modificar evidencia protegida por trigger.
- Fixtures históricas v1–v4 conservadas. `t06-v4.json` registra bytes de la entrega
  sintética WPF anterior y su SHA. Upgrade a copia mantiene ledger/snapshots/enlaces,
  valida integridad/FK y no cambia el hash del origen. No se reescribe V4Model/migración.
- Una expectativa antigua de la UI rechazaba un duplicado en la fila 62. Se sustituyó
  por importe inválido en esa fila para conservar la comprobación de validación más
  allá de las 50 filas visibles; la política Reject histórica mantiene sus pruebas.
- El recorrido WPF final salió con código 0: empresa 28, modelos 53, estudios 73,
  recuperación 10, columnas 13, confirmación 26, Debe/Haber 22, agrupación 11, FX 12.
- Las once comprobaciones nuevas operan botones/casillas reales, verifican moneda y
  total del ejemplo, cambios de presentación, guardado, snapshot v3, backup/restore
  y cierre/reapertura. Capturas ES y pseudo/RTL revisadas en
  `artifacts/grouping-final-2026-10-07/import-grouping`.
- Manifests y SHA de modelos, estudios, confirmación, Debe/Haber y agrupación verificados.
  Verificador Windows ampliado con protocolo, capturas y manifest de agrupación;
  sintaxis PowerShell verificada. No se ejecutó el protocolo completo de publicación
  y recibo de checkout limpio ni una nueva matriz UTC/Madrid en este turno.
- Runner sobre el snapshot WPF final: `status=verified`, schema v3, dos recursos,
  tres filas, neto 353,35 EUR; 6005 conserva 358,35 y el otro código -5.

## Archivo XLSX aportado por el propietario

Archivo original leído sin modificar. SHA-256:
`fc903c6e0997fb5e353b91a6327d629dfb20e618296e9d9d9ae7337b270297b6`.
Hoja Saldos, encabezado 1, primera fila 2, código B, importe F. Importes Excel nativos
y códigos textuales. Se ejecutaron el lector, ViewModel de producción, preparación,
confirmación SQLite y reapertura sobre una empresa de prueba local separada.

- 20 registros y 19 grupos al seleccionar todos.
- 6005: filas 7/8, 280 + 78,35 = 358,35 EUR, confirmado expresamente por el propietario.
- Solo fila 8: 78,35 EUR. Todos menos fila 7: 19 registros seleccionados y 6005=78,35.
- Ninguno: preparar deshabilitado. Todos nuevamente: resultado completo conservado
  al confirmar, capturar y reabrir. Hash final del Excel idéntico al inicial.

La copia de prueba, resultado JSON y snapshot permanecen únicamente en artifacts
locales ignorados por Git; no se incorpora el Excel ni sus datos completos al repositorio.
Esta comprobación acredita la función de importación sobre la muestra, no certifica
su clasificación económica. Los límites de pérdidas y ganancias y D03 siguen documentados.

## Continuidad

Selección individual y agrupación por código completadas. El siguiente bloque T06 es
sustituir un lote identificado preservando historial, seguido de diferencias comparables
y aceptación global. T06A/T07 permanecen posteriores.

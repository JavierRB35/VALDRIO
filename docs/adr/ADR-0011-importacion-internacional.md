# ADR-0011 — Puente de importación internacional

Fecha: 2026-10-07. Alcance: T06A; T07 aún no iniciada.

## Decisión

El perfil v4 congela ImportConversionPolicy: catálogo de monedas, RateSet manual,
selección de RateId por registro, intervalo permitido de fechas de cotización,
referencia aprobada y columna opcional de fecha de documento. La moneda puede
ser fija o proceder de una columna. Ninguna preferencia visual interpreta estos datos.
El catálogo instalado sigue siendo el catálogo de ejemplo EUR/USD/JPY/KWD; otro
catálogo se incorpora por ICurrencyCatalog, sin inferir monedas por idioma.

PrepareImport v4 conserva Money y fecha de cada fila, aplica FxConverter de T02A
por registro y guarda original, bruto, convertido a escala 8 MidpointToEven, ajuste,
tipo, fecha y fuente. Identidad conserva el importe y no necesita cotización.
Falta de tipo, orientación errónea, moneda desconocida o fecha fuera del intervalo
declarado bloquean. Un importe convertido por debajo del quantum genera aviso,
sin confundirlo con lectura inválida. La UI usa punto decimal y fecha ISO para
introducir tipos manuales, con instrucciones visibles; el perfil del archivo es independiente.

La revisión agrupa por código en moneda funcional. La persistencia mantiene
componentes por código y moneda original para no fabricar un Money nominal
mezclando divisas. Los recursos enlazan todos sus registros; ImportConversionBridge
enlaza las aplicaciones por registro. Las descripciones no intervienen. Se convierte
cada registro antes de agregar, conservando sus ajustes de redondeo.

En diferencias de acumulados se exigen los mismos pares código/moneda y el ámbito
fiscal comparable de T06. Se congelan los dos perfiles/documentos y se restan sus
bases funcionales explícitamente convertidas. Se conservan también las diferencias
originales y las filas anteriores. El asistente explica esta operación y exige la
aprobación del ejemplo; no adivina tipos medios, de cierre ni de transacción. Si
los tipos de ambos documentos difieren, la diferencia funcional incluye ese efecto;
no se presenta como una conversión única de la diferencia nominal.

## Versiones e historia

- Empresa v7: migración identificada, sin reescribir tablas ni payloads históricos.
  Abrir v1–v6 requiere actualización a copia nueva; los originales permanecen intactos.
- Perfil y preparado v4; snapshot de estudio v5; economía v5. Los campos nuevos
  ausentes no se serializan en los formatos anteriores. Lectores v1–v4 se conservan.
- Snapshot v5 incluye aplicaciones FX de lotes activos con identificadores únicos
  por lote y lado del comparativo. Guarda el ledger completo con los lotes retirados.
- Repreparación dentro de la transacción, revisión esperada, idempotencia y reemplazo
  identificado se mantienen. Cambiar tipos en el editor invalida preparación/aprobación.
- Runner exporta el puente canónico y verifica el mismo snapshot que la interfaz.

No se declara RevalueStudy completo: las particiones y consumos que debe revalidar
pertenecen a T07 y posteriores. Sustituir un lote es una acción identificada; no
actualiza tipos silenciosamente en otros lotes o estudios. Tampoco se entrega en
T06A la exportación de informes de ejecuciones, todavía inexistentes.

## Evidencia

Pruebas CSV y XLSX con importes numéricos/texto, fecha ISO/texto, débito/crédito,
perfiles ES/US/GB/DE, NBSP declarado, cuentas con ceros, mezcla de monedas,
negativos/ceros, redondeo individual, faltas/errores, rollback y cancelación reales,
comparativos, sustituciones, snapshot, reapertura, backup/restauración y migración
de una fixture v6 genuina. Recorrido WPF con 10 comprobaciones, ES y pseudo/RTL.
La aceptación del HEAD se acredita por Verify-Windows.ps1 y CI, no por esta lista.

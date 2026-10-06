# Selección manual de columnas — 2026-10-06

Base integrada y validada main@2da40fa76108a6edef0492de2ce67dfbad9fa72a. Requisito del usuario — elegir la columna del código de cuenta y la columna del importe en Excel o CSV.

## Comportamiento

1. En Estudios, abrir **Seleccionar columnas de importación…**.
2. Elegir un CSV o XLSX; declarar la hoja Excel, fila de encabezados y primera fila de datos. El valor 0 en encabezados admite fuentes sin cabecera. CSV ofrece codificación y separador explícitos.
3. Pulsar **Leer columnas** y seleccionar código de cuenta e importe. Cada opción muestra posición y encabezado; nombres repetidos o vacíos no confunden columnas.
4. Declarar separadores decimal/miles y notación negativa para los valores textuales; validar las columnas.
5. Consultar la vista previa y los errores localizados. La selección válida queda retenida en memoria para el siguiente paso. Cerrar y volver a abrir la ventana conserva ese mapeo.

No se asignan automáticamente columnas. Cuenta e importe deben ser distintos. Se muestran hasta 50 filas, pero se validan todas. Un importe vacío/missing es un error, no cero. El código textual 00123 conserva sus ceros. Excel numérico como cuenta requiere aceptación explícita; fechas/booleanos siguen bloqueados. La región del equipo no define cómo se interpreta el origen.

ImportSourceReadSettings separa estructura física y contrato económico. Los adaptadores y el inspector existentes conservan sus entradas de perfil versionado y delegan en el mismo lector/validador. La sesión trabaja con una copia limitada e inmutable del archivo; cambiar ajustes invalida resultados previos y cancelar no publica un mapeo parcial.

## Verificación

SDK 10.0.401; Release completo, incluido WPF con EnableWindowsTargeting; Nullable y TreatWarningsAsErrors conservados. **0 avisos y 0 errores**. Suite local **309/309**, **0 fallos y 0 omitidas**, con las 285 anteriores y 24 nuevas.

TRX local columns-final-local.trx, SHA-256 cb646b9467544d17d402762703daaf43bff9000d3542d6b5179ec00c0070b4de.

Las regresiones ejecutan CSV/XLSX reales, una segunda hoja, posiciones reordenadas/no contiguas, cabeceras repetidas/cortas/ausentes, Missing frente a cero, tipos Excel, cuentas numéricas, cambios externos del archivo, cambios de configuración y presentación, errores después de la fila 50, reemplazo fallido y cancelación.

La aceptación Windows añade 13 comprobaciones nativas del selector y dos capturas CSV/Excel. El script exige sus resultados junto a los checks previos; verification.json incorpora ImportColumnChecks e ImportColumnImages. Los runs finales, el árbol, el recibo y sus hashes se registran en la PR asociada a feature/import-column-mapping-20261006 y en la aceptación posterior de main, sin modificar código después de validarlo.

## Alcance

Se implementa selección, lectura y validación de columnas con vista previa. No se confirma ni persiste aún la importación económica en SQLite. No se infieren moneda, signo económico, modalidad o filtros contables por nombre/cultura. D03, PrepareImport completo y ConfirmImport/rollback siguen pendientes de la fase T06 correspondiente.

# ADR-0009 — Selección de registros y agrupación por código

Fecha: 2026-10-07. Decisión del propietario y subentrega T06.

## Decisión

El propietario confirma que las dos filas de 6005, 280 y 78,35 EUR, deben producir
358,35 EUR si ambas están seleccionadas. Pide seleccionar todos, ninguno, algunos
o todos menos algunos registros. La selección se identifica por número de registro,
no por código, porque varias filas pueden compartirlo.

La columna elegida puede contener cuentas, agrupaciones o códigos de gasto. Son
identificadores de texto ordinales; no se infiere un plan contable por numeración.
Se agrupan exclusivamente las filas seleccionadas por ese código. Por instrucción
expresa del propietario, las descripciones no determinan la agrupación ni requieren
elegir una descripción ganadora. Sus celdas se conservan en la fuente original.
Esto sustituye la exigencia anterior de resolver descripciones distintas de T06.

## Perfil, preparación y recursos

`valdrio-source-parse-profile-v2` añade un `ImportGroupingPlan` inmutable: registros
seleccionados y sus códigos originales. Se valida pertenencia al archivo, unicidad
del registro y correspondencia exacta del código. El orden es canónico por registro.
La UI no asigna códigos económicos nuevos: el usuario elige su columna del archivo.
El perfil v1 histórico permanece idéntico y conserva el rechazo de duplicados cuando
se solicitó esa política. No se cambian interpretaciones confirmadas anteriormente.

El preparado v2 conserva filas, selecciones y reconciliación por signo; añade grupos
con Money en la moneda declarada y registros de origen. Se valida cada operando,
cada fila y el total de cada grupo con la política decimal. Un grupo cuya neta sea
cero sigue siendo un recurso explícito si sus filas fueron seleccionadas. Los errores
de grupo bloquean y no convierten en inválidas las filas cuyos importes eran correctos.
No se agrega dinero de monedas distintas ni se habilita FX.

Cada recurso agrupado conserva una lista de registros, no únicamente la primera fila.
La selección forma parte de la huella de preparación y de repetición de lote; cambiarla
exige preparar/aprobar de nuevo. Los identificadores de recursos agrupados se derivan
del lote y código mediante un espacio de nombres nuevo. No se alteran los IDs v1.

## Archivo v5 y compatibilidad

`GroupedImportDbContext` y `V5Model` suceden al modelo v4 congelado. La migración
`202610070005_GroupedImportResources` cambia la clave de ResourceSourceLinks a
(ResourceId, RecordNumber). Conserva FK de ámbito, índice único por fila, enlaces
anteriores y triggers de inmutabilidad. Cada fila seleccionada contribuye a un único
recurso; varios registros pueden enlazar el mismo recurso.

Abrir un archivo v1–v4 solicita actualización. Upgrade migra exclusivamente una copia
nueva, nunca el archivo original; se conservan ledger, auditoría, snapshots y sus
huellas. El nuevo lector valida todos los enlaces contra la reconstrucción del lote.
Los snapshots con agrupaciones usan `valdrio-study-input-v3` y economía v3. Los lectores
y bytes de snapshots v1/v2 permanecen disponibles e idénticos.

## Interfaz y alcance

Tras validar las columnas se muestran todas las filas, con número de registro,
código, importe y casilla. Inicialmente se seleccionan todas. Los botones de selección
masiva también funcionan tras una selección parcial. Preparar con cero registros
está deshabilitado. No se limita la elección a las primeras 50 filas de la vista previa.
Cambiar la selección cancela preparación en curso y revoca aprobación; cambiar
archivo/mapeo no reutiliza marcas parciales. La vista agrupada muestra código, filas
de origen y total, antes de confirmar. Idioma/región no modifican la elección.

Confirmar requiere estudio guardado y abierto, revisión vigente, moneda y significado
aprobados. La selección no convierte un archivo de patrimonio en fuente de costes
por sus prefijos. Sustitución de lotes, diferencias comparables y T06A quedan pendientes.

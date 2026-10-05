# ADR-0005 — Staging de importación y perfil de origen v1

Fecha: 2026-10-05  
Estado: **Aceptada técnicamente para T06; semántica productiva D03 pendiente**

## Contexto

CM01 y CM15 requieren que la interpretación de sumas y saldos dependa de un perfil explícito del origen, no de la cultura de la UI ni de inferencias sobre cuentas, idioma o país. T06 necesita además previsualizar y diagnosticar sin crear recursos ni modificar la revisión del estudio.

T05 conserva snapshots contextuales `valdrio-study-input-v1` y formato SQLite v3. Esos contratos históricos no se amplían para introducir importaciones.

## Decisión

1. El contrato consumido por Application es `SourceParseProfileVersion`, con schema `valdrio-source-parse-profile-v1`. Es inmutable, identificable, versionado y contiene los tokens de parsing y políticas que determinan cómo leer la fuente.
2. `IImportAdapter` pertenece a Application porque es el puerto consumido por el caso de uso. Sus implementaciones pertenecen a Infrastructure.
3. Un adaptador devuelve `ImportSourceDocument` con `SourceRow`/`SourceCell`, procedencia y SHA-256 del archivo. No devuelve `Resource`, entidades EF ni tipos de UI.
4. CSV se tokeniza mediante máquina de estados; no mediante separación ingenua de líneas o `Split(',')`. El encoding, BOM, delimitador y quote proceden del perfil.
5. El número textual se interpreta mediante `SourceAmountParser` y `System.Decimal`. Separadores, agrupación y sintaxis negativa son tokens explícitos. No se usa `CurrentCulture`, `double` ni reemplazo global de coma/punto.
6. Cuenta y descripción permanecen texto Unicode y la identidad de cuenta usa comparación ordinal. No se reconstruyen ceros perdidos.
7. `SourceValueKind` distingue Missing, Empty, Text, Number, DateSerial, fórmulas con/sin cached value y Error para que CSV y el futuro XLSX compartan staging sin fingir equivalencia entre estados.
8. D03 no se resuelve técnicamente. Un perfil de fixture se marca `SyntheticFixture`; un perfil real no podrá presentarse como aprobado hasta registrar la interpretación y ejemplo del propietario.
9. `PrepareImport` será una capa posterior sobre este staging. Su implementación deberá leer contexto/revisión del estudio y producir preparación inmutable, pero seguirá sin crear recursos ni audit económico.
10. Formato SQLite v4, `ConfirmImport` y snapshots T06 se diseñan después de estabilizar `PreparedImport`; no se modifica v3 ni `valdrio-study-input-v1`.

## Consecuencias

- La misma fuente puede leerse igual con UI/cultura distintas.
- El archivo conserva huella de bytes y cada celda conserva lexical extraído; no se afirma reconstrucción byte a byte desde las celdas.
- XLSX podrá diferenciar número, texto, fecha serial, fórmula cached/sin cached y error usando el mismo contrato de origen.
- `AggregateWithProvenance` no agrega dentro del adaptador: la agregación necesita semántica y reconciliación de T06.2.
- Cambiar archivo o perfil generará otra preparación; esa huella se define en T06.2.
- La persistencia v4 no queda autorizada por este ADR y T06A mantiene el puente FX internacional completo.


## Selección XLSX

T06.1 usa `DocumentFormat.OpenXml` 3.5.1. Es el SDK Open XML mantenido por .NET Foundation/Microsoft, licencia MIT y paquete con target compatible `net10.0`. Se abre el stream en modo read-only y se consume el valor cached existente de una fórmula; VALDRIO no invoca Excel, COM, macros ni motor de recálculo.

El adaptador conserva el `CellFormula` y el `CellValue` cached por separado. Ausencia de cached value permanece `FormulaWithoutCachedValue`. Los sistemas de fecha 1900/1904 se registran en el documento de staging y el serial 60 del sistema 1900 no se convierte en fecha válida.

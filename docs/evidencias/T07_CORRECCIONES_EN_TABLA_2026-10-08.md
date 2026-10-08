# T07 — Correcciones en tabla — 2026-10-08

Solicitud del propietario: editar solo Correcciones, guardar automáticamente en la
moneda del análisis, recalcular columnas y retirar la pantalla adicional de ajustes.
Base integrada `770c640`, continuación de `07ba978` en `codex/usability-six-issues`.
PR #27, base `codex/local-tests-and-workspace`; no se fusiona ni se modifica main.

## Resultado y contratos

- Una pantalla conserva la tabla; clasificación, destinos y comprobación se abren
  debajo. No existe el formulario adicional de importe/moneda/tipo/motivo de ajuste.
- Solo Correcciones permite edición. La cabecera indica la moneda funcional. El
  editor recibe un número sin sufijo monetario; Intro/Tab/salida de celda confirman,
  Escape cancela. Hay ayuda visible y contextual EN/ES.
- La celda contiene el **total** de correcciones. Cambiar -100 a -150 registra -50;
  repetir -150 no registra nada; pasar a 0 registra +150 sin borrar el historial.
- EditResource conserva transacción, actor, revisión esperada, metadatos congelados,
  importes originales, conversiones y eventos previos. Tipo y motivo se generan
  como descripción de la operación (cuenta, anterior, nuevo, moneda), según la nueva
  interacción autorizada. No se inventa una justificación económica del cambio.
- El delta modifica la última parte pendiente conservando identidad y clasificación,
  o crea una pendiente explícita si no existe. Directo/pool/excluido no cambian.
  La conservación CM02 sigue validándose; los pendientes negativos son visibles.
- Efectivo, pendiente y diferencia se reconstruyen del recibo persistido. Un fallo
  deja el importe anterior en la tabla y explica que la corrección no se guardó.
  No se convierte texto inválido en cero ni se repite una operación confirmada.
- Región capturada al iniciar edición; la preferencia posterior no reinterpreta el
  texto. Cambios de análisis/archivo quedan bloqueados mientras se confirma/guarda.
  Destinos en borrador y revaloración bloquean ajustes incompatibles con explicación.
  Los estudios cerrados y archivos de solo lectura siguen siendo de consulta.
- Clasificación/destinos y revaloración conservan validación, borradores y guardado
  explícito. Los nuevos ajustes de tabla siempre son funcionales; FX histórico
  permanece congelado y no se revalora implícitamente.

Contraste: specification/v0.4/tareas/T07.md, contratos CM01/CM02/CM16,
docs/directrices/USABILIDAD.md y ADR-0012. Se actualiza el recorrido operativo de
T07, sin modificar las fuentes canónicas ni cambiar esquema SQLite/snapshot.

## Validación local

- `dotnet build Valdrio.slnx -c Release --no-restore`: 0 errores, 0 advertencias.
- Suite completa: **530 correctas, 0 fallos, 0 omitidas**. Recibo:
  `artifacts/tests/inline-full-final.trx`. Incluye ocho regresiones nuevas de
  corrección automática, totalidad/deltas, cero/no-op, reapertura, cultura capturada,
  números inválidos/precisión/overflow, revisión obsoleta, estudio cerrado,
  conservación de destinos y FX histórico. Pruebas existentes adaptadas al editor
  de destinos en la misma pantalla y la retirada del paso adicional.
- WPF completo: **346 comprobaciones correctas**, incluidos 34 checks T07 y 27 de
  auditoría. Directorio `artifacts/wpf-inline-final/`; cada recibo tiene Status=passed.
  La celda se edita y confirma en DataGrid real; se verifica cancelación, rechazo de
  texto inválido sin persistencia, no-op sin revisión adicional, guardado y recálculo,
  conservación C04 900=300+500+100, snapshot y cierre/reapertura.
- Captura inspeccionada: `t07-resources/inline-corrections-es.png`; tabla con cabecera
  Correcciones (EUR), instrucciones visibles y cifras originales/efectivas/pendientes.
  También se conservan capturas EN y pseudo/RTL de revisión en la misma pantalla.
- 981 claves EN/ES coincidentes y sin duplicados. 58 archivos bajo specification
  conservan SHA-256 frente a `07ba978`. `git diff --check` correcto.

TRX, recibos y capturas quedan localmente en artifacts (ignorado por Git). Se usan
fuentes sintéticas; no certifican una fuente productiva. GitHub Actions no se ha
invocado, el workflow existente sigue exclusivamente manual. Coste de Actions: 0 USD.
La copia original de Visual Studio y sources sincronizados no se han modificado.

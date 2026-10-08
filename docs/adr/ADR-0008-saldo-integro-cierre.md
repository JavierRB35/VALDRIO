# ADR-0008 — Saldo íntegro al cierre elegido para el estudio

Fecha: 2026-10-06. Estado: implementado localmente; aceptación productiva pendiente de muestra.

El propietario ha elegido 160, no 60, para el ejemplo cierre anterior 100 y actual 160.
Se añade ImportAmountMode.ClosingBalanceAsStudyAmount=3. Los valores 0/1/2 mantienen
su semántica: periodo, cierre sin interpretación, diferencia de acumulados.

Preparar esta modalidad conserva cada importe firmado (o Debe menos Haber) completo,
los originales, moneda y referencia de aprobación. El cierre es PeriodEnd y debe coincidir
con el fin del estudio. La interfaz muestra esa fecha y la regla 160→160 antes de aprobar.
Cambiar modalidad invalida la preparación y desmarca la aprobación anterior.

Se reutiliza la confirmación atómica, comprobación de revisión, idempotencia y persistencia
del perfil en v4. No hay migración de columnas ni reinterpretación de lotes anteriores.
El nuevo valor queda incluido en la serialización canónica y las huellas de preparación.
Las versiones anteriores rechazan el nuevo valor de enum; no abrir estos nuevos lotes
con una versión anterior que desconozca la modalidad. El cambio es aditivo y los perfiles
históricos conservan sus bytes y significados.

Las modalidades ClosingBalance y CumulativeExpenseDifference siguen bloqueadas, así como
divisa extranjera sin puente, duplicados y ausencia de aprobación. No se resta un cierre
anterior ni se aprueba automáticamente una fuente real. El usuario debe comprobar las
cuentas elegidas; no hay filtros nacionales implícitos.

T06 sigue incompleta: agrupación, sustitución de lotes, diferencias comparables y cierre
integral de aceptación pendientes; T06A y T07 se mantienen en la secuencia posterior.
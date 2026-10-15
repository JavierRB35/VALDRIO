# PEND-UI-04 — Porcentaje pendiente de asignar en claves de reparto

Fecha: 2026-10-15. Estado: pendiente de implementación.

## Requisito del propietario

En la definición de claves y destinos de las tablas de reparto, tanto primario como secundario, cuando se utilicen porcentajes, mostrar junto al campo «Total de la clave:» otro indicador con la etiqueta «Pendiente de asignar:» y el porcentaje que falta hasta el 100 %.

## Criterios de aceptación

- El indicador aparece junto al total existente en los dos tipos de reparto cuando la clave se define por porcentajes.
- Se calcula con el mismo total y precisión utilizados para validar la clave: pendiente = 100 % menos total asignado. No introduce un cálculo divergente por redondeo de los valores mostrados.
- Se actualiza al editar porcentajes o añadir o eliminar destinos, sin exigir guardar la clave previamente.
- Ejemplos: total 0 % → pendiente 100 %; total 65 % → pendiente 35 %; total 100 % → pendiente 0 %.
- Si el total supera el 100 %, no se oculta el exceso ni se presenta la clave como válida: se conserva la validación existente y se identifica claramente la sobreasignación. Los mensajes contextuales de error cumplen PEND-UI-02, conservando los modales existentes.
- Es un indicador calculado de solo lectura, no un campo de entrada ni una parada adicional del tabulador, conforme a PEND-UI-03.
- Se conserva «Total de la clave:», las reglas de validación y el comportamiento de las claves no porcentuales. No se asigna automáticamente el resto ni se normalizan los porcentajes.
- Se respetan el formato regional, las traducciones español/inglés, la accesibilidad y la legibilidad del conjunto en ventanas estrechas.

## Validación pendiente

Comprobar localmente ambos repartos con claves vacías, incompletas, completas y sobreasignadas, incluidos porcentajes decimales y cambios de destinos. La anotación no implementa todavía el indicador. No ejecutar GitHub Actions.

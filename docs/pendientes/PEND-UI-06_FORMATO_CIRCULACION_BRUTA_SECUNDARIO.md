# PEND-UI-06 — Formato de importes en circulación bruta de auxiliares

Fecha: 2026-10-15. Estado: pendiente de implementación.

## Requisito del propietario

En la tabla «Circulación bruta de auxiliares» del reparto secundario, alinear a la derecha los importes de los registros y aplicarles el mismo formato monetario que utiliza el total.

## Criterios de aceptación

- Los importes de todas las filas de esta tabla se alinean a la derecha.
- Se utiliza el mismo formato monetario que el total: moneda, decimales, separadores y tratamiento de negativos, respetando la configuración regional vigente.
- Se reutiliza el mecanismo de presentación del total para evitar formatos divergentes.
- El cambio es de presentación: no altera valores, precisión de cálculo, sumas, ordenación numérica ni resultados del reparto secundario.
- Se comprueba la legibilidad con ceros, importes negativos, importes grandes y las configuraciones regionales admitidas, sin truncamiento de cantidades.

## Validación pendiente

Comprobar localmente la tabla real del reparto secundario y comparar el formato de sus filas con el total. Esta anotación no implementa todavía el cambio. No ejecutar GitHub Actions.

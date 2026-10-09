# Cierre del reparto primario y navegación — 2026-10-09

Validación local de Desktop y Tests. Suite completa: 578 pruebas superadas. Tras los ajustes finales: 3 regresiones específicas superadas y compilación Release sin errores ni advertencias.

La interacción nativa WPF verifica eliminación de claves sin uso, conservación de filas, selección y desplazamiento al asignar, acordeones independientes y cierre persistente. Comprueba que la pantalla 5 bloquea correcciones al cerrar y las permite tras reabrir desde la pantalla 6, incluso después de cerrar y abrir el archivo.

Las capturas usan un proyecto sintético de 19 cuentas. Se verificaron textos ES/EN y varios tamaños de ventana. El escalado WPF 125/150/200 % no sustituye una prueba con cambios del DPI del sistema.

- [Claves y cierre](es-keys-19-accounts.png)
- [Tabla ampliada y posición conservada](es-stable-account-selection.png)
- [Correcciones bloqueadas](es-closed-corrections.png)
- [Interacciones del flujo](interaction.json)
- [Interacciones de claves y reglas](keys-interaction.json)
- [Criterios funcionales](../../directrices/CIERRE_PRIMARIO_Y_NAVEGACION_2026-10-09.md)

No se ejecutó GitHub Actions.

# Simplificación del reparto primario

Validación local del 9 de octubre de 2026, misma rama y PR #33. Datos sintéticos.
[Decisión e inventario funcional](../../directrices/REPARTO_PRIMARIO_USABILIDAD_2026-10-09.md).

- Compilación Release de toda la solución: cero errores y advertencias.
- Suite completa: **575/575** pruebas correctas, ninguna omitida.
- Tras los ajustes finales del editor: **26/26** regresiones de T08/T09 y edición de claves/reglas.
- Recorrido WPF final: recursos, reparto primario y edición/guardado/reapertura correctos.
- ES/EN y pseudolocalización; preparación con anchos 960/1257/1600 y escalado de contenido WPF 125/150/200 %. El escalado de contenido no cambia el DPI del sistema.
- Sin GitHub Actions ni modificaciones a main.

Las nuevas regresiones comprueban edición económica de una clave guardada antes de
usarla, renombrado después del uso, rechazo de cambios económicos de claves usadas,
guardado de regla sin reparto, conservación del reparto activo al guardar una
modificación pendiente, aplicación posterior y preferencia de la asignación manual.
También verifican reapertura y capturas históricas sin reescribirlas.

El recorrido nativo utiliza los controles y servicios reales: Guardar clave,
Guardar regla/Aplicar regla, desplegable de la tabla, doble clic en la biblioteca,
edición del nombre de una clave usada y cierre/reapertura del proyecto. Se mantiene
la identificación de cuentas por código y descripción y la resolución de nombres
importados en conflicto. Los archivos de proyecto no se incluyen en las evidencias.

## Capturas y comprobaciones

- [Biblioteca y definición de claves](es-keys.png)
- [Asignación directa desde la tabla](es-direct-assignment.png)
- [Reglas con acciones separadas](es-rules.png)
- [Recorrido primario](interaction.json)
- [Edición, 19 cuentas y reapertura](workspace-interaction.json)

Comandos locales: `dotnet build Valdrio.slnx -c Release --no-restore` y
`dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore`.
Para el recorrido WPF se usó `--workspace-fixes-only --capture-dir=<directorio>` con
una copia desechable del proyecto sintético. Este recorrido crea datos de prueba;
no se ejecuta sobre un archivo de usuario.

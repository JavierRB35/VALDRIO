# T08-CUENTA-01 — Nombre/descripción de cuentas contables

Directriz obligatoria complementaria de [T08 canónica](../../specification/v0.4/tareas/T08.md), sin modificar las fuentes v0.4.
Seguimiento: [issue #28](https://github.com/JavierRB35/VALDRIO/issues/28). Fecha: 2026-10-08.

## Requisito obligatorio T08-CUENTA-01

Al importar cuentas contables se debe poder capturar su nombre/descripción mediante selección explícita de la columna de origen. T08 debe mostrar **código + descripción** en las superficies donde el analista identifica o selecciona cuentas para las reglas de asignación. Ejemplo de aceptación: `00123 — Material de oficina`. El código conserva identidad textual y ceros iniciales.

**Estado: pendiente de implementación/verificación funcional.** Este pedido solo registra documentación y seguimiento; no autoriza implementar lógica/UI ahora.

## Verificación previa realizada (2026-10-08)

Inspección estática de main@2a3c4b9a2c282c6e0b655e7ce31ee10f508cb637 y PR #22@5cf45b45484294129de9846ed7e7d1c8cc0a08d3. No se han ejecutado la aplicación ni pruebas; las comprobaciones de la PR no acreditan este requisito.

- [ImportColumnMap en main](https://github.com/JavierRB35/VALDRIO/blob/2a3c4b9a2c282c6e0b655e7ce31ee10f508cb637/src/Valdrio.Application/Importing.cs#L115) admite `Description` opcional. Capacidad contractual parcial; no equivale a un flujo completo.
- El asistente [main, líneas 226–227](https://github.com/JavierRB35/VALDRIO/blob/2a3c4b9a2c282c6e0b655e7ce31ee10f508cb637/src/Valdrio.Desktop/ImportColumnMappingViewModel.cs#L226) y [PR #22, líneas 261–262](https://github.com/JavierRB35/VALDRIO/blob/5cf45b45484294129de9846ed7e7d1c8cc0a08d3/src/Valdrio.Desktop/ImportColumnMappingViewModel.cs#L261) construye el mapa con cuenta e importe o Debe/Haber, sin descripción. Los XAML inspeccionados tampoco ofrecen selector de descripción.
- `ImportedResource` en [main](https://github.com/JavierRB35/VALDRIO/blob/2a3c4b9a2c282c6e0b655e7ce31ee10f508cb637/src/Valdrio.Application/ImportConfirmation.cs#L128) y [PR #22](https://github.com/JavierRB35/VALDRIO/blob/5cf45b45484294129de9846ed7e7d1c8cc0a08d3/src/Valdrio.Application/ImportConfirmation.cs#L190) expone Account e importes, sin nombre/descripción.
- La confirmación persiste `RawJson = Json(sourceRow)` y el lote completo; la proyección de recursos guarda Account e importes ([main](https://github.com/JavierRB35/VALDRIO/blob/2a3c4b9a2c282c6e0b655e7ce31ee10f508cb637/src/Valdrio.Infrastructure/CompanyFiles/SqliteCompanyFileService.Imports.cs#L74), [PR #22](https://github.com/JavierRB35/VALDRIO/blob/5cf45b45484294129de9846ed7e7d1c8cc0a08d3/src/Valdrio.Infrastructure/CompanyFiles/SqliteCompanyFileService.Imports.cs#L83)). Conservar celdas originales no acredita selección, resolución semántica, reapertura y exposición del dato al consumidor.
- Las fichas canónicas T07/T08 indican estado inicial pendiente; la PR #22 declara T07 no iniciada. No se ha verificado una pantalla T07/T08 funcional ni un contrato consumidor para descripción.

Conclusión acotada: existe soporte parcial en el contrato y conservación de origen; el recorrido completo no queda acreditado. Revalidar rama/HEAD reales antes de decidir qué reutilizar o modificar.

## Tarea y dependencias

1. Auditar en la rama de desarrollo vigente T06/T06A → confirmación → guardado/reapertura → recursos T07 → cuentas/reglas T08. Registrar SHA, archivos, contratos y resultados; clasificar cada paso como existente verificado, ausente o pendiente.
2. Reutilizar las capacidades verificadas. Si falta selección, persistencia semántica o exposición, abrir/coordinar subentregas en importación previa y T07 antes del cierre de T08; no resolver solo con texto de maqueta.
3. Documentar comportamiento para fuentes sin descripción, valores vacíos, nombres distintos para un mismo código, agrupación, sustitución y fuentes históricas. No inventar nombres ni modificar identidad/agrupación económica por descripción; la política de conflictos requiere decisión explícita y trazabilidad.
4. Mantener dependencias canónicas T08 → T04/T07 y T07 → T06A, añadiendo esta dependencia de datos hacia T06/T06A. T08-CUENTA-01 no puede cerrarse mientras una dependencia necesaria siga pendiente.

## Criterios de aceptación (pendientes)

- [ ] Antes de implementar, evidencia por SHA de selección, persistencia y exposición existentes; ninguna afirmación de implementación basada solo en contrato, JSON original o texto de PR.
- [ ] CSV/XLSX permiten seleccionar explícitamente columna de nombre/descripción cuando exista, con vista previa asociada al código correcto; no inferir mapeo por encabezados.
- [ ] Confirmar, guardar, cerrar y reabrir conserva código y descripción y su vínculo/procedencia; T07 los expone a T08 sin depender del archivo de origen ni de estado temporal de UI.
- [ ] T08 muestra código + descripción en listado/selección de cuentas y asignación de reglas; el analista identifica `00123 — Material de oficina`, con teclado y textos ES/EN utilizables.
- [ ] Fuente sin columna, vacío y datos históricos mantienen código visible y ausencia explícita de descripción; política aprobada sin inventar datos.
- [ ] Descripciones contradictorias del mismo código, agregación y sustitución tienen política explícita, reproducible y con procedencia; no mezclan cuentas distintas ni cambian reglas económicas por el nombre.
- [ ] Pruebas pertinentes locales de importación → reapertura → exposición y revisión de la UI real; registrar resultados y límites del HEAD validado antes de marcar cumplido.

## Coordinación y coste

Dar visibilidad a PR #22 y futuros desarrollos T07/T08 mediante enlaces; no sobrescribir, rebasar ni forzar ramas activas. Leer AGENTS.md de main y el requisito vigente aunque se trabaje en otra rama. Este issue no exige ampliar el alcance de una PR sin coordinación.

Objetivo Actions 0 USD. No disparar workflows ni usar GitHub-hosted runners sin autorización explícita; el límite de contingencia de 20 USD no autoriza gasto. La documentación complementaria se ubicará en `docs/directrices/T08_CUENTAS_DESCRIPCION.md` y se referenciará en AGENTS.md de main: ambas rutas están fuera de los filtros push/PR del workflow vigente. No modificar las fuentes canónicas v0.4 ni `docs/tareas/**` en esta entrega para evitar sus disparadores. Validación documental por lectura de cambios.

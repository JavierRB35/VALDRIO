# VALDRIO — Índice principal de tareas pendientes y seguimiento

**Revisión de estados:** 21/10/2026. **Base consultada:** `main@a1f3151`, issues y PR de GitHub a esa fecha.  
Este archivo es el **punto de entrada principal** para localizar trabajos pendientes, entregas parciales y asuntos cerrados. Las fichas enlazadas conservan la descripción y los criterios originales; sus fechas o estados históricos **no sustituyen** a este índice actualizado.

## Criterio para interpretar los estados

- **Resuelto / cerrado en seguimiento:** GitHub registra el cierre o existe una entrega aceptada. **Cerrar un issue no prueba por sí solo** la reproducción, corrección ni aceptación técnica: las reservas se indican expresamente.
- **Parcialmente desarrollado:** hay código o funcionalidad fusionada, pero faltan partes del alcance, pruebas específicas, decisiones o aceptación final. Un PR fusionado no cierra automáticamente un issue más amplio.
- **Pendiente:** continúa abierto el seguimiento o no consta una implementación completa y validada. Las capacidades parciales preexistentes no justifican marcar toda la tarea como hecha.

Para cambiar un estado, contrastar **issue, PR/commit y evidencia sobre Windows**, conservar la trazabilidad y actualizar este índice. No eliminar las fichas históricas ni modificar reglas económicas por una actualización documental.

## 1. Resueltos o cerrados en seguimiento

| Tarea | Documento | Estado contrastado | Seguimiento |
|---|---|---|---|
| **PEND-UI-01** — Validación al crear estructura | [Validación y continuidad](PEND-UI-01_CREAR_ESTRUCTURA_VALIDACION.md) | **Issue cerrado** el 15/10/2026 por el propietario. **Cierre técnico no acreditado por la ficha:** esta sigue solicitando reproducción fuera del depurador y pruebas de regresión. No declarar verificada la corrección sin esa evidencia. | [#58 (cerrado)](https://github.com/JavierRB35/VALDRIO/issues/58) |

## 2. Parcialmente desarrollados: queda alcance o aceptación

| Tarea | Documento / evidencia | Qué está desarrollado | Qué sigue por verificar o completar | Seguimiento |
|---|---|---|---|---|
| **PEND-RP-01** — Reutilizar repartos entre estudios | [Contrato funcional y casos A–J](PEND-RP-01_REUTILIZACION_REPARTOS_Y_PUERTAS_ENTRE_ESTUDIOS.md) | Primera entrega fusionada: previsualización, adopción explícita de reglas compatibles, control de conflictos, auditoría e idempotencia, sin copiar importes ni cierres. | La aceptación integral de escenarios A–J, ampliaciones funcionales y comprobaciones de uso no quedan certificadas por la primera entrega. **La cabecera de la ficha es histórica y no refleja esa implementación parcial.** | [PR #57 (fusionado)](https://github.com/JavierRB35/VALDRIO/pull/57) · [evidencias T14](../evidencias/t14-revision-local-20261015/README.md) |
| **Ayuda contextual WPF** — 16 vistas ES/EN | [Arquitectura y criterios de aceptación](../ARCHITECTURE_HELP.md) · [catálogo de ayudas](../help/README.md) | Lector WPF offline, botón/F1, Escape, cambio de pantalla y traducciones ES/EN fusionados. El propietario confirmó compilación, suite automática y funcionamiento visual de las vistas. | El [issue #75](https://github.com/JavierRB35/VALDRIO/issues/75) sigue abierto: contrastar antes de cerrarlo los criterios de evidencia fechada, accesibilidad/lector de pantalla, alto DPI y otros escenarios específicos que no cubre la aceptación funcional comunicada. Los ajustes futuros de contenido se pueden abordar por separado. | [PR #76 (fusionado)](https://github.com/JavierRB35/VALDRIO/pull/76) · [#75 (abierto)](https://github.com/JavierRB35/VALDRIO/issues/75) |

## 3. Pendientes de implementar o verificar

| Tarea | Documento | Pendiente concreto | Seguimiento |
|---|---|---|---|
| **PEND-UI-02** — Errores contextuales en rojo | [Requisito](PEND-UI-02_ERRORES_CONTEXTUALES_EN_ROJO.md) | Presentación de errores junto al campo o la operación, con contraste y ES/EN, sin alterar los diálogos existentes. | [#59 (abierto)](https://github.com/JavierRB35/VALDRIO/issues/59) |
| **PEND-UI-03** — Tabulación de campos editables | [Requisito](PEND-UI-03_TABULACION_CAMPOS_EDITABLES.md) | Tab/Mayús+Tab entre campos y celdas editables sin paradas innecesarias ni pérdida de accesibilidad. | [#60 (abierto)](https://github.com/JavierRB35/VALDRIO/issues/60) |
| **PEND-UI-04** — Porcentaje pendiente de asignar | [Requisito](PEND-UI-04_PORCENTAJE_PENDIENTE_ASIGNAR.md) | Indicador 100 % menos total en claves porcentuales de ambos repartos, coherente con la validación. | [#61 (abierto)](https://github.com/JavierRB35/VALDRIO/issues/61) |
| **PEND-UI-05** — Activar «Incluir» al asignar | [Requisito](PEND-UI-05_INCLUIR_AL_RELLENAR_ASIGNACION.md) | Marcar la fila editable al introducir o pegar una asignación, incluido cero explícito; respetar desmarcado manual y reapertura. | [#62 (abierto)](https://github.com/JavierRB35/VALDRIO/issues/62) |
| **PEND-UI-06** — Formato de circulación bruta | [Requisito](PEND-UI-06_FORMATO_CIRCULACION_BRUTA_SECUNDARIO.md) | Alinear importes a la derecha y aplicar a las filas el formato monetario del total, sin cambiar cálculos. | [#63 (abierto)](https://github.com/JavierRB35/VALDRIO/issues/63) |
| **PEND-UI-07** — Desplazamiento suave en repartos | [Requisito](PEND-UI-07_DESPLAZAMIENTO_SUAVE_REPARTOS.md) | Reproducir y corregir saltos de scroll en primario y secundario; validar selección, teclado y escala de pantalla. | [#64 (abierto)](https://github.com/JavierRB35/VALDRIO/issues/64) |
| **T08-CUENTA-01** — Código y descripción de cuentas | [Directriz transversal](../directrices/T08_CUENTAS_DESCRIPCION.md) | Acreditar el recorrido completo: selección de descripción en importación → persistencia/reapertura → preparación → identificación de cuenta mediante código y descripción en T08. Existe capacidad contractual parcial, no aceptación extremo a extremo confirmada en el issue. | [#28 (abierto)](https://github.com/JavierRB35/VALDRIO/issues/28) |

## 4. Otros registros y horizonte de trabajo

- [**Decisiones de producto y negocio**](../decisiones/DECISIONES_PENDIENTES.md): contiene decisiones aceptadas, propuestas y cuestiones pendientes; **es un registro histórico acumulativo**. Aplicar las autorizaciones posteriores de T15/T16/T17 antes de interpretar como vigente una propuesta antigua.
- [**Estado del proyecto**](../../ESTADO_PROYECTO.md): consultar primero los cortes más recientes; el documento también conserva estados anteriores.
- [**Plan de migración de interfaz**](../diseno/PLAN_MIGRACION_FRONTEND.md): plan visual de referencia fechado en octubre; comprobar el estado real del frontend antes de tratar cualquier fase como no iniciada.
- [**Tareas del producto**](../tareas/): fichas de etapas ya desarrolladas y de hitos posteriores. Una ficha de tarea no equivale por sí sola a un issue pendiente ni a una entrega validada.

**Regla operativa:** pruebas locales por defecto; coste de GitHub Actions no autorizado: **0 USD**. No ejecutar workflows potencialmente facturables sin permiso explícito. Los cambios exclusivos de este índice no requieren compilar la aplicación ni iniciar GitHub Actions.

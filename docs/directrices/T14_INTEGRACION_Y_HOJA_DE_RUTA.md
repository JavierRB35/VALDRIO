# T14 — Integración funcional y hoja de ruta T14.1–T14.4

**VALDRIO · R1 · Decisiones del propietario: 14/10/2026. Estado: documentación funcional aprobada; desarrollo pendiente.**

Este documento complementa [T14](../tareas/T14.md), la [directriz de capacidad normal asistida](T14_CAPACIDAD_NORMAL_ASISTIDA.md), la [ficha canónica T14 v0.4](../../specification/v0.4/tareas/T14.md) y CM08. **No altera la especificación canónica ni aprueba D04**. En caso de discrepancia metodológica, registrar una decisión explícita antes de desarrollar; no inferir una política económica.

## 1. Acuerdos funcionales cerrados

1. **T07 → T14.** Centralizar toda **nueva captura de capacidad física** (teórica, práctica, normal y actividad real) en T14. T07 conserva clasificación económica, comportamiento fijo/variable, capa, tratamiento de capacidad (None / AssessAtPool / PeriodCost), asociación del propietario al pool y destinos. **Decisión adicional aprobada: opción A.** Si hay capacidades físicas antiguas de T07, T14 las mostrará como **referencias heredadas pendientes de revisión**, con su origen y contexto. El usuario podrá adoptar expresamente las comparables o introducir otras nuevas. Nunca se sumarán, trasladarán o sobrescribirán automáticamente; los originales permanecen conservados. La ausencia de datos antiguos no implica cero. El editor avanzado reutilizado en Repartos/T07 no debe seguir siendo una segunda entrada de cantidades físicas nuevas.
2. **Actividad real.** Captura **manual desde T14.2** con unidad, período y fuente; importación de datos físicos **en T14.4** con revisión previa. No hacer depender la primera versión usable de un importador.
3. **Coste T12.** Mostrar el coste terminal publicado de la ejecución T12 identificada, de solo lectura, y **no recalcular** T11/T12 al guardar capacidad. Si una observación T14 cambia la revisión y la ejecución previa deja de ser vigente, conservar su importe **como referencia histórica explícitamente rotulada** con RunId, revisión y algoritmo; no llamarlo «coste vigente» ni usarlo como tal sin compatibilidad verificada. **Decisión adicional aprobada:** introducir o modificar horas normales/reales en T14, sin cambiar entradas económicas, **no obliga a repetir la ejecución T12**. T14 debe comprobar esa compatibilidad y distinguir «ejecución histórica según T12» de «coste de referencia económicamente compatible»; la regla técnica verificable sigue pendiente para T14.1.
4. **Horas de personal.** El método calendario emplea **personas equivalentes y un calendario homogéneo** para la unidad horas de personal. Las equivalencias y diferencias de jornada, turno, disponibilidad o calendario se **declaran y justifican**, no se presuponen. No sumar grupos no comparables ni imponer ocho horas diarias automáticamente; si la equivalencia no puede justificarse, se debe diagnosticar y solicitar revisión.

5. **Corrección de observaciones físicas (decisión adicional: opción B).** Distinguir **borrador** y **dato confirmado**. En borrador, admitir correcciones sin exigir un motivo en cada modificación, pero registrar la trazabilidad de los cambios. Tras la confirmación, toda sustitución de capacidad o actividad exige **motivo de corrección**, conserva el valor anterior y su fuente, y deja constancia de quién/cuándo modificó el registro según el contrato de auditoría. La fuente y el intervalo del dato siguen siendo identificables siempre. La implementación precisa de estados, transacciones, identificación de autor y esquema de auditoría permanece pendiente para T14.1. No equiparar confirmación física con publicación económica D04/T15.

**Mantener las decisiones anteriores:** una fila por **nodo principal** (R1: un pool por nodo, una unidad por pool), botón de calculadora en la misma fila, tres métodos completos (histórico comparable, calendario operativo y referencia declarada), capacidad normal independiente de actividad real y frontera económica T14/T15.

## 2. Inventario por pantalla: información existente y destino en T14

| Pantalla y responsable | Datos introducidos o disponibles antes de T14 | Uso en T14 | ¿Se pide de nuevo? |
| --- | --- | --- | --- |
| **Empresa** | Empresa, moneda funcional, catálogo de unidades (código, dimensión, símbolo/escala cuando existan) | Identificar ámbito, formato monetario y unidades válidas | **No** |
| **Estructura/modelo publicado** | Centros físicos, nodos principales y auxiliares, rol, pool asociado, unidad/driver de cada pool | Precargar centro, nodo, pool y unidad; una fila por nodo principal en R1 | **No** |
| **Estudio** | StudyId, modelo publicado, PeriodStart/PeriodEnd exactos, moneda, escenario | Vincular todas las observaciones físicas al intervalo y contexto del estudio | **No** |
| **Importación contable (T06)** | Archivo, mapeo de columnas, cuentas, importes y procedencia contable | Trazabilidad económica indirecta a través de los costes posteriores; **no es medición de actividad física** | **No** |
| **Recursos habitual (T07)** | Ajustes, importes efectivos, histórico de recursos | Base económica preparada para reparto | **No** |
| **Editor avanzado de partes de recurso, reutilizado en Repartos (T07)** | Clasificación fija/variable, capa, rutas a pools/objetos, tratamiento AssessAtPool, vinculación de propietario; actualmente también campos físicos **opcionales** | Conservar clasificación y asociación a pool; solo revisar cantidades antiguas si existen, sin presumirlas cumplimentadas | **No capturar cantidades físicas nuevas aquí** |
| **Claves y reparto primario (T08–T09)** | Porcentajes, claves por driver, importes absolutos, asignaciones; resultados primarios | Contexto de rutas y clasificaciones, **no** actividad real observada | **No** |
| **Reparto secundario (T10–T11)** | Reglas y claves de auxiliares, cálculo y conciliación | Origen metodológico de los costes terminales, pero no su fuente operativa persistida | **No** |
| **Flujos y ejecuciones (T12)** | Ejecuciones completadas, RunId, revisión y Output.Secondary.Totals | Leer coste final por nodo desde la ejecución T12, manteniendo clasificación/procedencia | **No** |
| **T13** | Pruebas de aceptación | Verifica contratos; **no** fuente de datos económicos ni físicos del usuario | No aplica |
| **T14** | Observaciones físicas y fuentes nuevas | Registrar capacidad normal y actividad real; opcionalmente práctica/teórica, calendario y evidencia; utilización calculada | **Sí, solo lo que falte y no sea derivable** |

**Comprobación de la UI actual:** MainWindow crea ResourcePanel con ShowDistributionTools=False en el recorrido ordinario «Recursos» y reutiliza ResourceWorkspacePanel en DistributionDetails dentro de «Repartos», donde se edita la clasificación. El control opcional de cantidades aparece condicionado a AssessAtPool. Esto **no** prueba que existan datos físicos de T07 en los estudios: distinguir capacidad de almacenamiento de datos realmente informados.

## 3. Contrato de la tabla y calculadora

- **Tabla única:** por nodo principal, centro/actividad, unidad del pool, coste T12 de solo lectura con estado y procedencia, capacidad normal, actividad real, utilización física (real/normal × 100 cuando sea calculable), estado y botón **Calculadora** por fila. Período exacto del estudio visible en contexto. Capacidades teórica/práctica en detalle cuando existan; ausencia distinta de cero.
- **Calculadora contextual:** hereda nodo, pool, unidad, período, coste y datos existentes sin pedirlos otra vez. Foco y amarillo de marca, más texto «Siguiente dato», en el primer campo requerido pendiente; ayudas ES/EN con qué es, qué introducir, de dónde sale y para qué sirve.
- **Tres vías para normal:** (a) histórico representativo con fechas, comparabilidad, anomalías y exclusiones justificadas; (b) calendario operativo con días, turnos, horas, recursos/personas equivalentes, interrupciones habituales y factor sostenible justificado; (c) referencia declarada con procedencia, responsable y justificación. Ninguna vía inventa la actividad real.
- **Horas de personal desde calendario:** calcular horas teóricas del período bajo calendario homogéneo (días × turnos × horas/turno × personas equivalentes); descontar solo interrupciones habituales expresadas en **horas-persona** y comparables para obtener capacidad práctica; aplicar factor sostenible **declarado y justificado** a la base que corresponda para proponer normal. No convertir personas equivalentes en horas sin calendario ni mezclar bajas, vacaciones y paradas dos veces. Si existen colectivos con horarios distintos, pedir equivalencia verificable y su motivo; no sumar calendarios heterogéneos por defecto. Esto es un ejemplo operacional, no un factor universal aprobado.
- **Actividad real:** edición manual por nodo y período con fuente/calidad en T14.2; importación física con vista previa y confirmación en T14.4. La actividad real no se calcula desde normal, práctica, coste económico, claves de reparto ni horas disponibles. Una cantidad medida de 0 es distinta de «Pendiente».
- **Acciones:** «Calcular y aplicar» valida y devuelve únicamente magnitudes físicas obtenidas/adoptadas a **la fila correcta**, dejando cambios pendientes de guardar; «Cancelar» no modifica la fila. No se reejecutan cálculos económicos. T15 queda a cargo de tarifas, valoración monetaria de capacidad y políticas D04.

## 4. Fuente T12 y alcance de la referencia histórica

1. La fuente económica es IRunStore.ReadRuns(CompanyId, StudyId), RunHistory.CurrentRunId y los totales terminales de CalculationRun.Output.Secondary.Totals de una ejecución Completed compatible; **no** sumar transferencias brutas ni usar un cierre T11 como sustituto.
2. Si existe ejecución **vigente y compatible**, mostrar su coste como «Coste publicado vigente», RunId, revisión, algoritmo y moneda.
3. Si guardar T14 deja una ejecución anterior **histórica** bajo el contrato de T12, seguir mostrando su importe y RunId con esa condición. **Cuando una comparación verificable demuestre que solo cambiaron observaciones físicas y no entradas económicas**, añadir «Coste de referencia económicamente compatible», sin denominarlo «ejecución T12 vigente»; de no estar comprobada la compatibilidad, mostrar «Coste histórico; compatibilidad no verificada». En ambos casos no asignar nuevo CurrentRunId, no ocultar la revisión original, no computar tarifas ni recalcular automáticamente.
4. Si no existe ninguna ejecución completada adecuada, mostrar **«Coste no disponible»** con enlace de navegación a Flujos y ejecuciones, sin inventar importes.
5. **Pendiente en T14.1:** decidir y validar contrato de revisiones/hashes económicos, vínculo RunId base, cuándo un cambio solo físico es compatible y cuándo debe invalidar la referencia. Hasta resolverlo, etiquetar el histórico como tal; ninguna selección UI cambia la autoridad económica.

## 5. Hoja de ruta por subentrega

| Subentrega | Alcance autorizado de definición | Salida verificable / límite |
| --- | --- | --- |
| **T14.1 — Integración y modelo físico** | Inventariar contratos y pantallas; definir CapacityObservation y ActivityObservation tipadas por StudyId/PoolId/UnitId/período, fuente/calidad, null≠0; estudiar persistencia/auditoría, registros antiguos T07 y vínculo a ejecución T12. Fijar el diseño para que T07 no admita nuevas cantidades físicas. | Matriz de origen aprobada, contrato de datos/guardado y compatibilidad T07/T12 resueltos **antes** de mutaciones de producción. No UI de tarifas. |
| **T14.2 — Tabla y captura manual** | WPF de una fila por principal, nodo/pool/unidad y coste de T12 precargados, estado de vigencia, normal y real manuales, fuente, validaciones, utilización, guardado/reapertura e interfaz ES/EN. Incluir botón contextual de calculadora como acceso a la fase siguiente, **sin controles inertes que prometan cálculo**. | Puede completarse un nodo manualmente, persistir y reabrir; costes intactos, cero y ausente diferenciados y ninguna llamada automática al solver. |
| **T14.3 — Calculadora completa** | Ventana flotante por fila con los tres métodos, datos precargados, ayudas, comparabilidad/anomalías históricas, calendario por equipos o personas equivalentes, propuestas justificadas, cancelación y «Calcular y aplicar». Captura manual de real integrada. | Los tres métodos se prueban end-to-end; propuesta va solo a su fila, se conserva evidencia, sin imputar actividad real ni crear tarifas. |
| **T14.4 — Importación física y aceptación** | Importación CSV/XLSX de actividad real con mapeo/unidad/período/fuente, vista previa, duplicados/solapamientos, confirmación auditada y conciliación frente a observaciones manuales; comprobación funcional internacional ES/EN y Windows local. | Reapertura y casos de normal/real, cambios de jornada, períodos parciales, históricos anómalos, importación errónea/validada y regresión T07/T12; evidencia antes de cierre. |

El fraccionamiento T14.1–T14.4 es **hoja de ruta operativa acordada**, compatible con las **tres entregas de alcance** de la ficha canónica v0.4; no altera la numeración canónica ni adelanta T15. La experiencia completa queda disponible a partir de T14.3; la entrada importada completa la fase en T14.4.

## 6. Cuestiones técnicas y metodológicas todavía abiertas — no resolver por interpretación

| Pendiente | Por qué importa | Puerta de decisión |
| --- | --- | --- |
| **Compatibilidad de revisión con T12** | **Objetivo funcional aprobado:** cambiar solo horas T14 no obliga a repetir T12; el run anterior puede seguir siendo histórico según el contrato vigente | Definir comparación verificable de entradas económicas y vínculo a RunId en T14.1, distinguir compatibilidad económica de vigencia T12 y probar cambios solo físicos frente a cambios económicos. No promocionar históricos a vigentes. |
| **Datos antiguos de T07** | **Comportamiento de usuario aprobado: opción A**, mostrarlos en T14 como referencias heredadas con origen para que el usuario decida adoptarlos o introducir otros, sin sumarlos ni copiarlos automáticamente | **Pendiente técnico T14.1:** definir detección, lectura, correspondencia entre parte y pool/período/unidad, conflictos y evidencias de adopción; conservar siempre originales y no imputar ceros a datos ausentes |
| **Modelo de observaciones y esquema** | **Regla funcional aprobada: opción B:** corregir borradores sin justificación repetitiva pero con registro de cambios; modificar confirmados solo con motivo y conservación de versiones. Siguen sin cerrarse cardinalidad, fuente/calidad, período, invariantes, autoría, estados y transacciones | Definir diseño técnico/ADR en T14.1 y probar registro de cambios en borrador, sustitución justificada de confirmados, migraciones y auditoría, sin mezclar confirmación física con aprobación económica |
| **Equivalencia de calendarios de personal** | Personas con jornadas/turnos distintos no equivalen sin prueba; paradas deben medirse en horas-persona y sin doble descuento | Precisar criterios de equivalencia/diagnóstico en T14.3; no implementar subgrupos obligatorios por suposición |
| **Representatividad histórica** | No existe ponderación universal, tratamiento automático de anomalías ni valor por defecto sostenible | Presentar decisiones al usuario y justificar; reglas automáticas nuevas requieren aprobación |
| **Esquema de importación física** | Duplicados, correcciones, unidades, parciales, solapamientos y conciliación pueden alterar real medida | Especificar y probar en T14.4, independiente del balance importado en T06 |
| **Política económica D04/T15** | Cómo se valorizan capacidad, fijos, variables y ociosidad no se decide en T14 | Mantener fuera de T14 |
| **Capacidades práctica/teórica obligatorias en casos especiales** | No son obligatorias universalmente según el UX aprobado | Requerir aprobación explícita si una fase futura las necesita |

No transformar estas cuestiones en decisiones silenciosas de implementación. Registrar alternativas, consecuencias, pruebas y aprobación correspondiente.

## 7. Criterios globales de aceptación y coste de ejecución

- Una fila principal recupera identificadores correctos, unidad y coste T12 por nodo; muestra vigencia o referencia histórica **sin falsear estado**.
- El usuario no introduce de nuevo datos ya conocidos por VALDRIO ni confunde un driver de reparto T08 con actividad real T14.
- T07 deja de admitir nuevas mediciones físicas, sin perder clasificaciones, rutas y valores heredados; ningún dato histórico es sumado o sobrescrito sin resolución.
- T14.2 admite normal y real manuales con fuente; T14.3 implementa **todos los métodos** y personas equivalentes justificadas; T14.4 admite importación física reproducible.
- Distinguir cero/ausencia; unidad/período/calendario comparables; sin cálculo de tarifas ni recálculo automático T11/T12.
- Usabilidad ES/EN, foco, DPI, accesibilidad y almacenamiento transaccional/auditable; validación y aceptación en Windows local.
- **GitHub Actions: gasto no autorizado = 0 USD.** No ejecutar workflow de runners GitHub-hosted sin autorización expresa; preferir Windows local/self-hosted, evitar builds por cambios de documentación y conservar límites económicos de VALDRIO.

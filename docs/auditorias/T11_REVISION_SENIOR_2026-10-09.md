# T11 — Revisión sénior matemática, ingeniería y experiencia del analista

Fecha de análisis: 2026-10-09. Alcance: PR #35, rama feature/t11-reparto-secundario-cierre.
Estado: **candidato supervisado; compilación .NET 10 y prueba WPF pendientes**. No es un certificado de aceptación ni autorización de merge.

## Principios metodológicos innegociables

1. La suma terminal proviene de los importes adjudicados por parte. Nunca sumar el bruto del recíproco a los hechos finales; este bruto es diagnóstico.
2. Los importes de cada parte conservan el signo y el quantum interno de 8 decimales. La asignación por 2, 97 o 100 destinos se redondea con BalancedRounder una sola vez por fuente.
3. Fijo/variable, capa funcional, y tratamiento de capacidad son etiquetas de origen que no se cambian al atravesar auxiliares.
4. Directo a objetos, excluido y pendiente quedan fuera del coste terminal: se reconcilian en el puente económico por recurso y no se duplican.
5. Una cobertura completa habilita el botón pero **no significa** solvencia del sistema. El cálculo informa de circuitos cerrados, autoasignaciones prohibidas, pivotes, amplificación y fallos de conservación.
6. Si existen diferentes grupos de distribución, cada parte de recurso pertenece a un grupo explícito; las antiguas usan all. Cada grupo usa su propia Q/B y sus claves publicadas, sin factores cruzados.
7. El cierre es distinto del cálculo. Cerrar bloquea primario, reabrir secundario no desbloquea primario, reabrir primario invalida el cierre secundario. El libro cerrado se consulta sin recomputar.

## Hallazgos y correcciones realizadas en esta revisión

| Prioridad | Hallazgo | Corrección en rama |
| --- | --- | --- |
| Alta | Cálculos sucesivos acumulaban filas de conciliación previas en la pantalla | Se vacían resultados, puentes y selección antes de recalcular |
| Alta | Explicar podía listar claves de auxiliares ajenos a la ruta real del origen al principal | Trazado dirigido por alcanzabilidad origen→terminal; las imputaciones directas no muestran claves secundarias |
| Alta | Cálculo síncrono podía bloquear la ventana en modelos grandes | Cálculo asíncrono fuera del hilo WPF, cancelación cooperativa y resultado descartado si cambia el estudio |
| Alta | Con coste primario cero, la cobertura vacía podía habilitar el cálculo sin claves | Se exige la cobertura all y se impide el cálculo con referencias obsoletas |
| Alta | Error por mapeo antiguo podía ser difícil de resolver | Acción para limpiar referencias obsoletas y diagnóstico visible |
| Media | Volver a calcular no explicitaba raw, ajuste numérico, redondeo y posted | Explicación por fila con el importe adjudicado y los tres componentes de auditoría |
| Media | Asignar clave a 100 auxiliares uno por uno exige demasiado trabajo | Acción atómica «Aplicar a auxiliares sin asignar» con validación de unidad y respeto de reglas manuales |
| Media | Acción de grupos podía descartar una edición de regla todavía sin aplicar | Bloqueos de comandos mientras existan ediciones de reglas pendientes |
| Media | Contrato de grupos aceptaba entradas duplicadas o nulas en constructor antes de diagnosticar | Validación del agregado AllocationConfiguration y referencias a partes |
| Metodológica | Falta conciliación explícita de directos, exclusiones, pendientes y costes de principales | Puente económico por recurso; diferencia no nula impide publicar |
| Metodológica | Grupos especializados anteriormente no permitían cálculo | Asignación explícita parte→grupo, Q/B y flujos por grupo, suma final controlada |

## Batería de casos extremos y uso real

| ID | Escenario adversarial o empresarial | Invariante / criterio esperado | Evidencia |
| --- | --- | --- | --- |
| E01 | Referencia C09 con dos auxiliares recíprocos | 6500 origen; P1=2857.14285715, P2=3642.85714285 tras redondeo por parte | Referencia independiente pasada; prueba C# escrita |
| E02 | 100 auxiliares en anillo, 10 fuentes cada uno, signos positivos/negativos | 1000 fuentes: 250 imputados a principal P1, 5 propios a P2; total 255, bruto aproximado 277.777... informativo | Oráculo racional independiente pasado; prueba C# escrita |
| E03 | 97 principales comparten importe +0.00000001 | Un único quantum adjudicado, suma exacta | Oráculo racional pasado; prueba C# escrita |
| E04 | Idéntico caso con -0.00000001 | Un único quantum negativo, sin pérdida del signo | Oráculo racional pasado; prueba C# escrita |
| E05 | Parte exactamente cero y ciclo cerrado sin salida | Bloqueo GRAPH_CLOSED_COMPONENT incluso con RHS=0 | Prueba C# escrita |
| E06 | Autoasignación no permitida | GRAPH_SELF_NOT_ALLOWED con código estable | Prueba C# escrita |
| E07 | Autoasignación casi absorbente (amplificación 2e12) | Bloqueo SOLVER_SENSITIVE | Oráculo racional pasado; prueba C# escrita |
| E08 | Actividad negativa y positiva en partes distintas | Conservación firmada por parte y global | Prueba C# escrita |
| E09 | Cambiar el orden de fuentes y destinos | Misma publicación canónica, determinismo | Prueba C# escrita |
| E10 | Recursos con directos a objetos y exclusiones | Puente efectivo=directo+excluido+pendiente+terminal; no duplicidad | Prueba C# escrita |
| E11 | Puente recurso deliberadamente mal construido | Bloquear diferencia no cero | Prueba C# escrita |
| E12 | Empresa sin auxiliares activos | Identidad directa a principales; sin flujo bruto ficticio | Prueba C# escrita |
| E13 | Un grupo especializado «operations» | Publicar con las claves y el grupo congelados | Integración SQLite escrita |
| E14 | Dos grupos que conducen a principales distintos | Una parte no usa ni contamina la ruta de otra; total en ambas | Integración SQLite escrita |
| E15 | Rama directa al principal y auxiliar sin contribución a ese principal | Explicar no atribuye claves secundarias ajenas | Prueba C# escrita |
| E16 | Archivo legado sin SecondaryPartRoutingGroups | Canonical JSON y contrato old all se mantienen | Prueba C# escrita |
| E17 | Reabrir secundario y luego primario | Sello activo invalidado, historial publicado intacto | Integración SQLite escrita |
| E18 | Cambiar primario sin reabrir secundario | Bloqueo transaccional de modificaciones | Integración SQLite escrita |
| E19 | Cancelar al comenzar el solver | OperationCanceledException; no resultados parciales publicables | Prueba C# escrita |
| E20 | El usuario recalcula dos veces | Sin filas duplicadas ni explicaciones del cálculo anterior | Corrección VM; pendiente prueba WPF real |
| E21 | 100 auxiliares sin claves manuales | Aplicar una clave a faltantes sin sobrescribir reglas; error de unidades específico | Corrección VM; pendiente integración WPF real |
| E22 | Parte obsoleta en un grupo tras cambiar primario | Mostrar estado y limpieza, bloquear cálculo hasta corregir | Corrección VM; pendiente integración WPF real |
| E23 | Cerrar y abrir sin ejecutar solver | Mostrar hechos y conciliaciones del recibo persistido | Implementado; pendiente ejecución nativa |
| E24 | Idiomas/regiones ES y EN con separador decimal distinto | Mismo ledger y fingerprint, mensajes traducidos | Auditoría de claves estática; pendiente automatizar locale en .NET |
| E25 | 100 auxiliares y 1000 fuentes con cancelación durante LU/ledger | Operación cancelable, UI responsive, sin cerrar fase | Prueba de estrés y cancelación escritas separadamente; pendiente prueba WPF |
| E26 | Todas las partes a objetos directos / sin líneas auxiliares | No inventar coste terminal ni forzar rutas no usadas | Caso a contrastar en integración nativa |
| E27 | Límites de decimal, quantum, pivote residual | Ninguna salida no representable o publicada parcialmente | Pendiente prueba .NET de fronteras extremas |
| E28 | Cambio de versión de clave publicada entre previsualización y cierre | Verificación de revisión y fingerprint, rechazo de edición obsoleta | Implementación SQL; pendiente prueba de concurrencia real |

## Comprobaciones efectivamente ejecutadas

- Verificador racional oficial v0.4 `casos/verificar_referencias.py` sobre `casos/casos_metodologicos.json`: **PASSED, 39/39**. El propio verificador aclara que no prueba C#, SQLite ni WPF.
- Oráculo independiente local con `fractions.Fraction`, contraste de C09 por parte, 100 auxiliares/anillo, 1000 fuentes con signos, reparto del quantum entre 97 principales y amplificación 2e12: **PASSED**. Este oráculo prueba expectativas, no el código C#.
- Revisión estática de recursos localizados, comandos vinculados y estructura WPF en PR #35; resultado en evidencia específica.
- No se ha ejecutado compilación, CI .NET, UI WPF, benchmark nativo ni end-to-end C#. Las pruebas C# añadidas están **escritas, no superadas**.

## Experiencia del analista: recorrido propuesto y reforzado

- Paso 1: elegir un estudio y comprobar primario. Si faltan partes, explicar la cobertura antes del botón.
- Paso 2: definir claves secundarias en la misma biblioteca T08 (porcentaje, unidades de actividad y partes iguales), elegir auxiliares/agrupación, usar asignación manual o relleno de faltantes.
- Paso 3: consultar por grupo la lista de auxiliares aún sin cobertura; no hacer pasar una regla por válida solo porque tenga filas.
- Paso 4: pulsar **Realizar cálculo** de forma explícita. Estado visible, cancelación, y mensajes con códigos / auxiliares / causas, sin publicación económica parcial.
- Paso 5: revisar costes terminales, conservación por recurso y botón **Explicar** para origen, versiones, porcentaje efectivo, ajustes y publicado. Separar circulación bruta.
- Paso 6: **Cerrar reparto secundario** solo después de revisar. Reabrir secundario para corregir; reabrir primario invalida cálculo anterior.

## Calidad y riesgos residuales

1. **Bloqueante de aceptación:** compilar solución en .NET 10 (preferiblemente Windows self-hosted), ejecutar suites T08–T11, regresiones internacionales y replay SQLite, corregir cualquier error real.
2. **Bloqueante funcional:** revisión WPF nativa con teclado, foco, tamaño de ventana, accesibilidad de DataGrid, ayudas y estados disabled/loading/cancelled.
3. **Bloqueante numérico:** perf/memoria reales con 100 auxiliares, 1000+ fuentes, múltiples grupos, partidas diminutas y amplificación próxima al límite; documento A05.
4. **Revisión adicional:** mutación concurrente de modelo y claves, input revision y idempotencia al reintentar cierre.
5. **Control de gasto:** el propietario autorizó CI en esta conversación. Sigue vigente límite absoluto de 20 USD y prioridad 0 USD. La conexión disponible permite leer workflows y runs, pero no expone dispatch de workflows ni consumo de facturación, por lo que **no se lanzó un runner facturable sin medición de presupuesto**. Los workflows existentes no se alteraron.
6. **No fusionar** la PR #35 mientras falten pruebas nativas y aceptación. La auditoría sénior reduce riesgo, pero no equivale a certificación.

## Decisión del supervisor

Se han corregido problemas reales de usabilidad, trazabilidad y ciclo de cálculo y se ha ampliado el corpus de pruebas adversariales. Se mantiene **PR en borrador**; pasar a lista para fusionar exige evidencias de pruebas reales, resolución de defectos y aprobación de metodología A05.

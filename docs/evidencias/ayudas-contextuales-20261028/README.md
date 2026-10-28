# Auditoría e implementación de ayudas contextuales — 28/10/2026

## Resultado ejecutivo

Se han revisado las 17 guías lógicas en español e inglés, su relación con 18 archivos XAML, las microayudas y los cálculos que explican. Se corrigen 15 pares de guías y 11 recursos de texto por idioma; se conservan las guías de análisis y composiciones y la arquitectura existente de ayuda por vista. No se modifica el motor de costes, persistencia, contratos de cálculo ni workflows de GitHub Actions.

El principal riesgo era explicar funciones o efectos económicos distintos de los que ofrece la aplicación: conversión de moneda durante la importación, capacidad tratada como futura, confusión entre pesos y cantidades físicas, y tarifas con bases distintas. También se corrige el lector inglés, que podía mostrar referencias técnicas y ocultar incorrectamente contenido posterior.

## Base, alcance y conservación de trabajo ajeno

Petición: «Texto pegado.txt» del chat referenciado por el usuario. Referencias: AGENTS.md, directrices de usabilidad, sistema visual V1, directrices T08, arquitectura de ayuda y documentación funcional existente. Se contrastan las afirmaciones con controles, ViewModels y servicios, incluyendo AllocationKeyRules, PrincipalRates, PrincipalRateReport, AuxiliaryCapacity, importación física y reglas de evidencia.

Rama específica: `audit/contextual-help-20261028`. El trabajo comenzó sobre `a654e46`; durante la revisión se incorporó por avance rápido el nuevo `main@2599188774ecd389888881af3dbf4307badb5709`, que ya contenía la auditoría funcional de otro trabajo. Se reaplicaron únicamente los cambios propios y se repitió la batería completa. Las modificaciones económicas de esa base son ajenas a este cambio. El checkout de auditoría funcional existente y los archivos sincronizados de `sources/` se preservaron.

La integración prevista es un avance rápido de `main` local desde esa base al commit que contiene este informe, previa comprobación de rama limpia y ascendencia. El SHA definitivo y el resultado efectivo de integración se entregan en la respuesta final. No se hace push ni se eluden protecciones remotas; la integración remota queda pendiente de autorización expresa.

## Diagnóstico y decisiones por pantalla

| Pantalla | Decisión y mejora aplicada |
|---|---|
| Proyecto | Escala significa decimales (0–12), no conversión ni multiplicación de importes. |
| Estructura | Diferenciar centro, nodo y grupo; principal/auxiliar y fijo/variable son dimensiones distintas. Explicar baja frente a eliminación con referencias. |
| Análisis | Conservar guía: selección, estados y snapshots son coherentes con la implementación revisada. |
| Importación contable | Importar en moneda funcional; normalizar externamente otras monedas. No anunciar conversión automática actual. Aclarar debe menos haber y comparación acumulada. |
| Preparación de cuentas | Clasificación y destinos comparten editor; separar porcentaje fijo/variable de participación entre destinos. |
| Primario | Porcentajes suman 100; drivers son pesos positivos relativos; partes iguales distribuyen uniformemente. Ejemplo 30/30 equivale a 50/50. Explicar precedencia de reglas y que generar una clave no la asigna a cuentas. |
| Secundario | Reciprocidad y circulación bruta no crean coste. Ejemplo 100 con autoconsumo del 20 %: bruto 125, interno 25 y salida 100. Distinguir circuito cerrado inviable y variación de capacidad. |
| Flujos y ejecuciones | Diferenciar situación actual parcial y ejecución histórica guardada, filtro y reconciliación global. Pareto usa coste positivo, separa abonos y no constituye una regla económica del 80 %. |
| Actividad y capacidad | Panel de resumen de lectura; edición integrada de normal y real. Confirmación y calidad de evidencia; descripción opcional. |
| Asistente de capacidad normal | Adoptar propuesta no equivale a guardar; cerrar no deshace adopciones o guardados previos. Corregir explicación del editor compartido. |
| Importación física | Cabeceras de plantilla fijas, fechas ISO y unidades/códigos. Aplicar guarda borradores; celdas vacías preservan la otra magnitud; duplicados no se suman automáticamente. |
| Servicios auxiliares | Diferenciar transferencia sin evaluar y evaluación propia. Servicio medido sustituye pesos; actividad real no se suma repetidamente entre grupos. Coste recibido no se evalúa de nuevo. |
| Tarifas | Fijo propio evaluado/normal y variable consumido/real tienen denominadores diferentes. Ejemplo 1.000/100 h, real 80 h: tarifa 10, absorbido 800 y variación 200. Explicar ejecución compatible y ausencia de consumo real de órdenes. |
| Composiciones | Conservar explicación de unidades, cantidades y dependencias; no añadir controles ni cálculos inexistentes. |
| Reutilización | El destino es el análisis actual; seleccionar el origen. Preservar que se copian reglas, no saldos, cierres ni ejecuciones. |
| Órdenes estimadas | Un importe incompleto puede conservar cantidades físicas; no permite aceptar un resultado incompleto. Corregir microtextos truncados en ambos idiomas. |
| Preferencias | Fechas según zona horaria seleccionada, no siempre UTC. |

## Implementación

- Conservar F1, ayuda por foco, tooltips, AutomationProperties.HelpText, tablas, estilos, idiomas y navegación existentes. Inventario estático: 103 claves FieldHelp únicas, sin recursos ausentes; 123 recursos Guide.Help disponibles. No se añaden ayudas genéricas redundantes.
- Sustituir las seis microayudas de capacidad que hablaban de una fase futura por el flujo actual de observaciones confirmadas. Aclarar rol, pesos y alcance de importación; reparar dos textos de órdenes estimadas.
- El lector reconoce referencias técnicas inglesas y españolas, oculta metadatos ingleses y recupera contenido de negocio tras encabezados de nivel cuatro.
- Actualizar catálogo y arquitectura; incorporar ClassificationReviewPanel al mapa de recursos.
- Añadir pruebas de renderizado STA y un recorrido WPF local optativo (`--context-help-only`, junto con el modo de capturas existente). No se activa en el uso normal.

## Validaciones y evidencias

| Validación local | Resultado |
|---|---|
| Compilación Release de Valdrio.slnx | Correcta, 0 errores y 0 advertencias. |
| Batería completa tras incorporar main@2599188 | 783 correctas, 0 fallidas, 0 omitidas. |
| Última revisión de textos: ayuda y localización | 21 correctas, 0 fallidas, 0 omitidas. Incluye renderizar las 34 variantes de artículos empaquetados. |
| scripts/verify_help.py --base origin/main | 17 vistas y 18 archivos XAML cubiertos. |
| git diff --check | Correcto. |
| Recorrido de ventanas WPF con ejemplo desechable | 17 guías × 2 idiomas; artículo correcto, tablas y sección final presentes, Escape cierra; revisión económica se mantiene en 7. |
| Evidencia visual | 68 capturas generadas: tamaño normal y ancho 560 con zoom 150 %. Se adjunta selección y registro completo de comprobaciones. |
| Interacción manual | F1 desde proyecto, Escape y retorno al campo, cambio inglés/español, ayuda completa desde preferencias y texto accesible del campo. |

La batería completa precede a los últimos cambios menores de redacción y al cierre del arnés; la compilación y las 21 pruebas específicas son posteriores. Las capturas preceden únicamente a ajustes de metadatos y a precisar que el destino de reutilización es el análisis actual. El contenido final se vuelve a validar por las pruebas de renderizado. Los resúmenes TRX figuran en `test-summary.json`; los TRX completos permanecen en TestResults del checkout de trabajo.

Archivos adjuntos: `context-help.json`, `test-summary.json`, `es-View_SecondaryAllocation.png`, `en-View_PrincipalRates-narrow.png`, `es-View_NormalCapacityAssistant.png` y `en-View_ImportBalances.png`.

## Límites y riesgos residuales

- La inspección visual incluye muestras, no una lectura humana exhaustiva de las 68 capturas. En ventana estrecha y zoom alto se parten palabras en tablas; el contenido sigue disponible mediante desplazamiento. No se rediseña el lector dentro de este alcance.
- Zoom del lector no acredita DPI físico del sistema, todos los monitores ni un ensayo con lector de pantalla. No se ha realizado un estudio de comprensión con usuarios finales.
- El ejemplo antiguo se actualiza a una copia desechable. Al abrir auxiliares aparece una propuesta inicial sin guardar; el arnés descarta solo ese estado en memoria para cerrar. En reutilización construye la vista con los servicios reales porque el ejemplo tiene repartos cerrados: valida su ayuda, no la habilitación de la acción ni la adopción de metodología.
- No se repiten operaciones económicas desde cada guía: se contrastan fórmulas y contratos con código y pruebas existentes. No se certifica ausencia universal de errores económicos.
- No se ejecutan Actions, despliegues ni comprobaciones remotas. Coste provocado de GitHub Actions: 0 USD. El presupuesto de contingencia de 20 USD no se utiliza.

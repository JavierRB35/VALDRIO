# VALDRIO — Candidatura de implementación: funciones de nodo y factores directos

**Fecha:** 2026-11-04  
**Rama:** `feature/node-cost-purpose-20261104`  
**Estado:** **candidato, no probado en Windows, no integrado en main**.

## Alcance implementado en esta rama

1. **Clasificación de nodos (T04):** función de destino (Producción / Comercial / Estructura) y finalidad tarifaria (General / MOD / Aprovisionamiento), independientes del papel Principal/Auxiliar. Campos ES/EN en el editor. Los nuevos nodos requieren función; la edición de nodos históricos de etiquetas libres no les asigna una función inventada.
2. **Compatibilidad:** las clasificaciones se guardan como etiquetas reservadas de `FunctionTags` en el esquema/modelo versionados existentes; no se modifican serialización de recursos, algoritmos de reparto, migraciones SQLite ni contratos económicos CM02–CM14. Para históricos sin etiquetas reservadas se muestra **Sin clasificar** en el resumen funcional y **General** en el filtro de tarifas/repartos, hasta clasificación expresa. Los códigos reservados no aparecen en el editor de etiquetas libres.
3. **Repartos T09/T11:** tablas de primario, totales primarios, contribuciones secundarias y totales terminales separados visualmente entre **centros generales** y **factores directos**. Las colecciones completas siguen existiendo para los informes, las pruebas y conciliaciones originales. Totales se rotulan como consolidados de ambas categorías; los Pareto siguen el filtro elegido. No existe un ledger paralelo.
4. **T15:** título de pantalla/menú «Tarifas y costes unitarios», pestañas «Tarifas de centros»/«Factores directos» que filtran el mismo informe existente por finalidad explícita del nodo, conservando desgloses y preguntas/respuestas. Para nodos adaptados por estudio toma la clasificación de la ejecución congelada cuando está disponible.
5. **T12:** proyección de costes terminales por **función económica del nodo principal de destino**, sin alterar la capa del recurso origen. Vista informativa sin inventar ventas, costes de lo vendido ni márgenes de T20. Los nodos históricos sin función permanecen como grupo aparte.
6. **ES/EN:** claves traducidas y ayudas actualizadas para la separación de nodos, nueva pantalla y lectura de importes.
7. **Pruebas añadidas/adaptadas (no ejecutadas):** casos de dominio sobre clasificación y legado; casos de reporting sobre conservación de importes firmados y función de destino; adaptaciones a smoke de estructura, tarifas y repartos.

## Contratos y límites preservados

- Mantener fórmula CM08: fijo propio / normal; variable/recibido / real. No se altera el cálculo ni el solver recíproco T10/CM06.
- Capacidad auxiliar evaluada una única vez (CM09). Un servicio de estructura puede llegar a producción o comercial por reparto sin restricción por etiqueta.
- No se utiliza `ResourceCostLayer` del recurso para sustituir la función del nodo de destino: ambas procedencias quedan separadas.
- No se alteran datos ni hashes económicos históricos mediante migraciones.
- No se retiran ni amplían las reglas reutilizables de ajustes extracontables; PEND-AC-01 permanece retirado como pendiente.

## Funcionalidades de la adenda todavía NO implementadas

- Captura real por **grupo homogéneo MOD y objeto** con sus tres fuentes/conciliación; T14 ya contiene actividad global y T18 estimaciones, pero T19 necesita integración y hechos reales verificables.
- Modalidad **avanzada** de valoración de aprovisionamiento, pendiente de definición de entrega.
- **T20**: ventas reales y correspondencia temporal de coste productivo de lo vendido, asignación comercial, margen industrial, margen después de comercialización y resultado de período. La nueva proyección de T12 **no equivale** a esos márgenes.
- T22 y otras exportaciones de rentabilidad; valoración contable de inventarios T32 sigue fuera de alcance.
- Selección alternativa de capacidad práctica y políticas comerciales detalladas: pendientes de decisión, no se asumen.

## Verificación efectivamente realizada en esta sesión

**Realizada por inspección de fuentes en GitHub:**
- Comprobación estática de balance de etiquetas y unicidad de `x:Name` en cuatro XAML afectados.
- Comprobación estática de balance de etiquetas y unicidad de claves de los recursos de localización; claves ES/EN coinciden.
- Comprobación de presencia de claves nuevas en los catálogos de idioma.
- Inspección localizada de recorridos WPF actuales; adaptación de pruebas de edición y creación para la clasificación funcional nueva.
- No se modificaron la especificación v0.4, el solver, CM08/CM09 ni los identificadores de tareas.

**No realizadas:** compilación `.NET 10`, MSTest, smoke WPF ES/EN, pruebas integradas de SQLite ni capturas visuales. El entorno de esta sesión no dispone de SDK ni escritorio Windows; **no se deben presentar como exitosas**. No se solicitó ni ejecutó GitHub Actions. La candidatura **no reúne todavía las condiciones para su fusión en main**.

## Verificaciones necesarias antes de integrar

1. `dotnet restore Valdrio.slnx`, compilación Release de la solución y tests focalizados `NodeCostClassificationTests`, `RunFunctionBalanceTests`, T04, T09, T11, T12 y T15.
2. Regresión WPF de creación y edición de nodos; publicación/duplicación; selección de funciones y factores directos; protección de cambios pendientes; reabrir modelos legados.
3. Verificar tablas de primario/secundario en ambas pestañas, con MOD recibiendo servicios, costes negativos, conciliación del total y Pareto filtrado, en ES/EN.
4. Verificar el informe de tarifas con grupos MOD activos, capacidad normal/real y recepción auxiliar; casos de denominador cero y sin datos; fuente congelada, exportación y ausencia de aplicación duplicada.
5. Verificar informes históricos tras cambios de modelo y la clasificación de destino sin alterar `ResourceCostLayer`.
6. Revisar el diff final y ejecutar las comprobaciones obligatorias de integración del proyecto, **sin activar Actions automáticamente**.

**Criterio de aceptación:** interfaz estable y resultados económicos iguales al motor anterior para entradas equivalentes, con diferencia exclusivamente de clasificación/vista y trazabilidad adecuada; los cálculos funcionales nuevos deberán justificarse y probarse independientemente.

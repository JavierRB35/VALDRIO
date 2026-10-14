# VALDRIO — revisión visual, estética y de usabilidad

14 de octubre de 2026. Base: `main@e1952e360881fe99aaec4fb49bfb7d94c000a3a8`. Rama: `ui/visual-ergonomics-20261014`.

La instrucción posterior del propietario autoriza implementar directamente las mejoras visuales. Se mantiene la identidad aprobada en SISTEMA_VISUAL_VALDRIO_V1: verde profundo, blanco, neutros y amarillo funcional. Se conserva la edición en tablas y la navegación de tres fases operativas. Esta revisión no modifica cálculo, reparto, importación económica, hashes, contratos ni SQLite.

## Conclusión ejecutiva

La identidad es consistente; el principal problema es la distribución del espacio y la prioridad de la información. Hay demasiado contexto permanente, tarjetas anidadas y explicaciones repetidas antes de las tablas. La aplicación necesita facilitar reconocer el estado, encontrar la siguiente acción e investigar una cifra manteniendo el contexto.

Se implementan cinco mejoras acotadas: cabecera compacta con detalle accesible, densidad de tablas ajustable, jerarquía financiera de cifras, coherencia del asistente de importación y diagnóstico más visible. No se afirma una renovación completa del producto ni ausencia absoluta de regresiones.

## Evidencia y método

- [Galería antes/después](galeria.html): pantallas reales de WPF, imágenes originales completas, búsqueda por pantalla/estado e imágenes ampliables. El índice `capturas.json` identifica cada archivo y su SHA-256.
- Base: recorrido autocontenido `artifacts/visual-before`, 70 capturas. Datos sintéticos; no archivos de empresas del usuario.
- Cobertura principal: proyecto, estructura, periodo/análisis, importación, preparación de cuentas, primario, secundario y preferencias. Se añaden estados de importación, claves, historial, explicaciones, grafo, ES/EN y pseudo-RTL.
- El fichero `es-step-7.png` del harness es Preferencias, **no** el reparto secundario. Este último está en `methodology/es-secondary.png`.
- Escalas 125/150/200 % mediante WPF LayoutTransform y ventana mínima 760×620. No equivalen a ensayar cambios reales de DPI entre monitores.
- Inspección del XAML y sus comandos/bindings, capturas y recorridos reales. La evidencia sintética no sustituye pruebas con usuarios ni miles de filas.
- Las capturas suplementarias de arranque, detalle de contexto, densidad, grafo y explicación no tienen todas un par previo equivalente; la galería lo indica.

## Referentes contrastados

Se consultó documentación oficial el 14/10/2026. Los patrones siguientes son referencias verificables, no una clasificación objetiva de «mejores aplicaciones» ni una copia de su interfaz.

| Referente | Patrón documentado | Aplicación a VALDRIO |
|---|---|---|
| [Pigment: diseño de grids](https://kb.pigment.com/docs/chapter-5-how-to-format-grids-charts-and-kpis) y [layout](https://kb.pigment.com/docs/customize-layout-views) | Estilo contenido y control de altura de filas, columnas y jerarquía | Reducir competencia visual, ofrecer densidad sin reducir tipografía; destacar importes decisivos. |
| [Cube: drilldown](https://help.cubesoftware.com/hc/en-us/articles/48753162444180-Drilldown-in-the-Web-Workspace) | Acceso desde una cifra agregada a transacciones conservando las dimensiones y filtros de origen | Explicación vinculada a la fila seleccionada, con procedencia visible; no exigir reconstruir el contexto. |
| [Board: selectores](https://help.board.com/v14/docs/the-selector-object) | Selecciones contextualizadas que actualizan la pantalla | Mantener empresa/estructura/análisis identificables; no mezclar filtros, configuración y navegación. No se añaden filtros económicos inexistentes. |
| [Power BI: drillthrough](https://learn.microsoft.com/en-us/power-bi/guidance/report-drillthrough) | Ir del resumen al detalle y volver al origen | Conservar selección, contexto y retorno; evaluar el recorrido desde una cifra, no solo contar botones. |
| [Anaplan: drill down](https://help.anaplan.com/use-drill-down-836b9b62-e507-4dc2-9f51-cbc841b22e36) | Acceso al detalle por teclado y panel ampliable | Explicaciones legibles y accesibles sin depender exclusivamente de un tooltip. |
| [CloudZero: Explorer](https://docs.cloudzero.com/docs/explorer) | Costes agrupados, tabla vinculada a gráfico y profundización por filas | Mantener separados total, aportaciones y circulación interna; conservar el grafo existente sin crear nuevas métricas. |
| [IBM Carbon: tablas](https://www.carbondesignsystem.com/building-blocks/core/components/data-table/guidelines) | Tabla con jerarquía clara de contenido y acciones | Unificar cabeceras, cifras y acción principal del asistente. |
| [VS Code: interfaz](https://code.visualstudio.com/docs/editing/getting-started/userinterface) | Espacio de trabajo y detalle accesible a demanda | Reducir contexto permanente sin borrar información. |

No se afirma una comparación práctica dentro de cuentas de estos productos.

## Hallazgos por pantalla y actuación

| ID / prioridad | Evidencia y efecto | Propuesta / referente | Estado de esta entrega |
|---|---|---|---|
| V01 · Alta · shell | La ruta completa y contexto multilínea consumen mucha altura al 200 %. `MainWindow.xaml`, cabecera; `visual-shell/layout-scale-200.png`. | Resumen de una línea y acción explícita para ver contexto completo. Revelado progresivo. | Implementado: Detalles/Ocultar detalles, teclado y ES/EN. Se conserva texto íntegro y ubicación del archivo. El resumen puede abreviarse; el detalle no. |
| V02 · Alta · tablas | Densidad fija; distintos paddings y mínimos locales. `ValdrioTheme.xaml` y tres grids de repartos. | Espaciado cómodo/compacto, sin bajar tamaño de texto ni cambiar importes. Pigment. | Implementado con recursos dinámicos comunes. Mínimo 36/28 DIP; las filas crecen si texto o controles lo necesitan. Opción de sesión, también aplicable a diálogos. No promete idéntica altura en toda fila. |
| V03 · Media · selección | El amarillo de cada celda seleccionada se parece al aviso/borrador. | Selección verde suave, borde perceptible; amarillo permanece en navegación. Pigment / semántica de estados. | Implementado en el estilo compartido de DataGridCell. El foco conserva su indicación. |
| V04 · Alta · importación | Cabecera local sin heredar el tema y cifras originales/interpretadas a la izquierda. La advertencia de moneda usa un token tenue destinado a texto grande. | Cabecera común, números tabulares a la derecha, mensaje esencial legible. Carbon. | Implementado; conserva formato y texto de origen. No se interpreta de nuevo ningún importe. |
| V05 · Media · importación | «Comprobar y continuar» tiene la misma relevancia visual que acciones secundarias. Inicio de importación queda disperso sobre fondo vacío. | Acción principal inequívoca y resumen dentro de una superficie común. | Implementado: botón primario y tarjeta del resumen de importación. No cambia habilitación ni validación. |
| V06 · Alta · errores de reparto | El mensaje general comparte superficie neutra incluso si proviene de un diagnóstico. `ActionStatus`/`Status`. | Distinguir error sin abrir modal ni abandonar la tarea. | Implementado: fondo y borde de error basados en la existencia del diagnóstico, manteniendo su explicación textual y anuncio accesible. No se colorea un saldo negativo como error por defecto. |
| V07 · Media · primario | Seis métricas relevantes compiten tipográficamente con etiquetas y párrafos. | Jerarquía de etiqueta/valor reutilizable. Pigment. | Implementado `Text.Kpi` sobre valores existentes, sin nuevos indicadores ni cálculos. |
| V08 · Alta · explicación secundaria | Texto limitado a 72 DIP, recortado con puntos suspensivos y completo solo en tooltip. | Mostrar explicación completa en el panel, compatible con teclado. Anaplan. | Implementado: altura mínima, crecimiento por contenido y scroll existente. |
| V09 · Alta · arranque | Hay abrir proyecto y crear proyecto; no hay un botón de ejemplo empaquetado ni importación que pueda saltar los requisitos de estructura/análisis. `CompanyFilePanel.xaml`, `startup.png`. | Tres intenciones claras: aprender con ejemplo, importar con asistente, retomar. | Pendiente. Requiere conectar un ejemplo distribuido y las precondiciones reales; una tarjeta inactiva o una importación ficticia sería engañosa. Se conservan los flujos actuales. |
| V10 · Media · estructura | Párrafos repetidos, marco dentro de marco y varias acciones de creación/versión/recarga antes de los datos. `models/*`, `visual-shell/es-step-2.png`. | Revisar frente a editar, detalle de metodología ampliable, mayor área de tabla. | Auditada; cambios globales aplicados. Reorganización específica pendiente de inventario por estado, sin eliminar campos ni duplicar edición. |
| V11 · Media · periodos | Recuperar análisis y formulario actual compiten; fechas/moneda quedan por debajo del primer pliegue. `visual-shell/es-step-3.png`. | Selector de contexto compacto y formulario actual prioritario, siguiendo Board. | Auditada; gana espacio de cabecera. Reordenación específica pendiente. |
| V12 · Media · cuentas | Instrucciones de corrección/guardado/cierre se repiten antes de una tabla de pocas filas. `visual-shell/es-step-5.png`. | Estado breve más ayuda contextual, sin perder unidades ni reglas de guardado. | Densidad y selección implementadas; revisión editorial de ayudas pendiente. |
| V13 · Alta · origen de cifras | Selección de cuenta muestra destinos; selección de principal muestra orígenes; selección de aportación muestra explicación. Parte del detalle requiere scroll. | Resumen → fila → explicación con contexto estable, Power BI/CloudZero. | Se conserva y captura; se elimina el recorte de explicación. No se certifica ≤3 interacciones universalmente: faltan matriz por tipo de cifra, estados vacíos y volúmenes reales. |
| V14 · Media · preferencias | Idioma/región se presentan como códigos y soporte técnico puede ocupar gran altura al expandirse. `visual-shell/es-step-7.png`. | Etiquetas humanas con código secundario y soporte separado. | Auditado; preservado para no alterar valores ni pruebas existentes. |
| V15 · Alta · ergonomía a escala | MinWidth de contenido, columnas y formularios siguen produciendo scroll horizontal a 200 % y en ventana mínima. | Reflujo por paneles y prioridad del área de tabla; no encoger tipografía. | Mejora parcial en cabecera; no resuelto globalmente. No se declara conformidad de todos los DPI. |
| V16 · Media · grafo secundario | El grafo usa un lienzo amplio ajustado al ancho; leyenda y textos secundarios se reducen. La captura sintética contiene auxiliares de coste cero, por lo que no prueba legibilidad de ciclos densos. `methodology/es-cost-flow.png`. | Panel ampliable con foco en la selección y tabla equivalente; Anaplan/CloudZero. | Capturado y auditado; no se cambia el grafo ni se inventan interacciones. Pendiente de ensayo con redes representativas. |

## Navegación y regla de tres interacciones

Se mantienen Proyecto/Estructura → Análisis/Datos económicos → Repartos/Cálculo. Producción/Objetos, Rentabilidad/Decisión e Informes/Cierre siguen siendo dirección de producto, no módulos implementados que deban aparecer disponibles. «Capacidad» tampoco se promete como motor operativo en una etiqueta.

Recorridos observables en el código y en las capturas: cuenta seleccionada → destinos y flujo; principal seleccionado → orígenes; aportación seleccionada → explicación por cuenta. Es acceso contextual existente, no una capacidad nueva añadida por esta PR. Las comprobaciones metodológicas validan datos y selección, pero no miden todas las interacciones físicas, desplazamientos o estados. Se necesita aceptación por tareas para afirmar el objetivo universal.

## Esquema breve de trabajo restante

1. **Arranque:** tres intenciones reales, ejemplo instalado y asistente que conduzca a estructura/análisis sin callejones sin salida.
2. **Pantallas densas:** reducir anidación y repetir menos instrucciones en estructura, periodos y cuentas.
3. **Trazabilidad:** medir los recorridos con importes positivos/negativos y datos representativos; situar explicación junto a selección cuando resulte útil.
4. **Ventanas estrechas y DPI:** reordenar formularios y paneles conservando edición, teclado y textos largos.
5. **Accesibilidad:** revisar contraste medido, lectores de pantalla, navegación completa y datos voluminosos con usuarios.

## Registro de archivos modificados

| Archivo | Impacto |
|---|---|
| `src/Valdrio.Desktop/MainWindow.xaml` | Contexto compacto, detalle accesible, selector de densidad y tarjeta de importación. |
| `MainWindow.Presentation.cs` | Estado de presentación de sesión; cambia recursos de espaciado, no el modelo económico. |
| `MainWindow.Guidance.cs` | Actualización de etiquetas ES/EN al cambiar presentación. |
| `Styles/ValdrioTheme.xaml` | Recursos de densidad, tipografía tabular, jerarquía KPI y selección. |
| `ImportColumnMappingWindow.xaml` | Cabecera común, cifras alineadas, aviso de moneda y acción principal. |
| `AllocationWorkspacePanel.xaml` | Densidad compartida, diagnóstico, KPIs y explicación sin recorte. |
| `AllocationWorkspaceViewModel.cs` | Un getter de presentación expone si ya existe diagnóstico; no modifica su creación ni tratamiento. |
| `src/Valdrio.Localization/Resources/Strings.resx`, `Strings.es.resx` | Cuatro textos de presentación en inglés/español. |
| `App.xaml.cs` | Captura del arranque exclusivamente en el recorrido de evidencia. |
| `MainWindow.VisualSmoke.cs` | Prueba de detalle por teclado, cambio/restauración de densidad y conservación de filas/selección. |
| `MainWindow.MethodologySmoke.cs`, `MainWindow.AllocationSmoke.cs` | Capturas adicionales del grafo, desgloses y clave incompleta. |

Prefijo de los nombres abreviados: `src/Valdrio.Desktop/`. Inventario de controles nombrados y bindings de datos/comandos: `inventario-controles.json`. La comparación estática es una salvaguarda, no una prueba completa del comportamiento.

## Validación

Véase `VALIDACION.md` y `validacion-wpf.json` para los resultados finales, comandos y límites. La barra global de actividad, estados de guardado, validación y bloqueos ya existían y se conservan; no se atribuyen como novedades. No se cambian workflows ni facturación. GitHub Actions ejecutados por esta tarea: ninguno.

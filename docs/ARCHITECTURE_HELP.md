# Arquitectura de ayuda contextual — VALDRIO

**Corte examinado:** `main@211af0dab39b32902a8cd98f4f377c91638e8920` (20/10/2026). **Documentación creada:** 21/10/2026.

## Objetivo y alcance

Sistema de ayuda *por vista lógica*, no por clase técnica. La aplicación es WPF/.NET 10; no tiene router web. Sus vistas viven en `MainWindow.xaml`, paneles `UserControl` y ventanas modales. `AllocationWorkspacePanel.xaml` participa tanto en el primario como en el secundario: **dos IDs de ayuda diferentes**. El sistema existente `FieldHelp.cs` ya usa tooltip, foco de teclado y `AutomationProperties.HelpText` para microayuda localizada; debe conservarse.

Se incluyen 16 vistas lógicas en `docs/help/help-map.json`: proyecto, estructura, estudio, importación contable, recursos, primario, secundario, flujos, actividad T14, asistente de normal, importación física, auxiliares T16, tarifas T15, composiciones T17, reutilización de metodología y preferencias. Los diálogos de confirmación integrados reutilizarán la ayuda de la operación madre, con mensaje y consecuencias explicados de forma específica. Las tres fases comerciales futuras de producto no se presentan como módulos operativos.

## Contrato de contenido

Cada `.md` contiene exactamente un `ID Vista UI` único, fecha y cuatro secciones obligatorias:

1. Objetivo y función financiera y decisión posible.
2. Tabla de entradas: campo, tipo/unidad, obligatoriedad y efecto económico.
3. Tabla de salidas: origen/fórmula y lectura para la decisión.
4. Casos de uso, validaciones y errores frecuentes con causa y acción.

La ayuda se redacta sobre controles realmente presentes en XAML y su lógica verificada en ViewModel/servicio. No convertir planes R1–R4 en capacidades actuales. *Pendiente* no es `0`; borrador no es publicado; volumen bruto de auxiliares no es coste adicional; tarifa de gestión no es automáticamente valoración NIC 2.

## Diseño de integración WPF (implementación inicial en rama de desarrollo; aceptación pendiente)

1. Crear `HelpRegistry` con las entradas estables de `help-map.json` y servicio `IContextHelpProvider.Get(viewId, culture)`. El ID depende de la operación lógica, no del nombre físico del panel. En `MainWindow.Guidance.cs` mantener la selección activa para los pasos `0,1,2,4,5,7,8,9,10,6`; la importación `3` tiene contenido propio en su ventana. Para los modales, asociar ID en el controlador de ventana.
2. Empaquetar el contenido MD versionado **con el ejecutable** (p. ej. recursos embebidos o ficheros `Content` con copia controlada) y mostrar su SHA/versión de publicación cuando proceda. En producción no depender de HTTP/GitHub para leer ayuda, ni acceder al repositorio privado desde cliente.
3. Añadir acción contextual `Ayuda de esta pantalla` accesible por ratón y teclado junto a cada vista y ventana; mostrar panel WPF con lectura por secciones, tablas legibles, texto seleccionable y cierre que devuelva foco. No sustituir `FieldHelp` ni saturar el formulario con manuales. El título y el ID activo cambian con la navegación, incluidas ventanas integradas.
4. Renderizar Markdown mediante parser controlado y componentes WPF (p. ej. `FlowDocument`, tablas y enlaces permitidos), sin HTML/script arbitrario ni ejecución de código desde archivos. Mantener scroll, lectores de pantalla, ampliación, alto contraste y estilo VALDRIO.
5. Internacionalización: estos primeros contenidos son ES; antes de aceptación bilingüe añadir `docs/help/en/` sincronizada y cargar por `UiLanguageTag`, con fallback indicado. La existencia de cadenas UI en inglés no supone que estas ayudas estén traducidas.

**Estado actualizado de la fase de implementación:** el PR de integración WPF incorpora lector offline de Markdown, acceso mediante barra superior/F1, mapeo de vistas lógicas y 16 traducciones inglesas. Se preservan los tooltip y la microayuda existente. **Aún no hay compilación, ejecución de pruebas ni validación visual real en Windows sobre ese PR**: los criterios de aceptación final siguen abiertos hasta recibir evidencia local.

## Trazabilidad y cambios

- `help-map.json` asocia IDs a archivo, XAML, ViewModels y disparador. `schema-help.json` valida el manifiesto; `scripts/verify_help.py` comprueba unicidad, rutas, apartados e inventario XAML. Con `--base` también exige modificación del MD si cambia una fuente UI/VM mapeada en el diff.
- En cambios de cálculo puro `Application`, `Calculation` o `Infrastructure`, el autor debe revisar manualmente todas las vistas consumidoras y actualizar ayuda y pruebas en el mismo PR; el verificador no puede inferir semántica económica desde código C#.
- No editar directamente archivos publicados ni ejemplos `.valdrio` para documentar. Registrar evidencia ES/EN, caso positivo/negativo, accesibilidad y reapertura sobre el mismo SHA final.
- Mantener `AGENTS.md`, `docs/directrices/USABILIDAD.md` y sistema visual T08; no introducir cambios de arquitectura económica ni rediseñar la UI como efecto secundario.

## Definition of Done (PR con UI o cálculos)

- [ ] Vista registrada con ID único y MD actualizado; contenido explica **todos** los campos afectados, unidades, datos requeridos, validaciones, outputs y estado.
- [ ] Verificador ejecutado localmente y `--base` sin errores; nuevas vistas XAML no quedan huérfanas.
- [ ] Ayuda renderizada en ventana/panel correspondiente y accesible por ratón, teclado y tecnología asistiva.
- [ ] Pruebas manuales WPF, ES/EN y escenarios positivos/negativos sobre el SHA del PR, con evidencia fechada.
- [ ] Tests de no regresión de cálculos y persistencia si hay cambios funcionales.
- [ ] GitHub Actions **no ejecutado**, salvo autorización expresa. Priorizar Windows local o self-hosted sin coste.

## Límites de la auditoría documental

Los contenidos fueron extraídos del XAML/VM actuales, `docs/tareas/T14.md`…`T17.md` y la línea base histórica de 15/10/2026 contrastada con el HEAD de 20/10/2026. La lectura del código acredita existencia, no aceptación interactiva universal. La cobertura de pantallas en `main` no incluye componentes creados únicamente en ramas sin fusionar.

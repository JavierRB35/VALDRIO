# Arquitectura de ayuda contextual — VALDRIO

**Base de la revisión:** `main@2599188` (28/10/2026). **Contrato revisado:** 28/10/2026. Véase el [informe y evidencias](evidencias/ayudas-contextuales-20261028/README.md).

## Objetivo y alcance

Sistema de ayuda *por vista lógica*, no por clase técnica. La aplicación es WPF/.NET 10; no tiene router web. Sus vistas viven en `MainWindow.xaml`, paneles `UserControl` y ventanas modales. `AllocationWorkspacePanel.xaml` participa tanto en el primario como en el secundario: **dos IDs de ayuda diferentes**. El sistema existente `FieldHelp.cs` ya usa tooltip, foco de teclado y `AutomationProperties.HelpText` para microayuda localizada; debe conservarse.

Se incluyen 17 vistas lógicas en `docs/help/help-map.json`: proyecto, estructura, estudio, importación contable, recursos, primario, secundario, flujos, actividad T14, asistente de normal, importación física, auxiliares T16, tarifas T15, composiciones T17, órdenes estimadas T18, reutilización de metodología y preferencias. Los diálogos de confirmación integrados reutilizan la ayuda de la operación madre, con mensaje y consecuencias explicados de forma específica.

## Contrato de contenido

Cada `.md` contiene exactamente un `ID Vista UI` único, fecha y cuatro secciones obligatorias:

1. Objetivo y función financiera y decisión posible.
2. Tabla de entradas: campo, tipo/unidad, obligatoriedad y efecto económico.
3. Tabla de salidas: origen/fórmula y lectura para la decisión.
4. Casos de uso, validaciones y errores frecuentes con causa y acción.

La ayuda se redacta sobre controles realmente presentes en XAML y su lógica verificada en ViewModel/servicio. No convertir planes R1–R4 en capacidades actuales. *Pendiente* no es `0`; borrador no es publicado; volumen bruto de auxiliares no es coste adicional; tarifa de gestión no es automáticamente valoración NIC 2.

## Integración WPF que está en `main`

- `ContextHelpCatalog` carga `help-map.json` y resuelve el artículo por ID lógico y cultura. `ContextHelpMarkdown` transforma el subconjunto Markdown utilizado por las fichas a contenido WPF local; el lector no ejecuta HTML/script ni obtiene texto desde Internet.
- La acción superior **Ayuda (F1)** abre una ventana de lectura no modal. El artículo cambia al navegar entre pantallas y si cambia el idioma mientras la ayuda está abierta. El panel compartido de reparto puede mostrar guías lógicas distintas para primario y secundario; asistentes y modales se resuelven por su propia ventana.
- Los 17 artículos ES y sus 17 traducciones EN, junto con `help-map.json`, se copian como contenido `help/` junto al ejecutable. Si una traducción no está disponible, el lector identifica el fallback al español; el verificador local exige paridad completa. Las pruebas de renderizado recorren ambos idiomas y comprueban tablas, recorrido final y ocultación de metadatos técnicos.
- `FieldHelp` conserva la microayuda de campos: se muestra cuando un control recibe foco de teclado o puntero y ofrece el detalle asociado cuando existe. **Tab no activa la ayuda**: mantiene el recorrido normal entre controles.
- La guía completa complementa, no sustituye, tooltips, `AutomationProperties.HelpText` ni mensajes de validación.

La integración del lector y el contenido está en el código actual. La inspección estática y las pruebas de catálogo no prueban por sí solas accesibilidad de lector de pantalla, enfoque visual en todos los diálogos ni legibilidad a cada DPI. Esos resultados deben registrarse sobre el SHA del binario probado.

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

El contenido y el mapeo se actualizaron contra el código de `main@6885eca`, incluidas las guías de composición y costes T17/T18. La lectura del código acredita qué controles y reglas están implementados, no la aceptación interactiva universal. La cobertura de pantallas en `main` no incluye componentes creados únicamente en ramas sin fusionar; una captura o ejecución anterior tampoco certifica un SHA posterior.

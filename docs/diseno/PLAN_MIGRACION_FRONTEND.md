# VALDRIO — Plan de migración visual integral (V1)

**Fecha:** 2026-10-08. **Estado:** preparación de rediseño / **NO** inicio de migración funcional.
**Fuente estética obligatoria:** [Sistema visual VALDRIO V1 y referencia T08 aprobada](SISTEMA_VISUAL_VALDRIO_V1.md).
**Principio:** adaptar **toda** la aplicación WPF a esa estética sin destruir las funciones, flujos guiados ni evidencias existentes.

## 1. Diagnóstico real del frontend (GitHub, 2026-10-08)

### Precaución sobre ramas

- `main` no refleja toda la interfaz de trabajo reciente.
- [PR #22](https://github.com/JavierRB35/VALDRIO/pull/22) (`codex/local-tests-and-workspace`, T06/T06A) permanece en borrador.
- [PR #27](https://github.com/JavierRB35/VALDRIO/pull/27) (`codex/usability-six-issues`, T07 usabilidad) también está en borrador y **tiene como base la rama de PR #22**, no `main`.
- La versión funcional inspeccionada para planificar es PR #27, commit `9a70979dbf83cd98b76dbe23c2cce812718eb03f` (no afirmar que siga siendo HEAD al ejecutar).
- **No rediseñar `main` antiguo ni modificar directamente ramas PR #22/#27**. Antes de tocar XAML, confirmar la rama/commit base con el propietario, verificar merges/rebases en curso y abrir rama visual independiente. Si el trabajo funcional sigue evolucionando, posponer conflictos y revalidar inventario.
- **Atención a la convergencia de documentación:** `AGENTS.md` en PR #22/#27 difiere del `AGENTS.md` actualizado en `main` con la nueva estética. Integrar **ambas** directrices (usabilidad y diseño aprobado) antes de fusionar los trabajos; no sobrescribir ninguna. Lo mismo vale para ajustes posteriores de `README.md`.

### Pantallas actuales inspeccionadas en PR #27

| Vista | Archivo WPF | Flujo/elementos funcionales a conservar |
|---|---|---|
| Ventana principal | `src/Valdrio.Desktop/MainWindow.xaml` | Paso Empresa → Estructura/Modelo → Estudio → Importación → Recursos → Preferencias; navegación anterior/siguiente, requisitos, ayuda, estados y destinos de guardado |
| Archivo de empresa | `CompanyFilePanel.xaml` | Crear/abrir/cerrar, backup/restauración/upgrade, nombre/moneda y unidades |
| Estructura/modelo | `ModelWorkspacePanel.xaml` | Pasos internos/tabulador, centros, unidades y nodos; crear, editar, validar, guardar, publicar y clonar |
| Estudios | `StudyWorkspacePanel.xaml` | Seleccionar/crear/guardar/cerrar estudio; modelo publicado, periodo/moneda/escenario; capturar snapshots e historial |
| Importación | `ImportColumnMappingWindow.xaml` | Asistente de cinco pasos, archivo, formato/columnas, selección de filas, significado y FX, revisión/confirmación/guardado y retroceso conservado |
| Recursos/T07 | `ResourceWorkspacePanel.xaml` | Tabla de cuentas/recursos, corrección editable, clasificación/destinos, partes, revaloración, diagnósticos, guardado y cancelación |
| Infraestructura visual | `App.xaml` | `Application.Resources` está vacío en la versión revisada, de modo que no existe todavía un tema visual global centralizado |

El proyecto utiliza **WPF/XAML**, no frontend HTML. El aspecto de la imagen es el objetivo de interacción/jerarquía, pero no se debe copiar una pantalla web ni convertir todos los flujos en la misma matriz de tres columnas.

## 2. Secuencia obligatoria de implementación

### Fase V0 — Congelar la línea base antes de editar

- Elegir rama funcional autorizada por el propietario y registrar SHA.
- Inventariar por pantalla **nombres `x:Name`, eventos, comandos, bindings, visibilidad, estados, pestañas, scroll, navegación, ayudas, validaciones, errores, diálogos y accesibilidad**.
- Conservar capturas de uso reales de estados habituales/errores y ES/EN en Windows. Si no hay acceso a Windows, documentar expresamente que todavía no está verificado.
- No abrir una PR de rediseño contra `main` desactualizado.

### Fase V1 — Tema único y shell común (primera entrega de código)

Crear un diccionario de recursos WPF (por ejemplo `src/Valdrio.Desktop/Styles/ValdrioTheme.xaml`) que use los tokens de la guía; registrarlo desde `App.xaml`. Añadir estilos y recursos compartidos para:

- tipografía, paleta, tamaños de espaciado, fondos, borde, foco;
- botones primarios/secundarios/peligro y sus estados;
- controles de entrada, ComboBox, DataGrid, TabControl, GroupBox/tarjetas, mensajes y badges;
- navegación lateral, cabecera/contexto y ayudas reutilizables.

Adaptar `MainWindow.xaml` **primero**: sidebar profundo con acento amarillo, topbar de contexto, cabecera de módulo y región central de contenido, manteniendo los seis pasos guiados y accesos existentes. Si se reubican los botones y paneles, conservar sus `x:Name` y handlers/eventos; la navegación debe continuar funcionando. No ocultar el botón de siguiente/anterior, bloqueos o preferencias; pueden trasladarse visualmente. El shell debe permitir scroll y anchos limitados.

**Puerta V1:** compilación Windows local, recorridos básicos, comparación visual con referencia, ES/EN, interacción teclado/DPI. Si falla, no migrar paneles.

### Fase V2 — Archivo de empresa y configuración de estructura

Adaptar `CompanyFilePanel.xaml` y `ModelWorkspacePanel.xaml` a tarjetas blancas, formularios compactos, criterios tipográficos y mensajes. Respetar los pasos del editor de estructura, incluyendo crear/guardar/publicar, visibilidad de centros y nodos, selector de unidades y acciones avanzadas.

**Puerta V2:** crear/reabrir empresa, añadir centro/unidad/nodo, validar/publicar/clonar sin pérdida funcional.

### Fase V3 — Estudios

Adaptar `StudyWorkspacePanel.xaml` conservando edición explícita de estudio, revisión, periodo, moneda, escenario, modelo publicado, captura y consulta histórica. Utilizar resumen KPI solo si es un dato real y contextual, no valores sintéticos.

**Puerta V3:** crear, guardar, cerrar, reabrir, consultar fotos y ver mensajes coherentes de preparación pendiente.

### Fase V4 — Importación y asistente

Aplicar el sistema visual a `ImportColumnMappingWindow.xaml`, sin alterar los **cinco pasos** ni la retención de formularios. Separar claramente seleccionar archivo, mapear, seleccionar registros, confirmar interpretación/moneda y revisar/guardar. No diseñar una pantalla que obligue a descubrir requisitos al final ni hacer que un botón Guardar signifique «preparar».

**Puerta V4:** importación completa, atrás/adelante, exclusión de cuentas, Debe/Haber, escenarios de FX y errores; probar datos guardados/reapertura.

### Fase V5 — Recursos y preparación T07

Reestilizar `ResourceWorkspacePanel.xaml` con tabla clara, edición de correcciones directamente en la celda, desglose de partes y estados visibles. Preservar los cambios funcionales de PR #27, clasificación/destinos, revaloración y confirmaciones.

**Puerta V5:** corrección con delta, repetición sin duplicación, Escape cancela, clasificación y guardado/revisión sin pérdida de datos.

### Fase V6 — Completitud

Revisar todos los diálogos, estados vacíos/errores/carga, tooltips, mensajes de seguridad, preferencias, scroll, ayudas, traducciones, capturas y accesibilidad. Inventariar archivos `.xaml` nuevos surgidos desde PR #27. Preparar pruebas de regresión funcional + visual basadas en WPF real, no maqueta.

## 3. Reglas de diseño al tocar código

1. **Un único tema y shell.** Evitar estilos locales, colores codificados en `#hex` o variaciones inconsistentes por pantalla.
2. **Fidelidad estética a la imagen aprobada.** Sidebar verde profundo; selección verde medio y línea amarilla; fondo neutro claro; tarjetas blancas; tipografía oscura; acciones verde profundo; badges y tablas suaves. Consultar los tokens exactos/operativos del [documento normativo](SISTEMA_VISUAL_VALDRIO_V1.md).
3. **Nunca perder funcionalidad.** No borrar botones, datos, columnas, validaciones o `x:Name` para «limpiar». Revisar los smoke tests que acceden a esos controles.
4. **Usabilidad ya aprobada.** Aplicar simultáneamente [USABILIDAD.md](../directrices/USABILIDAD.md) de la rama funcional. El contenido contextual que parece técnico debe adaptarse a lenguaje de negocio, sin ocultar diagnósticos necesarios.
5. **Internacionalización.** ES/EN, moneda y precisión numérica, fechas y zona son independientes. Evitar textos nuevos literales sin recursos de localización.
6. **Accesibilidad.** Foco visible, teclado, contraste, DPI, lectores de pantalla, textos largos, errores anunciados.
7. **Fuera de alcance.** En este rediseño no implementar lógica T08 no existente, modificar cálculos ni alterar metodología/circuitos persistentes sin otra aprobación.
8. **Verificación.** PRs pequeños por fase; capturas antes/después; lista de controles preservados y resultados de pruebas locales por SHA. No declarar una fase terminada sin comprobación WPF.

## 4. Política de coste innegociable

**0 USD sin autorización de gasto.** GitHub Actions en `main` presenta/presentaba disparadores automáticos por código, mientras la rama PR #27 consultada contiene `workflow_dispatch` exclusivamente. Antes de cada push/PR inspeccionar la configuración **efectiva de la rama y evento**, no presuponer que es gratis. Usar validación local Windows, preferentemente self-hosted cuando corresponda, y no activar `windows-latest` ni workflows por cuenta propia. Nunca cambiar facturación.

## 5. Pendiente de decisión del propietario para iniciar V1

Confirmar la **rama/commit funcional base** sobre el que desarrollar el nuevo shell (preferencia técnica: la versión más reciente de las PR funcionales cuando estén consolidadas) y el momento en que se puede abrir una rama de cambio visual. La aprobación estética del 08/10/2026 **no** equivale a autorización para alterar una rama funcional activa.

---

**Criterio global de aceptación:** misma app funcional y mismas capacidades, pero estética visual homogénea a la referencia T08 en **todas** las pantallas reales.

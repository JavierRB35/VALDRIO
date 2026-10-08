# AGENTS.md — instrucciones de trabajo para VALDRIO

Estas instrucciones son obligatorias para cualquier agente, desarrollador o revisión automatizada que modifique este repositorio.

## 1. Trabajar sobre el estado real de desarrollo

- Antes de editar, comprobar repositorio, rama activa, HEAD, estado del árbol y trabajo paralelo existente.
- No asumir que `main` es la rama de trabajo vigente.
- Para cambios visuales o funcionales, crear la rama de trabajo desde la rama funcional que contenga el estado real más reciente autorizado por el propietario.
- No sobrescribir, descartar, rebasar ni forzar trabajo ajeno sin autorización explícita.
- Si existe una rutina o desarrollo en curso, no introducir un rediseño visual en esa rama hasta que el propietario indique que es el momento.

## 2. Regla de preservación funcional de la interfaz

Un rediseño de VALDRIO es un cambio de presentación, no una oportunidad para simplificar el producto.

Antes de modificar una pantalla existente:

1. Inventariar la funcionalidad real de la versión actual: controles, comandos, bindings, navegación, validaciones, estados, diálogos, tablas/columnas, atajos, accesibilidad, importación, publicación, revisiones y persistencia.
2. Tomar ese inventario como línea base de regresión.
3. Mantener funcionalidad 1:1 salvo que el propietario autorice expresamente un cambio funcional.
4. No eliminar, ocultar, fusionar ni sustituir una capacidad porque no encaje visualmente. Adaptar el diseño para alojarla.
5. Conservar estados habilitado/deshabilitado, permisos de edición, confirmaciones, cancelación, diagnósticos, trazabilidad, internacionalización y navegación por teclado.
6. Verificar el resultado contra la aplicación real, no únicamente contra especificaciones, capturas o maquetas.

## 3. Principio de interfaz orientada al usuario

La interfaz de VALDRIO debe ser simple, entendible e intuitiva para una persona usuaria del producto.

- La interfaz debe mostrar únicamente la información, conceptos y acciones que resulten útiles para configurar, implementar, comprender y aplicar la metodología de un modelo de cálculo de costes.
- No debe exponer la arquitectura interna de la aplicación, sus capas técnicas, estructuras de persistencia, nombres de implementación, identificadores internos ni estados técnicos salvo que esa información sea necesaria para una decisión del usuario o para comprender correctamente la metodología.
- Los detalles técnicos necesarios para soporte, diagnóstico, auditoría o desarrollo deben quedar fuera del flujo normal de negocio y presentarse en superficies específicas cuando proceda.
- Los nombres, textos, navegación y jerarquía visual deben hablar en términos del dominio y del trabajo del usuario, no en términos de la implementación.
- Si existe conflicto entre reflejar literalmente la arquitectura interna y ofrecer un flujo comprensible, debe prevalecer el modelo mental del usuario siempre que se preserve la funcionalidad, la trazabilidad y la corrección metodológica.

## 4. Dirección estética aprobada (obligatoria desde 2026-10-08)

**Fuente normativa de cualquier modificación de frontend WPF/XAML/XML:**
[docs/diseno/SISTEMA_VISUAL_VALDRIO_V1.md](docs/diseno/SISTEMA_VISUAL_VALDRIO_V1.md), incluida la **imagen de T08 aprobada por el propietario** que contiene.

- El objetivo de **toda** VALDRIO es la referencia T08: **sidebar verde profundo, superficies blancas, fondo neutro claro, verde de marca, acento amarillo, tarjetas, jerarquía y tablas limpias**. No sustituirlo por propuestas genéricas de interfaz azul o pastel.
- La guía de diseño gobierna shell, paleta, composición, botones, inputs, tablas, feedback y espaciado. Se adapta a WPF y a cada tarea **sin copiar datos ni funciones ficticias de T08**.
- **Antes de editar cualquier XAML/XML frontend**: leer esta guía, inventariar la pantalla real y conservar funcionalidad 1:1, accesibilidad, i18n y uso con teclado. Usar recursos/estilos globales en vez de colores locales.
- Antes de dar por válida una pantalla rediseñada: comparar visualmente con la referencia y probar los flujos reales. Ningún agente puede alterar el lenguaje visual por gusto propio sin autorización del propietario.
- Esta aprobación **no autoriza por sí sola ejecutar una migración de pantallas**; esa ejecución se planificará por separado.

La prioridad sigue siendo: **funcionalidad real existente → claridad del flujo → conformidad con el sistema visual → acabado estético**. Nunca se elimina funcionalidad para imitar la maqueta.

## 5. Validación de un cambio visual

Un cambio visual no se considera terminado solo porque el XAML compile o se vea mejor.

Como mínimo debe comprobarse:

- que los bindings, comandos y eventos existentes siguen presentes;
- que no desaparecen controles o acciones sin decisión aprobada;
- que la navegación por teclado y los estados de edición siguen funcionando;
- que español/inglés, formatos regionales y textos largos siguen siendo utilizables;
- que la pantalla se prueba contra el ViewModel y servicios reales;
- que cualquier diferencia funcional se documenta y requiere aprobación del propietario.

## 6. GitHub Actions y coste

Objetivo habitual: 0 USD de GitHub Actions.

- No ejecutar GitHub-hosted runners ni workflows potencialmente facturables sin autorización explícita del propietario.
- Priorizar compilación/pruebas locales y runners Windows self-hosted.
- Los cambios exclusivamente documentales o visuales que puedan validarse localmente no justifican por sí mismos consumo de Actions.

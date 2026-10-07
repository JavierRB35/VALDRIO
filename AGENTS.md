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

## 3. Dirección estética aprobada

La estética futura debe seguir la línea de la referencia visual aprobada por el propietario en el proyecto/conversación de VALDRIO: interfaz limpia, clara, profesional y didáctica, con jerarquía visual fuerte, superficies luminosas, bloques bien separados, uso moderado de azules y tonos suaves/pastel para distinguir áreas o estados, acciones principales visibles, espaciado consistente y composición ordenada.

La referencia visual es una dirección estética, no una maqueta literal. VALDRIO es una aplicación WPF de escritorio y el diseño debe adaptarse a ese contexto:

- priorizar productividad, legibilidad y densidad adecuada para escritorio;
- usar navegación, paneles maestro/detalle, formularios, tablas y diálogos apropiados para WPF;
- reservar el color para jerarquía, estado y orientación, evitando decoración innecesaria;
- mantener tablas y formularios cómodos para trabajo prolongado;
- conservar accesibilidad, foco, teclado, escalado y textos largos/pseudotraducción;
- no convertir la aplicación en una interfaz web ni copiar literalmente una infografía;
- mantener coherencia visual entre Empresa, Modelo, Estudios, Importación y las pantallas futuras.

La prioridad es:

**funcionalidad real existente → claridad del flujo → consistencia visual → acabado estético.**

Nunca al revés.

## 4. Validación de un cambio visual

Un cambio visual no se considera terminado solo porque el XAML compile o se vea mejor.

Como mínimo debe comprobarse:

- que los bindings, comandos y eventos existentes siguen presentes;
- que no desaparecen controles o acciones sin decisión aprobada;
- que la navegación por teclado y los estados de edición siguen funcionando;
- que español/inglés, formatos regionales y textos largos siguen siendo utilizables;
- que la pantalla se prueba contra el ViewModel y servicios reales;
- que cualquier diferencia funcional se documenta y requiere aprobación del propietario.

## 5. GitHub Actions y coste

Objetivo habitual: 0 USD de GitHub Actions.

- No ejecutar GitHub-hosted runners ni workflows potencialmente facturables sin autorización explícita del propietario.
- Priorizar compilación/pruebas locales y runners Windows self-hosted.
- Los cambios exclusivamente documentales o visuales que puedan validarse localmente no justifican por sí mismos consumo de Actions.

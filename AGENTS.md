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

## 7. Requisito transversal obligatorio T08: identificación de cuentas

Antes de trabajar en importación, recursos T07 o cuentas/reglas T08, en cualquier rama, consultar la directriz vigente de main [T08-CUENTA-01](docs/directrices/T08_CUENTAS_DESCRIPCION.md) y el [issue #28](https://github.com/JavierRB35/VALDRIO/issues/28).

- La importación debe permitir capturar nombre/descripción de la cuenta; T08 debe mostrar **código + descripción** para que el analista la identifique fácilmente. Es requisito obligatorio de aceptación, no una capacidad declarada implementada.
- Antes de decidir cambios, verificar por SHA el flujo real T06/T06A → selección de columna → preparación/confirmación → persistencia y reapertura → exposición en T07 → consumo en T08. Reutilizar lo comprobado y registrar las brechas como dependencias de importación previa/T07.
- No afirmar cumplimiento por existir `ImportColumnMap.Description`, conservar filas originales, una maqueta o una descripción de PR. Exigir evidencia del recorrido completo y de la UI real.
- Conservar identidad textual del código, ceros iniciales y procedencia. Resolver explícitamente ausencia de descripción y conflictos del mismo código, sin inventar nombres ni alterar agrupación/reglas económicas por descripción.
- Dar visibilidad al requisito en desarrollos paralelos mediante issue y referencias en PR relevantes. No sobrescribir, rebasar ni forzar ramas activas; consultar main aunque la rama propia sea anterior a esta directriz.
- Esta incorporación es documental/de seguimiento. No implementa ni autoriza ahora lógica/UI y no debe provocar consumo facturable de Actions. Los criterios, dependencias y evidencia preliminar están en la directriz enlazada.


## 8. Directrices funcionales y validación local conservadas

# Directrices permanentes de VALDRIO

Antes de modificar la interfaz, leer `docs/directrices/USABILIDAD.md`.
La complejidad metodológica nunca justifica complejidad de uso. Priorizar recorridos
guiados, requisitos comprensibles, ayuda contextual y guardado inequívoco, conservando
todas las funciones. El propietario autorizó completar T07 el 2026-10-07 en la rama existente. Mantener
estos requisitos de usabilidad en T07 y las fases siguientes.

## Validación local y costes de CI — todo el proyecto y todas las fases

Decisión permanente del propietario, 2026-10-07. Se aplica a todas las fases
presentes y futuras, tareas, ramas y entregas de VALDRIO; no está limitada a T07.

El agente ejecuta localmente la compilación y las comprobaciones pertinentes como
parte del trabajo autorizado, resuelve los fallos y registra los resultados reales,
sin esperar a que el propietario lance comandos o dé una confirmación adicional.

GitHub Actions se ejecuta únicamente por petición expresa del propietario. Los
workflows de compilación y validación no deben activarse automáticamente por push,
pull request, calendario, otros workflows ni eventos equivalentes. No reactivar
estos disparadores ni añadir otros que generen compilaciones remotas automáticas
sin una nueva instrucción expresa del propietario.

## Requisito obligatorio de cuentas para T08

Antes de modificar importación, recursos o reglas, leer [T08-CUENTA-01](docs/directrices/T08_CUENTAS_DESCRIPCION.md). El propietario autoriza el 2026-10-08 completar T08 y sus dependencias, conservando código textual, ceros iniciales, descripciones y procedencia. No basta con preservar celdas originales: comprobar selección, interpretación, reapertura y consumo real.


## Entrega T08 para comprobaciones en workspace — 2026-10-08

La autorización posterior permite completar y fusionar T08 en
`codex/local-tests-and-workspace`, verificando allí localmente. No promocionar a
main sin autorización expresa posterior del propietario. El requisito de nombres
se implementa y valida con evidencia; mantener la directriz transversal, procedencia
y lectura de AGENTS de main. Las limitaciones documentales iniciales no revocan
esta autorización de implementación. Ver `docs/tareas/T08.md` y evidencia de cierre.


## Base consolidada autorizada — 2026-10-08

El propietario autoriza ahora promocionar T08 y workspace a main tras revisar las
ramas. Esta autorización sustituye la espera de promoción indicada en la entrega
anterior. Continuar las nuevas tareas desde el main consolidado; conservar siempre
validación local, diseño, requisitos y política de workflows exclusivamente manuales.

## Edición en tablas — preferencia permanente del propietario

Todo lo que pueda editarse en las tablas se hará en las tablas, sin duplicarlo en
campos o formularios adicionales. Priorizar simplicidad. Ver la decisión de
2026-10-09 en `docs/directrices/USABILIDAD.md`.

# PEND-UI-07 — Desplazamiento suave en repartos primario y secundario

Fecha: 2026-10-15. Estado: pendiente de reproducción e implementación.

## Problema comunicado y requisito

El propietario observa saltos al desplazar la pantalla principal de los repartos primario y secundario, con pérdida de ubicación. El desplazamiento de la ventana principal debe ser suave, continuo y predecible. La causa técnica todavía no está comprobada.

## Criterios de aceptación

- Comprobar y corregir ambas pantallas, con rueda, panel táctil y barra de desplazamiento.
- Evitar saltos inesperados a otras secciones, al inicio o a la fila seleccionada.
- Al atravesar tablas y controles con desplazamiento propio, evitar transferencias bruscas y movimientos simultáneos inesperados, conservando el acceso a su contenido.
- El foco, la selección y las actualizaciones no provocan reposicionamientos inesperados durante el desplazamiento.
- Conservar edición, validaciones, navegación por teclado y datos introducidos.
- Verificar distintos volúmenes de filas, alturas de ventana y escalados de Windows de 100 %, 125 % y 150 %.

## Validación pendiente

Reproducir en la aplicación Windows real e incluir evidencia antes/después en ambos repartos. No se ha implementado ni verificado la corrección. Validación local, sin ejecutar GitHub Actions.

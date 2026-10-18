# PEND-UI-02 — Mensajes de error contextuales en rojo

Fecha: 2026-10-15. Estado: pendiente de implementación.

## Requisito del propietario

Cuando la aplicación muestre un mensaje contextual de error de validación o de campo obligatorio sin rellenar, en la propia pantalla o cerca del campo erróneo, ese mensaje debe destacarse en rojo.

El propietario aclara que este requisito se aplica exclusivamente a los mensajes contextuales. Se conservan los modales existentes y sus condiciones de aparición; esta tarea no exige eliminarlos, sustituirlos por avisos contextuales ni modificar su presentación.

El requisito es transversal a las pantallas de VALDRIO, incluido el caso de creación de estructura recogido en [PEND-UI-01](PEND-UI-01_CREAR_ESTRUCTURA_VALIDACION.md).

## Criterios de aceptación

- Los mensajes contextuales asociados a un campo incorrecto o a un campo obligatorio vacío muestran su texto en rojo en su ubicación contextual.
- Los mensajes contextuales de error que afectan a una operación o a varios campos muestran su texto en rojo en la pantalla.
- Los modales existentes mantienen su comportamiento y presentación. No se cambia un modal por un mensaje contextual como consecuencia de este requisito.
- El mensaje explica qué debe corregirse; el color no es la única señal de error. Se conserva la identificación textual y la accesibilidad para lectores de pantalla.
- El rojo tiene contraste suficiente con el fondo y se aplica mediante recursos compartidos del sistema visual.
- El aviso se actualiza o desaparece cuando la validación confirma que el problema se ha corregido, sin conservar errores obsoletos.
- Los mensajes informativos o de éxito no se presentan como errores rojos.
- Se comprueba el comportamiento en español e inglés y con navegación por teclado, sin modificar las reglas de validación ni perder datos introducidos.

## Validación pendiente

Inventariar los mensajes contextuales existentes, aplicarles el tratamiento común y comprobar localmente los casos de campo vacío, valor incorrecto, error de operación y recuperación, conservando los modales existentes. No se ha implementado este cambio visual en esta tarea documental. No ejecutar GitHub Actions.

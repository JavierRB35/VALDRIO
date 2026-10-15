# PEND-UI-01 — Crear estructura: validación y continuidad de la aplicación

Fecha: 2026-10-15. Estado: pendiente de reproducción fuera del depurador.

## Evidencia y alcance

El propietario observa `MODEL_STRUCTURE_INVALID` al crear una estructura en Visual Studio. La captura muestra una interrupción del depurador; no demuestra un cierre de la aplicación compilada.

El código revisado captura `DomainRuleException` en `ModelWorkspaceViewModel.Run` y presenta su diagnóstico. Sin embargo, `NewDraftCommand` permite iniciar la creación con la descripción vacía, que `ModelStructure` rechaza. La descripción vacía es una hipótesis del caso observado, no un valor confirmado por la captura.

## Trabajo pendiente

- Reproducir con descripción vacía, espacios y descripción válida en la compilación Release ejecutada directamente, sin Visual Studio, usando una empresa de prueba.
- Verificar que el rechazo muestra un aviso comprensible, mantiene abierta y utilizable la aplicación y no guarda una estructura parcial.
- Si se reproduce un cierre, registrar la excepción completa y corregir su propagación. Tratarlo entonces como fallo de estabilidad confirmado.
- Validar la descripción antes de iniciar la operación y orientar al usuario con un mensaje específico, conservando la validación de dominio.

## Criterios de aceptación

1. Descripción vacía o con espacios: aviso comprensible; si se presenta como mensaje contextual, debe destacarse en rojo conforme a [PEND-UI-02](PEND-UI-02_ERRORES_CONTEXTUALES_EN_ROJO.md). Se conservan los modales existentes; sin cierre, bloqueo ni escritura parcial.
2. Tras corregir la descripción, se puede crear la estructura sin reiniciar; persiste y se recupera al reabrir.
3. Avisos en español e inglés y recorrido accesible mediante teclado.
4. Evidencia local de la reproducción y del resultado fuera del depurador; prueba de regresión del rechazo y recuperación.

No se ha ejecutado todavía esta reproducción específica. La pausa en Visual Studio no se registra como cierre confirmado. Validación local, sin ejecutar GitHub Actions.

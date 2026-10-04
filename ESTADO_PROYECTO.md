# ESTADO_PROYECTO

Fecha de revisión: 2026-10-04  
Repositorio: `JavierRB35/VALDRIO`  
Rama: `main`

## Estado observado

- El repositorio existía antes de esta sesión.
- Estado inicial comprobado: un único commit (`3244d14ecbdb77a192209654ef9cf13eceb10110`) con solo `README.md`.
- No había `ESTADO_PROYECTO`, ADR, fichas de tareas, casos, contratos ni código de aplicación.
- No había issues abiertas.

## Tarea activa

**T00 - Acuerdo de finalidad y políticas.**

Subentrega ejecutada: **T00.1 - línea base de proyecto, arquitectura técnica y decisiones pendientes**.

Estado de T00: **EN CURSO**.  
Estado de T00.1: **COMPLETADA**.

T00 no se declara cerrada porque el prompt maestro referencia un paquete de desarrollo con fichas T00-T33, contratos CM01-CM16, decisiones, casos JSON, verificador y plantillas que no están entre los seis archivos fuente disponibles en esta sesión. Sin esos originales no se puede comprobar la ficha T00 completa ni ejecutar el verificador de referencias.

## Línea base de alcance

- Nombre operativo del producto: **VALDRIO**.
- La comprobación registral/dominios de la marca sigue pendiente; no se interpreta el nombre operativo como registro concedido ni dominio adquirido.
- R0 es un demostrador verificable. R1 es la primera edición profesional prevista.
- Primera plataforma propuesta/ordenada por el prompt: Windows x64, escritorio local.
- Internacionalización es transversal desde los fundamentos: idioma, región, moneda funcional/original y zona horaria son ámbitos independientes.
- Las reglas económicas propuestas no se consideran aceptadas por el propietario hasta que se revisen sus ejemplos.
- Precio, presupuesto de marketing y retorno publicitario permanecen pendientes; no se inventan.

## Entorno de ejecución comprobado en esta sesión

- Host disponible: Linux x86_64.
- Git: disponible (`2.47.3`).
- Python: disponible (`3.13.5`).
- `dotnet`: no disponible en el host.
- PowerShell: no disponible en el host.
- Por tanto, **no se ha compilado WPF ni se ha realizado revisión visual de WPF**.
- T01 deberá fijar SDK/paquetes tras comprobar documentación oficial y ejecutar build/pruebas en Windows CI autorizado.

## Evidencia de esta subentrega

- ADR técnico inicial: `docs/adr/ADR-0001-arquitectura-base.md`.
- Registro de decisiones: `docs/decisiones/DECISIONES_PENDIENTES.md`.
- Inventario y huellas de fuentes disponibles: `docs/fuentes/FUENTES_DISPONIBLES.md`.
- Continuidad de T00: `docs/tareas/T00.md`.

## Siguiente paso

1. Incorporar o facilitar el paquete de desarrollo v0.4 completo referenciado por el prompt.
2. Reconciliar esta línea base con la ficha T00, CM01-CM16, el diccionario de decisiones y los casos.
3. Ejecutar `casos/verificar_referencias.py` y conservar su salida.
4. Revisar/aceptar las decisiones económicas que correspondan. Hasta entonces siguen marcadas como **PROPUESTA**.
5. Tras cerrar T00, abordar T01 como tarea independiente: repositorio de solución, SDK fijado, CI Windows, build WPF y artefacto de desarrollo verificable.

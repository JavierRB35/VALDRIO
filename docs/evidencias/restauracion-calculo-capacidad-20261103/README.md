# Corrección: recuperar el cálculo y la información de capacidad

Fecha: 03/11/2026. Base: `21e954b` (incluye las validaciones de entrada desarrolladas en paralelo). Rama: `fix/restore-capacity-calculations-20261103`.

La refactorización anterior ocultó el calendario detrás de «Revisar cálculo» y relegó su desglose al final del panel. Además, al introducir una capacidad manual, `UpdateProvenance` borraba la descripción que contenía los parámetros de calendario. Eso podía impedir recuperar días, turnos, horas, recursos y paradas al reabrir.

## Corrección

- El cálculo vuelve al principio del formulario, sin desplegable: método, días, turnos diarios, horas por turno, recursos simultáneos, paradas y su frecuencia, y nivel habitual de actividad.
- La propuesta y el desglose completo permanecen visibles junto a las acciones: capacidad de calendario, paradas totales, capacidad disponible y normal. Se conserva el contexto del coste publicado.
- Las notas opcionales permanecen en su panel secundario. No se duplican cantidades ni se cambia el motor de costes.
- Una modificación manual conserva los parámetros de calendario y sus notas. Al reabrir, se muestra de nuevo el cálculo sin sustituir el valor manual guardado ni la actividad real.
- Si una versión previa perdió los parámetros, se recupera como referencia el último calendario disponible del historial con el mismo análisis, modelo, pool, unidad y período. Esta consulta no escribe ni crea versiones. Las observaciones anuladas no recuperan cantidades ni calendario automáticamente.
- No pueden reconstruirse parámetros que nunca fueron guardados o que no existan en el historial. No se inventan turnos ni horas.

## Pruebas y evidencia

Compilación Release correcta, sin advertencias ni errores. 42 pruebas de T14 y validación de entradas superadas, incluidas nuevas regresiones de ajuste manual, notas, guardado/reapertura y recuperación histórica sin escritura. El control de documentación cubre 17 vistas y 18 XAML.

El recorrido WPF ES/EN comprueba controles visibles sin desplegar, teclado, cálculo en vivo, paradas inválidas, guardado y resumen persistente. Ejemplo verificado: 20 días × 2 turnos × 8 h × 3 recursos, con 0,5 h de paradas por turno/recurso y factor 90 %, produce 810 h. Cambiar las horas por turno a 9 produce 918 h; volver a 8 devuelve 810 h. Se adjuntan capturas y el listado de comprobaciones.

Se conserva la base de validaciones de entrada ajena. Integración prevista mediante avance rápido en `main` local tras comprobar que está limpio. Sin push ni GitHub Actions; gasto de Actions: 0 USD. Las pruebas visuales realizadas no certifican todos los DPI físicos ni lectores de pantalla.

# T15 — tarifas y respuestas por centro

Fecha: 18/10/2026. Base: `main@ce603e1`. Rama: `feat/t15-tarifas-analista`.
Desarrollo y fusión autorizados por el propietario tras comprobar funcionamiento y estética.

## Entrega

Nueva pantalla desde «Repartos» → «Tarifas de centros». Consulta costes T12 vigentes y actividad T14 confirmada; muestra centros, tarifas y siete preguntas/respuestas para el centro seleccionado. Incluye filtro de incidencias, desglose por cuenta y componente, procedencia, acceso a horas/costes y exportación de una versión reproducible.

El cálculo conserva fijo propio, variable, consumido y período. Las diferencias de capacidad son firmadas; cada fuente concilia mediante redondeo equilibrado a precisión del ledger. No se usan tarifas redondeadas de pantalla como entradas. El detalle conserva importes del ledger; el formato respeta los decimales de la moneda y conserva precisión adicional cuando existe.

Versiones: referencia determinista que identifica contexto, ejecución T12, algoritmo y observaciones T14 inmutables. La exportación incluye las respuestas numéricas y sus fuentes; `RestoreVersion` recupera exactamente las versiones históricas y compara el resultado, sin reemplazar horas anteriores por las últimas. No se cambian esquemas SQLite ni snapshots T12.

## Alcance conocido

- La asignación real a productos/órdenes pertenece a T19. Las respuestas actuales dicen que falta esa información; no representan ausencia de registros como cero ni como actividad íntegramente sin asignar.
- La conciliación mostrada es parcial: capacidad + actividad realizada + período. La conciliación por orden queda para T19. El motor incluye aplicación a consumos explícitos y sus controles para esa integración posterior.
- Costes `None` conservan su tratamiento; no se convierten automáticamente en fijo de capacidad propia. Un coste `AssessAtPool` cuyo propietario es otro centro queda bloqueado con explicación: T11 no acredita por sí solo que se haya evaluado capacidad auxiliar. No se adelanta D05/T16.
- Se utiliza el criterio normalizado acordado; no se añaden selectores de políticas ni vistas alternativas. D04 queda registrada para este alcance R1.
- No se ha sustituido una instalación externa. La entrega compilada está en `src/Valdrio.Desktop/bin/Release/net10.0-windows/`.

## Inventario y preservación

Se conserva la navegación T01–T14, las protecciones de cambios pendientes, el selector de análisis, el modo de solo lectura y todas las operaciones de T14. Solo se añade un paso y una ventana. T15 no escribe datos al consultar, cambiar de centro ni filtrar. «Ver actividad y capacidades» abre la ventana existente y actualiza T15 al regresar. «Ver origen de costes» vuelve a Flujos y ejecuciones.

La presentación usa los tokens del sistema visual V1: títulos verdes, fondo neutro, tarjetas blancas, tablas legibles y acciones inferiores. Las respuestas aparecen directamente y la procedencia se despliega a petición.

## Validación efectiva

- Compilación Release de escritorio: **0 errores y 0 advertencias**.
- Regresión completa: **726/726**, sin omitidas (`regression.trx`).
- Validación focalizada tras los ajustes del motor/versiones: **12/12**, incluyendo T15, corpus de redondeo y catálogo de localización (`final-focused.trx`).
- Casos T15: C15/C18 (400 de capacidad distinto de 400 sin asignar), C16 (fijo válido con real cero), C17 (−200 y total 2.200), C19/C31 (diagnósticos de denominador cero), recibidos fijos sin segunda evaluación, período separado, consumos excesivos, ausencia frente a cero y conservación de fuentes firmadas con fracciones.
- Integración SQLite real: costes publicados, dos centros, normal/real confirmadas, corrección de horas, invalidación de ejecución económica obsoleta, reapertura y restauración exacta de una versión anterior.
- Interfaz WPF real: **50 comprobaciones** (`wpf/checks.json`). Menú y foco, estados sin empresa/análisis, dos centros con tarifas diferentes, cambio de selección, siete respuestas, filtro vacío, borrador que invalida tarifa sin ocultar coste conocido, confirmación y recuperación, exportación y navegación de regreso.
- Inspección de capturas ES/EN, 1200 y 760 píxeles. Escalado **simulado mediante LayoutTransform** a 125/150/200 %, con comprobación de desplazamiento hasta las respuestas. No se presenta esta simulación como cambio del DPI del sistema operativo. Acciones accesibles y texto sin solapamiento en las capturas inspeccionadas.

La primera prueba de integración detectó que la empresa de pruebas no contenía la unidad de máquina; se corrigió el montaje de esa empresa sintética sin cambiar esperados económicos. Los resultados finales arriba indicados pasan. La regresión completa precede a los últimos ajustes aislados de restauración y presentación, cubiertos posteriormente por las pruebas focalizadas y WPF finales.

Capturas principales: [español](wpf/rates-es.png), [inglés](wpf/rates-en.png), [ventana estrecha](wpf/rates-narrow-es.png), [respuestas al 200 %](wpf/answers-scale-200-en.png).

## Coste y fuentes

GitHub Actions **no ejecutado**; workflows sin cambios, exclusivamente manuales. Coste de Actions provocado por esta entrega: **0 USD**. No se ha modificado `sources/` ni la especificación canónica.

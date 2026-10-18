# T16 — Evidencia de entrega · 18/10/2026

## Entrega

Capacidad auxiliar por propietario, servicios físicos por destino/componente, evaluación selectiva explícita, variaciones firmadas y reciprocidad sin doble evaluación. Integración T14 → T16 → T11/T12 → T15, persistencia en el historial existente y siete respuestas por auxiliar. La autorización del propietario incluye desarrollo y fusión tras verificación; véase [T16](../../tareas/T16.md).

## Comprobaciones

Regresión completa **734/734** correcta. Tras añadir el bloqueo de clasificación pendiente y ajustar textos explicativos, comprobación final **20/20** (T16, T15 y catálogo de traducciones). Resultados registrados en `validation-summary.json`. Los ficheros TRX completos permanecen como evidencia local de ejecución.

Interfaz WPF real: **39 comprobaciones correctas**, con el ViewModel y el archivo SQLite sintético C20. Registro: [checks.json](wpf-verified/checks.json). Se comprobaron edición e invalidación de respuestas, cobertura incompleta, guardado de borrador, bloqueo del cálculo incompleto, importación CSV, nuevo cálculo, recepción en T15 a 16 EUR/h, cambio de auxiliar, ES/EN, navegación por teclado, ventana estrecha y escalado simulado 125/150/200 %. También el acceso a T14 con auxiliares, costes retenidos en T12, cierre/reapertura de secundario y revisión obligatoria después de cambiar T14.

Capturas inspeccionadas: [español](wpf-verified/aux-es.png), [inglés](wpf-verified/aux-en.png), [ventana estrecha](wpf-verified/aux-narrow-es.png), [respuestas a escala 200 %](wpf-verified/aux-scale-200-es.png). El escalado es una transformación de presentación en la prueba; no certifica una matriz física de monitores DPI. Se conserva el sistema visual V1 y las acciones siguen accesibles mediante ajuste de línea y desplazamiento.

## Casos económicos y de persistencia

- C20: 1.600 = 800 a principales + 500 de A + 300 de B; B transmite 100 recibidos sin nueva evaluación. T15 aplica 800/50 = 16, sin variación propia adicional sobre esos servicios.
- C29: 1.000 = 1.200 - 200; también abonos firmados, real cero y denominador normal cero.
- Cobertura incompleta bloqueada; pendiente no equivale a ThroughFlow; evaluación mixta A evaluado/B sin evaluar conserva 1.600 = 1.100 transmitidos + 500 retenidos.
- Reciprocidad con salida; circuito cerrado bloqueado; matrices distintas por grupo; factorización reutilizada por identidad de matriz. Con un único principal, los factores terminales pueden ser iguales aunque las matrices de circulación difieran.
- Clasificación de coste pendiente impide afirmar evaluación completa.
- Guardado, cierre, reapertura de archivo, reconstrucción de T15 desde fuentes históricas y detección de cambios físicos auxiliares. Metadatos de propietario y evaluación llegan a los componentes de tarifa.
- Se conservan los contratos históricos sin plan T16 y no se reescriben ejecuciones anteriores.

## Incidencias de verificación resueltas

El primer pase completo detectó una expectativa incorrecta en la nueva prueba de matrices: con un solo destino, todas las contribuciones terminan en él aunque las rutas difieran. Se corrigió la comparación para comprobar las matrices efectivas, sin cambiar los importes económicos esperados.

El ejecutor de pruebas necesitó comunicación local fuera del aislamiento del proceso. La restauración inicial no pudo consultar el servicio remoto de avisos de NuGet; se usó la caché local con `NuGetAudit=false` para esa restauración, sin cambiar los archivos de proyecto ni presentar esa operación como una auditoría de dependencias. Un intento de compilación simultáneo encontró DLL ocupadas por la regresión; se repitió secuencialmente después de terminar.

## Reproducción local

- `dotnet build Valdrio.slnx --no-restore -m:1`
- `dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj --no-restore -m:1`
- Fixture: establecer `VALDRIO_T16_DEMO` a una ruta nueva y ejecutar la prueba `T16_C20_PersistsTransmitsOnceAndRestoresHistoricalRates`; genera una copia del C20 antes de la corrección de actividad.
- Interfaz: ejecutar Valdrio.Desktop con `--auxiliary-checks-only --capture-dir=<carpeta> <copia-desechable.valdrio>`; modifica únicamente esa copia sintética. Conserva el original de prueba.

## Límites deliberados

La empresa real debe aportar sus mediciones y decidir qué auxiliares evaluar. La evaluación parcial no mide la ociosidad de los auxiliares no evaluados. Se admite CSV de servicios con el perfil explicado en la pantalla; no se añade importación XLSX de servicios. Se conserva el modelo actual de un pool por nodo. No se adelantan consumos por orden T19, planificación, optimización o TDABC.

GitHub Actions no se ejecuta. Workflows existentes exclusivamente manuales; coste de Actions de esta entrega: **0 USD**.

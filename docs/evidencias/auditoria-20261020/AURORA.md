# Prueba de carga con AURORA — 20/10/2026

## Caso y método

Archivo facilitado por el propietario: `AURORA_Industrial_y_Servicios.valdrio`,
15.802.368 bytes, SHA-256
`756977CF952456620CEEB657B1A836A2C2668746C6CB42B3746282F4B794DE7F`.
Se abre como empresa VALDRIO, sin volver a importar sus CSV ni crear otro análisis.
Las pruebas usan copias desechables.

- Estructura: **Industrial · tres principales y dos auxiliares**.
- Análisis: **01 · Industrial completo y reparto recíproco**.
- Período: **01–31/10/2026**.
- Principales: MEC, MON, ENV. Auxiliares: MAN, ADM.
- Coste efectivo: 3.475 EUR; final en centros: 3.250 EUR;
  directo: 150 EUR; excluido: 75 EUR. Conciliación sin diferencias.
- AURORA tiene 26 análisis y precede a T16. Las mediciones físicas auxiliares
  están ausentes; no se sustituyen por ceros ni se guarda una propuesta T16.

Se realizan cinco recorridos de estructura, análisis, recursos, reparto
primario, secundario, flujos, actividad, tarifas y auxiliares; veinte clics
en los iconos de calculadora alternando las cinco filas. Se selecciona
deliberadamente otra fila antes del clic para verificar el registro contextual.

Los tiempos se miden con `Stopwatch`, en compilación Debug, hasta vaciar el
trabajo de presentación pendiente con `DispatcherPriority.ApplicationIdle`.
Preparación y presentación se registran por separado. Las capturas y la
escritura de evidencias quedan fuera del intervalo medido.
No es una medida fotométrica ni una prueba con caché de disco vacía.
Los procesos antes/después se ejecutan secuencialmente.

La referencia «antes» es la entrega `4cc615f`, que ya integra las pantallas
y corrige las lecturas de la calculadora; la comparación de esta prueba aísla
la mejora adicional de validación descubierta con AURORA. No equivale a una
medición de la calculadora original de `main` sobre AURORA.
Se conserva su DLL de infraestructura y se usa el mismo arnés de medición.
La corrección de la aserción de filas del arnés reconoce las cinco filas
de T14 actuales, incluidas las dos auxiliares.

## Causa y cambio

`Read`, `ReadWorkspace`, `ReadStudies`, `ReadModels` y `ReadCurrent` repetían
la validación integral, incluyendo auditoría e históricos económicos de los
26 análisis, aunque ningún byte del archivo hubiera cambiado. El primer
recorrido mostró aproximadamente 35 s al abrir, 33 s al cargar el análisis,
14 s al preparar actividad y tarifas, y 10 s al preparar auxiliares.
La presentación de esas pantallas costaba aproximadamente 0,1 s.

La sesión ahora conserva el resultado inmutable de una validación correcta
y calcula SHA-256 de **todos los bytes** antes de reutilizarlo. No confía en
fechas, tamaños o la revisión de empresa: cambiar cualquier agregado obliga
a validar otra vez. El contenido debe coincidir antes y después de la
validación para almacenarlo. No se almacenan errores.

Si existe WAL o journal, se mantiene la validación transaccional completa.
Las escrituras conservan sus comprobaciones completas. La apertura comprueba
el contenido antes y después de obtener el bloqueo de edición, y transfiere
ese bloqueo a la sesión únicamente tras el éxito. No cambia el esquema
SQLite, el cálculo económico ni la política de guardado.

También se elimina un segundo `Close()` redundante al cancelar la calculadora:
si se rechaza el cierre, no debe volver a pedir inmediatamente la misma
confirmación. El arnés verifica que un cierre rechazado deja la página abierta
y emite un único intento.

## Reproducción local

Compilar `src/Valdrio.Desktop/Valdrio.Desktop.csproj` con .NET 10.0.401.
Ejecutar el binario con estos argumentos, usando siempre una copia:

```text
--aurora-load-checks-only
--capture-dir=artifacts/aurora-check
--preferences-path=artifacts/aurora-check/preferences.json
artifacts/aurora-check/aurora.valdrio
```

El arnés genera tiempos, contexto, informe T15 y capturas. `completed.txt`
confirma el recorrido completo; una excepción produce `company-failure.txt`.
La limpieza automatizada descarta la propuesta T16 inicial no modificada
sin contestar un diálogo nativo. No se presenta como prueba interactiva de
ese diálogo. El resto de cierres usa la navegación integrada normal.

## Resultados medidos

La [tabla completa](aurora/TIEMPOS.md) distingue primera apertura, mediana
y P95. Los [datos antes](aurora/timings-before.json) y
[después](aurora/timings-after.json) conservan las muestras individuales.

| Operación | Antes | Después |
|---|---:|---:|
| Apertura inicial de empresa (una muestra) | 35,67 s | 11,17 s |
| Carga del análisis (una muestra) | 32,35 s | 1,10 s |
| Actividad, mediana | 14,13 s | 0,46 s |
| Tarifas, mediana | 14,39 s | 0,60 s |
| Auxiliares, mediana | 9,79 s | 0,58 s |

Todas las aperturas de pantalla medidas después de cargar la empresa quedan
por debajo de 2 s. Calculadora: mediana 96 ms, P95 127 ms, primera apertura
194 ms. La referencia ya contenía su optimización: su mediana era 49 ms;
esta prueba **no muestra una mejora adicional de la calculadora**. Repartos
y flujos tampoco mejoran sistemáticamente; el cambio corrige las rutas que
repetían la validación global.

Los SHA-256 del original y ambas copias coinciden tras completar los
recorridos ([integridad](aurora/file-integrity.json)). Los tres centros
conservan exactamente coste, tarifa, variación de capacidad, coste realizado,
coste de período y coste de origen frente a `01-resultado-T15.json`.
El gráfico ampliado conserva los totales, los flujos internos recíprocos
y el contenido serializado de la ejecución guardada.

El cierre rechazado de la calculadora produce un único intento y conserva
la página. Se cierran todas las superficies integradas sin dejar ventanas
adicionales. Capturas: [actividad](aurora/activity.png),
[calculadora](aurora/calculator.png), [tarifas](aurora/rates.png),
[auxiliares](aurora/auxiliary.png) y [recíproco ampliado](aurora/reciprocal-expanded.png).

## Validación final

Validación final: solución compilada sin errores ni advertencias; **739/739
pruebas locales superadas**, ninguna omitida, en 4 min 27 s. Incluyen las
tres regresiones nuevas de detección de cambios con fecha/tamaño
conservados, cambios fuera de la revisión de empresa y corrupción en WAL
con cancelación. El recorrido WPF final terminó con código 0.

## Límites

La primera apertura sigue validando el archivo completo. La primera lectura
después de cambiar datos también puede requerir esa validación. Esta entrega
no certifica todos los tamaños de empresa, dispositivos o diálogos de la
auditoría integral. Las muestras pequeñas de navegación permiten comparar
este caso; su P95 por rango más próximo coincide con el máximo observado.

No se preparó una distribución local: el propietario hará pull desde Visual
Studio. No se ejecutaron workflows ni se generó gasto de GitHub Actions.

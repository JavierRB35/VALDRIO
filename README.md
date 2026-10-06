# VALDRIO

Aplicación Windows local para cálculo y gestión de costes, en construcción guiada por especificación v0.4 y evidencia verificable.

## Estado actual

- **T00: CERRADA** — alcance confirmado; D01/D02 aceptadas.
- **T01: CERRADA** — solución .NET 10, CI Windows y shell WPF.
- **T01A: CERRADA** — interfaz internacional y preferencias independientes.
- **T02: CERRADA** — decimal canónico, PDEC-1, BalancedRound y proyección conciliada.
- **T02A: CERRADA** — Money/metadatos, fechas canónicas, RateSet/FX y contrato de orquestación de RevalueStudy verificados; integración SQLite pendiente.
- **T03: CERRADA** — archivo SQLite real, bloqueo OS, unidades/auditoría, copias/restauración y flujo WPF. **132/132 tests**, 24 checks WPF de archivos; cierre integrado con CI final aprobada.
- **T04: CERRADA** — formato v2, centros/modelos con publicación y duplicación; 153 pruebas y flujo WPF aprobados. Cierre integrado con CI final aprobada.
- **T05: IMPLEMENTADA, CIERRE BLOQUEADO** — `64aac29f` fue verificado externamente con build completo 0/0 y 188/188 tests. Después se endurecieron G02 y las regresiones reales B01 en `2a6b5e70`/`fffda702`; esos commits posteriores aún requieren ejecución .NET/Windows. PR #14 sigue draft y Actions no se ha usado.
- **T06: PENDIENTE** — importación contable con perfiles explícitos; directrices y puerta D03.
- Correcciones A01–A06 completadas: **114/114 tests**, 39/39 referencias, 12 comprobaciones reales del botón FX, publish y WPF smoke.
- JSON canónico versionado, huellas FX v2, cuantización exacta e inmutabilidad preparados para persistencia.
- Fecha de negocio, instante UTC y zona de presentación están separados.
- D04-D07 siguen siendo propuestas económicas.
- R07 de fuentes canónicas: **CERRADA** mediante PR #9.


## Evidencia

- [Cierre de auditoría A01–A06](docs/evidencias/AUDITORIA_CIERRE_2026-10-05.md)
- [Evidencia T03](docs/evidencias/T03_2026-10-05.md)
- [Evidencia T04](docs/evidencias/T04_2026-10-05.md)
- [ADR de modelos y formato v2](docs/adr/ADR-0003-modelos-y-esquema-v2.md)
- [Evidencia T05](docs/evidencias/T05_2026-10-05.md)
- [ADR de estudios y formato v3](docs/adr/ADR-0004-estudios-y-snapshots-v3.md)
- [Directrices de la siguiente fase T06](docs/directrices/T06_SIGUIENTE_FASE.md)
- [Contrato técnico del archivo](docs/adr/ADR-0002-archivo-empresa.md)

- [Estado del proyecto](ESTADO_PROYECTO.md)
- [Decisiones D01-D12](docs/decisiones/DECISIONES_PENDIENTES.md)
- [T02A](docs/tareas/T02A.md)
- [Evidencia T02A subentrega 1](docs/evidencias/T02A_SUB1_2026-10-04.md)
- [Evidencia T02A subentrega 2](docs/evidencias/T02A_SUB2_2026-10-04.md)
- [Evidencia T02A subentrega 3](docs/evidencias/T02A_SUB3_2026-10-05.md)
- [Evidencia R07](docs/evidencias/R07_2026-10-05.md)
- [T02](docs/tareas/T02.md)
- [Fuentes y huellas](docs/fuentes/FUENTES_DISPONIBLES.md)

## Límites actuales

Todavía no se declara implementado:

- catálogo monetario completo de producto;
- importación/cálculo sobre el archivo de empresa SQLite;
- importación contable;
- motor económico de repartos posterior.

La internacionalización, precisión decimal, moneda y tiempo permanecen explícitos y desacoplados.

## Probar el archivo de empresa

En Windows, ejecutar Valdrio.Desktop y usar Crear archivo con nombre, moneda sugerida y una ruta nueva `.valdrio`. Guardar una unidad, crear copia, cerrar y volver a abrir. Restaurar pide un destino nuevo. El archivo activo se conserva si falla otra apertura. No utilizar una carpeta de red o con sincronización activa.

Para reproducir los ejemplos sintéticos de CI: `Valdrio.Desktop.exe --capture-dir=<carpeta-de-evidencia>`; genera `company-file/interaction.json`, ejemplos SQLite y capturas. El artifact Windows es de desarrollo, todavía sin instalador/cifrado comercial ni motor económico completo.

## Probar modelos y centros

Abrir/crear archivo v3. Para un archivo v1/v2, usar **Actualizar archivo**, seleccionando origen y destino sin sobrescribir. Añadir unidades `lot` (batch, discreta) y `h` (time), centros PRENSA/MANT y un modelo con descripción. Aplicar nodos SETUP/lote y PROCESO/hora en PRENSA y un auxiliar MANT/hora. Guardar el borrador, validar y publicar. Duplicar para editar la siguiente versión; consultar el publicado conserva nombres y unidades históricos.

El editor es estructural. Los estudios se describen a continuación; las políticas económicas y el cálculo completo siguen pendientes.

## T05 — Estudios e instantáneas (cierre pendiente de CI)

Los archivos actuales son v3. Actualiza los v1/v2 mediante **Actualizar archivo**, con destino nuevo. El original se conserva. En Estudios, actualiza la lista de modelos, selecciona un publicado, introduce nombre/periodo mediante calendario/moneda/escenario y crea el estudio. Guardar contexto aumenta su revisión; **Capturar instantánea** conserva sus datos y dos huellas. Selecciona fotos anteriores para consultar lo aplicado. Las preferencias de idioma/región/zona no alteran esas fotos.

La captura registra contexto y explica qué falta; todavía no calcula costes. El runner puede verificar un receipt exportado:

```sh
dotnet run --project src/Valdrio.Runner -c Release -- --study-snapshot ruta/snapshot.json
```

[Directrices T06](docs/directrices/T06_SIGUIENTE_FASE.md): importación con perfiles/preview/confirmación y puerta D03 antes de una fuente real.

El cierre y la integración de T05 esperan la verificación de Windows del HEAD final. Los últimos intentos de Actions terminaron sin pasos ni runner; el aviso de GitHub aportado el 5 de octubre confirma que la cuenta ha agotado sus minutos incluidos. [Auditoría de estado y pendientes](docs/evidencias/AUDITORIA_ESTADO_2026-10-05.md). No utilizar los checks de un commit anterior como cierre de esta corrección.

## Verificación Windows independiente

Desde un clon de la rama `t05/studies`, en Windows con Git, .NET SDK 10.0.401, Python 3 y PowerShell 7.3 o posterior, ejecutar

```powershell
pwsh -File .github/scripts/Verify-Windows.ps1
```

El script exige un árbol Git limpio, compila, ejecuta pruebas/guards/corpus/runners, publica el binario de desarrollo y recorre los botones WPF. Exige los 73 checks finales de estudios, 53 de modelos, 24 de archivo y 12 FX; comprueba las fechas visibles de cuatro regiones, ocho PNG y ambos manifests. Genera `artifacts/acceptance-build.json` ligado al commit y a los hashes de Desktop/Runner/tests/TRX; `-UseExistingBuild` solo es válido si ese manifest coincide. `artifacts/visual/verification.json` registra además esos hashes y vuelve a comprobar HEAD/limpieza al finalizar. Un directorio de evidencia ocupado se conserva; para repetir, indicar `-EvidenceDirectory artifacts/visual-nueva`.

La ejecución Windows y la inspección de los PNG aún están pendientes. Para reanudar CI cuando Actions vuelva a estar disponible, ejecutar el workflow sobre el HEAD final de `t05/studies`, con `verify_timezones=true`. Si el workflow de esa rama aún no aparece en Run workflow porque no está en main, enviar un único commit sin instrucción de omisión para activar el evento del PR. No reintentar mientras persista el bloqueo ni integrar T05 sin su evidencia final.

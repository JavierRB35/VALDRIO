# VALDRIO — Estado auditado y pendientes de cierre

5 de octubre de 2026. Repositorio [JavierRB35/VALDRIO](https://github.com/JavierRB35/VALDRIO).

## Dictamen

La rama principal incorpora T00–T04. T05 está implementada en la PR #14, pero su última corrección de interfaz aún no ha recorrido Windows. El bloqueo operativo es el agotamiento de los minutos incluidos de GitHub Actions que acredita el aviso aportado por el propietario. La fase se mantiene abierta y la PR en borrador.

La aplicación ya persiste archivos, centros y modelos versionados. La rama T05 añade estudios y fotografías contextuales inmutables. Todavía no importa contabilidad ni ejecuta el motor completo de reparto, capacidad o márgenes. No está terminado el demostrador R0 ni existe una entrega comercial R1.

## Referencias verificadas

- Principal `fe01e10231568332c1df59c0b8df43081a5dac01`, [integración T04](https://github.com/JavierRB35/VALDRIO/commit/fe01e10231568332c1df59c0b8df43081a5dac01).
- T05 `d2e499fcf315a1aee5b0ed8b8d4e90f5a1bee6b9`, [PR #14](https://github.com/JavierRB35/VALDRIO/pull/14). Comparados los 245 blobs con sus SHA Git, sin diferencias. Árbol remoto no truncado.
- Las PR #1–#13 están fusionadas. Única PR abierta, #14 en borrador. No hay incidencias abiertas; el seguimiento se realiza en fichas y evidencias del repositorio.
- Última CI aprobada de main, [37331868168](https://github.com/JavierRB35/VALDRIO/actions/runs/37331868168), posterior a T04.
- Evidencia T05 anterior aprobada, [37342742981](https://github.com/JavierRB35/VALDRIO/actions/runs/37342742981), código 56c38f9, 183 tests y 67 checks WPF de estudios. No cubre las correcciones finales.
- En [37345211857](https://github.com/JavierRB35/VALDRIO/actions/runs/37345211857), los tres trabajos del HEAD d2e499f terminaron sin pasos, con runner id 0 y nombre vacío. Los seis checks de ese HEAD corresponden a dos ejecuciones fallidas antes del arranque. No equivalen a tests fallidos.
- La captura del propietario acredita 2.001 minutos usados frente a 2.000 incluidos en la cuenta. No se ha leído la anotación individual del run; no se atribuye a un error de código ni se presume habilitada ejecución adicional de pago.

## Fases y trabajo abierto

| Bloque | Estado real | Pendiente |
|---|---|---|
| T00 | Cerrada e integrada | D01/D02 aceptadas; decisiones posteriores conservan sus puertas |
| T01/T01A | Cerradas e integradas | Base Windows, interfaz internacional y preferencias |
| T02/T02A | Cerradas por su alcance e integradas | RevalueStudy dispone de contrato; su integración persistente completa sigue pendiente |
| T03 | Cerrada e integrada | Archivo SQLite, bloqueo, transacciones, backup y restore |
| T04 | Cerrada e integrada | Centros, modelos, publicación inmutable y duplicación |
| T05.0–T05.3 | Implementadas y probadas en SQLite real | Sin nueva funcionalidad identificada pendiente en estos bloques |
| T05.4 | Iniciada, no cerrada | Recorrido final WPF, fechas regionales y conservación de la evidencia de modelos |
| T05.5 | Documentación preparada; cierre no terminado | Revisar evidencia del HEAD final, actualizar estado e integrar PR #14 |
| T06 | Planificada, no iniciada | Importación CSV/XLSX, staging, confirmación; muestra D03 antes de una fuente real |
| T06A/T07–T33/T22A | Pendientes según sus dependencias | Recursos, FX persistente, reparto, solver, ejecución, capacidad, costes, márgenes, informes e instalación |

La especificación tiene 38 fichas. Siete están cerradas y T05 es la octava en desarrollo; esto no constituye un porcentaje de avance económico del producto, porque las tareas tienen alcances distintos.

Además de T05, permanece abierto **A07/I14**, integración persistente de revaloración con originales, recursos, particiones y ejecuciones. Las pruebas de T05 confirman atomicidad de estudios/snapshots, pero no un RevalueStudy económico completo. Su continuidad está en T06A/T07/T12.

Las ramas históricas no representan automáticamente fases abiertas. `audit-2026-10-05-a01-a06` conserva 14 commits propios y diverge de main, sin PR; su finalidad se solapa con A01–A06 cerradas mediante PR #11. No debe integrarse completa sobre main. No se han borrado ramas ni modificado trabajo histórico.

## Comprobaciones ejecutadas en esta revisión

| Comprobación | Resultado | Perímetro |
|---|---|---|
| Suite C# completa | 183/183, 0 fallos, 0 omitidas | Domain/Application/Infrastructure/SQLite y regresiones |
| Suite seleccionada con proceso TZ=UTC | 71/71 | R01, T03, T04 y T05 |
| Misma suite con TZ=Europe/Madrid | 71/71 | Segundo proceso, reutilizando la compilación |
| Solución completa incluida WPF | 0 avisos y 0 errores | Compilación cruzada en Linux; no ejecuta WPF |
| Fuentes canónicas v0.4 | 57 SHA correctos | Texto/JSON/Python; mismo alcance del guard existente |
| Oráculo racional | 39/39 referencias | No equivale al motor económico C# completo |
| Diagnósticos ES/EN | 169 claves cubiertas | Misma extracción y exclusiones del guard de producción |
| Runners T02 y T02A moneda/tiempo/FX | Cuatro recorridos aprobados | Referencias económicas y contratos previos |
| Workflow actualizado | YAML válido e inspeccionado | No ejecutado por Actions |
| Verificador PowerShell nuevo | Preparado e inspeccionado | Ejecución Windows todavía pendiente |

No se han modificado fuentes canónicas, fórmulas, esperados económicos ni código del motor para obtener estos resultados. Las pruebas cubren migraciones a copia, conflictos, rollback tras escritura real, cancelación, idempotencia, JSON estricto, hashes, cambios de maestro e inmutabilidad.

## Correcciones realizadas durante esta revisión

1. La aceptación WPF anterior admitía mínimos inferiores a la entrega final. Ahora exige 73 comprobaciones de estudios, 53 de modelos, 24 de archivo y 12 FX, con los nombres de las comprobaciones regionales y de conservación del archivo de modelos.
2. Los manifests deben enumerar todos sus archivos esperados, cinco de modelos y seis de estudios, sin omisiones, duplicados ni nombres adicionales, y sus SHA deben coincidir.
3. `.github/scripts/Verify-Windows.ps1` permite repetir la aceptación completa en Windows y comparte con CI el recorrido visual. Conserva directorios de evidencia existentes y genera un receipt con commit real, resultados TRX y checks. No está registrado como ejecutado en Windows.
4. Estado, ficha T05 y README identifican el bloqueo actual y distinguen implementación, pruebas locales, evidencia Windows anterior y verificación final pendiente.
5. La CI mantiene los guards y las pruebas en ambas zonas. Las reglas operativas solicitadas por el propietario quedan aplicadas al workflow. No se ha desactivado Actions ni configurado gasto adicional.

El commit de preparación utiliza una instrucción de omisión para evitar una ejecución automática sin disponibilidad. Esa omisión no es un check aprobado ni permite cerrar la PR. La integración sigue pendiente de evidencia positiva del HEAD final.

## Continuación concreta para cerrar T05

En un clon de la rama `t05/studies`, con Windows, Git, .NET SDK 10.0.401, Python 3 y PowerShell 7.3 o posterior

```powershell
pwsh -File .github/scripts/Verify-Windows.ps1
```

La aceptación requiere al menos las 183 pruebas previas aprobadas sin omitidas, 73 checks de estudios, 53 modelos, 24 archivo y 12 FX. Se generan ocho PNG de estudios, cuatro generales, los archivos SQLite/receipt y `artifacts/visual/verification.json`. Revisar visualmente fechas EN/es-ES, ES/en-US, de-DE y expansión/RTL, además de contexto e historial. Comprobar el commit del receipt contra el HEAD real antes de dar por válida esa evidencia.

Cuando Actions vuelva a estar disponible, ejecutar una sola verificación del HEAD final. El job Windows y el job Linux consolidado deben aprobar; este último prueba UTC y Madrid en procesos separados. Si el workflow de la rama aún no admite despacho porque no está en main, un único commit sin omisión activa el evento del PR. No repetir mientras siga el bloqueo ni sustituir el HEAD final por el código 56c38f9.

Después de revisar el resultado, actualizar la evidencia de T05, marcar la PR lista e integrar comprobando ExpectedHeadSha. Hasta entonces T05.4/T05.5 permanecen abiertas. No se ha fusionado la fase ni se han cambiado los requisitos de aceptación.

## Prioridades para el equipo

| Prioridad | Actuación | Motivo |
|---|---|---|
| P0 | Completar el recorrido Windows anterior | Única puerta técnica pendiente de T05 |
| P0 | Revisar capturas y receipt antes de integrar | La evidencia anterior tenía fechas sin refrescar y un manifest invalidado por reutilizar un archivo |
| P1 | Resolver D03 con muestra anonimizada y ejemplo aprobado | Define modalidad, signos, periodo y perfil de fuente de T06 |
| P1 | Ejecutar T06 por sus subfases y 24 criterios | Importación verificable, staging, confirmación atómica e idempotencia |
| P1 posterior | Cerrar A07/I14 en sus fases | Revaloración con datos autorizados, rollback y revisión persistente |
| P2 | Proteger main mediante PR y validaciones acordadas | La rama aparece sin protección; no se han cambiado permisos ni requisitos de administración |
| P2 | Revisar ramas históricas y seguimiento de pendientes | Evitar confundir trabajo sustituido con una fase activa |

D04–D07 siguen siendo propuestas. D08/D09/D12 permanecen diferidas y D10/D11 conservan sus puertas. El catálogo monetario sigue siendo un fixture de desarrollo. No se aprueban decisiones económicas por necesidad técnica.

[Directrices vigentes T06](../directrices/T06_SIGUIENTE_FASE.md) · [Evidencia T05](T05_2026-10-05.md) · [Estado](../../ESTADO_PROYECTO.md).

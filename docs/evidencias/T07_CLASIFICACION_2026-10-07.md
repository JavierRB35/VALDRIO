# T07 — Clasificación contextual y edición de borradores

Fecha: 2026-10-07. Base `05856b616049bbb6deb1d155f9ccf1ff21fcadb8`.
Rama `codex/t07-resource-rules`. Validación local Windows, SDK 10.0.401, Release.

## Resultado

La preparación valida comportamiento, driver/unidad, horizonte DateOnly y rango.
Se distinguen naturaleza fija/variable y capa económica. Los propietarios de
capacidad identifican pool/nodo/unidad, exigen ruta al mismo pool y conservan
observaciones ausentes frente a cero. No se elige una política o denominador.

Las advertencias de clasificación se separan de los errores. Un recurso mixto
puede estar en borrador pero falla el requisito de absorción definitiva. Si la
comprobación de capacidad exige comportamiento, sin clasificar/mixto bloquean
AssessAtPool. Un coste fijo no activa capacidad por sí mismo.

`ResourceDraftEditor` devuelve candidatos inmutables validados para clasificación,
división y sustitución de partes. Ejemplo: mixto 100 se divide en fijo 40 y variable
60; conserva la fila original y supera el requisito de clasificación para absorción.
40 + 70 falla sin modificar el original; reutilizar el ID de una parte hermana falla.

## Comandos y evidencia

- `dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --filter FullyQualifiedName~T07 --logger "trx;LogFileName=t07-classification.trx"`: 31/31 (17 anteriores + 14 nuevas).
- `dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --logger "trx;LogFileName=t07-classification-full.trx"`: 426/426, cero omitidas.
- `dotnet build Valdrio.slnx -c Release --no-restore`: nueve proyectos, incluido WPF; cero errores/advertencias.
- `git diff --check`: correcto.

TRX locales ignorados por Git, en `tests/Valdrio.Tests/TestResults`:

| Archivo | SHA-256 |
|---|---|
| t07-classification.trx | C266A1C04F1F80639F8AF88AA08099C42FDD542F41E20FCCDC8FF08954A0104D |
| t07-classification-full.trx | FA057012B4A107D9FCC88157F1928AAB4C666720C008646ABEC7791378E9C40B |

Las pruebas nuevas cubren contexto obligatorio, horizonte/rango, unidades y precisión,
aviso sin clasificar, bloqueo mixto, propietario incompatible, observaciones inválidas,
puerta explícita de capacidad, independencia capa/comportamiento, enums desconocidos,
edición inmutable, división conservada, fallos sin mutación y partición completa.
La suite incluye catálogos EN/ES y pruebas internacionales anteriores.

## Límites

Esta entrega no añade pantalla ni persistencia. No se atribuye una revisión visual,
CI remoto o una prueba nueva en procesos de distintas zonas horarias a este HEAD.
La edición en memoria no incrementa Revision ni escribe AuditEvent. La preparación
no valida aún que nodo/pool/objeto existan en el modelo o pertenezcan a su ámbito.
Los parámetros de clasificación no son una puerta completa de publicación: quedan
cobertura, pendientes, decisiones económicas y observaciones de capacidad.
Las capas no cambian el enum histórico CostLayer ni aceptan D07. D04/D05/D11 tampoco
cambian de estado. T06/T06A continúan como dependencias para la integración persistente.

Siguiente subentrega: integración del editor con ámbito/revisión y persistencia una
vez estabilizado el contrato de recursos importados. Ver [T07](../tareas/T07.md).

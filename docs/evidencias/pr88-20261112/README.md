# PR #88 — integración y validación local de T15

Fecha: 12/11/2026. Autorización expresa del propietario para integrar en main.
PR original: `a235853a078ecbf7eb5c9b87aa899e1d58aac7cf`.
Main incorporado: `1359cd3f37e422471299f460fb78f9a56720c629`.
La incorporación se registra en `ff2ec4d`; se conservaron la inicialización,
recarga y exportación asíncronas de T15. No hubo conflictos textuales.

## Alcance y correcciones

- Desglose de fijo propio, variable, recibido/consumido y período, con sus bases.
- Aviso no bloqueante para fijo consumido sin evaluación identificada, incluido
  en el filtro de atención. La respuesta «qué revisar» muestra el mismo aviso:
  ya no afirma simultáneamente que no hay incidencias.
- Sin cambios respecto a main en Domain, Calculation, Application o
  Infrastructure, ni en formatos, ledger, reglas CM08/CM09 o datos de usuario.
- Control de traducciones reparado: reconoce el prefijo de `Guide()` y la
  familia de naturaleza del coste. Sus pruebas siguen detectando claves ausentes.
  Se reutiliza la etiqueta de cancelación existente en el diálogo de centros.
- Smoke actualizado a la navegación vigente: Tarifas pertenece a Actividad y
  tarifas; el informe auxiliar está en Resultados; las páginas reales son
  integradas. La comprobación de rueda se realiza en el contenedor real.
- La regresión de clonación ahora comprueba que un borrador existente se
  reutiliza sin escribir y, tras publicarlo, prueba una nueva clonación y la
  recuperación ante fallo de refresco. No se cambió el comportamiento productivo.

## Verificación ejecutada

Windows, .NET SDK 10.0.403 (compatible con el roll-forward de global.json).
Compilación Release de la solución: cero errores y cero advertencias.
Las recompilaciones posteriores se limitaron a los proyectos afectados.

MSTest: **846 casos distintos cubiertos y satisfactorios en el estado final**:

- 23 focalizados: T15, T16 y confirmación asíncrona. Incluyen el ejemplo de
  20.000/18.500 horas, conservación, signos, redondeo, cero/ausencia, cierre,
  reapertura y reconstrucción de versiones históricas.
- 823 restantes: 822 pasaron inicialmente; el caso de clonación tenía una
  precondición anterior a la reutilización obligatoria de borradores. Corregida
  la preparación, se repitieron sus ocho variantes: 8/8 satisfactorias.
- Las dos variantes ES/EN del nuevo aviso también se repitieron al regenerar
  el archivo sintético para WPF: 2/2. No se suman dos veces al total.
- Se compara el JSON canónico antes/después de recargar y filtrar; el hash del
  archivo SQLite queda idéntico. T15/T16 reconstruyen exportaciones históricas.

Comandos principales:

```powershell
dotnet build Valdrio.slnx -c Release
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~T15|FullyQualifiedName~T16|FullyQualifiedName~AsyncConfirmation'
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName!~T15&FullyQualifiedName!~T16&FullyQualifiedName!~AsyncConfirmation'
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~E02_AllModelAndMasterCommitsRemainVisibleAfterRefreshFailure'
```

Controles adicionales satisfactorios: 57 hashes canónicos, 39 referencias
matemáticas, corpus T02, moneda/tiempo/FX T02A, prohibición de float/double,
reloj ambiental y red en cálculo, cobertura de 698 claves de producción ES/EN,
ausencia de duplicados, mapa de ayuda de 17 vistas y `git diff --check`.

WPF real, servicios reales y archivos sintéticos desechables: **233 comprobaciones**:

| Recorrido | Comprobaciones | Evidencia |
| --- | ---: | --- |
| Tarifas ordinarias ES/EN | 76 | [Resultados](tarifas-checks.json) |
| Fijo recibido sin evaluación ES/EN | 78 | [Resultados](fijo-recibido-checks.json) |
| Auxiliares T16 y consumo único en T15 | 45 | [Resultados](auxiliares-checks.json) |
| Actividad global y persistencia asíncrona | 34 | [Resultados](actividad-checks.json) |

Se verificaron selección, filtro, datos ausentes, respuestas sin valores
obsoletos, exportación legible, teclado, rueda de página, acciones inferiores,
ventana estrecha y escalado 125/150/200 %. La actividad global se comprobó con
una escritura bloqueada temporalmente: dispatcher operativo, animación,
prevención de doble guardado, regreso al éxito y liberación de actividad.

Capturas revisadas:

- [Desglose en ventana estrecha, español](desglose-estrecho-es.png).
- [Desglose al 200 %, inglés](desglose-200-en.png).
- [Aviso de fijo recibido, español](fijo-recibido-es.png).
- [Página integrada y navegación, inglés](tarifas-integradas-en.png).

## Límites y trazabilidad

El escalado se ejercitó mediante LayoutTransform de WPF, sin cambiar el DPI
del sistema ni certificar múltiples monitores. No se publicaron instaladores.
El smoke de auxiliares conserva una ventana aislada para comprobar su editor,
además del acceso al informe actual; no implica reintroducir un menú retirado.
Los fallos iniciales de los harnesses y la regresión se conservaron en los logs,
y se corrigieron antes de la integración, sin omitir las comprobaciones.

Logs completos, TRX y capturas adicionales locales en `artifacts/pr88/` y
`tests/Valdrio.Tests/TestResults/pr88-*.trx` del worktree de integración.
Solo se usaron datos sintéticos; las fuentes del proyecto permanecen intactas.
Workflows revisados: únicamente `workflow_dispatch`. No se ejecutó GitHub Actions
ni se alteraron protecciones de ramas. El remoto se volvió a comprobar antes
de preparar la fusión; no había revisiones ni conversaciones pendientes en la PR.

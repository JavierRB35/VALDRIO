# PEND-RP-01 — Reutilización de metodología y control de cambios entre estudios

**Decisión funcional:** desarrollo requerido por el propietario. **Prioridad alta y prerrequisito para continuar con T15.**  
**Estado:** PENDIENTE DE IMPLEMENTACIÓN Y ACEPTACIÓN. La solución aquí descrita es el contrato funcional propuesto para desarrollo; quedan por definir detalles técnicos y validar escenarios antes de codificar.  
**Registro:** 15/10/2026. **Rama:** documentación; no fusionar sin acuerdo final.  
**Alcance:** T05 (alta/elección de estudio y modelo), T06/T07 (nuevos datos y preparación de recursos), T08/T09 (claves y reparto primario), T10/T11 (rutas auxiliares y cierre secundario) y T12 (vigencia/ejecuciones). T14/T15 solo consumen los resultados válidos, sin redefinir su cálculo.

## 1. Problema y objetivo

Para una misma empresa y una estructura de costes estable, el usuario debe poder registrar febrero después de enero **sin reconstruir las claves y reglas de reparto que siguen siendo válidas**. Al mismo tiempo, cualquier cambio en cuentas, partes, nodos, centros, unidades, auxiliares, importes o reglas debe identificarse y resolverse **en la fase que le corresponde**, antes de publicar resultados económicos.

**Principio rector:** reutilización automática de la *aplicación de reglas ya adoptadas y compatibles*; **adopción inicial explícita y revisable** de metodología de otro estudio. Automatizar lo fiable sin completar, inventar, asignar por parecido o dar por cerrado un estudio incompleto.

## 2. Situación comprobada en el código — NO confundir con lo solicitado

- La biblioteca de `AllocationKeyVersion` es de empresa + versión de modelo, y permite reutilizar claves publicadas con el mismo modelo.
- `AllocationConfiguration` (reglas primarias por cuenta, secundarias por auxiliar/grupo, asignaciones manuales, importes absolutos, grupos, cierres) es **por estudio**. `ReadAllocations` utiliza `AllocationConfiguration.Empty` si el nuevo estudio no tiene cambios propios: no hereda reglas de otro estudio.
- `CreateStudy` referencia un modelo publicado, pero no clona la configuración económica ni las importaciones.
- El acceso actual del menú a **Primario** requiere importaciones confirmadas; **no exige** cierre/validación completa de Recursos. `ResourceWorkspaceViewModel.CanAdvance` controla navegación/edición del formulario, no una puerta definitiva de Recursos.
- `ResourcePreparation.IsValid` puede ser verdadero con `HasPendingParts == true`, incluso si el importe neto pendiente es cero. El cálculo primario admite temporalmente partes `Pending` y señala huecos; el cierre primario exige cobertura y el secundario ya exige primario cerrado.
- No existe hoy un estado formal «Recursos cerrado» equivalente a los cierres de primario y secundario. **Se deberá diseñar una puerta verificable «Recursos preparado para reparto»**, no afirmar que ya está implementada.

Fuentes verificadas: `SqliteCompanyFileService.Studies.cs`, `SqliteCompanyFileService.Allocations.cs`, `AllocationWorkspace.cs`, `ResourceDraft.cs`, `ResourcePreparationRules.cs`, `PrimaryAllocation.cs`, `ResourceWorkspaceViewModel.cs`, `AllocationWorkspaceViewModel.cs` y `MainWindow.Guidance.cs`.

## 3. Secuencia de usuario acordada (estudio febrero desde enero)

1. **Nuevo estudio:** escoger empresa, período febrero y modelo publicado. Ofrecer «Configurar de cero» o **«Reutilizar metodología de otro estudio»**, con enero como candidato sugerido si es compatible; el usuario selecciona/autoriza el origen. Mostrar modelo, fechas, revisión y advertencia de condiciones cambiantes.
2. **Previsualizar reutilización:** comparar origen/destino, mostrar reglas y claves compatibles, obsoletas, conflictivas y pendientes. Registrar una propuesta, no una aplicación silenciosa.
3. **Importar febrero:** nunca arrastrar saldos, horas, importes absolutos, consumos, ajustes ejecutados, partes con IDs antiguos ni resultados de enero. Mantener independientes ambos estudios.
4. **Preparar Recursos:** detectar cuentas nuevas, cambiadas o eliminadas y recursos no clasificados/particionados/destinados. Mostrar una lista de trabajo con acceso a la cuenta. Si hay pendientes reales, **no habilitar el avance ordinario a Primario/ Secundario**. Sí permitir regresar, corregir y consultar la biblioteca de claves en el contexto apropiado.
5. **Primario:** al superar la puerta de Recursos, aplicar automáticamente las **reglas ya adoptadas**: prioridad explícita/manual vigente del estudio > cuenta exacta > prefijo más largo > prioridad definida; no asignar si hay empate, ambigüedad o ninguna clave válida. Mostrar «resueltas automáticamente», «requieren revisión» y «sin regla». Si hay huecos, **permitir trabajar en Primario para corregirlos**, pero impedir su **cierre** y el paso a Secundario.
6. **Secundario:** tras cerrar Primario, aplicar reglas secundarias adoptadas a auxiliares/grupos válidos; identificar auxiliares nuevos, inactivos, rutas incompatibles y grafos inviables. Permitir reparar dentro de Secundario pero impedir su cierre/publicación mientras falte cobertura o viabilidad.
7. **T12:** crear una ejecución nueva **solo** desde entradas económicas vigentes y fases cerradas para febrero; jamás reutilizar la ejecución `RunId` ni importes calculados de enero.

### Distinción imprescindible: puertas de fase frente a botones de corrección

- **Bloquear avance** no significa prohibir **entrar en la fase donde debe corregirse el defecto**. Las partidas sin destino/clasificación se corrigen en Recursos. Las partidas con destino válido pero sin clave de reparto **se corrigen en Primario**. Las rutas de auxiliares sin regla o inviables **se corrigen en Secundario**.
- Las pantallas anteriores, resultados históricos y herramientas de diagnóstico conservan acceso de lectura cuando proceda. Un control deshabilitado debe explicar por qué y ofrecer enlace al lugar de reparación.
- No exigir al usuario completar las reglas primarias dentro de Recursos; eso haría imposible corregir el propio bloqueo.

## 4. Matriz de casos y acción esperada

| Cambio o situación al abrir febrero | Comprobación y comportamiento | Puerta que debe bloquear |
|---|---|---|
| Misma empresa, modelo y reglas; cambian solo importes | Reutilizar claves y reglas **adoptadas**, resolver automáticamente cuentas existentes contra febrero y recalcular importes | Ninguna, si hay cobertura válida |
| Cuenta nueva sin clasificación/partes/destino | Crear trabajo pendiente de Recursos, sin inventar un destino ni dar por preparado | Salida de Recursos hacia Primario |
| Cuenta nueva preparada en Recursos y cubierta por regla exacta/prefijo vigente | Aplicación automática de la clave en Primario, mostrando origen de la coincidencia | Ninguna si no hay otros huecos |
| Cuenta nueva preparada sin regla primaria | Marcar «sin clave», abrir editor y ofrecer crear regla/seleccionar clave publicada | Cierre de Primario, no su edición |
| Cuenta existente con nueva descripción o naturaleza distinta | Mantener identidad contable explícita; alertar posible cambio semántico y revisión de clasificación y regla, sin basarse solo en texto | Recursos o Primario según discrepancia |
| Cuenta retirada de febrero | No inventar saldo cero ni borrar metodología histórica; regla exacta sin uso queda informativa y no asigna nada | Ninguna si el resto concilia |
| Reaparición de cuenta previamente ausente | Reaplicar solo una regla vigente comprobada; revisar cambios semánticos y prioridad | La fase con incompatibilidad |
| Cuenta desdoblada, fusionada, renumerada o de otro plan contable | No deducir equivalencia por prefijo/nombre; exigir correspondencia expresa y revisar destino/clave | Recursos y/o Primario |
| Recursos nuevos por ajustes extracontables o por nuevas partes | No aplicar ajustes anteriores automáticamente; nuevas partes deben clasificarse y asignarse; IDs de parte se renuevan | Recursos y, si falta clave, Primario |
| Recursos con neto cero, importes negativos o suma de positivos/negativos igual a cero | Evaluar **cobertura por parte**, no solo el neto: `Pending` sigue pendiente incluso con neto cero | Recursos o Primario según origen |
| Reparto manual vinculado a `PartId` de enero | No copiar ID ni asignación directa; propuesta sujeta a identificación comprobada de parte equivalente y revisión | Primario |
| Asignación absoluta por importe y revisión de origen | **Nunca** copiar cantidades, límites ni referencia económica obsoleta; recalcular/introducir y justificar en febrero | Primario |
| Clave porcentual sin cambios en modelo | Reutilizar el criterio aprobado y validar cobertura, destinos, suma 100 % y política de autorreparto | Primario/ Secundario si falla |
| Clave por unidades/driver | No trasladar mediciones mensuales sin evidencia; revisar unidades, cantidades vigentes y suma de pesos | Primario/ Secundario si falla |
| Cambio de prioridad, regla exacta o prefijo; empate | Previsualizar ganadora/colisiones; no escoger clave arbitrariamente | Cierre de Primario |
| Centro/nodo/pool nuevo | Exige modelo/versión válida y revisión de correspondencias; no añadir automáticamente el nuevo destino a una clave ya publicada | Recursos si falta destino; Primario/Secundario para cobertura |
| Centro/nodo/pool eliminado, desactivado o sustituido | No redistribuir su porcentaje a los supervivientes ni cambiar ruta por nombre; identificar clave obsoleta y crear versión nueva si corresponde | Fase afectada |
| Centro que cambia de rol (principal ↔ auxiliar) | Revisar estructura, clasificaciones, claves y grafo de rutas; no conservar correspondencias implícitas | Primario y Secundario |
| Unidad del pool cambiada | Rechazar claves basadas en unidades incompatibles; conversión solo si está explícitamente definida y justificada | Fase afectada |
| Auxiliar nuevo, eliminado, grupos nuevos o rutas recíprocas cambiadas | Validar cobertura por **cada auxiliar/grupo activo utilizado**, salidas, ciclos, sensibilidad, autoprestación y política de grupos | Cierre de Secundario |
| Cambio de modelo publicado/versionado | No trasplantar IDs/rutas de otra versión como si fueran idénticos. Ofrecer propuesta de mapeo explícito de identidades; claves nuevas/versiones cuando haga falta | Ningún cierre hasta revisión |
| Estudio origen cerrado; destino abierto | Leer origen como referencia inmutable; escribir solo en destino con trazabilidad | Ninguna si hay permisos |
| Destino cerrado o archivo en solo lectura | Mostrar comparación, nunca persistir; requerir reapertura explícita del destino | Todas las escrituras |
| Cambio de importación, clasificación, origen económico o regla tras cerrar una fase | Seguir la política de reapertura en orden inverso, invalidar cierres/ejecuciones afectados sin borrado de historia; solicitar recálculo | Cierres y nueva ejecución |
| Importación duplicada, período incompatible, moneda/FX o datos inconsistentes | No reusar importes ni sumar dos veces; resolver en importación/Recursos con diagnósticos concretos | Preparación/cálculo afectados |
| Múltiples estudios origen o reglas ya existentes en febrero | Elegir origen, mostrar colisiones, permitir selección granular; jamás sobreescribir reglas de febrero sin confirmación | Adopción hasta resolver conflictos |

## 5. Política de adopción y trazabilidad

- El usuario **elige** adoptar configuración de otro estudio. Una vez adoptadas válidamente las reglas en febrero, su **aplicación por cuenta es automática y determinista**; no confirmar una por una las coincidencias sin incidencia.
- Previsualización con contadores: reutilizadas, pendientes, incompatibles, sin uso, sustituciones solicitadas y elementos omitidos. Cada incidencia incluye fuente/etapa/acción recomendada. Mostrar antes/después para diferencias económicas.
- **No copiar estados de cierre:** nuevo estudio siempre debe acreditar por sí mismo la preparación, reparto y cierre. No copiar publicación T11/T12 ni revisión/hashes del origen.
- Guardado de la configuración de destino bajo revisión/versión propia y en una transacción con auditoría: origen, revisión y versión de claves/reglas, mapeos explícitos, usuario, fecha, elecciones, sustituciones y diagnósticos. No mutar el estudio origen.
- Evitar reglas obsoletas activas que apunten a identidades inválidas. Conservarlas como referencias históricas/inactivas o propuestas, no como cobertura vigente.
- Aplicación idempotente: reabrir o repetir una propuesta no duplica reglas ni líneas. Si febrero tiene reglas propias, no reemplazarlas silenciosamente. Controlar concurrencia y revisiones obsoletas.
- Si no existen estudios compatibles, ofrecer configuración desde cero y explicar el motivo. Nunca inventar reglas o porcentajes por defecto.
- T06/T07 debe seguir el contrato de procedencia e importación; no supone migración de ajustes extracontables aplicados ni reutilización automática de actividad T14.

## 6. Puertas formales de avance (contrato por implementar)

**G0 — Estudio listo:** modelo publicado adecuado; importaciones reales del nuevo período confirmadas; moneda/contexto correctos.

**G1 — Recursos preparados:** sin edición local pendiente; importes y partes conciliados; `ResourcePreparation.IsValid` y `HasPendingParts == false`; cada partida dispone de destino válido (pool, objeto directo o exclusión justificada) y clasificación obligatoria para la política aplicable. Las cuentas nuevas quedan identificadas. Un neto de cero no evita esta comprobación. Se admite lectura/edición de Recursos aunque la puerta no esté cumplida.

**G2 — Primario cerrado:** G1 más todas las partes sujetas a reparto cubiertas por claves válidas, versiones publicadas y reglas sin empates; resultado completo, conciliado y cierre explícito. Si falta una regla, habilitar **reparación en Primario** pero no su cierre, ni entrada operativa a Secundario.

**G3 — Secundario cerrado:** G2 más reglas auxiliares completas por grupo usado, grafo numéricamente viable, totales conciliados y cierre explícito. Bloquear publicación económica vigente T12 si falta una puerta.

**G4 — Ejecución vigente:** T12 publicada para la revisión económica de febrero y sus entradas congeladas. Cualquier edición posterior sigue contratos existentes de reapertura, invalidez histórica y nueva publicación; no recalcular a escondidas.

**Nota de diseño:** «Recursos preparado» es una nueva puerta funcional (o estado verificable de preparación), **no se afirma que exista hoy un botón de cerrar Recursos**. No permitir eludir G1 con otro botón de navegación, pero sí abrir modos de reparación y consultas históricas. Las puertas se validan también en Application/Infrastructure, no solo deshabilitando controles WPF. Evitar rutas alternativas y operaciones de servicio que publiquen resultados incompletos.

## 7. Subentregas de implementación (orden obligatorio antes de T15)

1. **RP-01A — Modelo de reutilización y diagnóstico:** inventario de elementos reutilizables/no reutilizables, compatibilidad por modelo/nodo/pool/unidad/cuenta/regla, identidad y origen/versionado. Pruebas de diferencia de estudios.
2. **RP-01B — Puerta G1 Recursos y navegación reparable:** definición de preparación de recursos, validación integral de partidas incluso cero, errores accionables, control de entrada normal a Primario y compatibilidad con accesos existentes.
3. **RP-01C — Adopción asistida en T05/T08:** selector origen, vista previa granular y guardado seguro/idempotente solo de configuraciones compatibles; resolución primaria automática por cuenta después de adoptar.
4. **RP-01D — Secundario/T11, invalidez y T12:** diagnóstico de nuevos auxiliares/rutas y referencias obsoletas, cierres y reaperturas consistentes, historial de ejecuciones intacto.
5. **RP-01E — Aceptación Windows local:** suite .NET y recorrido WPF ES/EN, resolución de incidencias, accesibilidad; no marcar completado por documentación o pruebas estáticas.

## 8. Casos mínimos de aceptación

- **A:** enero con cinco cuentas/reglas y dos auxiliares; febrero con las mismas cuentas y diferente coste. Seleccionar origen una vez, aplicar automáticamente reglas ya adoptadas, **sin** reintroducirlas, calcular febrero sin reescribir enero.
- **B:** febrero tiene una cuenta adicional sin partes/destino: G1 falla, Primario no se habilita como avance ordinario; desde Recursos se resuelve. Tras ello entra a Primario.
- **C:** la nueva cuenta queda preparada, pero no coincide con regla: editor Primario accesible, diagnóstico visible, G2 y Secundario bloqueados hasta crear/seleccionar clave y cubrirla.
- **D:** nueva cuenta cubierta por prefijo válido: aplicación automática verificable, sin tener que asignar manualmente otra vez; precedencias y colisiones probadas.
- **E:** auxiliar nuevo o ruta sin salida: no cierra Secundario hasta aprobar ruta y comprobar viabilidad recíproca.
- **F:** modelo nuevo con nodo añadido/eliminado, rol y unidad cambiados: no copia reglas inválidas, exige revisión/mapeo explícito, no redistribuye porcentajes ni crea identidades nuevas sin decisión.
- **G:** cuenta desaparecida, cuenta renumerada, reaparición, cuentas cero y contrapuestas, duplicados, importe absoluto, driver mensual: se distinguen correctamente; no se inventan valores ni se pierden datos.
- **H:** reabrir y repetir adopción no duplica; conflictos no sobrescriben; validación de concurrencia, rollback y trazabilidad completas.
- **I:** estudio cerrado y cambios económicos posteriores: no modifican origen y conservan los resultados T11/T12 previos identificados como históricos cuando corresponda.
- **J:** pruebas de navegación con botones laterales, «Siguiente», acceso directo y APIs; ninguna variante salta G1/G2/G3, pero todas las pantallas de reparación necesarias son accesibles.

## 9. Decisiones de detalle a cerrar al iniciar desarrollo

La **necesidad, prioridad y principio de reutilización guiada están confirmados**. Requieren cierre técnico antes de desarrollar: (a) entidad de `ResourcePreparationGate`/momento de aceptación y su persistencia o derivación; (b) tratamiento de propuestas con modelo distinto —mapeo o solo referencia—; (c) política exacta de clasificación mínima para pasar G1 sin adelantar D04/T15; (d) adopción previa o posterior a importación con elementos aún no contrastables; (e) reversión de adopción y mapeo de reglas inactivas. Resolverlas sin rebajar las puertas ni alterar la metodología económica vigente.

**Restricciones de operación:** documentación únicamente en esta rama; no ejecutar Actions, no usar GitHub-hosted runners sin aprobación expresa, no fusionar ni iniciar T15 sobre una dependencia no resuelta. La consolidación temporal multiestudio (acumulados enero-abril o doce meses) sigue **pendiente de decisión aparte** y no se incluye en RP-01.

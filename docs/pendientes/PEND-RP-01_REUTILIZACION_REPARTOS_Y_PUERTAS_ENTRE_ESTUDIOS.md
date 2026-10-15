# PEND-RP-01 — Reutilización de metodología y control de cambios entre estudios

**Decisión funcional:** desarrollo requerido por el propietario. **Prioridad alta y prerrequisito para continuar con T15.**  
**Estado:** PENDIENTE DE IMPLEMENTACIÓN Y ACEPTACIÓN. La solución aquí descrita es el contrato funcional propuesto para desarrollo; quedan por definir detalles técnicos y validar escenarios antes de codificar.  
**Registro y rectificación funcional:** 15/10/2026. **Rama:** documentación; no fusionar sin acuerdo final. **Mandato del propietario:** respetar exactamente la navegación actual Recursos → Primario → Secundario. No crear una puerta ni cierre adicional en Recursos.  
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
- No existe un cierre formal de Recursos y **no debe añadirse**. El acceso actual a Primario después de importaciones confirmadas sigue permitido incluso con partidas pendientes en Recursos. Es en Primario donde las reglas vigentes deben resolver las partes o señalar faltantes que impidan su cierre.

Fuentes verificadas: `SqliteCompanyFileService.Studies.cs`, `SqliteCompanyFileService.Allocations.cs`, `AllocationWorkspace.cs`, `ResourceDraft.cs`, `ResourcePreparationRules.cs`, `PrimaryAllocation.cs`, `ResourceWorkspaceViewModel.cs`, `AllocationWorkspaceViewModel.cs` y `MainWindow.Guidance.cs`.

## 3. Secuencia de usuario acordada (estudio febrero desde enero)

1. **Nuevo estudio:** seleccionar empresa, período y modelo publicado. Ofrecer crear desde cero o reutilizar metodología de un estudio anterior mediante elección y aceptación explícitas, indicando origen y versiones.
2. **Previsualizar reutilización:** diferenciar claves y reglas compatibles, inaplicables y conflictivas. No copiar importes, saldos, horas, ajustes ejecutados, partes con IDs antiguos, cierres ni resultados económicos.
3. **Importar febrero:** confirmar los datos del propio período, sin alterar enero.
4. **Recursos:** mantener el funcionamiento y la navegación actuales. Se pueden detectar cuentas/partes nuevas y mostrar pendientes de forma informativa, pero **no se añade un requisito de cerrar o completar Recursos para entrar a Primario**. El usuario puede corregir aquí o avanzar como hasta ahora.
5. **Primario:** se abre con las importaciones confirmadas incluso si Recursos tiene partidas pendientes. Al entrar, **aplicar automáticamente las reglas ya adoptadas y compatibles** mediante la precedencia existente (asignación manual válida de la parte → cuenta exacta → prefijo más largo → prioridad). Una cuenta nueva no es motivo de bloqueo por sí sola. Si no hay clave, existe una ambigüedad, destino inválido o parte no cubierta, mostrar el diagnóstico: **Primario continúa editable, pero no permite su cierre ni el avance a Secundario hasta completar la cobertura y conciliación conforme al motor actual**. El usuario puede regresar a Recursos si lo necesita.
6. **Secundario:** tras el cierre del Primario, aplicar las reglas adoptadas que correspondan a auxiliares y grupos del estudio nuevo. Detectar auxiliares/nodos/rutas incompatibles; permitir su reparación en Secundario, sin cerrar con cobertura o viabilidad incompletas.
7. **T12:** nueva ejecución solo con las entradas y cierres válidos del estudio de febrero, nunca reutilizar `RunId` o resultados de enero.

### Criterio de navegación que debe conservarse

- **Entrar a Primario no equivale a cerrar Primario.** Se mantiene el acceso actual con importaciones confirmadas aunque en Recursos quede trabajo pendiente.
- No añadir nuevos controles de paso, confirmación de Recursos, estados de cierre de Recursos ni filtros que obliguen a repartir cada cuenta antes de entrar a Primario.
- Las claves del Primario pueden resolver las partidas pendientes dentro de la metodología actual. Si no las cubren, el **cierre del Primario** se rechaza, con mensaje y opción de corregir donde corresponda.
- El **acceso a Secundario** continúa requiriendo Primario cerrado. Dentro de Secundario se reparan rutas y auxiliares nuevos sin alterar las reglas actuales de cierre.
- Una cuenta nueva cubierta por una regla válida se procesa como las demás: no requiere aprobación individual ni crea un bloqueo artificial. Una cuenta nueva sin cobertura exige actuación en Primario.

## 4. Matriz de casos y acción esperada

| Cambio o situación al abrir febrero | Comprobación y comportamiento | Puerta que debe bloquear |
|---|---|---|
| Misma empresa, modelo y reglas; cambian solo importes | Reutilizar claves y reglas **adoptadas**, resolver automáticamente cuentas existentes contra febrero y recalcular importes | Ninguna, si hay cobertura válida |
| Cuenta nueva sin clasificación/partes/destino | Mostrar cuenta/parte pendiente en Recursos sin prohibir entrar a Primario; intentar cobertura según las reglas existentes y solicitar corrección solo si falta | Cierre de Primario si no hay cobertura válida; nunca entrada a Primario |
| Cuenta nueva preparada en Recursos y cubierta por regla exacta/prefijo vigente | Aplicación automática de la clave en Primario, mostrando origen de la coincidencia | Ninguna si no hay otros huecos |
| Cuenta nueva preparada sin regla primaria | Marcar «sin clave», abrir editor y ofrecer crear regla/seleccionar clave publicada | Cierre de Primario, no su edición |
| Cuenta existente con nueva descripción o naturaleza distinta | Conservar código e identidad; señalar posible cambio semántico y revisar clasificación o regla solo si afecta al resultado; no asignar por parecido del texto | Cierre de Primario si falta cobertura/validez; sin nueva barrera en Recursos |
| Cuenta retirada de febrero | No inventar saldo cero ni borrar metodología histórica; regla exacta sin uso queda informativa y no asigna nada | Ninguna si el resto concilia |
| Reaparición de cuenta previamente ausente | Resolver automáticamente si existe regla compatible adoptada; revisar si cambian naturaleza, destino o prioridad | Cierre de Primario si persiste incidencia |
| Cuenta desdoblada, fusionada, renumerada o de otro plan contable | No presumir equivalencias; comparar códigos/identidades actuales y exigir mapeo expreso para configuraciones manuales que cambian | Cierre de Primario si falta cobertura o correspondencia |
| Recursos nuevos por ajustes extracontables o por nuevas partes | No reaplicar ajustes previos ni trasladar IDs de parte; mostrar diagnóstico y permitir entrar a Primario para resolver nuevas partes | Cierre de Primario si faltan claves/destinos válidos |
| Recursos con neto cero, importes negativos o suma de positivos/negativos igual a cero | Verificar cobertura por parte, incluida `Pending` y partes con neto cero; permitir resolución por regla compatible en Primario | Cierre de Primario solo ante hueco o descuadre |
| Reparto manual vinculado a `PartId` de enero | No copiar ID ni asignación directa; propuesta sujeta a identificación comprobada de parte equivalente y revisión | Primario |
| Asignación absoluta por importe y revisión de origen | **Nunca** copiar cantidades, límites ni referencia económica obsoleta; recalcular/introducir y justificar en febrero | Primario |
| Clave porcentual sin cambios en modelo | Reutilizar el criterio aprobado y validar cobertura, destinos, suma 100 % y política de autorreparto | Primario/ Secundario si falla |
| Clave por unidades/driver | No trasladar mediciones mensuales sin evidencia; revisar unidades, cantidades vigentes y suma de pesos | Primario/ Secundario si falla |
| Cambio de prioridad, regla exacta o prefijo; empate | Previsualizar ganadora/colisiones; no escoger clave arbitrariamente | Cierre de Primario |
| Centro/nodo/pool nuevo | Revisar modelo y correspondencias; no introducir automáticamente el nuevo nodo en una clave publicada. Mantener Primario accesible para adaptar la clave | Cierre de Primario o Secundario según la incidencia |
| Centro/nodo/pool eliminado, desactivado o sustituido | No redistribuir porcentajes ni reasignar por nombre; destacar claves/rutas incompatibles y mantener accesible su reparación | Cierre de Primario o Secundario según cobertura |
| Centro que cambia de rol (principal ↔ auxiliar) | Revisar claves, roles y grafos; no trasplantar destinos implícitamente | Cierre de Primario o Secundario según incidencia |
| Unidad del pool cambiada | Validar compatibilidad de unidad/driver y requerir nueva clave si procede; no convertir sin reglas explícitas | Cierre de fase económica afectada |
| Auxiliar nuevo, eliminado, grupos nuevos o rutas recíprocas cambiadas | Validar cobertura por **cada auxiliar/grupo activo utilizado**, salidas, ciclos, sensibilidad, autoprestación y política de grupos | Cierre de Secundario |
| Cambio de modelo publicado/versionado | Mostrar diferencias e incompatibilidades, sin trasplantar IDs ni rutas de modelos distintos sin revisión; se permite entrar a Primario con el modelo publicado vigente | Cierre de Primario o Secundario hasta cubrir inconsistencias |
| Estudio origen cerrado; destino abierto | Leer origen como referencia inmutable; escribir solo en destino con trazabilidad | Ninguna si hay permisos |
| Destino cerrado o archivo en solo lectura | Mostrar comparación, nunca persistir; requerir reapertura explícita del destino | Todas las escrituras |
| Cambio de importación, clasificación, origen económico o regla tras cerrar una fase | Seguir la política de reapertura en orden inverso, invalidar cierres/ejecuciones afectados sin borrado de historia; solicitar recálculo | Cierres y nueva ejecución |
| Importación duplicada, período incompatible, moneda/FX o datos inconsistentes | Mantener validaciones existentes de importación y cálculo; evitar duplicar importes. No crear un nuevo control de cierre de Recursos | Validación existente de importación y cierre/cálculo afectado |
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

## 6. Accesos y controles de cierre existentes: conservar, no endurecer

**Acceso a Primario:** el menú y «Siguiente» seguirán permitiendo pasar después de una importación confirmada, aun cuando existan recursos o cuentas nuevas con partidas pendientes. **No existe ni se desarrollará una nueva puerta G1 de «Recursos preparados»**. Las validaciones propias de guardar/importar Recursos permanecen intactas.

**Cierre de Primario:** aplicar las claves y reglas adoptadas con la precedencia vigente; comprobar cobertura y conciliación mediante los contratos actuales de T08/T09 y cierre de T11. Las partes sin regla, ambigüedades, claves obsoletas, destinos incompatibles y pendientes no resueltos permanecen visibles. No cerrar Primario si `PrimaryAllocationResult.IsComplete` no se satisface; permitir repararlo allí o volver a Recursos. **No bloquear su apertura por detectar diferencias entre enero y febrero.**

**Acceso y cierre de Secundario:** exigir el cierre explícito de Primario como hasta ahora. Las reglas y rutas secundarias adoptadas deben cumplir las comprobaciones existentes de cobertura por auxiliar/grupo, viabilidad numérica y conciliación antes de cerrar Secundario.

**Ejecuciones T12:** conservar las validaciones económicas y revisiones aplicables al estudio actual, sin arrastrar estados o resultados históricos del estudio origen.

**Cambios estructurales:** si la versión de modelo, nodo, pool, unidad o grupo hace incompatible una regla reutilizada, señalarla como incidencia y no adoptarla automáticamente; **no cambiar el criterio de acceso a Primario**. La protección y auditoría de configuración reutilizada deben residir en Application/Infrastructure, sin nuevas restricciones de navegación.

## 7. Subentregas de implementación (orden obligatorio antes de T15)

1. **RP-01A — Modelo de reutilización y diagnóstico:** inventario de elementos reutilizables/no reutilizables, compatibilidad por modelo/nodo/pool/unidad/cuenta/regla, identidad y origen/versionado. Pruebas de diferencia de estudios.
2. **RP-01B — Compatibilidad con la navegación actual:** probar entrada a Primario con partidas de Recursos pendientes, sin crear nuevos estados/puertas; identificar diferencias entre períodos y presentarlas en la cobertura y diagnóstico ya existentes del Primario.
3. **RP-01C — Adopción asistida en T05/T08:** selector origen, vista previa granular y guardado seguro/idempotente solo de configuraciones compatibles; resolución primaria automática por cuenta después de adoptar.
4. **RP-01D — Secundario/T11, invalidez y T12:** diagnóstico de nuevos auxiliares/rutas y referencias obsoletas, cierres y reaperturas consistentes, historial de ejecuciones intacto.
5. **RP-01E — Aceptación Windows local:** suite .NET y recorrido WPF ES/EN, resolución de incidencias, accesibilidad; no marcar completado por documentación o pruebas estáticas.

## 8. Casos mínimos de aceptación

- **A:** enero con cinco cuentas/reglas y dos auxiliares; febrero con las mismas cuentas y diferente coste. Seleccionar origen una vez, aplicar automáticamente reglas ya adoptadas, **sin** reintroducirlas, calcular febrero sin reescribir enero.
- **B:** febrero tiene una cuenta adicional sin reparto en Recursos: permite entrar a Primario como ahora, intenta la cobertura con reglas vigentes; si persisten partes sin cubrir, no permite **cerrar** Primario.
- **C:** una cuenta nueva sin clave compatible muestra incidencia en Primario, permite definirla o regresar a Recursos y no deja cerrar Primario ni entrar a Secundario hasta tener cobertura válida.
- **D:** nueva cuenta cubierta por prefijo válido: aplicación automática verificable, sin tener que asignar manualmente otra vez; precedencias y colisiones probadas.
- **E:** auxiliar nuevo o ruta sin salida: no cierra Secundario hasta aprobar ruta y comprobar viabilidad recíproca.
- **F:** modelo nuevo con nodo añadido/eliminado, rol y unidad cambiados: no copia reglas inválidas, exige revisión/mapeo explícito, no redistribuye porcentajes ni crea identidades nuevas sin decisión.
- **G:** cuenta desaparecida, cuenta renumerada, reaparición, cuentas cero y contrapuestas, duplicados, importe absoluto, driver mensual: se distinguen correctamente; no se inventan valores ni se pierden datos.
- **H:** reabrir y repetir adopción no duplica; conflictos no sobrescriben; validación de concurrencia, rollback y trazabilidad completas.
- **I:** estudio cerrado y cambios económicos posteriores: no modifican origen y conservan los resultados T11/T12 previos identificados como históricos cuando corresponda.
- **J:** pruebas de menú, «Siguiente», acceso directo y APIs: se puede abrir Primario con Recursos pendiente; no se eluden los controles de **cierre** de Primario/Secundario y las pantallas necesarias para reparar siguen accesibles.

## 9. Decisiones de detalle a cerrar al iniciar desarrollo

La **necesidad, prioridad y principio de reutilización guiada están confirmados**. Requieren cierre técnico antes de desarrollar: (a) reutilización con modelo distinto —mapeo o referencia—; (b) adopción antes/después de importar los datos que se compararán; (c) reversión y reglas inactivas; (d) tratamiento de asignaciones por `PartId` entre estudios. **Se descarta expresamente diseñar una nueva puerta de Recursos o un criterio nuevo de bloqueo de entrada a Primario.** Mantener los contratos actuales de cálculo/cierre sin adelantar D04/T15.

**Restricciones de operación:** documentación únicamente en esta rama; no ejecutar Actions, no usar GitHub-hosted runners sin aprobación expresa, no fusionar ni iniciar T15 sobre una dependencia no resuelta. La consolidación temporal multiestudio (acumulados enero-abril o doce meses) sigue **pendiente de decisión aparte** y no se incluye en RP-01.

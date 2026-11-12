# Usabilidad — decisión permanente del propietario (2026-10-07)

«Una aplicación de este tipo ya es lo suficientemente compleja en su metodología
como para complicarla en su uso y funcionamiento».

Usabilidad y sistema visual aprobado se aplican conjuntamente. La presentación sigue
[el sistema visual V1](../diseno/SISTEMA_VISUAL_VALDRIO_V1.md), sin reducir la claridad del flujo. Conservar todas las funcionalidades y
reglas económicas, sin obligar a conocer la arquitectura interna para trabajar.

- La aplicación proporciona el orden de trabajo y explica el siguiente paso.
- Mostrar solo el contexto del paso actual; las opciones avanzadas siguen accesibles.
- Antes de pedir datos, explicar su finalidad y los requisitos previos. No permitir
  llegar al final de una importación para descubrir que falta guardar el estudio.
- Cada campo debe tener ayuda contextual, disponible con ratón y teclado. Los
  requisitos críticos también son visibles: no esconderlos únicamente en tooltips.
- Distinguir explícitamente revisión, cambios pendientes y datos guardados. Mostrar
  destino y resultado del guardado. No perder formularios al cambiar de paso.
- Un bloqueo explica qué falta y cómo resolverlo. Los códigos técnicos son apoyo,
  no sustituyen la explicación. No inventar unidades, roles ni criterios económicos.
- Verificar el recorrido desde empresa vacía hasta importación guardada, reapertura,
  retroceso, corrección de errores y opciones avanzadas. Mantener localización y teclado.

T07 fue autorizada para implementación completa el 2026-10-07; el recorrido añade
Recursos después de Importación. Esta prioridad no elimina funcionalidades
ni sustituye las validaciones del dominio; reorganiza cómo se presentan y ejecutan.

## Edición directa en tablas — decisión del propietario (2026-10-09)

Todo lo que pueda consultarse o modificarse en una tabla se hará en la propia tabla.
No duplicar campos en formularios externos ni añadir paneles de edición para datos
que ya aparecen en filas. Priorizar simplicidad, controles por celda y un guardado
inequívoco que solo se habilite cuando existan cambios pendientes.

En reparto primario: comportamiento Fijo/Variable/Mixto mediante combo por cuenta.
Los porcentajes fijo y variable pertenecen a esa fila; solo se muestran como campos
editables para Mixto. Fijo equivale a 100/0 y Variable a 0/100. Guardar confirma los
cambios de la tabla y vuelve a desactivarse hasta la siguiente modificación.

### Complemento fijo automático — 2026-10-11

En una fila Mixto solo se edita el porcentaje variable. El porcentaje fijo se
muestra como dato calculado de solo lectura: 100 menos el porcentaje variable.
No pedir dos entradas para una misma decisión. Guardar conserva el mismo control
de cambios pendientes y la validación del rango.

### Consulta de resultados y desplazamiento — 2026-10-11

- Un cierre bloquea la edición, pero conserva selección, copia y desplazamiento
  en las tablas. Deshabilitar los editores, no la tabla completa.
- La rueda desplaza la página hasta entrar en una tabla con clic o teclado. En
  esa tabla, llegar a un extremo no transfiere el movimiento a la página. Un clic
  o foco fuera devuelve el desplazamiento a la página. Mantener estable la altura
  de la ayuda contextual para que pasar por campos no cambie el espacio visible.
- Seleccionar una aportación secundaria muestra su explicación automáticamente;
  la fila debe seguir resaltada aunque el usuario consulte el detalle.
- Primario: cuenta/recurso, nodo destino, importe adjudicado y comportamiento.
- Secundario: nodo de origen, nodo principal destino, importe adjudicado y
  comportamiento. Acompañarlo con el total por principal y la separación entre
  coste que conserva del primario y coste recibido de auxiliares. El detalle por
  cuenta se consulta en la tabla de explicación, conservando sus claves.
- La viabilidad debe explicar si las rutas permiten que el coste llegue a nodos
  principales. Los indicadores matemáticos permanecen como detalle técnico, no
  como la explicación principal del resultado.

### Claves y resultados automáticos — 2026-10-11

- En primario y secundario, agrupar nombre y criterio; situar guardar debajo de los destinos (decisión del propietario, 2026-10-15).
  Las tablas de claves y destinos deben aprovechar la altura de la ventana;
  evitar alturas fijas pequeñas y grandes espacios vacíos en la cabecera.
- «Asignar claves a cuentas» permite ordenar cada columna en ambos sentidos.
  Los importes se ordenan por valor numérico, incluidos los abonos negativos.
- Mostrar la viabilidad automáticamente con el cálculo secundario y al consultar
  un cierre guardado. No exigir una comprobación adicional. Si el cálculo falla,
  mostrar el diagnóstico en rojo; no confundir cualquier error con falta de
  viabilidad matemática de las rutas. Invalidar la información al cambiar datos.
- Situar cerrar/reabrir en una barra inferior fija de primario y secundario, junto al cálculo y descarte; mantener las acciones locales debajo de sus datos (decisión del propietario, 2026-10-15, sustituye la posición superior).
- Suprimir la tabla «Conciliación por recurso» de la pantalla secundaria por
  decisión del propietario. Conservar las comprobaciones económicas internas.

### Autoasignación de auxiliares — decisión del propietario (2026-10-11)

- En el reparto **secundario**, una clave puede distribuir parte de los costes al propio nodo auxiliar **sin solicitar autorización especial**. Esto es válido tanto para claves nuevas como para versiones históricas cuya propiedad persistida `SelfAllocationPolicy` indique `Forbidden`.
- La interfaz no expone una casilla para autorizarlo. Al crear claves secundarias nuevas, el valor serializado se establece en `Allowed` por defecto para conservar consistencia con el comportamiento. No se alteran retroactivamente los datos históricos.
- `AllocationKeyRules.ResolveSecondary` admite esas rutas y `ReciprocalGraphBuilder` las transmite a la matriz de cálculo con `AllowSelf=true`. El servicio de reparto recíproco sigue validando porcentajes, integridad, cobertura, conectividad a nodos principales, singularidad y estabilidad numérica. **Autoasignación permitida no equivale a grafo resoluble**.
- Para un reparto del 100 % al propio auxiliar sin salida hacia nodos principales, la validación de viabilidad del cálculo muestra `GRAPH_CLOSED_COMPONENT`; no se debe bloquear la simple elección de la clave ni prometer resultados.
- `ReciprocalFactorization` mantiene el contrato técnico genérico `AllowSelf` para llamadas externas/fixtures explícitos, sin convertirlo en una decisión que el usuario de VALDRIO deba tomar.
- Criterios de prueba: reparto mixto con autoprestación, clave histórica con valor `Forbidden`, circuito cerrado 100 % propio, conservación de costes y persistencia tras reabrir el estudio.

## Importación de saldos en una única moneda — decisión del propietario (2026-10-12)

- Toda importación normal de saldos usa exclusivamente la moneda funcional del análisis.
  La moneda propuesta a nivel de proyecto solo sirve para crear análisis; una vez
  elegido el análisis, la importación no permite cambiar la moneda.
- Retirar de la pantalla de importación los controles de moneda de origen,
  columnas de divisas, fechas de cotización, RateSet, tipos manuales e ID de tipo
  por fila. No esconderlos bajo otro desplegable.
- No obtener, inferir, calcular ni aplicar tipos de cambio durante la importación.
  Si los importes no están expresados en la moneda del análisis, el usuario debe
  normalizarlos previamente en su sistema de origen.
- Mostrar la moneda del análisis y esa condición de forma explícita. Un archivo
  sin metadatos de moneda no permite verificar por sí solo la divisa real de sus
  importes; no afirmar que se ha detectado una moneda inexistente en el archivo.
- Conservar la lectura y auditoría de lotes internacionales históricos (T06A)
  y sus conversiones congeladas. La compatibilidad de esos proyectos no convierte
  el flujo internacional antiguo en una opción disponible para nuevas importaciones.
- Mantener la regla de validación existente: un perfil normal con moneda distinta
  de la funcional no puede confirmarse sin puente FX; la interfaz nueva no permite
  crear ese perfil. Cualquier futura eliminación del motor heredado requerirá una
  migración auditada por separado.

## Lenguaje comprensible y reducción de campos — instrucción del propietario (2026-10-13)

- **Todo campo debe explicar:** qué significa, qué se introduce (unidad y ejemplo), de dónde procede o cómo se calcula y para qué se utiliza. Una microayuda operativa visible y una explicación contextual ampliable deben estar disponibles con teclado y ratón; no esconder requisitos críticos en tooltips.
- **Antes de añadir un campo, justificar que no pueda eliminarse, deducirse, precargarse o agruparse sin pérdida metodológica.** Mostrar primero lo esencial; funciones avanzadas solo cuando sean útiles.
- **No pedir por duplicado una magnitud que la aplicación ya deriva.** Las cantidades calculadas son de solo lectura; las propuestas adoptadas se copian a una sola fuente editable, preservando evidencia, versión y trazabilidad.
- **Lenguaje de usuario**, no nombres de clases ni fórmulas sin interpretación. Si se muestra un porcentaje, explicar numerador, denominador, significado y límites; un campo sin dato no se convierte silenciosamente en cero.
- **Validación de comprensión:** un analista no desarrollador debe poder realizar el flujo sin explicaciones externas; si no comprende una etiqueta, reducir pasos y mejorar microtexto antes de aceptarlo.
- Aplicación concreta en [T14 — Capacidad normal asistida](T14_CAPACIDAD_NORMAL_ASISTIDA.md). Esta regla también orienta futuros desarrollos, sin autorizar cambios funcionales ni eliminar validaciones existentes.

## Preguntas y respuestas para el analista desde T15 — decisión del propietario (2026-10-18)

**Requisito obligatorio para T15 y las pantallas de las fases posteriores:** mostrar preguntas de negocio útiles para el analista junto a respuestas concretas obtenidas de la información disponible hasta ese momento. La pantalla debe explicar qué puede concluir el usuario, además de presentar cifras y controles.

- Presentar una tabla sencilla «Pregunta / Respuesta», adaptada a la finalidad de cada pantalla. En T15 corresponde al centro de coste seleccionado; cambiar la selección debe actualizar todas las respuestas y conservar visible el contexto de análisis, período y unidad.
- Seleccionar preguntas relevantes para la fase y la información disponible. No copiar indiscriminadamente las mismas preguntas en todas las pantallas ni añadir configuraciones para poder consultarlas.
- Responder con importes, cantidades, unidades y una explicación breve, en lenguaje comprensible. Evitar términos técnicos como «objetos» cuando puede decirse «productos u órdenes».
- Obtener las respuestas de los mismos datos y resultados validados que utiliza la pantalla. No inventar cifras, deducir hechos desconocidos, presentar estimaciones como consumos reales ni convertir ausencia de datos en cero.
- Cuando una pregunta relevante todavía no pueda responderse, indicar qué información falta y dónde se registra o en qué fase estará disponible. Diferenciar «pendiente de datos», cero comprobado y «no aplicable»; no presentar como error un dato perteneciente a una fase aún no disponible.
- Mostrar las respuestas directamente; dejar fórmulas completas, desglose y procedencia en «Ver detalle» o acceso equivalente. Para incidencias corregibles, facilitar acceso al dato de origen cuando exista.
- Actualizar o invalidar las respuestas cuando cambien los datos, el centro, el análisis o el cálculo. No conservar respuestas antiguas como si fueran vigentes.
- Una conciliación parcial debe identificarse como parcial y explicar qué falta. No afirmar conciliación final sin conocer todos los destinos necesarios.
- Conservar sencillez, accesibilidad por teclado, traducciones ES/EN y formatos de cantidades/monedas coherentes con la aplicación.

**Aceptación:** para cada nueva pantalla, comprobar preguntas pertinentes, respuestas con datos completos, ausencia de datos, cero real, cambios de selección y resultados desactualizados. El analista debe distinguir qué sabe la aplicación, qué falta y qué puede revisar sin explicaciones externas.

La aplicación comienza en **T15**; esta decisión no ordena rediseñar retroactivamente T01–T14 ni modifica las políticas económicas pendientes. Véase la [aplicación concreta a T15](../tareas/T15.md).

## Ventana única de trabajo — decisión del propietario, 2026-10-20

Las pantallas funcionales, formularios completos, configuración ordinaria,
tablas extensas, gráficos principales y flujos de cálculo/edición se muestran
dentro de la ventana principal. Usar páginas, pestañas o paneles internos
no flotantes, conservando menú, contexto y navegación. Ampliar un gráfico
también se hace dentro de la aplicación.

Reservar ventanas emergentes para información adicional de campos, ayuda
contextual, mensajes, avisos, errores y confirmaciones necesarias. No introducir
`Show`/`ShowDialog` para presentar módulos funcionales. Los selectores nativos
de archivos conservan su función específica.

Antes de abandonar una edición, resolver explícitamente los cambios pendientes;
cancelar la navegación conserva la página y sus datos. Adoptar una propuesta de
calculadora no equivale a guardarla: el guardado continúa siendo explícito.
Una vista contextual pertenece al registro seleccionado y no reutiliza campos
temporales de otro registro.

La integración actual y sus límites están documentados en la
[auditoría de 20/10/2026](../evidencias/auditoria-20261020/README.md).


## Desplazamiento exterior y acciones fijas — decisión del propietario, 2026-11-03

- En primario, secundario y el resto de pantallas de trabajo, las tablas crecen con sus filas y el desplazamiento vertical pertenece a la página/pestaña. Seleccionar una tabla no debe atrapar la rueda: esta decisión sustituye la excepción de scroll interior del 2026-10-11.
- Las acciones inferiores de guardar/publicar/cerrar/cancelar permanecen visibles. Las acciones de una pestaña solo se muestran mientras esa pestaña está activa, conservando permisos, validaciones y comandos. La nueva posición fija de guardar claves sustituye su posición bajo los destinos.
- Mantener el desplazamiento horizontal necesario para columnas o diagramas. Los desplegables y editores de texto conservan su desplazamiento propio; no son una segunda zona de trabajo.


## Indicador global de actividad — obligatorio en toda pantalla (COR-02, 2026-11-05)

- **MUST**: cada operación interna perceptible de VALDRIO — actual o futura, en cualquier página del espacio de trabajo — debe anunciarse mediante el mismo indicador verde animado de la ventana principal (`GlobalActivityBar`). No crear barras locales inconsistentes ni depender del cursor ocupado como único feedback.
- Reutilizar `CompanyFileViewModel.IsProcessing` y los estados de actividad de cada workspace, encadenándolos sin sobrescribir la actividad de otros módulos. Notificar inicio y fin (incluidos fallo y cancelación) y desactivar el indicador al quedar inactiva la aplicación.
- Toda operación costosa debe realizar su trabajo fuera del dispatcher de WPF y reservar el hilo de interfaz para actualizar controles. La animación debe seguir moviéndose y la ventana debe responder; no basta con mostrar una barra inmóvil.
- Mostrar indicación contextual del paso ejecutado y errores recuperables cuando proceda. El usuario no debe confundir espera con bloqueo ni con éxito confirmado. Para tareas de duración indeterminada mantener animación; para las cancelables preservar cancelación.
- Verificar nuevos recorridos con operaciones lentas, fallidas y canceladas, incluidos carga de estructuras/análisis, importación, cálculos, guardados, exportaciones y componentes embebidos.

### Aplicación del indicador en páginas integradas — 2026-11-12

- Las operaciones que no pertenecen a un workspace con estado ocupado usan un ámbito
  `CompanyFileViewModel.BeginActivity()` (o su acceso desde el estudio), liberado con
  `using`. Los ámbitos se acumulan: terminar uno no apaga otro que sigue activo.
- Las páginas integradas reutilizan `RunActivityAsync`: deshabilita su contenido,
  impide salir mientras escribe y restaura la interacción al terminar, incluso con error.
  Las cargas iniciales se esperan mediante `InitializeAsync`; un render no implica
  que los datos hayan terminado de cargar.
- Capturar entradas en el hilo de interfaz, ejecutar almacenamiento/cálculos costosos
  fuera de él y aplicar el resultado al regresar. No envolver colecciones enlazadas
  o controles completos en `Task.Run`, ni bloquear el dispatcher esperando una tarea.
- En la calculadora de actividad, **Confirmar datos** guarda y vuelve a la tabla solo
  al éxito. **Guardar borrador** permanece en la calculadora. Un error conserva los
  datos y permite corregir/reintentar; la espera no admite otra confirmación.
- [Auditoría, alcance y verificaciones](../evidencias/actividad-global-20261112.md).

## Selectores con confirmación explícita (COR-06 y COR-10, 2026-11-05)

- En las listas de selección que requieren el botón «Cargar selección», «Abrir análisis» o acción equivalente, distinguir el elemento **seleccionado** del **realmente cargado**. La apariencia inicial del botón es secundaria (blanca), y cambia al verde primario en cuanto hay una selección distinta pendiente de cargar; vuelve al estado neutro tras cargar o revertir la selección.
- Mantener texto/ayuda accesible que explique que la selección sigue pendiente, sin depender únicamente del color. No cargar automáticamente al cambiar la selección.
- En «Análisis y periodo», refrescar los análisis disponibles al abrir el desplegable; no duplicar un botón de actualización. No sustituir el análisis activo ni perder campos pendientes por el mero refresco.

## Tablas legibles y adaptables (COR-03, 2026-11-05)

- Todas las tablas actuales y futuras deben ajustar las columnas a contenido y espacio disponible con anchuras mínimas y máximas razonables, conservando cabeceras legibles, desplazamiento horizontal cuando haga falta y acceso al texto completo (por ejemplo, mediante tooltip).
- Evitar que una descripción corta absorba la anchura que necesitan otras columnas. Revisar especialmente la tabla de Nodos, con «Función económica» separada de «Funciones» y del papel principal/auxiliar.



## Diseñador del proceso y propuestas secundarias — COR-05 (2026-11-05)

- «Estructura de costes → Revisar y finalizar» presenta un **mapa opcional** de los nodos agrupados por función económica (estructura, producción/actividad principal, comercial y nodos históricos sin clasificar) y por papel (principal/auxiliar). El mapa sustituye la preeminencia visual del árbol textual, que permanece accesible en una sección desplegable.
- Dos tipos de aristas, **sin equivalencia económica automática**:
  - **Secuencia operativa**, entre nodos principales: orden, bifurcaciones y rutas de producción/prestación; nunca añade automáticamente una transferencia de costes entre principales.
  - **Prestación auxiliar**, desde un nodo auxiliar hacia otro nodo principal o auxiliar: expresa un servicio y **solo propone destinos** de claves de reparto secundario; admite autoprestaciones y ciclos entre auxiliares porque su resolubilidad corresponde al cálculo recíproco existente.
- **Interacción principal (aprobada 2026-11-05):** al seleccionar una tarjeta del gráfico, o un nodo del selector de origen, se muestra **a la izquierda** un panel con «Nodos siguientes» (principales) o «Nodos atendidos» (auxiliares). Cada destino tiene una casilla marcada cuando existe la relación; marcar/desmarcar actualiza inmediatamente las flechas. No se exige activar un modo ni elegir origen/destino mediante dos clics. La conexión directa por dos clics se conserva, pero queda bajo controles avanzados opcionales.
- **Restricciones visuales y de cálculo:** una secuencia une un principal con **otro principal** y no puede crear ciclos de rutas; las opciones incompatibles se muestran deshabilitadas con explicación. Las prestaciones **solo nacen de auxiliares**, pueden dirigirse a principales o auxiliares de cualquier función económica e incluyen exclusivamente la **autoprestación del propio auxiliar**. Un principal no puede tener un enlace consigo mismo. La autoprestación permitida no significa que una asignación del 100 % a sí mismo sea económicamente resoluble: CM06/CM09 y las validaciones del secundario siguen siendo obligatorias.
- El usuario puede conectar, desconectar, mover dentro de la agrupación, autoordenar, ampliar/reducir, revisar ciclos, validar y exportar el diagrama; mantener operación por teclado y ratón. Las posiciones y relaciones quedan asociadas a la **versión de estructura** y sus identidades; al clonar se remapean los nodos y se exige nueva validación. En versiones publicadas, **solo lectura**.
- **Validar el mapa es una acción expresa e independiente del cálculo de costes**. No dibujar ni validar el mapa no impide continuar un estudio ni calcular repartos. Las referencias inexistentes, la semántica incorrecta y los ciclos de secuencia se notifican, sin afectar la integridad del motor económico.
- En «Reparto secundario», cuando exista un mapa validado, el usuario puede elegir una propuesta auxiliar y preparar un **borrador normal de clave**, con destinos marcados y pesos **sin definir**. El usuario debe introducir, validar, guardar y eventualmente publicar la clave con los controles económicos habituales. No se crean asignaciones ni claves publicadas, no se sobrescriben configuraciones o reglas existentes y puede descartarse la propuesta.
- La representación de flujos **no sustituye** la definición de inductores, porcentajes, grupos de componentes, reglas o claves económicas del cálculo de repartos. La distinción debe ser comprensible para un analista sin conocimientos técnicos.

## Gestión segura de versiones y nodos — COR-07 a COR-09 (2026-11-05)

- Un análisis abierto permite adaptar únicamente nodos **existentes** cuando el contrato conserva sus identidades; reabrir repartos primarios/secundarios **no habilita** añadir o eliminar nodos estructurales. Para hacerlo debe abrirse una **nueva versión editable** desde Estructura de costes. La interfaz no mostrará estados falsos de guardado ni controles de inserción estructural operativos dentro del análisis.
- Las acciones «Crear estructura» y «Crear versión editable» tienen consecuencias distintas. La navegación no debe crear borradores nuevos cuando se ha seleccionado una estructura para cargar. Evitar clones involuntarios; recuperar la versión editable ya existente cuando procede y distinguir inequívocamente nombre/estado/versión.
- Puede **eliminarse un borrador sin dependencias**, con confirmación y auditoría terminal verificable. Si tiene análisis, claves, versiones descendientes u otras referencias históricas, se rechaza y se explica el motivo. No borrar datos publicados ni ejecutar cascadas sobre históricos.
- Las versiones **publicadas** solo pueden **archivarse o restaurarse** mediante registro de auditoría independiente: su modelo, contenido canónico, hash, revisión y vínculos con análisis previos permanecen inmutables. Las archivadas dejan de ofrecerse para nuevos análisis, sin impedir consultarlas o restaurarlas desde la gestión de estructuras.

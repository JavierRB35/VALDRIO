# VALDRIO — Especificación de producto internacional

Versión 0.4 · 4 de octubre de 2026 · Documento de diseño para desarrollo mediante LLM.

## 1. Decisión de producto

VALDRIO es el nombre definitivo de la solución. Se construirá desde cero con identidad propia. Su contrato será esta especificación, los ejemplos de negocio aprobados y las decisiones que se incorporen al registro del proyecto. Tendrá su propio dominio, esquema de datos, motor, interfaz y formato de archivo.

El producto está diseñado desde el inicio para uso internacional. Idioma, formato regional, moneda funcional, moneda original y zona horaria son conceptos independientes. Las convenciones de España, el euro o el español no forman parte del motor. Se podrá añadir un idioma, un formato de entrada o una política económica sin reescribir los cálculos.

El cambio importante es de enfoque. No basta con pedir a un LLM que programe «subactividad», «ABC» o «trazabilidad». Cada capacidad debe convertirse en un contrato con datos obligatorios, reglas, operaciones, errores, resultados verificables y una explicación para el usuario. Este paquete establece esos contratos y divide la construcción en tareas acotadas.

Este documento es una especificación propuesta, no una aplicación implementada. Los ejemplos numéricos son sintéticos. La versión 0.4 incorpora nombre definitivo, localización desde R0, moneda tipada, conversión explícita y contratos de extensión. Las reglas económicas propuestas necesitan aceptación antes de uso definitivo.

## 2. Objetivo, alcance y usuario

El usuario principal es un controller o responsable de costes que necesita explicar cómo unos recursos económicos se transforman en costes de centros, actividades, productos, órdenes y otros objetos de coste. Debe poder trabajar sin programar, seguir una conciliación y defender cada resultado con sus datos y supuestos.

El flujo de trabajo de la primera edición profesional será importar sumas y saldos, revisar origen y signos, ajustar y separar recursos, configurar centros y claves, distribuir costes primarios, resolver prestaciones recíprocas, introducir capacidades y actividad, calcular tarifas y capacidad no utilizada, valorar consumos u órdenes y analizar costes y márgenes.

El sumas y saldos aporta importes contables. No aporta automáticamente cantidades fabricadas, horas de máquina, servicios entre centros, consumos de material ni inventarios. La aplicación identifica qué datos adicionales necesita cada método y permite introducirlos o importarlos. Los datos faltantes producen diagnósticos concretos.

### 2.1 Supuestos de partida

| Aspecto | Decisión propuesta |
| --- | --- |
| Plataforma | Windows, aplicación local de escritorio, inicialmente x64. |
| Trabajo | Un editor por archivo de empresa. Otros usuarios pueden trabajar con copias, sin fusión automática. |
| Finalidad inicial | Gestión de costes, capacidad, precios y rentabilidad. |
| Idiomas | Español e inglés completos desde R0; catálogo ampliable mediante recursos externos al motor. |
| Formatos | Región seleccionable independientemente del idioma; perfiles iniciales es-ES, en-US, en-GB y de-DE. |
| Moneda | Moneda funcional por estudio, código ISO 4217 explícito y moneda original conservada. Conversión manual/importada con tipo fechado y versionado. |
| Fechas y horas | Periodos como fechas sin hora; auditoría UTC; zona de visualización independiente. |
| Entrada de costes | XLSX y CSV con perfil de importación explícito. Un saldo de periodo y un acumulado son modalidades distintas. |
| Método secundario | Prestaciones recíprocas mediante sistema lineal. |
| Capacidad | Absorción analítica normalizada del componente fijo; variable según consumo observado. |
| Órdenes | Estimación y valoración de consumo real diferenciadas. |
| Venta | Licencia perpetua de versiones adquiridas y derecho de actualización separado. Proveedor por decidir antes de comercializar. |

Estas decisiones permiten avanzar. La simultaneidad desde varios equipos y la valoración contable de existencias requieren confirmar alcance, porque cambian arquitectura o reglas. Las opciones económicas se marcarán como propuestas hasta aceptar sus ejemplos; la primera implementación puede usarlas en estudios de demostración sin atribuirles aprobación del propietario.

### 2.2 Ediciones del producto y fronteras

| Entrega | Resultado utilizable |
| --- | --- |
| R0 · Demostrador verificable | Archivo local, estudio, importación, centros, claves, primario y reciprocidad con explicación y conciliación. Interfaz bilingüe, formatos independientes y moneda tipada. |
| R1 · Edición profesional | Añade capacidad, tarifas, materiales, recetas, órdenes, márgenes, escenarios, copias y distribución fiable. Alcanza el nivel metodológico 2. |
| R2 · ABC operativo | Actividades y pools, varios drivers y rentabilidad por producto, cliente y canal sobre hechos comunes. |
| R3 · TDABC y estándares | Capacidad práctica, ecuaciones de tiempo, referencias estándar y desviaciones. |
| R4 · Gestión ampliada | Comparación de métodos, simulación, presupuesto, previsión e integraciones elegidas. |

El proyecto completo tiene una ruta hasta R4. Cada edición tiene utilidad propia y criterios de salida. La entrega comercial inicial se limita a R1. Añadir todo el catálogo avanzado a la primera entrega impediría comprobar el núcleo con claridad y aumentaría el mantenimiento.

La valoración contable de existencias es una extensión separada. Necesita definir perímetro productivo, movimientos, producción en curso y marco aplicable. Un informe de rentabilidad con costes comerciales y de estructura no equivale a una valoración de inventario.

## 3. Stack y estructura de la solución

Se recomienda C# sobre .NET 10 LTS, WPF con MVVM, CommunityToolkit.Mvvm, SQLite local y EF Core 10 para persistencia. Se mantienen un solo lenguaje principal y un único proceso de aplicación. La solución se organiza en módulos internos con responsabilidades claras.

La decisión se apoya en la prioridad de escritorio Windows y autonomía local. .NET 10 dispone de soporte LTS hasta el 14 de noviembre de 2028 según la política oficial consultada. El SDK, los paquetes y sus parches se fijarán al iniciar el repositorio y se revisarán antes de cada entrega. [S01]

| Proyecto | Responsabilidad |
| --- | --- |
| Valdrio.Domain | Conceptos, unidades, estados, políticas e invariantes. Sin WPF, EF ni acceso a archivos. |
| Valdrio.Calculation | Motor puro, validación de grafo, reparto, solver, redondeo, tarifas y explicación. |
| Valdrio.Application | Casos de uso, permisos de escritura local, revisiones, transacciones y publicación de ejecuciones. |
| Valdrio.Infrastructure | SQLite, importación, exportación, copias, actualización y adaptador de licencia. |
| Valdrio.Localization | Recursos de texto, preferencias regionales, formatos y presentadores de diagnósticos. Sin cálculo económico ni WPF. |
| Valdrio.Reporting | Proyecciones e informes; contexto de idioma, formatos y moneda identificados. Sin recalcular costes con fórmulas propias. |
| Valdrio.Desktop | Vistas, navegación, ViewModels, formularios y mensajes de negocio. |
| Valdrio.Runner | Utilidad de desarrollo que ejecuta snapshots JSON y exporta resultados para comparación. |
| Valdrio.Tests | Pruebas numéricas, de aplicación, archivo real e instalación, separadas por categoría. |

La interfaz llama a casos de uso. Estos cargan entradas y llaman al motor. El motor devuelve resultados y diagnósticos sin conocer la base de datos. La infraestructura guarda entradas y ejecuciones. Las fórmulas de negocio se escriben en C# y se prueban fuera de la interfaz.

La base mantiene claves, restricciones, índices y transacciones. No contiene funciones o triggers de cálculo de costes. Los totales consultables se derivan de resultados publicados y no se recalculan mediante SQL distinto al motor.

La aplicación define puertos para almacenamiento, importación, catálogo de monedas, tipos de cambio, exportación, reloj, identidad local y licencia. Sus implementaciones se conectan en Desktop o Runner. Domain y Calculation no dependen de adaptadores, traducciones ni CultureInfo. El motor devuelve diagnósticos con códigos y argumentos tipados; la presentación construye el texto localizado.

### 3.1 Restricciones de SQLite

SQLite encaja con un archivo de aplicación local y poca administración. La edición simultánea sobre una carpeta de red o un archivo vivo sincronizado con OneDrive no forma parte del diseño. [S04]

Los importes y cantidades se guardarán como texto decimal canónico, con validación en C# y columnas TEXT explícitas. Microsoft.Data.Sqlite documenta que REAL pierde precisión para decimal; EF Core también tiene limitaciones de comparación y ordenación de estos tipos. Las sumas económicas autorizadas se harán en C#, después de filtrar por identificadores. [S05, S06]

La aplicación activará claves foráneas en cada conexión, usará revisiones explícitas para evitar sobrescrituras y probará las migraciones de esquema sobre archivos reales. Las copias se realizarán con una operación consistente de backup, incluyendo el estado de la base activa. [S07]

El cifrado requiere elegir y mantener una biblioteca que lo soporte. SQLite estándar no cifra por sí mismo. La decisión incluye distribución, recuperación de claves y restauración en otro equipo. No se prometerá un archivo cifrado antes de completar esa tarea. [S08]

### 3.2 Internacionalización desde el fundamento

El documento 05_Internacionalizacion_y_Extension.md especifica los contratos transversales. Cada preferencia tiene un ámbito diferente.

| Configuración | Qué cambia |
| --- | --- |
| UiLanguage | Textos de interfaz, estados, ayudas y diagnósticos. Primera entrega, español e inglés. |
| FormatCulture | Separadores y presentación regional; independiente del idioma. |
| SourceParseProfile | Interpretación del archivo importado; no toma la cultura actual del equipo. |
| FunctionalCurrency | Moneda económica del estudio y su ejecución. No cambia por idioma o región. |
| OriginalCurrency | Moneda de la fuente, conservada junto a importe y conversión. |
| DisplayTimeZone | Presentación de instantes UTC; no desplaza fechas de periodo. |
| ExportContext | Idioma, formatos, moneda, precisión y plantilla del informe. |

Se permite interfaz inglesa, formato español y moneda USD. Cambiar idioma o zona no cambia resultados ni hash económico. El primer inicio puede sugerir preferencias del sistema; se guardan por usuario. Abrir el archivo en otro equipo no cambia su moneda o datos.

Los textos se externalizan desde T01A mediante recursos .resx y claves semánticas. Se traducen mensajes completos con argumentos; el motor devuelve códigos y valores tipados. CI compara claves, parámetros y cobertura de cada catálogo publicado. Inglés es respaldo final, español es el segundo catálogo completo. Las nuevas traducciones amplían recursos y requieren revisión; no modifican fórmulas.

El selector funciona desde la primera ventana. Los layouts se prueban con textos largos y disposición RTL. Eso prepara idiomas de derecha a izquierda sin afirmar que ya estén traducidos. Las categorías del sistema guardan códigos; nombres y observaciones del cliente se conservan. [S13]

#### Fechas y números

Las fechas de periodo/documento se guardan DateOnly e ISO yyyy-MM-dd; auditoría y ejecución usan instantes UTC. Zona y fecha de negocio se separan. El primer alcance usa calendario gregoriano con presentación regional; otros calendarios requerirán adaptador explícito. [S14]

Una importación con 01/02/2026 necesita perfil día/mes o mes/día; el programa muestra la interpretación. El perfil del origen también declara coma/punto, miles, delimitador y moneda. Interfaz en inglés no obliga a interpretar un archivo español como estadounidense. Cambiar formato con texto pendiente exige confirmar/cancelar usando la cultura de edición inicial.

Persistencia e intercambio canónico usan decimal con punto, sin agrupación ni símbolos. Las vistas pueden mostrar 1.234,50 o 1,234.50. Comparaciones de identidad, IDs y desempates son ordinales; la ordenación visual localizable no cambia la matemática.

#### Moneda funcional, originales y conversión

Money contiene importe y código ISO 4217. La ejecución trabaja en una moneda funcional, con originales en una o varias monedas y conversión explícita previa. No se suman nominalmente EUR y USD. Una moneda de origen distinta exige tipo positivo, fecha, orientación, versión y fuente.

El catálogo versionado define unidades menores. Las pruebas incluyen EUR/USD con 2 decimales, JPY con 0 y KWD con 3. El informe utiliza esa escala; el ledger interno y las tarifas mantienen su propia precisión. SIX publica los metadatos oficiales. [S15]

Formato monetario recibe moneda del dato y separadores de la cultura. USD 1234.50 puede mostrarse como 1.234,50 USD o 1,234.50 USD sin cambiar divisa. No se usa ciegamente un formato regional que introduzca otra moneda.

Los tipos iniciales son manuales/importados. Quote significa unidades de destino por una unidad de origen. Un ejemplo sintético de 100 USD a 0,90 EUR/USD aporta 90 EUR. Se preservan original, convertido, tipo y ajuste de precisión. El snapshot congela el conjunto aplicado; una actualización no cambia el histórico. No es necesaria red para calcular.

El puente de conversión concilia originales por moneda y funcional después de convertir. Los repartos siguientes concilian en funcional. Diferencias de cambio o conversión no se llaman subactividad. Cambiar moneda económica de un estudio calculado requiere otro escenario, no un selector visual.

Los informes reciben ExportContext. CSV/JSON canónico conserva valores invariantes; vistas humanas usan formatos explícitos; XLSX distingue número, fecha y texto, con representación canónica para valores que no quepan exactamente en una celda numérica. Las exportaciones indican ejecución, moneda y precisión.

### 3.3 Extensión, volumen y colaboración

El sistema es un monolito modular con puertos. Nuevo idioma, catálogo; nuevo formato contable, adaptador; nuevo proveedor de cambio, RateSet; nuevo método, política económica y corpus; nuevo informe, proyección de hechos publicados. Esas ampliaciones no necesitan reescribir el solver. Los contratos de módulos, snapshots y archivos se versionan y se prueban.

El crecimiento de volumen se verifica mediante cargas, trazas dispersas, factorización reutilizada, consultas acotadas, paginación y cancelación. Los idiomas no duplican hechos económicos. Domain y Calculation tienen pruebas que bloquean dependencias de UI, traducción, SQL y proveedores.

La abstracción de repositorio permite otra persistencia. No convierte SQLite local automáticamente en colaboración simultánea. Ese modo requiere concurrencia, permisos y despliegue con su ADR y pruebas, conservando el motor. La modalidad inicial mantiene autonomía local.

La política de gestión es independiente del país. Valoración contable, fiscalidad, plan de cuentas y calendario laboral se seleccionan cuando corresponda; no se fijan a España en el núcleo. Internacionalización y cumplimiento normativo son alcances distintos.

## 4. Modelo conceptual y datos

### 4.1 Cinco ámbitos que no deben confundirse

| Ámbito | Datos y comportamiento |
| --- | --- |
| Empresa | Moneda, catálogos, centros físicos, unidades, productos y dimensiones. |
| Versión de modelo | Nodos de cálculo, pools, claves, recetas y políticas metodológicas publicadas. |
| Estudio | Periodo económico, finalidad y escenario; reúne recursos y actividad de ese contexto. |
| Revisión de entradas | Fotografía editable de importes, ajustes, cantidades y selección de modelo. Cada cambio aumenta Revision. |
| Ejecución | Snapshot y resultados inmutables con versiones de algoritmo, esquema y política decimal. |

Cambiar un maestro no modifica un modelo publicado. Cambiar una clave crea una nueva versión de modelo. Cambiar actividad o recursos genera una revisión del estudio. Volver a calcular genera otra ejecución. Una orden cerrada sigue ligada a la ejecución y receta con las que se valoró.

### 4.2 Entidades y relaciones mínimas

| Grupo | Entidades principales |
| --- | --- |
| Identidad | Company, UnitDefinition, PhysicalCenter, Product, DimensionMember. |
| Configuración | ModelVersion, CalculationNode, CostPoolDefinition, AllocationKeyVersion, AllocationKeyLine, PrimaryRule, SecondaryRule, RecipeVersion, RecipeLine. |
| Entradas | Study, InputRevision, ImportBatch, ImportedRow, Resource, ResourceAdjustment, ResourcePart, DirectAssignment, ActivityObservation, CapacityObservation, ConsumptionFact. |
| Resultados | CalculationRun, RunSnapshot, AllocationFact, ReciprocalDiagnostic, RateResult, CapacityVarianceFact, TerminalCostFact, Reconciliation, ExplanationLink. |
| Operación | AuditEvent, FileSchemaVersion, BackupRecord, LicenseEntitlement. |

Cada entidad tiene un identificador estable generado al crearla. Los códigos editables y el orden visual no son claves. Los códigos activos de centros o productos son únicos en su ámbito. Una línea de clave identifica su origen, destino y versión; no se interpreta mediante la posición de una fila de tabla.

Un centro físico puede tener distintos nodos lógicos si presta servicios y también recibe coste como destino final. Inicialmente cada nodo es principal o auxiliar. No se añade un tercer papel «mixto» sin definir sus ecuaciones y salidas.

Un recurso puede dividirse en partes. Cada parte mantiene su origen, comportamiento fijo o variable, unidad del driver, horizonte y forma de imputación. La suma de partes, exclusiones y pendientes coincide con el recurso ajustado. Un recurso mixto se descompone mediante importes o una regla aprobada; el programa no estima el componente fijo a partir de una casilla.

Directo o indirecto describe la relación con un destino concreto. Un coste directo al centro puede ser indirecto al producto. La función producción, comercial, estructura u otra se guarda separada del papel principal o auxiliar.

## 5. Experiencia de trabajo y estados

### 5.1 Pantallas y acciones

| Pantalla | Acciones y resultado visible |
| --- | --- |
| Inicio y archivos | Crear, abrir, copiar y restaurar. Muestra empresa, compatibilidad y última copia. |
| Modelo | Editar centros, unidades, pools, claves y recetas. Publicar o duplicar una versión con diagnóstico. |
| Estudios | Crear periodo y escenario; seleccionar una versión; clonar entradas para comparación. |
| Importación | Seleccionar archivo, hoja y columnas; previsualizar signos, periodos, errores y totales; confirmar lote completo. |
| Recursos | Consultar origen, ajustar con motivo, dividir, excluir y asignar. Indicador de cobertura por recurso. |
| Repartos | Vista de claves y asignaciones; detalle por recurso y por centro; explicación de fracciones. |
| Actividad y capacidad | Introducir normal, práctica, teórica y real cuando la política las necesite. Unidades y periodo visibles. |
| Cálculo | Validar, ejecutar o cancelar; ver etapa, errores y advertencias; publicar resultados completos. |
| Resultados | Primario, secundario, tarifas, capacidad, conciliación y «Explicar este importe». |
| Productos y órdenes | Recetas por unidad, lote u orden; estimación; registro de consumo real; cierre ligado a una ejecución. |
| Rentabilidad | Venta, coste del perímetro elegido, margen, recargo y contribución; comparación de escenarios. |

La interfaz debe indicar qué falta para calcular y enlazar directamente al dato. Un error «actividad variable positiva sin cantidad observada» es más útil que «datos incorrectos». Debe distinguir dato ausente, cero válido, advertencia y bloqueo.

### 5.2 Estados y cambios

| Objeto | Estados y reglas |
| --- | --- |
| Modelo | Borrador → Validado → Publicado → Archivado. Publicado es inmutable; se duplica para editar. |
| Lote importado | Preparado → Validado → Confirmado; también Cancelado o Rechazado. Solo Confirmado altera entradas. |
| Estudio | Borrador, Listo para calcular, Con cambios posteriores, Cerrado. El estado se deriva de revisión y validación. |
| Ejecución | Preparando, Calculando, Completada, Fallida, Cancelada, Obsoleta por revisión. Solo Completada contiene resultados publicables. |
| Orden | Borrador, Estimada, Con consumo real, Cerrada. Cerrar exige coherencia de cantidades y versión de tarifas. |

Las ejecuciones anteriores permanecen accesibles cuando cambia una entrada. La etiqueta «resultado actual» apunta a una ejecución completada cuya revisión coincide con la revisión vigente. El programa no reemplaza resultados al abrir un formulario.

Un modelo puede publicarse aunque un estudio concreto aún no tenga actividad, porque esos datos pertenecen al estudio. Una clave publicada y aplicable exige cobertura completa según su modalidad. Los estudios de trabajo permiten pendientes, pero una ejecución definitiva de R1 no oculta costes pendientes de asignación.

## 6. De la metodología al comportamiento del programa

El anexo 02_Contratos_Metodologicos.md contiene los contratos detallados CM01–CM16. Esta sección explica los puntos que necesitan decisiones de software más cuidadosas.

### 6.1 Importación, ajustes e incorporación

La importación tiene dos fases. Primero se interpreta el archivo en memoria y se produce una previsualización con filas originales, interpretación, avisos y conciliación. Después se confirma en una transacción. Si una fila tiene un importe inválido, no se convierte en cero. La cancelación o un fallo dejan intacto el estudio.

Cuenta y nombre son texto, conservando ceros iniciales. La convención de signos, el periodo, el tipo de saldo y el tratamiento de cuentas duplicadas forman parte del perfil de importación. No existe un filtro obligatorio de cuentas 6 y 7. Los ingresos pueden quedar como ventas, recursos negativos o exclusiones, según el destino aprobado; no se incorporan dos veces.

El importe original se conserva. Un ajuste es una diferencia firmada con motivo. El importe efectivo es original más ajustes. Un ajuste de cierre que se revierte en otro periodo es otra operación identificada, no la modificación retroactiva del original.

Cada parte del recurso tiene exactamente una ruta económica en una ejecución, aunque esa ruta tenga varios destinos. Las rutas son imputación directa, entrada a pool, exclusión o pendiente. Las partes suman el importe efectivo. Un material ya reconocido dentro del coste distribuido no vuelve a añadirse entero a una receta de producción real.

### 6.2 Claves, primario y cobertura

Una clave por porcentaje exige que las entradas sumen 100 %. Una clave por unidades define un total de unidades positivo y transforma cada peso en su participación. Partes iguales usa destinos seleccionados; importe absoluto es una operación de asignación específica de un recurso, no una plantilla reutilizable con significado ambiguo.

Las fracciones son no negativas. Los importes pueden ser negativos. Las unidades negativas o destinos repetidos generan errores. Las claves incompletas se pueden guardar en borrador; el programa no convierte un 80 % en un 100 % sin avisar y aprobar una regla distinta.

Cada resultado primario conserva recurso y parte de origen, regla y versión, destino, participación antes de redondear e importe adjudicado. Los totales de centros se obtienen sumando esos hechos. Se comprueba cobertura por recurso, no solo el total global, porque dos errores de signo opuesto pueden cancelarse.

### 6.3 Reciprocidad y contribución efectiva

Sean Q las participaciones de auxiliar a auxiliar, con origen en filas, y T las participaciones a destinos terminales, también con origen en filas. En la política inicial, los terminales del secundario son principales. La extensión de capacidad auxiliar añade salidas explícitas cuando está activada.

El cálculo resuelve `(I - Qᵀ) x = bA`. El coste de principales es `bP + Bᵀ x`, donde B son las columnas de T que corresponden a principales. Los flujos brutos entre auxiliares son circulación interna y no aumentan el coste de los recursos.

Antes de resolver se comprueban cobertura, destinos, autorreparto y salida alcanzable desde todos los auxiliares. Los circuitos cerrados se rechazan incluso si sus costes netos son cero. Después se comprueban pivotes, amplificación numérica, residuos y contribuciones. Un sistema problemático no cambia de método de forma automática.

La contribución de cada parte de recurso se calcula con la misma matriz y sus términos independientes. La factorización se reutiliza. El programa explica qué importe del recurso llega a cada principal, incluso cuando existen ciclos. El usuario puede ver los flujos internos, pero la explicación económica principal muestra contribuciones efectivas, que sí concilian.

El motor usa decimal de C# con la precisión disponible para el cálculo y una política versionada de cuantización. El «método exacto» significa resolver el sistema recíproco declarado, frente al método directo o escalonado; no significa precisión aritmética infinita. Las fracciones racionales del corpus sirven como referencia independiente para comprobar el solver.

### 6.4 Capacidad, actividad y costes no absorbidos

Hay que separar capacidad suministrada, actividad observada y consumo asignado a objetos. Cada cantidad lleva unidad, periodo, pool y procedencia. La política inicial calcula la tarifa fija normal `rF = Cf / qN` y la tarifa variable observada `rV = Cv / qR`, cuando los denominadores exigidos son positivos.

El consumo de cada objeto se multiplica por las tarifas de la ejecución. Las diferencias se registran como destinos económicos explícitos. Si el consumo asignado coincide con qR, el fijo aplicado es `Cf × qR / qN` y la diferencia fija es `Cf - fijo_aplicado`. Si faltan consumos para explicar la actividad observada, se registra actividad sin objeto; no se denomina toda esa diferencia «subactividad».

| Campo o resultado | Ejemplo de subactividad |
| --- | --- |
| Entradas | Fijo 1.000; variable 600; normal 100 horas; real 60 horas; consumos por objetos 40 y 20 horas. |
| Tarifas | Fija 10 por hora; variable 10 por hora. |
| Aplicación a objetos | Objeto A recibe 800; objeto B recibe 400. |
| Capacidad | Fijo no absorbido 400; variable sin asignar 0. |
| Conciliación | 1.600 = 800 + 400 + 400. |
| Interfaz | Muestra el resultado y permite abrir el desglose fijo, variable, actividad y recursos de origen. |

Con actividad real cero y normal positiva, una tarifa fija normal sigue siendo calculable. El fijo queda sin absorber. Variable positivo sin actividad exige revisar la entrada. La aplicación no muestra una tarifa cero para ocultar una división imposible.

Con sobreactividad, la política analítica normalizada puede aplicar más fijo del incurrido y presentar una diferencia negativa. Por ejemplo, fijo 1.000, variable 1.200, normal 100 y real 120 producen coste aplicado 2.400 y variación de capacidad -200. Ambos importes se muestran. Una política para valoración de producción requiere impedir esa sobreaplicación y definir un perímetro diferente; será un módulo separado. [S09, S10]

#### Capacidad de auxiliares

Hay dos políticas explícitas. En ThroughFlow, el auxiliar transmite el coste incorporado; la ociosidad se analiza donde exista un pool de capacidad medido. En OwnerOnceCapacity, cada pool auxiliar medido distingue fijo suministrado y variable consumido. El fijo se transmite según servicios efectivamente prestados sobre capacidad normal, con una salida de ociosidad o variación; el variable se transmite según servicio real.

La segunda política separa, una sola vez, el componente fijo propio del pool auxiliar en coste de servicio aplicado y diferencia de capacidad antes de resolver las prestaciones. Después transmite los servicios aplicados con las claves efectivas. Puede usar matrices diferentes si las claves de los componentes difieren. No se activa a partir de un porcentaje de reparto sin unidades de servicio. Cada origen de capacidad tiene un identificador que acompaña al coste; su ociosidad no vuelve a descontarse en un principal. Los servicios recibidos ya evaluados se aplican al consumo observado y conservan su naturaleza económica. Si faltan las cantidades necesarias, la aplicación informa «capacidad auxiliar no evaluada». No afirma medir la ociosidad de todos los recursos.

### 6.5 Precisión y residuos

La primera política propone 8 decimales para importes adjudicados, 12 para cantidades de entrada y hasta 18 para porcentajes introducidos. El solver mantiene decimal sin redondeo intermedio deliberado. La presentación ordinaria usa la unidad menor de la moneda, por ejemplo 0 en JPY, 2 en EUR/USD y 3 en KWD. Son reglas distintas.

El reparto de residuos se realiza por parte de recurso y conjunto de destinos con identificadores estables. Se conservan el valor matemático, el valor adjudicado y el ajuste de redondeo. Los totales se derivan de esas líneas adjudicadas, incluyendo la trazabilidad. El programa no redondea el total de un centro por un camino y sus recursos por otro.

Para 0,01 EUR repartido a tres destinos iguales, la vista conciliada de moneda adjudica 0,01 al primer identificador estable y 0 a los otros. Para -0,01 conserva la simetría de signo. El orden de una tabla en pantalla no decide quién recibe el residuo.

Si un origen tiene fracción de unidad menor, como puede ocurrir al convertir moneda, la proyección del informe redondea su total a par y adjudica las líneas a ese total. Registra la diferencia visual y conserva el ledger de 8. Los totales de informe se agregan desde estas proyecciones; no se confunden con un redondeo independiente del total matemático. CM04 define la operación y ejemplos.

Las tarifas pueden necesitar más decimales de los que se muestran. Una tarifa redondeada para imprimir no se utiliza como entrada de otra valoración. Cuando existe una variación negativa de sobreactividad, el redondeo admite destinos firmados y preserva la igualdad; no exige que todos los resultados sean positivos.

### 6.6 Productos, lotes, órdenes y materiales

Cada línea de receta define si el consumo es por unidad, por lote iniciado o por orden. Un lote de 50 unidades para una orden de 120 produce 3 lotes iniciados. Si cada lote necesita 2 horas de preparación, el consumo es 6 horas. Si cada unidad consume 0,1 horas de proceso, consume 12 horas. Ambos costes son diferentes y deben seguir existiendo cuando la orden se consulta después.

Una estimación utiliza receta y tarifas, pero no consume los recursos reales del periodo. La valoración de producción real utiliza cantidades y hechos de consumo. Las mermas y unidades indivisibles tienen reglas específicas; no se redondea una hora como si fuera una pieza.

El coste de un semielaborado puede transferirse a un producto terminado. Al analizar el periodo se elige una frontera de valoración. No se suman a la vez semielaborado, producto, pedido y cliente como si fueran costes nuevos. Para el mismo motivo, los componentes materiales externos de una estimación no se convierten en gasto real duplicado cuando el consumo ya está en los recursos importados.

### 6.7 Márgenes, escenarios y futuras técnicas

El margen sobre venta es `(venta - coste) / venta`; el recargo es `(venta - coste) / coste`. La contribución resta costes variables, por lo que cambia el perímetro económico. Un selector de denominador no sustituye a esa separación. Las devoluciones y denominadores cero tienen un tratamiento identificable.

Un escenario clona entradas o selecciona otra versión de modelo. La comparación exige misma moneda, periodo y frontera de coste, o advierte las diferencias. Se compara el efecto de claves, actividad y políticas, conservando la base de cada ejecución.

ABC divide los costes entre actividades o pools antes de aplicar varios drivers. Un centro de 1.000 dividido en pools de 600 y 400 sigue explicando 1.000. Aplicar el total del centro a cada driver duplicaría coste. TDABC añade capacidad práctica y tiempos medidos o supuestos aprobados. El estándar necesita referencias de precio y consumo permitidas para la producción real; no nace simplemente de una tarifa normal. [S11]

## 7. Motor, ejecución y trazabilidad

### 7.1 Contrato de ejecución

`Calculate(RunInput snapshot, CancellationToken) -> RunOutput` recibe todos los datos necesarios, con IDs, orden canónico y versiones. Devuelve hechos, tarifas, conciliaciones, diagnósticos y vínculos de explicación. No lee la base de datos, la hora del sistema, la cultura del equipo ni datos de red durante el cálculo.

El identificador de ejecución y la fecha se generan en la aplicación, fuera del motor. EconomicInputHash identifica valores, IDs, moneda, tipos y políticas relevantes; FullSnapshotHash identifica toda la procedencia congelada. Ambos usan representación canónica y excluyen preferencias visuales. Se registran por separado versiones de algoritmo, política y esquema. El mismo snapshot con las mismas versiones produce el mismo resultado económico y orden de resultados.

### 7.2 Publicación completa

La aplicación captura una revisión coherente, calcula fuera de la transacción de escritura y publica snapshot y resultados juntos. Al publicar comprueba que la revisión sigue vigente. Si ha cambiado, guarda la ejecución como histórica u obsoleta, sin convertirla en actual. Una cancelación deja un registro de intento, sin resultados económicos parciales publicados.

Cada conciliación tiene ámbito, etapa, entradas, salidas, diferencia exacta, tolerancia numérica si corresponde y estado. La conservación de importes adjudicados se exige de forma exacta a la escala interna. La tolerancia del solver sirve para validar su solución numérica, no para dar por válido un céntimo de coste sin destino.

### 7.3 Qué significa explicar un importe

Desde un coste final se podrán abrir las partes de recurso, ajustes, versión de clave, asignación primaria, transferencia secundaria efectiva, pool, tarifa, consumo y política que lo producen. Desde un recurso se podrán consultar todos sus destinos y las diferencias de capacidad correspondientes.

Cada explicación distingue importe de origen, adjudicado, ajuste de redondeo y circulación interna. Los ciclos se explican con factores efectivos. No se intenta enumerar un número infinito de caminos. Las capturas y exportaciones incluyen versión de modelo, ejecución, periodo y perímetro.

La auditoría local conserva qué cambió, valores anterior y nuevo, usuario local declarado, fecha UTC y motivo cuando procede. Facilita revisión, pero no es una prueba criptográfica de autoría ni un sistema multiusuario de permisos.

## 8. Hoja de ruta compartimentada

Cada tarea dispone de una ficha propia con dependencias, contrato, datos, operaciones, interfaz, pruebas, límites y criterio de terminado. La tabla siguiente describe el orden. No se estiman horas de un LLM; el avance se mide mediante ejecutables y evidencias verificables.

### 8.1 Fundamentos y demostrador R0

| ID | Entrega y criterio de salida |
| --- | --- |
| T00 | Acuerdo de finalidad y políticas. Ejemplos revisados, supuestos registrados y decisiones pendientes aisladas. |
| T01 | Repositorio, SDK y CI Windows. Compila WPF y publica un artefacto de desarrollo que abre en Windows. |
| T01A | Primera ventana bilingüe, preferencias independientes, recursos y pruebas de layout/traducción. |
| T02 | Dominio, unidades, precisión y corpus. El redondeo y la serialización pasan referencias independientes. |
| T02A | Money, catálogo ISO, moneda funcional, conversión versionada y fechas canónicas antes de persistencia. |
| T03 | Archivo de empresa y persistencia. Crear, abrir y recuperar archivo sin pérdida de decimal ni cruces de empresa. |
| T04 | Modelo versionado y centros. Publicar y duplicar sin modificar el publicado. |
| T05 | Estudios, revisiones y snapshot. Cambiar entrada invalida actualidad; histórico permanece. |
| T06 | Importación. Previsualización, perfil y confirmación atómica; duplicados y acumulados diagnosticados. |
| T06A | Perfiles internacionales y puente de conversión comprobados antes de ajustar o repartir. |
| T07 | Recursos, ajustes y particiones. Conserva origen y demuestra cobertura por parte. |
| T08 | Claves y asignaciones. Modalidades validadas; borrador incompleto visible; publicación estricta. |
| T09 | Primario. Totales y trazas derivados de hechos; residuos deterministas y conciliación por recurso. |
| T10 | Solver y diagnósticos. Referencias recíprocas, signos, ciclos cerrados y sensibilidad comprobados. |
| T11 | Secundario y explicación. Reutiliza factorización y conserva origen y comportamiento de cada parte. |
| T12 | Publicación y consulta. Snapshot, resultados y diagnóstico atómicos; cancelación y cambio de revisión probados. |
| T13 | Revisión R0. Se recorre desde archivo vacío hasta coste explicado, sin intervención técnica del propietario. |

### 8.2 Edición profesional R1

| ID | Entrega y criterio de salida |
| --- | --- |
| T14 | Actividad y capacidad. Registra normal, práctica y real con periodo, unidad y evidencias; faltantes distinguibles de cero. |
| T15 | Tarifas y absorción de principales. Fijo y variable separados; subactividad, sobreactividad y actividad sin objeto concilian. |
| T16 | Capacidad auxiliar. Evaluación única por propietario, servicios efectivos y terminales explícitos. |
| T17 | Materiales y recetas. Bases por unidad, lote y orden; ciclos de semielaborados bloqueados. |
| T18 | Estimaciones y órdenes. Mismo caso calcula setup y proceso; tarifa y receta quedan congeladas en cierre. |
| T19 | Consumos reales y frontera de valoración. Imputa recursos una vez; diferencia estimación, producción real y vistas agregadas. |
| T20 | Ventas, costes y márgenes. Perímetro identificable; margen, recargo y contribución comprobados. |
| T21 | Escenarios y comparación. Clona sin modificar origen; muestra diferencias y compatibilidad. |
| T22 | Informes y exportaciones. Resultado completo y explicación exportados con identidad de ejecución. |
| T22A | Informes bilingües, formatos y monedas variados, exportación canónica y cobertura internacional completa. |
| T23 | Copias, esquema y recuperación. Restauración en equipo distinto y fallo de migración probados. |
| T24 | Instalación, seguridad y licencia. Uso sin conexión; actualización no destruye datos; derechos de versión diferenciados. |
| T25 | Piloto y entrega R1. Caso real anonimizado y ejemplos aprobados; errores bloqueantes resueltos. |

### 8.3 Módulos posteriores y proyecto completo

| ID | Entrega y criterio de salida |
| --- | --- |
| T26 | ABC con pools. Varios drivers distribuyen una partición del coste; cada ejecución concilia. |
| T27 | Objetos y dimensiones. Producto, cliente, canal y periodo consultan los mismos hechos sin duplicación. |
| T28 | TDABC. Capacidad práctica y ecuaciones de tiempo tipadas, versionadas y explicables. |
| T29 | Estándares y desviaciones. Referencias aprobadas; precio y cantidad suman la diferencia total. |
| T30 | Métodos alternativos y simulación. Directo y escalonado se comparan como escenarios, con orden y límites explícitos. |
| T31 | Presupuesto, previsión e integraciones. Especificación de cada origen y supuesto; sin convertir previsiones en consumos reales. |
| T32 | Valoración de producción e inventario. Módulo opcional condicionado a finalidad y marco; movimientos y producción en curso concilian. |
| T33 | Fuentes contables adicionales. Adaptador independiente, staging y conciliación por origen. |

T10 es una prueba temprana de viabilidad numérica. Antes de extender interfaz o productos debe demostrar que el solver y la explicación funcionan sobre matrices y escalas representativas. T16 es una prueba metodológica específica; no se esconde dentro de una casilla de tarifa. T23–T25 cierran el producto, porque un cálculo correcto que pierde el archivo al actualizar no es una entrega profesional.

T26–T31 pueden definirse con mayor detalle de negocio después del piloto R1. Sus contratos iniciales y casos quedan preparados en las fichas. El LLM no debe implementar esas ampliaciones mientras el núcleo tenga fallos pendientes.

## 9. Cómo dirigir el desarrollo mediante un LLM

### 9.1 Unidad de trabajo

Una sesión aborda una sola tarea o un subconjunto declarado de su ficha. El LLM lee el estado del repositorio, la ficha, sus contratos y casos. Explica qué entregará y modifica el código necesario hasta disponer de una comprobación concreta. No reescribe todo el producto por cada conversación ni cambia una fórmula para hacer pasar una prueba.

El prompt maestro fija el producto y el procedimiento. Las fichas concretan el trabajo. Los contratos contienen la semántica. Los ejemplos contienen resultados esperados. El registro de decisiones contiene las modificaciones aceptadas. El archivo de continuidad indica qué está hecho y cuál es la siguiente tarea.

El paquete documental tiene precedencia de diseño, pero una instrucción posterior explícita del propietario puede modificarlo. El LLM actualiza entonces las decisiones y los ejemplos afectados antes de seguir, para que dos conversaciones no implementen políticas distintas.

### 9.2 Lo que debe entregar el LLM en cada tarea

| Elemento | Evidencia requerida |
| --- | --- |
| Código | Cambios acotados, sin dependencia circular ni fórmulas económicas en ViewModels. |
| Resultado visible | Ejecución del caso o pantalla utilizable con datos de demostración. |
| Pruebas | Casos aplicables con entrada y salida; ejecución real de comandos, errores y entorno registrados. |
| Integración | Funciona con las tareas anteriores; no basta un método aislado con mocks. |
| Documentación | Cambios de contrato, esquema, política o dependencias identificados. |
| Continuidad | Tarea cerrada o pendiente, evidencias, riesgos concretos y siguiente tarea. |

Una prueba que copie la fórmula del código puede repetir el mismo error. Por eso se incluyen referencias racionales, invariantes de conservación, casos negativos, reordenación de IDs, archivos SQLite reales y recorrido Windows. La autocertificación de un LLM no sustituye la revisión del ejemplo económico por una persona que conoce el negocio.

### 9.3 Papel del propietario, sin programar

El propietario decide qué significa el dato, acepta ejemplos de costes y prueba el flujo como usuario. El LLM compila, calcula, escribe pruebas, prepara paquetes y resuelve problemas técnicos. No se pedirá al propietario editar SQL, corregir código o ejecutar comandos de desarrollo para completar una tarea normal.

La infraestructura de trabajo sí debe permitir compilar y abrir WPF en Windows. Si el LLM trabaja en un entorno sin Windows, necesitará un agente de desarrollo autorizado o CI Windows y un mecanismo de revisión del ejecutable. Debe distinguir lo compilado, lo ejecutado automáticamente y lo observado en la interfaz. Este paquete no demuestra que ya exista ese entorno.

Al cerrar cada edición, el propietario recibe un ejecutable de prueba, un archivo de ejemplo, un guion corto de comprobación y los resultados esperados. La aceptación se formula en lenguaje de costes, por ejemplo «este recurso de 1.000 termina en 642,86 y 357,14», no «todos los tests están verdes».

### 9.4 Control de versiones y recuperaciones

La especificación aceptada, los casos y el código se versionan en Git. Cada tarea terminada tiene un cambio identificable y evidencia asociada. Antes de una modificación de esquema se conserva un archivo anterior y se prueba la recuperación. Las publicaciones a clientes se realizan desde versiones revisadas y firmadas, con notas de cambio y copia previa.

La gestión de tareas puede empezar con Markdown y Git; no requiere contratar un servicio ni construir un gestor de proyectos dentro del producto. El estado de una conversación no es el único lugar donde vive la decisión.

## 10. Verificación, piloto y límites de aceptación

### 10.1 Corpus numérico inicial

El paquete incluye 39 referencias numéricas y 15 casos de aceptación internacional. Cubre saldo de periodo y acumulados; ajustes; particiones; porcentajes incompletos; reparto de céntimos con signos; reciprocidad; autorreparto; circuito cerrado; sensibilidad; capacidad con actividad cero, inferior y superior a normal; actividad sin objeto; capacidad auxiliar; lotes; márgenes; pools ABC; tiempos TDABC; desviaciones estándar y valoración productiva limitada.

El archivo JSON diferencia entradas, resultado racional esperado, importe adjudicado cuando procede y diagnósticos. Un verificador Python independiente comprueba las referencias matemáticas del corpus. Esto valida los ejemplos del documento; las pruebas del motor C# deberán construirse y ejecutarse durante el proyecto.

### 10.2 Casos no numéricos obligatorios

- Cancelar una importación deja el estudio intacto; un saldo inválido no se convierte en cero.
- Repetir el mismo lote no duplica los recursos sin una decisión explícita.
- Cambiar entradas durante el cálculo no publica resultados como actuales.
- Reordenar centros en pantalla no cambia resultados ni adjudicación de residuos.
- Cerrar y abrir conserva todos los decimales y snapshots.
- Fallar al guardar no deja una ejecución económica parcialmente publicada.
- Restaurar una copia en otro equipo conserva empresa, modelos, estudios y resultados.
- Una actualización de esquema fallida permite recuperar el archivo anterior.
- Una orden cerrada mantiene tarifas y receta aunque se publiquen versiones nuevas.
- Sin conexión a Internet, abrir un archivo y calcular siguen funcionando bajo la licencia adquirida.

### 10.3 Aceptación internacional

Los casos I01–I15 exigen textos completos en español/inglés, región independiente, importación mediante perfil, fechas de negocio sin desplazamiento, UTC y hora local ambigua diagnosticada, moneda explícita con unidades menores variables, conversión congelada, exportación canónica e invariancia de resultados. Incluyen pseudotraducción larga, RTL de prueba y comprobación de dependencias. Son criterios del software futuro; aún no se han ejecutado contra .NET o WPF.

T01A establece estos contratos antes de formularios; T02A implementa monedas/fechas/tipos; T06A completa la entrada internacional; T22A verifica informes y toda la localización R1. Cada función posterior aplica el mismo contrato. El PDF o el oráculo numérico no sustituyen esa verificación.

### 10.4 Piloto y rendimiento

El piloto usa datos propios autorizados y preferiblemente anonimizados. Comprueba importación, interpretación de saldos, adecuación de claves y cantidades, resultado por recurso y centro, tarifas, una orden representativa y la explicación completa.

La tarea T10 medirá como cargas iniciales 10.000 partes de recurso, 100 principales y hasta 100 auxiliares. Son escenarios de ensayo propuestos, no capacidad prometida. Se registra equipo, tiempo, memoria, cancelación y tamaño de archivo. El presupuesto de rendimiento se decide con esa evidencia y necesidades reales. La aplicación no deja de responder durante el cálculo.

Una versión no se entrega si tiene diferencias sin explicar, datos perdidos, importes ocultos en pendientes, un solver que falla sin diagnóstico o un proceso de restauración no comprobado. Las capacidades metodológicas aplazadas aparecen en las notas de alcance.

## 11. Operación y mantenimiento reducido

El escritorio elimina la operación cotidiana de un servidor de cálculo propio. Persisten correcciones, parches del runtime, evolución de Windows, distribución, certificados y compatibilidad de archivos. El objetivo es que esos trabajos sean acotados y programables.

La licencia separa derecho a usar una versión y derecho a recibir otras posteriores. La falta de Internet o del proveedor no interrumpe el cálculo de una versión con derecho de uso válido. Las llamadas externas se limitan a activación o actualización según el esquema elegido; los recursos y resultados del cliente permanecen locales.

El instalador incorpora dependencias y guarda datos de usuario fuera de la carpeta de programa. El pipeline genera artefactos de desarrollo; solo una release revisada llega al canal de clientes. Una publicación autocontenida debe actualizar su runtime mediante nuevas entregas de la aplicación. [S01, S12]

Las copias incluyen base, versión de esquema y datos de recuperación necesarios. La aplicación ofrece exportación de diagnóstico con previsualización, evitando enviar saldos o nombres por defecto. Las obligaciones comerciales, privacidad y soporte se definen en las condiciones de venta; no desaparecen por utilizar un ejecutable local.

## 12. Decisiones económicas que deben resolverse

No hace falta resolver todo el negocio antes de crear el repositorio. Sí hay que aceptar la regla pertinente antes de usar sus resultados como definitivos. El archivo 03_Decisiones_y_Diccionario.md recoge responsables, estado, propuesta y tarea afectada.

| Decisión | Propuesta y momento de resolución |
| --- | --- |
| D01 · Finalidad | Gestión de costes y pricing en R1. Confirmar si la valoración de inventario debe adelantarse a T32. |
| D02 · Colaboración | Un editor local por archivo. Confirmar antes de T03 si hay edición simultánea desde varios equipos. |
| D03 · Fuente contable | Definir signos, saldo de periodo/acumulado y tratamiento de ingresos con un archivo de ejemplo antes de T06. |
| D04 · Capacidad | Aprobar ejemplos fijo/variable y normal/real antes de T15; indicar cómo se determina la actividad normal. |
| D05 · Auxiliares | Identificar qué auxiliares tienen servicios medibles y capacidad propia antes de T16. |
| D06 · Recetas y lotes | Confirmar lote parcial, setup, mermas y piezas indivisibles con dos órdenes reales antes de T17–T18. |
| D07 · Perímetro de márgenes | Elegir capas producción, comercial y estructura y ubicación de costes sin objeto antes de T20. |
| D08 · Cifrado y recuperación | Elegir necesidad de cifrado, credenciales y recuperación antes de distribuir archivos reales en T24. |
| D09 · Venta | Elegir proveedor, derechos y dispositivo/usuario antes de T24 comercial. |

Las decisiones técnicas ordinarias recaen en el desarrollo. El propietario no necesita elegir el algoritmo de factorización, nombres de clases o estrategia de índices. Las decisiones económicas se presentan como ejemplos de entrada y resultado, con alternativas y consecuencias claras.

## 13. Documentos, precedencia y punto de arranque

El paquete contiene esta especificación, 16 contratos, diccionario y decisiones, esquema y contratos de aplicación, especificación internacional y de extensión, 38 fichas —T00–T33 y T01A/T02A/T06A/T22A—, 39 referencias numéricas, 15 casos internacionales, verificadores documentales, plantillas de continuidad y el prompt maestro.

El prompt de inicio pide abordar T00 y T01 después de leer el paquete, y T01A antes de construir pantallas funcionales. T00 registra las propuestas que pueden usarse en demostración y las decisiones económicas que todavía necesitan aceptación. T01 entrega una aplicación WPF mínima que abre un archivo de ejemplo y dispone de CI. Las tareas siguientes avanzan de una en una según las dependencias.

Este enfoque convierte el objetivo amplio en un conjunto de entregas comprobables. La precisión metodológica se materializa mediante datos y contratos; la autonomía del LLM se acota mediante resultados esperados, verificación e historial de decisiones.

## 14. Fuentes y alcance de la evidencia

Las fuentes siguientes sustentan decisiones específicas. La arquitectura y los ejemplos son propuestas propias para el nuevo producto. Se consultaron fuentes primarias; las versiones deben verificarse de nuevo al implementar.

| Ref. | Fuente y uso |
| --- | --- |
| S01 | Microsoft. Política de soporte de .NET. https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core — versión LTS, parches y despliegue autocontenido. |
| S02 | Microsoft. WPF. https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/ — interfaz Windows. |
| S03 | Microsoft. CommunityToolkit.Mvvm. https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/ — soporte MVVM. |
| S04 | SQLite. Usos apropiados. https://www.sqlite.org/whentouse.html — formato local y límites de colaboración. |
| S05 | Microsoft. Tipos en Microsoft.Data.Sqlite. https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/types — decimal y representación TEXT. |
| S06 | Microsoft. Limitaciones EF Core SQLite. https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations — consultas, concurrencia y cambios de esquema. |
| S07 | SQLite. Online Backup API. https://www.sqlite.org/backup.html — copia consistente. |
| S08 | Microsoft. Cifrado SQLite. https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/encryption — dependencia de una biblioteca con cifrado. |
| S09 | BOE. Plan General de Contabilidad, NRV 10. https://www.boe.es/buscar/act.php?id=BOE-A-2007-19884 — separación de finalidad contable y gestión. |
| S10 | BOE e ICAC. Resolución de 14 de abril de 2015. https://www.boe.es/buscar/act.php?id=BOE-A-2015-4394 — capacidad y perímetro de producción. No implica certificación del producto. |
| S11 | Kaplan y Anderson. Time-Driven Activity-Based Costing, HBS 04-045. https://www.hbs.edu/faculty/Pages/item.aspx?num=15805 — capacidad práctica y tiempos. |
| S12 | Microsoft. Publicación .NET. https://learn.microsoft.com/en-us/dotnet/core/deploying/ — preparación de entregables. |
| S13 | Microsoft. Localización WPF. https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-globalization-and-localization-overview — recursos y disposición internacional. |
| S14 | Microsoft. DateOnly/TimeOnly. https://learn.microsoft.com/en-us/dotnet/standard/datetime/how-to-use-dateonly-timeonly — fechas de negocio independientes de instantes. |
| S15 | SIX. ISO 4217. https://www.six-group.com/en/products-services/financial-information/market-reference-data/data-standards.html — catálogo oficial y unidades menores. |

Los contratos y ejemplos definen VALDRIO. Las fuentes normativas se aplican al módulo y jurisdicción elegidos; no acreditan cumplimiento contable internacional.

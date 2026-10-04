# Prompt maestro — Desarrollar el nuevo VALDRIO desde cero

Versión 0.4 · 4 de octubre de 2026.

Utiliza este prompt junto con VALDRIO_Paquete_Desarrollo_v0_4.zip. Descomprime el paquete y conserva sus documentos, tareas, casos y plantillas en el repositorio nuevo. El informe explica el producto; los contratos y fichas concretan la implementación. VALDRIO se diseña desde cero, con identidad propia y uso internacional desde el inicio.

## 1. Encargo y papel

Eres responsable de implementar VALDRIO, nombre definitivo de una aplicación nueva de costes. Actúas como ingeniero de software y traduces una especificación económica a código verificable. El propietario conoce el negocio y no programará. Debes preparar código, pruebas, ejecutables y diagnóstico; sus decisiones se solicitan mediante ejemplos económicos y acciones de usuario.

Crea solución, dominio, motor, interfaz y esquema propios a partir de los contratos de este paquete. Toda referencia de producto, namespace, instalación, ejemplo e informe usa VALDRIO o Valdrio según el contexto. T33 incorpora fuentes contables adicionales mediante adaptadores; no condiciona el núcleo.

El objetivo es transformar recursos económicos de sumas y saldos en costes explicables de centros, servicios, productos, órdenes y otros objetos. Debes mantener conservación por origen, prestaciones recíprocas, política explícita de capacidad, actividad, residuos y perímetro de margen. El software debe mostrar por qué produce cada cifra.

No conviertas nombres de técnicas en implementación implícita. «Subactividad», «ABC», «TDABC» y «estándar» requieren sus datos, fórmulas y diagnósticos. No inventes consumos, tiempos, capacidades ni significados de saldos porque parezcan plausibles.

## 2. Documentos que debes leer y mantener

El paquete contiene estas fuentes.

1. especificacion/01_Producto_y_Hoja_de_Ruta.md — objetivo, alcance, stack, ediciones y secuencia.
2. especificacion/02_Contratos_Metodologicos.md — contratos CM01–CM16 con fórmulas, localización, monedas y aceptación.
3. especificacion/03_Decisiones_y_Diccionario.md — decisiones de negocio y técnicas, con estado.
4. especificacion/04_Datos_y_Contratos_de_Aplicacion.md — entidades, operaciones y publicación.
5. especificacion/05_Internacionalizacion_y_Extension.md — preferencias, recursos, perfiles, monedas, fechas, puertos y dirección de dependencias, obligatorio para toda tarea.
6. tareas/00_INDICE.md y ficha de tarea — 38 fichas con dependencias, subentregas y terminado.
7. casos/casos_metodologicos.json — 39 referencias numéricas sintéticas con salidas esperadas y diagnósticos.
8. casos/casos_internacionales.json — 15 criterios de aceptación para la futura aplicación; debes convertirlos en pruebas y ejecutarlas.
9. casos/catalogo_monedas_ejemplo.json — subconjunto de cuatro monedas para pruebas, distinto del catálogo monetario completo de producto.
10. casos/verificar_referencias.py — verificador racional independiente de las referencias matemáticas.
11. plantillas — continuidad, evidencia, decisión y revisión del usuario.

Las instrucciones explícitas posteriores del propietario pueden cambiar alcance o políticas. Cuando eso ocurra, registra la decisión, actualiza contratos y casos afectados y crea versión nueva cuando cambie el resultado económico. No mantengas dos reglas contradictorias en conversaciones distintas.

Si falta un documento necesario para una fórmula, pide ese documento o resuelve la duda mediante un ejemplo concreto. Puedes avanzar con trabajo independiente de ese dato. No improvises su semántica. Si el propietario acepta una propuesta en una sesión, registra esa aceptación para evitar consultarla otra vez.

## 3. Stack y alcance inicial

C# sobre .NET 10 LTS, WPF, CommunityToolkit.Mvvm, SQLite y EF Core 10. Verifica documentación oficial y parches antes de fijar SDK y paquetes. Usa dependencias mínimas y conocidas; documenta versiones y licencias.

Solución modular en un proceso local.

- Valdrio.Domain para conceptos, unidades, políticas y estados.
- Valdrio.Calculation para el motor puro, solver, reparto, redondeo y explicación.
- Valdrio.Application para casos de uso, revisiones y publicación.
- Valdrio.Infrastructure para archivo, SQLite, importación/exportación, copias y licencia.
- Valdrio.Localization para recursos, preferencias y presentadores, sin WPF ni cálculos económicos.
- Valdrio.Reporting para proyecciones de resultados publicados con contexto internacional, sin fórmulas propias de costes.
- Valdrio.Desktop para vistas y ViewModels.
- Valdrio.Runner para snapshots JSON y comparación sin interfaz.
- Valdrio.Tests para referencias numéricas, integración, archivo e instalación.

La primera plataforma es Windows x64. Primera edición, un editor y una empresa por archivo, una moneda funcional por estudio/ejecución y originales conservados en distintas monedas con conversión explícita. Español e inglés completos desde R0; formato regional y zona seleccionables de forma independiente. Si se necesita edición simultánea desde varios equipos, revisa D02 antes de dar por definitivo el modo de colaboración.

El motor no conoce WPF, EF, SQL, red, rutas, cultura, hora ni usuario. No pongas fórmulas económicas en ViewModels, consultas o triggers. No añadas un servidor, SaaS, microservicios, código de IA en el motor ni un sistema genérico de plugins para anticipar ampliaciones.

Define puertos acotados donde se consumen, entre ellos IStudyRepository, IRunStore, IImportAdapter, ICurrencyCatalog, IFxRateProvider, IExportWriter, IBackupService, IClock e ILicenseProvider. Application consume contratos y las implementaciones se conectan en Desktop/Runner. Comprueba referencias permitidas en CI; evita ciclos Infrastructure/Reporting. No expongas DbContext a UI o a Calculation.

### 3.1 Internacionalización obligatoria desde los fundamentos

UiLanguageTag, FormatCultureTag, SourceParseProfile, FunctionalCurrencyCode y DisplayTimeZoneId tienen ámbitos independientes. Cambiar idioma traduce textos; cambiar región representa valores; cambiar moneda exige operación económica explícita. Ninguna preferencia visual altera EconomicInputHash, entradas o resultados publicados.

T01A sigue a T01 antes de pantallas funcionales. Usa recursos .resx/ResourceManager, catálogo neutral inglés y español completo, claves semánticas y mensajes enteros con argumentos tipados. Externaliza etiquetas, menús, validaciones, estados, diagnósticos, ayuda, informes, instalador y licencia. Los textos propios del cliente se conservan. Códigos de sistema e IDs son estables y no se traducen al persistir.

El servicio observable permite cambiar idioma y actualiza bindings WPF. Define pluralización ampliable; CI compara claves/argumentos y bloquea catálogos publicados incompletos. El respaldo inglés registra faltas, sin justificar una traducción incompleta. Comprueba pseudotraducción de al menos 40 % más longitud, disposición RTL de prueba, teclado y accesibilidad. Publicar otro idioma requiere catálogo completo y revisión. No uses LocBaml como base para .NET actual.

La edición de valores captura la cultura al empezar. Cambiar región requiere confirmar o cancelar esa edición con su cultura anterior. Importar utiliza el perfil del origen, nunca la cultura actual de UI. Declara separadores, agrupación, patrón de fecha, encoding, delimitador, signo y moneda. Una fecha o número ambiguos sin perfil bloquean preparación; no reemplaces comas globalmente ni adivines por país.

DateOnly representa fechas de negocio y se persiste yyyy-MM-dd gregoriano. Auditoría y ejecución usan instantes UTC; la visualización aplica la zona elegida mediante adaptador. Cambiar zona no desplaza el periodo. Hora local ambigua/inexistente requiere resolución explícita. IClock está fuera del motor. Calendario fiscal y laboral son datos de negocio distintos de idioma/región.

Money contiene Amount y CurrencyCode ISO 4217. Sumar o restar exige misma moneda. CurrencyDefinition versiona unidades menores y su vigencia; cada ejecución congela metadatos. La proyección conciliada usa quantum = 10^-MinorUnitDigits, con JPY 0, EUR/USD 2 y KWD 3 en el catálogo de prueba. Monedas sin unidad menor numérica requieren política expresa. Tarifas no se recortan a esa escala visual.

CurrencyFormatter recibe Money, Culture y DisplayPolicy. Código ISO visible en la primera edición. USD 1234.50 se muestra como 1.234,50 USD con es-ES; EUR 1234.50 se muestra como 1,234.50 EUR con en-US. Cambiar región nunca sustituye la moneda por la propia de esa cultura ni aplica FX.

Cada estudio fija moneda funcional. Originales y ajustes en otra divisa requieren RateSet manual/importado con tipo positivo, fecha, fuente y orientación explícitas. Quote expresa destino por unidad de origen; Converted = Original × Quote. Cuantiza cada conversión a 8 con MidpointToEven y conserva original, tipo, convertido y diferencia. Efectivo funcional suma originales convertidos y ajustes funcionales. No se agregan originales nominalmente entre monedas. RateId se selecciona por importe; RevalueStudy aplica un conjunto nuevo con revisión, reconversión y revalidación atómicas.

Los tipos se congelan en snapshot. Una actualización crea otra versión/escenario, preservando históricos. IFxRateProvider prepara tipos fuera de Calculate; el motor no usa red. Tipo faltante, fecha inválida, orientación o moneda desconocida bloquean. Los ejemplos 100 USD × 0.90 = 90 EUR usan cotizaciones sintéticas. La política no implica cotización actual, promedio mensual o cierre automático.

Persistencia, snapshots y CanonicalExchange usan decimal invariante, fecha ISO y cabeceras/códigos estables. HumanLocalized declara idioma, cultura, moneda, fecha y precisión. ExportContext es explícito y Reporting consume una ejecución publicada. XLSX preserva cuentas como texto y acompaña importes de alta precisión de representación canónica cuando una celda numérica perdería dígitos. No prometas round-trip decimal de 28 dígitos mediante números de Excel.

Nueva traducción, fuente, moneda, proveedor o informe se incorpora con catálogo/adaptador, contrato y pruebas. Nuevo método económico necesita política, datos y versión. La estructura modular facilita ampliación funcional; el volumen se mide y la colaboración entre equipos se diseña aparte, con concurrencia y permisos. Una interfaz de repositorio no transforma SQLite en colaboración simultánea automática.

## 4. Reglas económicas centrales

### 4.1 Fuentes y clasificación

Cuenta y descripción son texto. Importación tiene preparación y confirmación separadas. El perfil declara columnas, signos, fechas, modalidad y duplicados. Saldo de periodo, saldo de cierre y diferencia de acumulados son modalidades distintas. No filtrar automáticamente cuentas 6/7 ni convertir errores a cero.

Conserva origen. Efectivo funcional = original convertido + ajustes funcionales firmados. Preserva originales y conversiones según CM16. Los ajustes tienen motivo y auditoría. Las partes de un recurso suman su efectivo y tienen una ruta única, directo, pool, excluido o pendiente. Una parte puede repartir su importe entre destinos, pero no entrar entera en dos rutas.

Directo/indirecto depende del objeto. Fijo/variable depende de driver, horizonte y rango. Un mixto se descompone explícitamente. Principal/auxiliar describe nodo; producción/comercial/estructura describe función. Un centro físico puede tener varios nodos/pools homogéneos.

### 4.2 Claves y primario

Porcentaje publicado suma exactamente 100 en sus entradas decimales. Unidades transforma pesos no negativos con total positivo en participaciones. Partes iguales utiliza destinos únicos. Importes absolutos corresponden a una parte concreta. No normalizar claves porcentuales incompletas.

En primario, bruto = importe de parte × participación. Adjudica residuos por origen con política versionada y conserva hechos. Los totales proceden de esos hechos. Una diferencia global cero no basta; cada origen debe conciliar.

### 4.3 Prestaciones recíprocas

Q tiene auxiliares origen en filas y auxiliares destino en columnas. B contiene salidas a principales. Resolver `(I - Q^T) x = bA`; final principal = `bP + B^T x`. Resolver en unidades de la moneda funcional.

Validar cobertura, diagonal según política, salida alcanzable, circuitos cerrados, singularidad, sensibilidad y residuo. Método inicial, LU decimal con pivoteo parcial determinista y RHS múltiples, sujeto a la puerta de viabilidad T10. No calcular una inversa como paso ordinario. No usar suma del vector como condición de validez. No cambiar a método directo para ocultar error.

Para cada parte r, resolver con bA_r y calcular `bP_r + B^T z_r`, o aplicar factores efectivos equivalentes. Reutiliza factorización si M es idéntica. Conserva naturaleza y evaluación previa de capacidad. Las circulaciones brutas no se suman como recursos nuevos.

Los umbrales concretos de PNUM-1 están en CM06. C09 contiene resultado racional, ledger adjudicado a 8 y proyección de moneda por fuente. Comprueba las tres semánticas; no fuerces que un total redondeado independientemente coincida con el agregado de sus contribuciones.

### 4.4 Capacidad y tarifas

Registra unidad, periodo, fuente y normal/práctica/teórica/real cuando proceda. Distingue actividad real observada y consumos asociados a objetos.

NormalizedManagement usa fijo propio de capacidad dividido por normal y variable observado dividido por real. Servicios recibidos ya evaluados se distribuyen según actividad real del pool destino, conservando naturaleza económica. Un fijo no evaluado como capacidad no se convierte automáticamente en coste variable.

Cuando consumos coinciden con actividad real, Cf=1.000, Cv=600, normal=100 y real=60 producen tarifas 10 y 10, aplicado 1.200 y capacidad 400. Si solo 40 de las 60 unidades tienen objeto, aplicado 800, capacidad 400 y actividad sin objeto 400. No mezcles esos dos destinos.

Normal positiva con real cero permite tarifa fija y todo el fijo sin absorber. Variable positivo con real cero exige aclaración. Sobreactividad analítica permite variación negativa explícita, no un ingreso nuevo. La valoración productiva limitada pertenece a otra política y módulo.

OwnerOnceCapacity evalúa cada capacidad propia una vez. En auxiliares separa primero servicio aplicado y variación; después resuelve servicios efectivos y propaga AssessmentId. No vuelve a medir como capacidad propia de otro centro el mismo coste de capacidad recibido. C20 prueba 1.600 = 800 aplicados + 500 de A + 300 de B. Si no hay servicios/capacidad medidos, ThroughFlow debe indicar capacidad auxiliar no evaluada.

### 4.5 Precisión y conservación

System.Decimal en cálculos; política PDEC-1 propone importes adjudicados a 8, cantidades de entrada a 12 y porcentajes a 18 decimales. Tarifas y solver mantienen precisión disponible. SQLite TEXT y JSON string para valores económicos; sin REAL ni double para importes autorizados.

BalancedRound conserva total de fuente, usa Floor, mayores restos y desempate por ID. Fuentes negativas con pesos positivos mantienen simetría de signo. Admite destinos negativos de variación conforme a CM04. Residuo técnico pequeño se identifica según política; una diferencia de negocio no se repara con redondeo.

Las vistas a moneda son una proyección conciliada identificada, no nuevas entradas. Si el total de origen tiene fracción de unidad menor, CurrencyProjection fija Target redondeado a par, adjudica mayores restos y muestra ProjectionDifference según CM04, conservando el ledger. Totales de centros se agregan desde contribuciones adjudicadas. No mezcles ledger de 8, proyección a unidades menores de la moneda y totales matemáticos como si fuesen intercambiables.

Conciliaciones de entradas, partes, primario, secundario y terminales. Los terminales son disjuntos en una frontera. No sumar producto, orden y cliente como tres costes cuando son tres vistas del mismo hecho. Inventario, ociosidad, periodo y pendientes deben tener destinos explícitos.

### 4.6 Productos y rentabilidad

Recetas distinguen por unidad, por lote iniciado y por orden. Lote 50, orden 120, setup 2 h/lote y proceso 0,1 h/unidad producen 6 y 12 h. A tarifas 30/20, coste 420 y unitario 3,5.

Estimación es teórica y no consume ledger real. Consumo real tiene evidencia y origen. Un material ya incorporado en recursos no vuelve a añadirse entero. Recetas de semielaborado sin ciclos en R1. Orden cerrada conserva RunId, receta y cantidades originales.

Margen sobre ventas, recargo sobre coste y contribución son conceptos distintos. Venta 200, variable 120 y fijo aplicado 30 dan margen 50/25 %, recargo 1/3 y contribución 80/40 %. Denominadores cero producen Undefined, no cero ficticio. El perímetro de coste se muestra.

ABC requiere partición de pools; TDABC necesita capacidad práctica y tiempos; estándares necesitan referencias aprobadas. Sus módulos siguen el núcleo. No renombres funciones R1 como técnicas que todavía no tienen datos y contratos implementados.

## 5. Persistencia, estados y operación local

Modelo publicado y ejecución completada son inmutables. Cada mutación de entradas aumenta revisión. Snapshot incluye datos descriptivos, moneda/metadatos, tipos, perfiles y reglas concretas, no referencias que consulten valores actuales. EconomicInputHash identifica valores y políticas relevantes; FullSnapshotHash identifica toda la procedencia congelada. Ambos usan representación canónica; ninguno depende de preferencias visuales. Conserva las versiones de algoritmo/política por separado.

Calculate recibe snapshot y devuelve resultados. La aplicación calcula fuera de transacción de escritura. Publica snapshot, hechos y estado en una transacción corta, con revisión esperada. Si cambió, conserva histórico sin marcarlo actual. Cancelación o fallo no publica datos económicos parciales.

Activar FK en conexiones y usar índices/ámbitos para impedir referencias cruzadas. Migrations sobre archivos reales y versión de esquema. No usar proveedor in-memory como única validación. Ordenación/comparación decimal autorizada y sumas se realizan en C#, después de filtros estructurales, sin CAST a REAL.

Backup consistente con API SQLite. Restaurar a ruta nueva conservando original y ensayar otro equipo. No copiar solo el .db activo ignorando WAL. Cifrado, si se decide, requiere proveedor y recuperación; no es una propiedad automática de SQLite.

Distribución con dependencias y datos fuera de carpeta del programa. CI genera artefactos de desarrollo; release revisada y firmada para clientes. Licencia de uso perpetuo y actualizaciones separadas; sin llamada obligatoria a Internet cada inicio. No incrustar claves privadas o secretos de proveedor. Datos económicos no se envían para activar licencia.

## 6. Forma de trabajar por tareas

Primero examina el estado real del repositorio. Identifica instrucciones locales, dependencias y herramientas disponibles. Si no hay Windows para abrir WPF, utiliza CI Windows autorizado y entrega evidencia de compilación y un artefacto comprobable. Distingue build, pruebas ejecutadas y revisión visual. No declares una pantalla probada por haber escrito XAML.

Usa las 38 fichas T00–T33 y T01A/T02A/T06A/T22A en el orden de dependencias del índice. Una sesión aborda una tarea o subentrega identificada. Puedes dividirla si es grande, preservando criterio de terminado y continuidad. No intentes implementar todo el producto en una respuesta ni reiniciar el diseño en cada sesión.

Para la tarea activa.

1. Lee ficha, contratos, casos y estado. Comprueba dependencias con evidencia, no solo etiquetas.
2. Indica entrega concreta y dudas que afecten realmente al resultado. Avanza con propuestas identificadas para demostración cuando no haya aceptación económica todavía.
3. Implementa código e integración, respetando reglas y versiones. No muevas lógica a UI para acelerar.
4. Ejecuta casos pertinentes y checks necesarios. Compara expected con salidas del motor, no con otra copia de su fórmula.
5. Comprueba integración con archivo y tareas anteriores; revisión de interfaz en Windows cuando corresponda. Verifica textos externalizados, valores invariantes, monedas tipadas y perfil de origen; repetir con otra presentación mantiene el resultado económico.
6. Actualiza estado, decisiones, evidencia y cambios de esquema. Prepara artefacto/ejemplo de usuario.
7. Termina con qué funciona, cómo se verificó, qué sigue pendiente y cuál es la siguiente subentrega.

Los casos esperados se versionan como especificación. Si fallan, no se actualizan para coincidir con el resultado observado sin demostrar por qué el esperado estaba equivocado y obtener aceptación de una regla económica modificada.

Usa revisiones de código y de ejemplos en una sesión separada cuando ayuden. La revisión debe leer el diff y la evidencia, buscar fallos e intentar contraejemplos. No hace falta un sistema de agentes ni infraestructura adicional para seguir este procedimiento.

No pidas al propietario programar, restaurar SQL o arreglar dependencias. Si el entorno no permite una comprobación, identifica el impedimento y prepara la alternativa concreta; no presentes lo no ejecutado como validado.

## 7. Ediciones y orden

R0, T00–T13 y T01A/T02A/T06A. Fundamentos internacionales, importación, ajustes, claves, primario, reciprocidad, ejecución y explicación. T10 valida matemáticas antes de seguir. T13 completa recorrido bilingüe con formatos independientes.

R1, T14–T25 y T22A. Capacidad, tarifas, auxiliares, recetas, órdenes/consumo, márgenes, escenarios, informes internacionales, recuperación, instalación/licencia y piloto. La primera entrega comercial exige estas tareas y ejemplos aceptados. Si hay política no evaluada, se indica el límite del modelo.

R2, T26–T27. ABC y rentabilidad multidimensional.

R3, T28–T29. TDABC y estándares con fuentes propias.

R4, T30–T31. Métodos alternativos, simulación, presupuesto, forecast e integraciones concretas.

Opcionales T32 inventario productivo y T33 fuentes contables adicionales. T32 requiere marco/jurisdicción explícitos, sin imponer normativa española al núcleo. Si el propietario lo adelanta, replanifica sin mezclar finalidad contable y gestión. T33 no es prerrequisito del núcleo.

## 8. Terminado y evidencia

Una tarea se cierra cuando el resultado de usuario/negocio funciona, código integra con lo existente, comprobaciones pertinentes pasan, datos sobreviven a reapertura cuando proceda, contratos/decisiones están actualizados y se conserva continuidad.

No basta código generado, compilación aislada, una captura de pantalla o «tests verdes». Los defectos numéricos se prueban contra referencias y propiedades; los de archivo mediante archivo real y fallo; los de UI mediante recorrido; los económicos mediante ejemplos revisados.

El verificador Python comprueba las 39 referencias numéricas sintéticas. Los 15 casos internacionales describen aceptación y deben implementarse/ejecutarse contra el software. Este paquete no demuestra que la aplicación C#, SQLite o WPF funcione. Incluye esa distinción en los informes de avance.

La primera edición debe demostrar que no pierde datos al importar/actualizar, no recalcula órdenes cerradas, no oculta pendientes, no depende de red para cálculo y puede explicar/conciliar cada resultado. También mantiene resultados al cambiar presentación, diagnostica fuentes ambiguas, congela FX y representa monedas según sus unidades menores. Performance se mide; no se anuncia una capacidad que no se haya ensayado.

## 9. Instrucción inicial para una sesión nueva

Si el repositorio aún no existe, empieza por T00 para registrar alcance y ejemplos de demostración, y después aborda T01 como siguiente tarea independiente. Crea estado y ADR iniciales. No implementes costes antes de preparar núcleo verificable.

Si el repositorio existe, lee ESTADO_PROYECTO, comprueba el último resultado y selecciona la primera tarea pendiente cuyas dependencias estén realmente terminadas. Respeta las decisiones ya aceptadas.

Para iniciar la sesión, utiliza este texto.

> Lee este prompt y el paquete documental. Comprueba el entorno y el estado del proyecto. Identifica la primera tarea o subentrega pendiente que se pueda ejecutar y llévala a un resultado comprobable. Para decisiones económicas pendientes, presenta ejemplos de entrada y salida y conserva las propuestas como tales. Completa el trabajo técnico autorizado que sea independiente. Entrega evidencia y continuidad sin declarar implementado lo que solo está diseñado.

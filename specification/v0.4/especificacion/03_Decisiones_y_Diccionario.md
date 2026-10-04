# Registro inicial de decisiones y diccionario

Versión 0.4. Este archivo se incorpora al repositorio nuevo y se mantiene con sus cambios. «Propuesta» significa que se puede implementar en demostración, identificándola, y se debe aceptar el ejemplo antes de usarla como regla económica definitiva. «Decidido técnicamente» evita pedir al propietario decisiones de programación ordinarias.

## 1. Decisiones de producto y negocio

| ID | Estado inicial | Propuesta y consecuencia | Resolución necesaria |
| --- | --- | --- | --- |
| D01 | Propuesta | R1 orientada a gestión, capacidad, pricing y rentabilidad. Inventario contable en T32. | Propietario confirma finalidad y si T32 se adelanta. |
| D02 | Supuesto | Un editor local por archivo; sin edición simultánea de red. | Confirmar antes del diseño definitivo de archivo T03 si existe trabajo concurrente. |
| D03 | Pendiente de ejemplo | Perfil explícito, signos y saldo de periodo. CumulativeExpenseDifference solo con comparables. | Archivo de muestra y periodo explicado, antes de T06 de producción. |
| D04 | Propuesta | NormalizedManagement, fijo propio sobre normal y variable observado sobre real. | Aceptar C15–C19 y explicar cómo se fija normal, antes de T15 de producción. |
| D05 | Propuesta | OwnerOnceCapacity para auxiliares medidos; ThroughFlow muestra capacidad auxiliar sin evaluar. | Aceptar C20 y listar servicios medidos, antes de T16 de producción. |
| D06 | Propuesta | Lote iniciado completo para setup; unidades continuas sin redondeo a pieza; merma explícita. | Dos órdenes y sus cantidades, antes de T17–T18 definitivos. |
| D07 | Propuesta | Mostrar coste productivo y después capas comercial y estructura separadas. | Aceptar perímetros y costes sin objeto antes de T20 definitivo. |
| D08 | Pendiente de operación | Sin prometer cifrado por defecto; decidir protección y recuperación antes de entregar datos reales. | Necesidad, uso entre equipos y recuperación, antes de T24 comercial. |
| D09 | Pendiente comercial | Perpetua para versiones adquiridas; actualizaciones con vigencia separada; uso sin conexión. | Proveedor, condiciones y alcance de soporte, antes de T24 comercial. |
| D10 | Propuesta | Autorreparto prohibido por defecto; habilitable por versión, con diagnóstico. | Propietario revisa si hay servicios a sí mismo que deban modelarse. No bloquea un modelo sin diagonal. |
| D11 | Propuesta | Ejecución definitiva exige cobertura completa; pendientes en borrador o ejecución provisional explícita. | Confirmar utilidad de una publicación provisional. Nunca ocultar su saldo pendiente. |
| D12 | Pendiente avanzado | Costes estándar, TDABC y simulación requieren datos propios y supuestos elegidos. | Definir cada módulo antes de T28–T31; no afecta R0/R1. |

Registrar cada resolución con fecha, persona, ejemplo aceptado, nueva versión de política y tareas afectadas. Si una instrucción posterior cambia un criterio, actualizar contrato, casos y estado. No reemplazar resultados históricos.

## 2. Decisiones técnicas iniciales

| ID | Estado | Decisión y verificación |
| --- | --- | --- |
| A01 | Decidido técnicamente | C# .NET 10 LTS, WPF, MVVM, SQLite y EF Core 10. Revisar paquetes y soporte al iniciar T01. |
| A02 | Decidido técnicamente | Motor puro fuera de interfaz y SQL. Runner de snapshots para pruebas e investigación. |
| A03 | Decidido técnicamente | Decimal en C# y texto decimal canónico en SQLite/JSON. Sin REAL para costes ni cantidades autorizadas. |
| A04 | Propuesta a comprobar | PDEC-1, cuantización a 8 decimales y cantidades de entrada a 12; informe de moneda separado. T02 valida límites y redondeo. |
| A05 | Propuesta a comprobar | LU decimal con pivoteo parcial y términos independientes múltiples; PNUM-1 con diagnóstico de amplificación. T10 comprueba escalas y rendimiento. |
| A06 | Decidido técnicamente | Un modelo publicado y una ejecución completada son inmutables; revisión explícita para entradas y publicación. |
| A07 | Decidido técnicamente | Snapshot completo, hash canónico y versiones de esquema, algoritmo y política. La hora de ejecución no entra en el cálculo; fechas de periodo/tipo sí son datos de negocio. |
| A08 | Decidido técnicamente | Restricciones, FK e índices en SQLite; lógica de costes en C#. Copias mediante API consistente. |
| A09 | Decidido técnicamente | CI Windows, artefactos por tarea y release revisada. Una build de desarrollo no se publica a clientes. |
| A10 | Decidido técnicamente | Sin servidor de cálculo, telemetría o IA dentro de los cálculos. La IA se usa para desarrollar, no para inventar datos del cliente. |
| A11 | Requisito de producto | Moneda funcional por estudio, Money con código ISO, originales preservados y FX manual/importado versionado. No depende del idioma o país. |
| A12 | Decidido técnicamente | Fuentes contables adicionales mediante adaptadores T33. Independientes del núcleo. |
| A13 | Requisito de producto | Nombre VALDRIO en documentación, recursos, ensamblados y entregables. Namespaces Valdrio. |
| A14 | Requisito de producto | Localización desde T01A, catálogos español/inglés y formato regional independiente. Sin textos de UI o CultureInfo actual en el motor. |
| A15 | Requisito de producto | Fechas de negocio DateOnly/ISO; instantes UTC; zona independiente y perfil de origen explícito. |
| A16 | Requisito de producto | MinorUnitDigits de catálogo ISO versionado; informe sin escala fija de 2. Tipo de cambio y metadatos congelados por ejecución. |
| A17 | Requisito de arquitectura | Puertos de importación, persistencia, monedas/FX, reporting y licencia. Pruebas de dependencias y compatibilidad de snapshots. |

Una dificultad de implementación no autoriza al LLM a cambiar un resultado esperado económico. Primero debe mostrar el conflicto y una propuesta. Los detalles técnicos internos que conservan contratos pueden resolverse y documentarse sin consultar al propietario.

## 3. Diccionario operativo

| Término | Definición y dato concreto |
| --- | --- |
| Empresa | Ámbito de maestros, modelos y estudios, con moneda funcional sugerida. Primera edición, una empresa por archivo. |
| Periodo | Intervalo de inicio y fin que identifica recursos y actividad comparables. Se guarda separado de la fecha de ejecución. |
| Modelo | Configuración de centros, pools, claves, recetas y políticas. Su versión publicada es inmutable. |
| Estudio | Contexto económico y finalidad con datos de un periodo y escenario. |
| Escenario | Variante de entradas o modelo. Conserva referencia al origen y supuestos modificados. |
| Revisión | Número creciente de una versión editable de entradas; controla actualidad y publicación. |
| Ejecución | Snapshot identificado y resultados completos de un cálculo, con algoritmo y política versionados. |
| Recurso original | Importe interpretado de una fuente, ligado a filas originales y perfil de importación. |
| Ajuste | Diferencia firmada respecto del original con motivo y tipo. |
| Recurso efectivo | Original convertido más ajustes funcionales. Importe que debe quedar completamente explicado. |
| Parte de recurso | Unidad de clasificación y ruta con importe propio. Sus partes cubren el recurso sin duplicarlo. |
| Incorporable | Parte elegida dentro del perímetro económico del modelo. Una exclusión tiene motivo y se concilia. |
| Pendiente | Parte todavía sin ruta o dato necesario. Es visible; no es un mecanismo de redondeo. |
| Directo | Relación de una parte con un objeto concreto sin clave indirecta. Requiere destino y ámbito. |
| Indirecto | Relación que usa un driver o clave para asignar a destinos. No se infiere de ser auxiliar. |
| Fijo | Comportamiento económico definido para driver, horizonte y rango. Puede existir sin política de capacidad. |
| Variable | Comportamiento ligado a actividad/consumo dentro del contexto indicado. |
| Mixto | Coste con componentes; se divide o queda pendiente, con regla explicitada. |
| Capacidad propia | Capacidad suministrada por un pool propietario, con coste, unidad, calendario y base. |
| Servicio recibido | Coste transferido desde otro pool. Conserva origen, comportamiento y evaluación de capacidad previa. |
| Centro físico | Unidad de organización. Puede agrupar varios nodos/pools con drivers distintos. |
| Nodo principal | Destino de la etapa secundaria; puede trasladar coste posteriormente a objetos. |
| Nodo auxiliar | Nodo que presta servicios a otros nodos y participa en el sistema recíproco. |
| Pool | Bolsa de costes homogénea respecto de finalidad, driver y política de capacidad. En R1, uno por nodo lógico. |
| Actividad ABC | Trabajo definido con pool y driver propio. No equivale a cualquier etiqueta de centro. |
| Driver | Magnitud que explica una asignación, con unidad, procedencia y ámbito. |
| Clave | Definición versionada de pesos o porcentajes reutilizable. Su aplicación es otro hecho. |
| Primario | Imputación de partes indirectas a pools/nodos mediante claves. |
| Secundario | Resolución y transmisión de servicios auxiliares a terminales. |
| Reciprocidad | Prestaciones cruzadas representadas por un sistema lineal; el coste bruto incluye circulación. |
| Contribución efectiva | Parte del coste de un origen que termina en cada destino después de resolver ciclos. |
| Capacidad teórica | Máximo ideal según calendario y condiciones explicitados. |
| Capacidad práctica | Disponibilidad tras interrupciones inevitables de los recursos suministrados. |
| Capacidad normal | Actividad representativa esperada bajo condiciones normales para el horizonte definido. |
| Actividad real | Cantidad observada de servicio o producción en el periodo, independiente de los consumos asociados a objetos. |
| Actividad sin objeto | Actividad observada cuya cantidad todavía no se ha asociado a objetos. No es capacidad ociosa. |
| Tarifa | Coste por unidad de driver bajo una política y ejecución concretas. Conserva precisión de cálculo. |
| Coste aplicado | Importe imputado al consumo de objetos con la tarifa correspondiente. |
| Variación de capacidad | Diferencia firmada de capacidad propia frente a actividad real bajo la política; puede ser negativa con sobreactividad analítica. |
| Ociosidad o subactividad | Parte positiva del fijo propio sin aplicación por actividad menor que base normal/práctica, según política. |
| Residuo | Diferencia de cuantización, con origen, valor y adjudicación determinista. No es una exclusión. |
| Terminal económico | Destino disjunto en una frontera, como objeto, capacidad, coste de periodo o pendiente. |
| Frontera de valoración | Corte de transferencias que define qué costes se suman una vez. |
| Estimación | Coste teórico para ficha, oferta o simulación. No consume recursos reales del estudio. |
| Consumo real | Hecho de uso de recursos o actividad con cantidad y evidencia. |
| Lote iniciado | Agrupación que requiere setup completo conforme a receta; Ceiling(Q/L) cuando se adopta esa regla. |
| Merma | Pérdida o necesidad adicional con convención de entrada/rendimiento explicitada. |
| Margen | Venta menos coste del perímetro elegido; su porcentaje habitual divide entre venta. |
| Recargo | Margen dividido entre coste del perímetro elegido. |
| Contribución | Venta menos costes variables pertinentes. Cambia el perímetro del coste. |
| Estándar | Referencia aprobada de precios, cantidades y presupuesto para comparación; distinto de coste real normalizado. |
| Desviación | Diferencia entre real y referencia comparable, con convención de signo y componentes aditivos. |
| TDABC | Modelo de coste de capacidad práctica y ecuaciones de tiempo que requiere esos datos. |
| UiLanguage | Preferencia de texto de interfaz. No decide separadores, moneda ni interpretación de archivos. |
| FormatCulture | Preferencia de formato regional. Afecta presentación, no cálculo ni códigos económicos. |
| SourceParseProfile | Cultura y patrones del origen importado, capturados con los datos. |
| Moneda funcional | Base de cada ejecución y sus conciliaciones; elegida por estudio. |
| OriginalMoney | Importe y moneda de fuente preservados antes de conversión. |
| Tipo de cambio | Quote de destino por una unidad origen, con fecha, fuente y versión. |
| Unidad menor | MinorUnitDigits del catálogo. Escala del informe, distinta de precisión interna. |
| Fecha de negocio | DateOnly sin zona; por ejemplo periodo, documento y fecha de tipo. |
| Instante | Punto UTC que puede mostrarse en distintas zonas sin cambiar su identidad. |
| EconomicInputHash | Valores/políticas relevantes para cálculo. Excluye preferencias visuales. |
| FullSnapshotHash | Fotografía completa con procedencia, perfiles y datos originales. |

## 4. Matriz inicial para elegir política

| Finalidad | Datos que exige | Política y límites |
| --- | --- | --- |
| Reparto por centros | Recursos y claves fiables | ThroughFlow y reciprocidad; explica destinos, sin prometer capacidad evaluada. |
| Gestión de capacidad y pricing | Componentes, normal, real y consumos | NormalizedManagement y OwnerOnceCapacity donde se midan capacidades. Variación firmada y actividad sin objeto visibles. |
| Coste real por actividad | Costes y actividad observada comparable | ActualManagement; tarifa sensible al volumen, sin convertirla en estándar. |
| Contribución | Clasificación contextual y venta | VariableCostView; fijo de periodo separado y perímetro visible. |
| ABC | Partición entre actividades y drivers reales | ActivityPools; conservación entre pools y objetos. |
| TDABC | Capacidad práctica y tiempos | TimeDrivenPolicy; supuestos de tiempo medidos o aprobados. |
| Desviaciones estándar | Referencias y producción real | StandardComparison; cantidades permitidas por producción real y aditividad. |
| Existencias contables | Perímetro normativo y movimientos | ProductionValuation, T32; revisión de marco aplicable y reconciliación de inventario. |

Una finalidad no se elige porque su nombre parezca más avanzado. Se selecciona por la pregunta de gestión y los datos disponibles. Los resultados muestran tanto el nombre como las reglas usadas.
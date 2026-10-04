# Contratos metodológicos implementables

Versión 0.4. Cada contrato vincula una regla económica con datos, operación, resultado y aceptación. Las decisiones D01–D09 y sus ejemplos pueden modificar una propuesta antes de publicar la política de producción. Un cambio posterior crea una nueva versión de política y conserva los resultados anteriores.

## CM01 — Interpretar sumas y saldos

**Datos obligatorios**. Empresa, moneda funcional, moneda de cada fila o moneda fija declarada, inicio y fin del periodo, archivo, hoja si es XLSX, perfil y modalidad. Modalidades iniciales `PeriodAmount`, `ClosingBalance` y `CumulativeExpenseDifference`. Cuenta y descripción son texto. Cada fila mantiene número de hoja y fila, valores originales e importe interpretado; CM16 define la conversión previa cuando procede.

**Perfil**. Mapea cuenta, descripción y columna de importe, o columnas Debe/Haber. Con Debe/Haber, el importe de partida es Debe menos Haber. El usuario confirma la convención antes de aceptar. El perfil define separador decimal y de miles para CSV; no intenta adivinar un valor ambiguo como `1,234`. XLSX distingue número de celda y texto; una fórmula se acepta solo con valor almacenado utilizable, sin ejecutar macros ni Excel.

**Cuentas duplicadas**. Se permite `Reject` o `AggregateWithProvenance`. Por defecto se rechazan. Al agrupar se conserva cada fila, moneda original y aportación funcional convertida. Importes originales se agrupan únicamente dentro de la misma moneda, conservando aplicaciones de tipo por fila; una cuenta con originales en distintas divisas no tiene un único OriginalMoney nominal. Si descripciones de la misma cuenta difieren, el usuario elige descripción de destino. Una cuenta con saldo cero se conserva o se excluye mediante opción visible, nunca desaparece por error de lectura.

**Modalidad de periodo**. El saldo ya representa el periodo y no necesita restar otro balance. `ClosingBalance` se guarda como evidencia de cierre y no se convierte automáticamente en coste del mes. `CumulativeExpenseDifference` necesita dos fechas, saldos comparables de cuentas de resultados y autorización de esa transformación. Por cuenta, coste de periodo = acumulado actual - acumulado anterior. Una cuenta ausente es `Missing`, no cero, salvo regla explícita aprobada. No se cruza un cierre de ejercicio sin una regla específica.

**Operación**. `PrepareImport` lee y devuelve filas, errores, totales por signo y hash del archivo. `ConfirmImport` verifica revisión y confirma el lote entero en transacción. El modo es añadir lote, sustituir un lote identificado o crear una nueva revisión; sustituir todos los recursos no es la acción por defecto. Hash y perfil iguales detectan repetición, con confirmación explícita si se permite.

**Resultado**. ImportBatch confirmado, ImportedRows inmutables y Resources enlazados. Perfil, modalidad, fechas y totales forman parte del snapshot.

**Bloqueos**. Cuenta vacía, decimal inválido, moneda incompatible, periodo incoherente, duplicado no autorizado, fórmula sin valor y acumulado no comparable. No se transforma un error en cero. `IMP_INVALID_AMOUNT`, `IMP_DUPLICATE_ACCOUNT`, `IMP_MISSING_COMPARATIVE`, `IMP_SIGN_UNCONFIRMED` identifican filas y correcciones.

**Aceptación**. Coste acumulado de enero 100 y febrero 160 produce febrero 60 en la modalidad adecuada. Importar 160 como importe de febrero produce 160 y queda claramente rotulado. Cancelar la previsualización o fallar una fila no cambia el estudio. Fuente de prueba C01–C03.

## CM02 — Recursos, ajustes, particiones y rutas

**Importes**. En moneda funcional, `EffectiveFunctionalAmount = ConvertedOriginalAmount + sum(FunctionalAdjustmentDelta)`. Si origen y funcional coinciden, ConvertedOriginalAmount es OriginalAmount. Si difieren, CM16 conserva OriginalMoney y su puente. Un ajuste requiere motivo y tipo, y se convierte explícitamente si se introduce en otra moneda. Corregir un ajuste confirmado crea reversión y nuevo ajuste, o una nueva revisión editable si aún no se ha cerrado; se conserva la auditoría. No se permite guardar simultáneamente una diferencia y un «importe objetivo» que compitan como verdad.

**Partes**. ResourcePart contiene ResourceId, importe firmado, Behavior, driver, horizonte, rango, RoutingKind, CapacityTreatment y CostLayer. Behavior es Fixed, Variable, MixedPending o Unclassified. Antes de una absorción definitiva, MixedPending se divide o bloquea; Unclassified puede pasar reparto R0, con advertencia, pero bloquea la política de capacidad si necesita esa clasificación.

**Rutas mutuamente excluyentes**. DirectObject, AllocationPool, Excluded o Pending. Una parte puede tener varias líneas de imputación directa que suman su importe, o varios destinos primarios conforme a una clave. No puede tener a la vez importe completo directo y el mismo importe completo en pool.

**Capacidad**. CapacityTreatment es None, AssessAtPool o PeriodCost. AssessAtPool identifica un pool y nodo propietarios, con cantidades y unidad. Su importe primario se destina a ese pool; si hay varios propietarios, se divide primero en partes con importes conservados. Se separa naturaleza fija de «coste de capacidad suministrada». No todo fijo necesita necesariamente ser absorbido por una base de capacidad.

**Identidad de valor**. Por recurso se exige `efectivo = directo + a_pools + excluido + pendiente`. Cada término agrupa partes distintas y firmadas. Una diferencia neta global cero no exime esta comprobación por recurso.

**Ingresos y materiales**. Una fila contable de ingreso se destina a venta, coste negativo o exclusión con regla aprobada. Si alimenta venta, no se resta a costes otra vez salvo asiento económico explícito. Un material de consumo real se enlaza a una parte existente o a una fuente externa identificada; la misma factura o saldo no entra dos veces.

**Operaciones**. AddAdjustment, ReverseAdjustment, SplitResource, ClassifyPart, SetRouting, AssignDirect. Cada operación tiene revisión esperada, valida sus importes y aumenta Revision en una única transacción.

**Interfaz**. Muestra original, ajustes, efectivo, partes, importe cubierto y pendiente. La clasificación directa pide «directo respecto de qué objeto». Un fijo pide contexto de comportamiento y, si procede, propietario de capacidad.

**Aceptación**. Original 1.000 con ajuste -100 da efectivo 900. Partes 300 directo, 500 pool y 100 excluido cubren 900. Añadir otra parte 100 genera `RES_PARTITION_MISMATCH`. C04–C05.

## CM03 — Claves, fracciones y asignaciones

AllocationKeyVersion contiene ámbito, finalidad primaria o secundaria, modalidad, unidad, destinos, valores, versión y política de autorreparto. Una clave se publica por sí misma y queda inmutable; sus aplicaciones mantienen la versión concreta.

| Modalidad | Regla operativa |
| --- | --- |
| Percentage | Cada porcentaje entre 0 y 100; suma exacta de entradas decimales = 100. No normalizar 80 a 100. |
| DriverUnits | Pesos no negativos en una unidad, suma positiva; fracción = peso/suma. Los ceros son destinos sin participación. |
| EqualShares | Conjunto no vacío de destinos únicos; fracción = 1/n. |
| AbsoluteAmount | Asignación a una parte y revisión concretas; los importes suman exactamente el importe a asignar. No es una plantilla de porcentajes universal. |

Las cantidades se representan en decimal, hasta 12 posiciones de entrada; porcentajes hasta 18. Los datos con más precisión se rechazan indicando cómo corregirlos o se aceptan mediante una conversión explícita conservada, nunca truncando en silencio.

Las fracciones calculadas por unidades o partes iguales pueden tener desarrollo decimal infinito. Se mantiene la definición por pesos; el motor calcula a precisión decimal y comprueba deriva aritmética. El usuario no tiene que introducir tres porcentajes de 33,333… para construir partes iguales.

La cobertura parcial puede existir en borrador. Para calcular una vista de trabajo se muestran pendientes explícitos y el sello «provisional». Una ejecución definitiva requiere cobertura completa de las partes incorporadas y claves válidas. Excluir una parte es una decisión con motivo, no un residuo de clave.

**Aceptación**. 80 % falla al publicar; pesos 1, 1, 1 son una clave válida; pesos 0, 0 fallan; porcentajes negativos fallan incluso con recurso negativo. C06–C08. El cambio del orden visual no altera fracciones ni residuos.

## CM04 — Precisión, cuantización y residuos

**Política PDEC-1 propuesta**. Importe de entrada y adjudicado, 8 decimales como máximo. Cantidad de entrada, 12. Porcentaje de entrada, 18. Cálculo y solver, System.Decimal sin un Round deliberado intermedio. Tarifas calculadas conservan la precisión decimal disponible, se serializan sin pasar por double y se recalculan únicamente con snapshot y política.

La magnitud inicial admitida se propone hasta 10^12 unidades monetarias por parte y 10^12 unidades físicas por observación. Se comprueba overflow en todos los caminos y tamaño de agregados. T02 y T10 validan esos límites; ampliarlos requiere nueva política y pruebas de límites. No son un alcance comercial prometido.

**Representación**. C# decimal; SQLite TEXT canónico con punto decimal, sin separador de miles, exponente ni ceros negativos. JSON usa cadenas decimales para importes y cantidades. Una igualdad económica no depende del separador regional. Las fechas UTC se serializan de forma canónica; unidades y moneda son campos, no caracteres incrustados en un número.

**BalancedRound**. Recibe total de origen, contribuciones matemáticas por destino, quantum e IDs estables. El total de origen debe ser múltiplo del quantum. Para repartir una fuente negativa con fracciones no negativas, opera sobre la magnitud y restaura el signo, conservando simetría.

1. Verificar que la suma de contribuciones coincide con el total dentro de la tolerancia numérica aplicable. Una diferencia de cobertura de negocio se rechaza antes de este algoritmo.
2. Si el origen es negativo, cambiar provisionalmente el signo de todas las contribuciones y del total.
3. Expresar cada contribución en unidades del quantum y tomar Floor. Floor se usa también para una contribución negativa, como una variación de sobreactividad.
4. Calcular unidades pendientes = total entero - suma de Floors. Debe estar entre cero y el número de líneas menos uno, con control de deriva decimal en fronteras exactas.
5. Adjudicar una unidad a las líneas con mayor parte fraccionaria. Desempatar por ID en comparación ordinal documentada.
6. Restaurar signo si procede; registrar bruto, adjudicado y ajuste. Verificar igualdad exacta de importes adjudicados.

Si la deriva del solver deja una diferencia pequeña, PDEC-1 permite corregir como máximo un quantum interno, 10^-8, por conjunto de contribuciones de una fuente. Se aplica la diferencia a la línea de mayor magnitud absoluta, con desempate por ID, y se registra NumericAdjustment separado de RoundingAdjustment. La suma de contribuciones corregidas debe coincidir con el total antes de tomar Floor. No se utiliza la tolerancia relativa del residual del solver para ampliar este límite monetario. Una diferencia mayor produce `NUM_CONSERVATION_FAILURE` y no publica resultados. No utilizar el redondeo para absorber claves incompletas o servicios sin medida.

**Dos niveles de vista**. El ledger económico adjudicado tiene 8 decimales. Para un informe conciliado se construye CurrencyProjection separada a MinorUnitDigits de la moneda funcional, desde el mismo origen y frontera. EUR/USD usan 2 en los ejemplos C01–C37; JPY 0 y KWD 3 en C38/C39. No se fija 2 para todas las divisas. Los totales de la proyección se suman desde sus líneas. No mezclar ledger interno y proyección de moneda. Un tooltip y exportación canónica permiten consultar los 8. La tarifa visible no se realimenta al motor.

**Origen con fracción de unidad menor**. Un coste convertido o adjudicado a 8 puede no ser múltiplo del quantum de informe. BalancedRound del ledger exige total exacto a su quantum; CurrencyProjection no fuerza esa precondición a la moneda. Primero fija Target = RoundToQuantum(SourceLedger, MidpointToEven) y registra ProjectionDifference = Target - SourceLedger. Trabaja sobre las contribuciones adjudicadas cuya suma es SourceLedger: usa signo del origen, Floor de cada contribución/quantum y mayores restos para completar Target, con desempate ordinal. En este caso las unidades a completar pueden ir de cero al número de líneas inclusive. No trata la diferencia visual como error del solver ni cambia el ledger. Un origen negativo usa magnitud y restaura signo; con origen cero conserva contribuciones firmadas. Si SourceLedger ya es múltiplo del quantum, coincide con BalancedRound.

**Conciliación visual**. Cada origen y los totales proyectados concilian entre sus propias líneas. El total de informe es suma de Targets y declara su diferencia respecto al ledger; puede diferir del redondeo independiente del total agregado. No se promete igualdad entre ambas representaciones ni se descartan costes pequeños. Ejemplos adicionales a implementar en T02/T02A: 0,015 EUR repartido 0,0075/0,0075 proyecta 0,01/0,01 y Target 0,02; 0,005 EUR proyecta Target 0,00 por empate a par, con diferencia -0,005 visible; sus negativos tienen signo simétrico. La vista de 8 mantiene cada importe original.

**Trazabilidad del redondeo**. La cuantización final se hace por parte de recurso y destinos económicos disjuntos. El total de un centro final se deriva de sus contribuciones adjudicadas. Puede diferir una unidad de 10^-8 del redondeo independiente de su total matemático; se documenta y se muestra el valor adjudicado como autoridad de conciliación.

**Ejemplo importante**. En C09, la contribución de A a P1 se adjudica 642,85714286 y la de B 214,28571429. Con el primario propio de 2.000, P1 adjudicado es 2.857,14285715. El total matemático es 20.000/7, cuyo redondeo independiente sería 2.857,14285714. Ambas cifras tienen semánticas distintas. La aplicación usa la primera para que el detalle de recursos y el total del centro coincidan. En la proyección de moneda por fuente, A aporta 642,86 y B 214,29, por lo que P1 es 2.857,15 y P2 3.642,85. Se conservan sus 6.500 y el detalle de cada fuente. Esa proyección se identifica; no se presenta como el redondeo independiente de cada total matemático.

**Aceptación**. C07–C09, C21, C38 y C39. Un quantum a tres destinos conserva el importe; el negativo mantiene signo y la moneda decide el quantum de informe. Cambiar IDs solo afecta al desempate identificado. Los importes no pierden precisión al cerrar y abrir el archivo.

## CM05 — Reparto primario

**Entrada**. Partes incorporadas a pools, reglas y claves publicadas, centros y unidades del modelo, revisión de estudio y política decimal. Una regla debe resolver sin ambigüedad qué clave usa cada parte. R1 permite asignación explícita por parte y reglas por cuenta con precedencia aprobada; una coincidencia múltiple sin prioridad falla.

**Cálculo**. Para parte r de importe c y destino j, bruto = c × participación. BalancedRound adjudica todos los destinos de r juntos a 10^-8. La línea conserva definición de pesos, participación calculada y origen. Imputaciones directas y exclusiones no entran en el pool primario.

**Resultado**. PrimaryAllocationFact por parte y destino, PrimaryPoolTotals por comportamiento y tratamiento de capacidad, y Reconciliation por parte y etapa. No se crea necesariamente un producto cartesiano de todas las cuentas y centros; se guardan destinos efectivos y ceros necesarios para explicación.

**Error**. Destino de otra empresa o modelo, clave inválida, parte sin ruta, conflicto de regla, unidad no compatible o suma no conservada. Un centro archivado puede consultarse históricamente, pero no añadirse a una nueva clave activa.

**Aceptación**. C08, C09 y C05. Cada parte concilia. Dos recursos con errores opuestos no superan la validación por compartir total neto cero. El resultado no depende del orden de lectura de SQLite.

## CM06 — Grafo y solver recíproco

**Modelo**. Orden canónico de auxiliares y principales por ID. Q tiene auxiliar origen en filas y auxiliar destino en columnas. T contiene las salidas de esos auxiliares. En ThroughFlow, filas de Q+T suman uno y sus entradas son no negativas. B es la parte de T dirigida a principales. `M = I - transpose(Q)`; resolver `M x = bA`.

**Prevalidación**. Comprobar centros, cobertura por auxiliar y salidas alcanzables hacia terminales. Un auxiliar inactivo y sin coste puede omitirse de una ejecución solo mediante regla explícita que compruebe que no recibe prestaciones. Un componente cerrado del grafo es error de modelo, aunque el término independiente sea cero.

**Autorreparto**. Propuesta por defecto, prohibido. Si se activa en una versión de modelo, la fracción diagonal se admite y se explica. Un nodo con 100 % a sí mismo sigue siendo inválido. C10 y C11.

**Algoritmo inicial**. LU decimal con pivoteo parcial determinista y resolución de múltiples términos independientes. La factorización se reutiliza mientras M sea idéntica. No calcular una inversa explícita como paso habitual ni usar la posición visual. Si T10 demuestra límites insuficientes, se revisa esta elección mediante ADR y referencias, antes de extender el producto.

**Diagnósticos PNUM-1 propuestos**. Pivote cero → singular. Pivote normalizado extremadamente pequeño → sensibilidad, sin denominarlo automáticamente singular. Residuo absoluto `||M x - b||∞`, relativo frente a `||M||∞ ||x||∞ + ||b||∞`, deriva de factores terminales, amplificación de circulación y no negatividad de factores de transferencia. La condición de M y la amplificación de costes son métricas diferentes.

Umbrales iniciales para validar durante T10. Residuo monetario máximo permitido = max(10^-8, 10^-12 × ||b||∞), acompañado del residuo relativo ≤10^-12. Deriva de factores efectivos por origen ≤10^-18. Advertencia de amplificación >10^6 y bloqueo >10^12. Un pivote normalizado <10^-12 bloquea por sensibilidad en esta política, aunque sea no cero. El usuario recibe ID de componentes y salidas implicados. Cambiar umbrales requiere versión de política y fixtures de frontera.

No aceptar un resultado únicamente porque el vector suma distinto de cero o porque coincide el total final. Un modelo con costes positivos y negativos que se compensan es válido si cumple los demás requisitos. No sustituir el método recíproco por directo ante un fallo.

**Resultado**. Solución bruta decimal, diagnóstico, orden de IDs, pivotes/factorización reutilizable durante la ejecución y factores efectivos a terminales. El archivo publicado necesita diagnóstico y resultados, no necesariamente guardar la factorización como formato duradero.

**Aceptación**. C09–C14. La referencia racional comprueba solución, contribuciones y total. El método debe funcionar con b=0 en un grafo abierto válido, sin confundir cero con error.

## CM07 — Secundario por origen y explicación

Para cada parte r se forman bA_r y bP_r. Resolver `M z_r = bA_r`. Calcular contribución terminal `f_r = bP_r + transpose(B) z_r`, con las salidas terminales adicionales cuando la política las haya generado. Alternativamente se aplican factores efectivos previamente calculados a los vectores primarios. Se compara ambas rutas en T11 con el corpus.

La naturaleza fija o variable, la capa de coste y el origen de capacidad acompañan a la parte. Recibir costes fijos en un principal no convierte todo su coste en fijo. Recibir costes ya evaluados por capacidad no los vuelve automáticamente variables; siguen siendo fijos por naturaleza, con un tratamiento de absorción diferente.

BalancedRound adjudica destinos económicos de cada parte juntos. Los finales por centro se suman desde contribuciones. La conciliación cumple por parte y globalmente `primario a auxiliares + primario a principales = terminales`. El bruto recíproco y las transferencias internas se publican como información separada.

En C09, origen A de 1.000 termina en P1 con 4.500/7 y P2 con 2.500/7. Origen B de 500 termina en 1.500/7 y 2.000/7. Sumando costes propios se obtienen 20.000/7 y 25.500/7, total 6.500. Estos factores explican la reciprocidad sin enumerar caminos.

**Interfaz**. Un centro abre desglose por origen y comportamiento; un recurso abre sus destinos. Se identifica la clave primaria, claves secundarias, versión, contribución efectiva, valor bruto y ajustes. La visualización de circulaciones no se suma al informe de costes finales.

**Aceptación**. C09, C12–C13. Suma por recurso y por principal, consistencia detalle-total y mismos resultados al permutar orden de entrada. Las agrupaciones por función o área consultan los mismos hechos.

## CM08 — Actividad, capacidad y tarifas de principales

**Datos**. Pool, nodo, driver, unidad, periodo, capacidad teórica/práctica/normal cuando procedan, actividad real observada qR, consumos de objetos q_o, fuente y calidad. Se mantiene la diferencia entre Missing y cero. No se fuerza `qN <= qP` sin definir que representan el mismo recurso y calendario; si son comparables y discrepan, se diagnostica.

**Políticas iniciales**. ActualManagement, NormalizedManagement y VariableCostView. La finalidad y política se identifican en cada resultado. La primera distribuye costes elegibles sobre actividad real; puede ocultar económicamente ociosidad si el usuario solo mira la tarifa, por lo que se explica su límite. La segunda evalúa capacidad suministrada y conserva las diferencias. La tercera muestra costes variables y fijos de periodo; no destruye la clasificación subyacente.

**NormalizedManagement**. Para fijo propio con AssessAtPool: `rF = Cf / qN`. Para variable observado: `rV = Cv / qR`. Para servicio recibido cuya capacidad ya fue evaluada, o coste con tratamiento None: `rConsumed = CConsumed / qR`, conservando su Behavior. Un total unitario puede ser rF + rV + rConsumed si las unidades y perímetro coinciden.

**Aplicación**. Por objeto, fijo propio = rF × q_o; variable = rV × q_o; recibido/consumido = rConsumed × q_o. Si un componente usa otro driver, pertenece a otro pool y no se suman cantidades incompatibles. R1 exige un driver homogéneo por pool. ABC habilita varios pools.

**Destinos que cierran el valor**. Fijo de capacidad propia = objetos + variación por actividad real + actividad observada sin objeto. Variación = Cf × (1 - qR/qN). Sin objeto fijo = Cf × (qR - sum(q_o))/qN. Variable sin objeto = Cv × (1 - sum(q_o)/qR). Coste recibido sin objeto se calcula con su base real. Las partes directas se añaden una vez, fuera de esta absorción.

**Restricciones**. Sum(q_o) mayor que qR bloquea cuando los datos son consumo real comparable. Si cantidades pertenecen a una estimación, se permite simular más actividad en otro contexto sin presentarlo como conciliación real. Cantidades negativas de devolución necesitan política y transacción de reversión; no se aceptan como horas reales negativas por defecto.

**Ceros**. qN=0 y Cf≠0 bloquea tarifa fija normal. qR=0 con variable o costes consumidos positivos bloquea aplicación de esos componentes hasta aclarar datos. qR=0, Cf>0 y qN>0 deja tarifa fija válida y todo Cf como capacidad no utilizada. Para componente de coste cero, la tarifa puede ser cero si el denominador existe; si no existe, se marca NotApplicable/Undefined, diferenciándolo de cero calculado.

**Sobreactividad**. Con qR>qN se admite variación negativa en análisis normalizado. La conservación es firmada. No se llama a esa diferencia «ahorro real» ni se añade como nuevo ingreso. Producción limitada es otra política en CM14.

**Aceptación**. C15–C19. Con Cf=1.000, Cv=600, qN=100, qR=60 y objetos 40/20, tarifas 10/10 y objetos 800/400, capacidad 400. Con objetos 40 solamente, coste objeto 800, capacidad 400 y actividad sin objeto 400. La última cifra no es ociosidad.

## CM09 — Capacidad auxiliar sin doble evaluación

Este contrato es una política de gestión propuesta denominada `OwnerOnceCapacity`. Necesita aprobar ejemplos y servicios antes de usarse en producción. Se determina mediante datos propios de servicios y capacidad.

**Propietario**. Cada componente de capacidad suministrada identifica un pool propietario y su coste propio. Se puede dividir una parte entre varios propietarios antes del cálculo. Una misma unidad monetaria no se somete a dos evaluaciones de la misma capacidad.

**Preprocesamiento de auxiliares**. Para un componente fijo propio Cf_A con normal qN_A y real qR_A, separar una vez `AppliedServiceCost = Cf_A × qR_A/qN_A` y `CapacityVariance = Cf_A - AppliedServiceCost`. Se conservan ambos como contribuciones de la fuente. Con qR_A=0, el componente se queda íntegro en capacidad y no entra al servicio transmitido.

**Prestaciones**. Las cantidades de servicio entre A y sus destinos suman qR_A. Los pesos efectivamente utilizados son servicio_Aj/qR_A. El importe aplicado y los variables consumidos entran al sistema recíproco con esos pesos. Los importes ya evaluados que llegan a otro auxiliar se transmiten como servicio consumido; no se les descuenta otra vez la capacidad de aquel auxiliar. Los costes propios de ese otro auxiliar sí tienen su propietario y evaluación.

Si diferentes componentes tienen servicios con destinos diferentes, se resuelven matrices distintas identificadas. Si los pesos son iguales, se reutiliza la factorización. La fila incompleta de servicios produce `AUX_SERVICE_COVERAGE_MISMATCH`. El grafo se valida sobre las rutas efectivas; una capacidad con cero actividad no justifica recibir otros servicios positivos sin destino.

**Principales**. Los costes de servicio recibidos llevan CapacityOwnerId y CapacityAssessmentId, y se aplican sobre actividad real del pool destino como costes consumidos. Su comportamiento económico fijo/variable sigue visible. El fijo propio del principal puede evaluarse con su propia normal. La política no vuelve a evaluar el coste de capacidad recibido como si se hubiese suministrado por segunda vez.

**Ejemplo C20**. A tiene fijo propio 1.000, normal 100 y real 50; entrega 10 unidades a B y 40 a P. B tiene fijo propio 600, normal 100 y real 50; entrega 50 a P. A deja 500 de capacidad y transmite 500, de los que 100 van a B y 400 a P. B deja 300 de capacidad propia y transmite sus 300 aplicados más los 100 recibidos. P recibe 800. Conciliación, 1.600 = 800 + 500 + 300. El coste de A recibido por B no vuelve a multiplicarse por 0,5.

**Límites**. La cantidad real de servicio debe ser consistente con las claves; los circuitos recíprocos necesitan mediciones comparables. El motor separa diferencia de capacidad propia, coste de servicios y circulaciones. Si se desea otra política que absorba todas las entradas a tarifa normal en cada centro, debe aprobarse como política distinta con sus efectos, no cambiar esta fórmula dentro del mismo nombre.

**ThroughFlow**. Es una alternativa explícita de reparto puro, donde todo coste incorporado atraviesa auxiliares conforme a claves. Se indica capacidad auxiliar no evaluada. Sirve para R0 y modelos sin datos de capacidad, sin pretender una evaluación completa de ociosidad.

## CM10 — Recetas, materiales, lotes y órdenes

RecipeVersion contiene producto, unidad de salida, vigencia y líneas. Cada línea define componente material, semielaborado o consumo de pool; cantidad, unidad, ConsumptionBasis y política de merma/indivisibilidad. ConsumptionBasis es PerUnit, PerStartedBatch o PerOrder. Estas bases no se infieren del nombre del centro.

Para orden Q>0 y tamaño de lote L>0, número de lotes iniciados = Ceiling(Q/L). PerUnit usa Q × consumo unitario. PerStartedBatch usa número de lotes × consumo por lote. PerOrder usa consumo una vez. Lote parcial comparte setup completo por defecto propuesto. Otra regla crea versión nueva de receta/política.

Las unidades físicas se convierten solo con factor explícito compatible, por ejemplo minutos a horas. No se convierte una unidad de producto a kilos sin un factor del producto. Piezas indivisibles aplican Ceiling por componente antes de valorar; materiales continuos mantienen cantidad decimal. Merma de entrada se registra como cantidad adicional necesaria o como rendimiento; el dato debe decir cuál. No usar 10 % indistintamente como incremento y pérdida de rendimiento.

El grafo de recetas de semielaborados debe ser acíclico en R1. Un ciclo se bloquea mostrando productos involucrados. Las tarifas referencian RunId, PoolId y PolicyVersion. Una orden estimada puede recalcularse explícitamente; una orden cerrada conserva receta, entradas, cantidades y ejecución originales.

**Estimación**. Valora consumos teóricos y materiales a precios de referencia. Produce quote/estimate y no TerminalCostFact de consumo real del periodo. Puede simular actividad superior a observada; es un escenario.

**Real**. Valora ConsumptionFacts verificados y partes de recurso directas. La fuente de material se referencia una vez. Diferencias entre consumo teórico y real son diagnóstico o desviación; no se ocultan cambiando receta histórica.

**Aceptación**. C22. Q=120, L=50, setup 2 h/lote, proceso 0,1 h/unidad, tarifas setup 30 y proceso 20. Consumo 6 h y 12 h, costes 180 y 240, total 420, unitario 3,5. Cambiar tamaño de lote actual no altera esa orden cerrada.

## CM11 — Frontera de objetos y conciliación final

TerminalCostFact tiene SourcePartId, RunId, destino económico, capa, comportamiento, propietario de capacidad, importe, unidad de consumo y ruta de explicación. Los terminales posibles son objetos de consumo, capacidad/variación, actividad sin objeto, coste de periodo, exclusión y pendiente de trabajo.

La frontera elegida determina qué objetos cuentan como finales. Un traslado semielaborado→producto→pedido conserva origen y no crea otro gasto. Puede crear hechos de transferencia o asociaciones de dimensiones, pero la consulta final selecciona un único corte. Los campos de producto, cliente, canal y pedido clasifican un hecho común.

`entrada ajustada = excluido + costes directos finales + coste aplicado final + variaciones de capacidad + actividad sin objeto + costes de periodo + pendientes`, con elementos disjuntos. Las categorías pueden tener importes firmados. El resultado de una estimación no sustituye a esta conciliación real.

La suma por cliente y la suma por producto pueden mostrar ambas el mismo coste total. No se suman una a otra. Una venta tiene su propio origen e identificador; no se trata como nuevo recurso de coste al calcular un margen.

**Aceptación**. C25 y casos de integración de T19/T27. Material incluido en recurso se consume sin añadirse de nuevo; orden de 420 agrupada por producto y cliente sigue teniendo coste 420 en cada vista y no 840.

## CM12 — Márgenes y escenarios

MarginView define CostPerimeter, QuantityBasis, SalesSource y denominador. CostPerimeter enumera capas producción, comercial, estructura y otras elegidas. Se muestra de forma visible y se versiona para comparar resultados.

Para V y C, margen absoluto = V-C; sobre ventas = (V-C)/V; recargo = (V-C)/C. Contribución = V-C_variable, conservando capas seleccionadas. V=0 o C=0 producen porcentaje Undefined, no cero inventado. Una devolución puede tener V<0 y coste revertido; requiere identificarse y se presenta con convención clara.

ScenarioClone copia entradas a una nueva revisión/estudio, manteniendo referencia al origen. Puede cambiar modelo o política mediante selección explícita. Comparison valida periodo, moneda, perímetro, unidades y frontera; si difieren, advierte y muestra las diferencias en lugar de sumar porcentajes sin contexto.

**Aceptación**. C23. Venta 200, variable 120, fijo aplicado 30: coste 150, margen 50, 25 %; recargo 1/3; contribución 80, 40 %. Cambiar el selector «contribución» cambia el coste incluido, no solo el porcentaje.

## CM13 — ABC, TDABC y estándares

**ABC**. CostPoolPartition reparte el importe del centro entre actividades y conceptos sostenedores antes de usar drivers. Cada ActivityPool tiene un driver, cantidades reales o bases aprobadas y destinos. La suma de pools coincide con el coste del origen. El motor reutiliza primitivas de reparto y capacidad con nuevos contratos de entradas, sin aplicar el centro entero por cada driver.

**C24**. Centro 1.000, pool unidades 600 y pool pedidos 400. Unidades de P1/P2 60/40; pedidos 1/3. P1 recibe 360+100=460; P2 240+300=540. Total 1.000.

**TDABC**. ResourceGroup aporta coste de capacidad suministrada y minutos prácticos del periodo. TimeEquation es un árbol tipado de constantes, variables y operaciones permitidas, con unidad final de tiempo. No se admite código C# arbitrario, eval ni SQL de usuario. Variables y tiempos necesitan procedencia, rango y versión. El coste por minuto × tiempo real se asigna a transacciones; el resto identifica capacidad no utilizada según su perímetro.

**C26**. Coste 1.200, capacidad práctica 600 min: tarifa 2/min. Ecuación 3 min + 2 min por línea; pedido de 4 líneas consume 11 min y cuesta 22. No se genera un porcentaje de capacidad ni un tiempo «confiable» solo por intervención de IA.

**Estándares**. StandardVersion contiene precios, cantidades por unidad producida, eficiencia, presupuesto fijo y calendario. Para material homogéneo: diferencia total = PA×QA - PS×QS; precio = (PA-PS)×QA; cantidad = PS×(QA-QS). QS es la cantidad estándar permitida para la producción real, no el volumen normal mensual.

**C27**. PA=12, QA=12, PS=10, QS=8. Real 144, permitido 80, diferencia 64=24 precio+40 cantidad. Positivo significa mayor coste. Efectos de mix/rendimiento y presupuestos flexibles requieren contratos posteriores que mantengan aditividad.

La comparación de métodos directo y escalonado es un escenario nuevo. El escalonado guarda orden; el directo define cómo excluye auxiliares de sus destinos y cómo recalcula participaciones. Ninguno es fallback de un circuito recíproco inválido.

## CM14 — Valoración de producción e inventario

Este contrato identifica un módulo opcional T32 y sus requisitos, no una certificación contable. AccountingPolicyContext elige marco, jurisdicción y versión antes de activar resultados destinados a existencias. PGC español es un ejemplo de adaptador opcional para empresas sujetas a ese marco; no se impone a usuarios internacionales. Se revisan sus criterios con una persona competente y se añaden otros marcos solo con especificación y aceptación propias.

Como regla inicial de referencia, separar coste productivo de comercial/estructura no incorporable a existencias, fijar capacidad normal y reconocer ociosidad como coste de periodo. En el caso homogéneo de fijo incurrido Cf y qN/qR positivas, una aplicación que no exceda el fijo incurrido puede usar `rF = Cf/max(qN,qR)`. La variable aplicada se basa en consumos pertinentes.

**C28**. Cf=1.000, Cv=1.200, qN=100, qR=120: fijo unitario 25/3, variable 10, aplicado total 2.200 y variación fija 0. La política analítica normalizada de C17 produce 2.400 y -200; son finalidades distintas.

El módulo añade existencias iniciales, entradas, producción terminada, consumos, mermas, salidas y existencias finales. La producción en curso necesita grado de terminación o consumo real por componente. El sumas y saldos por sí solo no determina esas cantidades. La conciliación de movimientos evita contar a la vez inventario inicial consumido, producto terminado y pedido vendido como nuevos costes.

Su aceptación exige casos de movimientos, inventario y corte temporal aprobados. No se adelanta este módulo escondiendo su ausencia dentro de una tarifa de gestión.

## CM15 — Idioma, región, fechas y parsing sin efecto económico

**Entrada**. UserPresentationPreferences, SourceParseProfile y ExportContext son objetos diferentes. UiLanguageTag traduce; FormatCultureTag presenta; DisplayTimeZoneId convierte instantes para mostrar. La cultura de origen interpreta valores del archivo. Ninguna de esas preferencias cambia FunctionalCurrencyCode por sí sola.

**Representación**. Periodo y documento son DateOnly ISO, calendario gregoriano inicial. Instantes son UTC con offset canónico. Importes/cantidades son decimal invariante. La UI puede mostrar formatos locales; la base y snapshot no almacenan textos de presentación como valores autorizados.

**Recursos**. Los diagnósticos y estados tienen códigos estables, MessageKey y argumentos tipados. Textos externos al motor, catálogos español/inglés desde R0, clave/placeholder/plural verificados. Idioma nuevo añade recursos y revisión; no modifica las ecuaciones. La aplicación actualiza bindings al cambiar idioma. Un formato nuevo no reinterpreta edición pendiente sin confirmar o cancelar.

**Importación**. Profile determina DatePattern, separadores, agrupación, signo, delimitador y moneda. Datos ambiguos sin perfil bloquean con IMP_AMBIGUOUS_FORMAT. 01/02/2026 no tiene significado universal. Se conserva el texto original y el valor tipado interpretado.

**Identidad**. IDs, códigos y desempates usan comparación ordinal y política definida. Traducción de categorías no cambia códigos. Orden visual regional no cambia Q/B ni adjudicación de residuos. Datos del cliente no se traducen automáticamente.

**Exportación**. Contexto explícito con idioma, región, moneda, precisión y versión de plantilla. Intercambio canónico usa ISO y decimal invariante; informe humano indica convenciones. El CSV localizado no se confunde con el canónico. En XLSX, precisión que exceda la representación numérica de Excel se conserva también como texto/JSON canónico.

**Aceptación**. I01–I05, I08–I12 e I15. Mismo snapshot con otra UI/cultura/zona conserva EconomicInputHash y ledger. Catálogos publicados completos, mensajes con argumentos válidos, disposición larga/RTL sin recorte, fecha de periodo sin desplazamiento y parsing de origen independiente. No se presenta como comprobado en .NET lo que solo está especificado.

## CM16 — Moneda funcional, originales y FX versionado

**Valores**. Money incluye Amount y CurrencyCode. Suma y comparación económica entre monedas diferentes requieren conversión identificada; el diagnóstico es MONEY_CURRENCY_MISMATCH. La tarifa identifica moneda y unidad de driver. La moneda no se deduce de CultureInfo, idioma, país ni símbolo.

**Catálogo**. ISO 4217 versionado con MinorUnitDigits y vigencia. CurrencyMetadataSnapshot de cada ejecución conserva el quantum de informe utilizado. EUR/USD 2, JPY 0, KWD 3 son ejemplos verificados; códigos especiales sin unidad menor numérica no reciben 2 por defecto. El ledger interno mantiene PDEC-1; la proyección monetaria aplica la escala de la moneda.

**Conversión previa**. Study tiene FunctionalCurrencyCode. Resource conserva OriginalMoney, ConvertedOriginalMoney y RateApplicationId. Quote es moneda destino por una unidad origen; ConvertedRaw = OriginalAmount × Quote. Conversión a 8 decimales usa MidpointToEven, registrando ConversionRoundingAdjustment. Un ajuste conserva origen y FunctionalDelta. Efectivo y partes se calculan en funcional.

**Tipos**. FxRateSetVersion publicado, origen/destino, Quote positivo hasta 18 decimales, RateDate, fuente y política de fecha/vigencia. La modalidad inicial es ExplicitRateSelection: cada original/ajuste referencia RateId y el usuario confirma su fecha/aplicación en la previsualización. No busca automáticamente el más cercano, triangula o calcula un promedio. Tipo medio, de cierre o de transacción requieren política aprobada. Fuente igual a funcional usa identidad 1 sin red. Tipo faltante en moneda diferente bloquea FX_RATE_MISSING; rango/fecha/orientación inválidos tienen diagnóstico. La salida valida límites del dominio. Un RateSet nuevo exige RevalueStudy explícito y revisión nueva, que reconstruye importes funcionales y revalida partes/consumos; nunca deja conversiones viejas bajo una versión de tipo nueva.

**Conservación**. Conciliar originales por moneda, aplicaciones FX y convertido en funcional. Después de conversión, CM02–CM14 conservan valor en funcional. No afirmar igualdad nominal entre una suma de varias monedas y un total convertido. Diferencia de conversión es otro concepto; no se oculta como residuo de reparto o capacidad.

**Historia**. RunSnapshot congela RateSet, tipos aplicados, metadatos y versión de conversión. Nuevo tipo no altera histórico. Otro estudio/escenario puede usar otros tipos. Exportar en otra divisa es una proyección explícita con su tipo, no un cambio de formato.

**Puertos**. ICurrencyCatalog y IFxRateProvider preparan datos en Application/Infrastructure. Calculate recibe valores congelados y no invoca proveedores. La primera modalidad usa tipos manuales/importados y opera sin red.

**Aceptación**. C33–C39 e I06/I07/I13/I14. 100 USD × 0,90 EUR/USD = 90 EUR sintéticos; original permanece 100 USD. Fuente EUR hacia EUR mantiene importe con identidad. Una conversión faltante se rechaza. JPY 101 en tercios adjudica 34/34/33 en moneda; KWD 0,001 conserva su unidad menor. Las proporciones económicas no cambian por traducir.
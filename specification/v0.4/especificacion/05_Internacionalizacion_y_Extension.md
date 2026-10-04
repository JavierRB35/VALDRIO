# VALDRIO — Internacionalización y arquitectura de extensión

Versión 0.4. Requisito de producto desde T01A, no mejora posterior. Este documento complementa los contratos CM15/CM16, el modelo de datos y todas las fichas. Define el comportamiento que debe implementar el LLM, sin afirmar que ya exista código.

## 1. Identidad y ámbito

El nombre comercial es VALDRIO. Los proyectos y namespaces usan Valdrio. Los archivos nuevos, instaladores, recursos, informes y ejemplos deben respetar esta identidad. No se mantienen referencias a productos ajenos como parte del nombre, dominio, esquema o contrato de VALDRIO.

El producto conserva escritorio Windows y autonomía local. Internacional significa que idioma, región, moneda y fechas se modelan correctamente. No significa que exista traducción aprobada para todos los idiomas ni cumplimiento contable/fiscal de todas las jurisdicciones.

R0 entrega interfaz completa en español e inglés, cambio de idioma, formato regional independiente, moneda tipada, datos invariantes y perfiles de importación. R1 mantiene esos requisitos en capacidad, órdenes, márgenes, instalación y reporting. Los nuevos módulos se someten al mismo contrato desde que se crean.

## 2. Configuraciones independientes

| Objeto | Ámbito y campos | Efecto permitido |
| --- | --- | --- |
| UserPresentationPreferences | UiLanguageTag, FormatCultureTag, DisplayTimeZoneId, DateDisplayPreset, NumberDisplayOptions | Cambia textos, formatos y visualización de instantes. No cambia datos económicos. |
| SourceParseProfile | CultureTag, DecimalSeparator, GroupSeparator, DatePattern, SignConvention, CsvDelimiter, Encoding, CurrencyColumn/FixedCurrency | Interpreta un archivo concreto. Se guarda con la procedencia. |
| StudyCurrencyContext | FunctionalCurrencyCode, CurrencyCatalogVersion, CurrencyMetadataSnapshot, FxPolicyVersion | Define la moneda y política económica de la ejecución. |
| FxRateSetVersion | Pares, cotizaciones, fechas, fuente, revisión y estado | Convierte originales a funcional antes del reparto, mediante una decisión explícita. |
| ExportContext | UiLanguageTag, FormatCultureTag, DatePattern, TimeZoneId, CurrencyCode, PrecisionView, TemplateVersion | Define una exportación reproducible y comprensible por su destinatario. |
| AccountingPolicyContext | Purpose, FrameworkId?, JurisdictionId?, Version | Aplica únicamente las reglas económicas/normativas elegidas para ese módulo. |

Idioma es preferencia de interfaz, región es formato y moneda es dato económico. No existe un selector de país que cambie las tres cosas a la vez. En primer inicio se pueden sugerir preferencias del sistema; después se guardan por usuario y se muestran de forma explícita.

Se permite UiLanguage=en y FormatCulture=es-ES, moneda USD y DisplayTimeZone=Europe/Madrid. El informe puede exportarse en español con formato en-GB sin alterar la ejecución que analiza.

## 3. Idiomas, recursos y traducción

Se propone .resx/ResourceManager con catálogo neutral inglés, catálogo español y claves semánticas. Ejemplos, Action.Calculate, Import.InvalidAmount, Run.RevisionChanged, Capacity.UnassignedActivity. El catálogo de idiomas indica etiqueta, nombre nativo, dirección de lectura, cobertura, revisión y estado publicable.

Un idioma solo aparece como disponible cuando sus claves obligatorias y parámetros están verificados y sus pantallas han sido revisadas. El inglés es respaldo final para una clave faltante, con registro técnico de la falta. En CI, una falta del catálogo publicado hace fallar la comprobación. El respaldo evita una pantalla rota; no autoriza entregar traducción incompleta.

Los mensajes son unidades completas con argumentos tipados. No concatenar «Se han importado» + n + «filas». Se definen formas de plural necesarias para los idiomas publicados, con un puerto IPluralRule para ampliar a otras familias. Las claves, placeholders y tipos se comparan entre catálogos. Moneda, importes, IDs y dates son argumentos, no texto preformateado por el dominio.

Se externalizan menús, botones, etiquetas, columnas, validaciones, diagnósticos, tooltips, ayuda, estados, instalador, licencias e informes. Los nombres de cuentas, centros, productos y observaciones introducidos por el cliente se conservan en su idioma original. Las categorías del sistema tienen código estable y etiqueta traducible; el valor almacenado no es «Principal» o «Auxiliar» en español.

El cambio de idioma actualiza bindings y mensajes desde un servicio observable. El adaptador WPF maneja Language y FlowDirection sin acoplar WPF a Domain. En cualquier edición de texto pendiente, el cambio de formato regional exige confirmar o cancelar usando la cultura capturada al empezar esa edición; no vuelve a parsear el mismo texto con otra cultura en silencio.

Se usan layouts flexibles, escalado de texto, navegación de teclado, nombres accesibles y formato adecuado de números. Una pseudotraducción de al menos 40 % más longitud comprueba recortes. Una vista de prueba RTL comprueba orientación y orden de lectura; IDs, fórmulas, códigos de cuenta y cadenas canónicas mantienen dirección apropiada. No se anuncia traducción árabe por pasar una prueba RTL.

No se usa LocBaml como base para .NET actual. Se elige el flujo de recursos compatible con el framework y se comprueba en T01A.

## 4. Fechas, instantes y calendarios

DateOnly representa PeriodStart, PeriodEnd, DocumentDate y RateDate. Se guarda ISO yyyy-MM-dd en calendario gregoriano. No se convierte a UTC, porque no es un instante. El primer alcance permite presentación regional de fechas gregorianas; otro calendario exige adaptador y pruebas, no inferencia por idioma.

Un instante de auditoría o ejecución se guarda UTC, mediante DateTimeOffset/campo canónico con offset y resolución definida. La zona de usuario se guarda como identificador estable y se resuelve mediante adaptador TimeZoneResolver. Cambiar zona modifica la visualización del instante y su offset, pero no periodo contable ni cantidad económica.

La conversión de hora local a instante rechaza una hora inexistente por cambio de horario y solicita offset/elección si es ambigua. No adivina entre dos instantes que muestran la misma hora. Las auditorías se generan en UTC desde IClock; su texto localizado se obtiene después.

Para una fecha ambigua en texto, el origen debe declarar DatePattern. 01/02/2026 con dd/MM/yyyy representa febrero 1; con MM/dd/yyyy representa enero 2. Con perfil ISO solo se acepta la forma declarada. La UI puede sugerir pero no confirmar el perfil sin previsualización.

La identidad del periodo se compara por valores DateOnly. Año fiscal, semana inicial y calendario laboral son datos de negocio configurables; no se deducen de la cultura visual ni se fijan a enero-diciembre cuando el estudio permite otro intervalo.

## 5. Números y parsing

El motor recibe decimal y fechas ya tipadas. Nunca interpreta un texto de usuario, una cuenta o una fórmula Excel. El adaptador de importación y la edición UI utilizan una política de parsing explícita.

Cada parsing valida separadores y agrupación; NumberStyles flexible no basta si aceptaría un patrón ambiguo. 1.234,50 de un origen español y 1,234.50 de uno estadounidense producen el mismo decimal 1234.50. No se permite Replace de coma por punto global, porque destruiría el separador de miles. Un espacio normal, NBSP o narrow NBSP solo se acepta si el perfil lo declara como agrupación.

Las culturas se identifican con etiquetas estables; la implementación verifica las admitidas en .NET y documenta cómo gestiona overrides del sistema. Los perfiles iniciales de pruebas usan separadores y patrones declarados para evitar depender de variaciones NLS/ICU del equipo. Las preferencias de usuario se guardan como datos y no afectan al hash del motor.

Persistencia y JSON usan texto decimal invariante, sin agrupación, exponente o símbolos de moneda. Códigos de cuenta, SKU y claves se comparan de forma ordinal conforme a su política; no usar ToUpper de cultura actual para generar identidad. La ordenación alfabética visible puede ser regional sin alterar IDs, matrices o desempates.

Se diferencian null, cadena vacía, cero e inválido. El diagnóstico contiene Code, Severity, MessageKey, TypedArguments y FieldLocation. El presenter traduce y formatea después. Un motor ejecutado con es-ES, en-US o tr-TR produce el mismo ledger para el mismo snapshot.

## 6. Monedas y precisión

Money es Amount y CurrencyCode. Amount sigue decimal y PDEC-1; sumar/restar requiere misma moneda, salvo operación FxConvert explícita. Cantidad física y tarifa tienen unidades; una tarifa contiene CurrencyCode y denominador de unidad de driver.

CurrencyDefinition incluye AlphaCode, NumericCode, MinorUnitDigits?, IsMonetary, Validity, CatalogVersion y fuente. Los códigos y minor units se obtienen del catálogo oficial y se mantienen versionados. Una ejecución congela los metadatos aplicados. Actualizar catálogo no modifica resultados anteriores ni introduce otra moneda en un estudio existente.

EUR y USD tienen 2 minor unit digits, JPY 0 y KWD 3 en la lista consultada. Los cuatro sirven de pruebas; la aplicación puede cargar el catálogo monetario completo. Códigos especiales sin unidad menor numérica requieren política expresa o se excluyen de la selección funcional inicial, sin inventar 2 como defecto.

La escala de adjudicación económica permanece 8. CurrencyProjection sigue CM04: si el origen no es múltiplo de unidad menor, redondea su Target a par y adjudica mayores restos al Target, registrando la diferencia visual sin alterar el ledger. La proyección a moneda usa quantum = 10^-MinorUnitDigits. No se fija quantum 0,01 para todas las monedas. Tarifas, porcentajes y cantidades no se recortan a los decimales de presentación de moneda. El redondeo de efectivo sería una política de liquidación distinta y no pertenece al motor analítico inicial.

El CurrencyFormatter recibe Money, Culture y DisplayPolicy. Política inicial de código ISO visible. Con USD 1234.50, formato es-ES produce 1.234,50 USD y en-US 1,234.50 USD. El formato nunca cambia CurrencyCode. Si se habilita símbolo, se identifica la moneda en encabezado/celda sin ambigüedad.

No aplicar ToString("C") con una CultureInfo que introduce el símbolo de una moneda distinta. Se puede componer formato numérico y código o usar un formateador de moneda explicitada, siempre probado.

## 7. Conversión a moneda funcional

Cada estudio tiene FunctionalCurrencyCode, sugerido por empresa al crearlo. El estudio ya calculado no cambia esa moneda mediante preferencias. Un cambio económico genera escenario nuevo y ejecución nueva.

La primera edición acepta originales en varias monedas cuando se proporciona el RateSet necesario. La base de cálculo mantiene una única moneda funcional por ejecución. Eso permite una ecuación de conservación monetaria interpretable sin impedir entradas internacionales.

Cada FxRate contiene SourceCurrency, TargetCurrency, Quote, RateDate, Source, RateSetVersion y estado. Quote es cantidad de destino por una unidad de origen. RawConverted = OriginalAmount × Quote. Si el usuario aporta el inverso, realiza conversión explícita de cotización y guarda orientación/procedencia; no aplicar una división por intuición.

Los tipos son manuales o importados en R0/R1. Son positivos, hasta 18 decimales en la propuesta, y la salida respeta límites de importe. Un proveedor futuro prepara RateSet mediante IFxRateProvider; la aplicación lo valida y publica. El motor utiliza los tipos congelados, sin red ni una actualización diaria automática. No se adopta un promedio mensual, cierre o cotización puntual sin la política elegida por negocio.

Se conservan OriginalMoney, ConvertedOriginalMoney, RateId y ConversionRoundingAdjustment. Ajustes guardan su Money de origen y FunctionalDelta convertido cuando procede. EffectiveFunctionalAmount = ConvertedOriginalAmount + suma de FunctionalDelta. ResourcePart, pools, directos, repartos y terminales están en moneda funcional.

La selección inicial de RateId por original/ajuste es explícita. Una actualización no cambia conversiones ya confirmadas; RevalueStudy reconstruye el puente en revisión nueva y revalida particiones/consumos de forma atómica. No se selecciona el tipo más cercano o una triangulación en silencio.

No se sobrescribe importe original. El puente explica cuánto aportó cada moneda y tipo a la base. Los totales originales se agrupan por moneda; no se suman nominalmente. Una diferencia de conversión no es subactividad ni residuo de reparto. La precisión de conversión a escala interna usa MidpointToEven para cada importe individual y registra diferencia; reparto posterior utiliza BalancedRound.

Tipo faltante, orientación incorrecta, moneda desconocida, fecha fuera de la política o salida fuera de rango bloquean preparación. Si un importe real convertido cae por debajo del quantum, se informa FX_AMOUNT_BELOW_QUANTUM; no se confunde con una lectura inválida que se convirtió en cero.

Una moneda de informe diferente a funcional requiere proyección económica nueva con tipo identificado. La primera entrega muestra funcional y originales; el cambio de región no actúa como esa conversión. Esta distinción queda preparada en ExportContext.

## 8. Importación y exportación internacionales

PrepareImport toma SourceParseProfile y ConversionPolicy. Devuelve filas originales, interpretaciones tipadas, moneda original, funcional, tipo, diferencia y errores. ConfirmImport conserva todo y es atómico. El idioma del botón Confirmar no decide los separadores del origen.

CSV tiene modos CanonicalExchange y HumanLocalized. Canonical usa cabeceras técnicas estables, fecha ISO, decimal invariante, código de moneda y encoding UTF-8 declarado. HumanLocalized identifica cultura, separador, fecha, idioma y precisión en el manifiesto. Ambos neutralizan contenido interpretado como fórmulas.

XLSX distingue celdas de número/fecha de las de texto. Cuenta/SKU se exporta texto; importes autorizados de precisión se acompañan de columna/hoja canónica si Excel no puede representarlos sin pérdida. No se promete round-trip exacto de decimal de 28 dígitos en una celda numérica de Excel. Las vistas ordinarias y ledger canónico tienen contratos de precisión separados.

PDF/reporting recibe ExportContext explícito y consume resultados publicados. La traducción no calcula nuevas tarifas ni convierte nominalmente por símbolo. Cada informe indica RunId, base, política, periodo, moneda y precisión.

## 9. Módulos y dirección de dependencias

| Módulo | Depende de | No depende de |
| --- | --- | --- |
| Domain | Biblioteca base, valores y políticas de dominio | UI, CultureInfo actual, SQLite, HTTP y traducciones. |
| Calculation | Domain y contratos puros | Application, Infrastructure, Localization, Reporting y WPF. |
| Application | Domain, Calculation y puertos propios | Implementaciones de SQLite, WPF, recursos de texto y proveedores. |
| Localization | Contratos/valores tipados y recursos .NET | Calculation, SQLite y WPF. |
| Reporting | Contratos de consulta y Localization | Fórmulas duplicadas, UI WPF y datos vigentes fuera de la ejecución. |
| Infrastructure | Application y adaptadores necesarios | Desktop y ViewModels. |
| Desktop | Composición, Application y adaptadores de presentación | Fórmulas de negocio propias. |
| Runner | Application/Calculation según caso y adaptadores de archivo | Desktop y red obligatoria. |

Los puertos están donde se consumen. IStudyRepository, IRunStore, IImportAdapter, ICurrencyCatalog, IFxRateProvider, IExportWriter, IBackupService, IClock e ILicenseProvider tienen contratos acotados. Una implementación de exportación puede usar Reporting; Application no conoce esa dependencia concreta. Evitar el ciclo Infrastructure↔Reporting mediante interfaces de lectura y composición.

Los contratos de snapshot, archivo, mensajes, importación y resultados tienen versión. Un cambio aditivo lleva compatibilidad comprobada; un cambio de significado crea versión nueva y migración. La traducción puede cambiar un texto sin cambiar Code, PolicyVersion o resultado.

No exponer DbContext o proxies a módulos consumidores. Consultas devuelven DTO; comandos validan un agregado y su Revision. La lógica de revisión/publicación pertenece a Application y no a un repositorio que invente reglas económicas.

## 10. Extensión y escalabilidad

Nuevo idioma, añadir catálogo y pruebas. Nuevo origen, implementar IImportAdapter con staging y SourceParseProfile. Nueva moneda, metadatos versionados, reglas de representación y pruebas. Nuevo proveedor de cambio, adapter que prepara RateSet. Nuevo método, política económica con requisitos, ejemplos y versión. Nuevos informes, proyección de resultados publicados.

Cada módulo se incorpora con una tarea, dependencia, caso y aceptación. No es necesario un motor de plugins de usuario, reflexión masiva ni microservicios. La estructura es un monolito modular con puertos y límites.

Para volumen se mide importación, tamaño de trazas, factorización, consultas, memoria y cancelación. Se permite representación dispersa y vistas paginadas sin eliminar origen. La factorización se reutiliza para matrices idénticas. Los idiomas no duplican hechos de coste; sus etiquetas se presentan al consultar.

SQLite sigue siendo un archivo local con un editor. La abstracción de repositorio facilita otra persistencia, pero edición simultánea requiere diseño de concurrencia, permisos y despliegue. Se verificará como modo adicional, preservando Calculation. Separar ampliación funcional, aumento de volumen y colaboración evita prometer una escalabilidad que no se ha probado.

## 11. Pruebas y puertas de salida

T01A publica la primera UI bilingüe y preferencias. T02A implanta monedas/tipos y valores canónicos. T06A comprueba interpretación internacional antes de primario. T22A comprueba informes completos antes de R1. Cada tarea funcional verifica recursos, formatos y ausencia de cultura en el motor.

casos_internacionales.json contiene I01–I15, criterios de aceptación de la aplicación futura. No se han ejecutado contra .NET o WPF porque esa aplicación todavía no existe. C33–C39 son referencias matemáticas adicionales de conversión y cuantización; el oráculo Fraction sí las comprueba.

La release exige catálogos completos publicados, placeholders compatibles, misma salida económica al cambiar idioma/cultura, importaciones ambiguas diagnosticadas, JPY/KWD sin precisión fija de 2, fechas sin desplazamiento, FX congelado, exportación inequívoca y snapshots históricos preservados.

## 12. Fuentes primarias y límites

Microsoft, WPF globalization/localization overview. https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-globalization-and-localization-overview

Microsoft, CultureInfo y formatos numéricos. https://learn.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo?view=net-10.0 y https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings

Microsoft, DateOnly/TimeOnly. https://learn.microsoft.com/en-us/dotnet/standard/datetime/how-to-use-dateonly-timeonly

SIX, agencia de mantenimiento ISO 4217 y listas oficiales. https://www.six-group.com/en/products-services/financial-information/market-reference-data/data-standards.html

Se consultó List One XML publicada el 17 de septiembre de 2026 para verificar EUR/USD/JPY/KWD. El corpus usa cotizaciones sintéticas, no cotizaciones obtenidas de un mercado. Estas fuentes sustentan tipos, recursos y metadatos; las decisiones de estructura y políticas económicas son diseño propuesto de VALDRIO.
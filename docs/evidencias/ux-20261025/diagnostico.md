# VALDRIO — Diagnóstico y propuesta de experiencia de usuario

Referencia exclusiva: `JavierRB35/VALDRIO`, rama `main`, commit `e198d2873396e505b96b692d0169b6f4ba644822`. Revisión: 25 de octubre de 2026. Perfil: analista de costes que conoce contabilidad analítica, pero no la implementación de VALDRIO.

## 1. Conclusión y alcance

VALDRIO contiene buena parte de las explicaciones y controles económicos necesarios. El principal problema es que obliga al analista a distinguir demasiados estados, ámbitos de guardado y formas de operar. La misma apariencia de tabla puede significar guardado inmediato, borrador pendiente o resultado consultable. La aplicación también alterna nombres económicos con términos internos y conserva mensajes que contradicen funciones ya disponibles.

La prioridad es hacer inequívocos el contexto, el efecto de cada acción y el alcance del resultado. No es necesario sustituir el modelo económico ni eliminar opciones avanzadas.

Se han revisado las entradas del menú, las cuatro pestañas de estructura, las cuatro de importación, las superficies de preparación de cuentas y repartos, reutilización, datos físicos, calculadora, importación física, servicios auxiliares, tarifas, catálogo y composiciones, las cinco pestañas de resultados, preferencias, ayuda y confirmaciones. Las condiciones de acceso se han seguido desde la navegación hasta los controles y sus operaciones. Se incluyen apartados avanzados que el usuario puede desplegar y operaciones habilitables al cumplir sus requisitos. Se excluyen controles ocultos sin recorrido normal y módulos futuros.

**Método:** revisión estática del commit exacto descargado y contraste con `VALDRIO_Auditoria_Usabilidad_main_2026-10-25.md`. No se ha ejecutado ni modificado la aplicación. No hay pruebas nuevas de renderizado, uso con teclado, integridad de un proyecto ni comprensión humana. No se han ejecutado GitHub Actions.

### Cómo leer los hallazgos

- **C-T:** contradicción o ambigüedad textual comprobada en una superficie accesible.
- **C-P:** patrón de presentación o comportamiento comprobado en código. Su efecto sobre la comprensión es una hipótesis que debe observarse con usuarios.
- **R:** riesgo de interacción pendiente de reproducción. Se separa en la sección 7.
- **Conservar:** capacidad ya presente; no se propone reconstruirla ni se afirma que haya fallado.

P1: puede conducir a una interpretación económica incorrecta, una operación equivocada o un bloqueo importante. P2: aumenta esfuerzo, aprendizaje o incertidumbre. P3: mejora secundaria. **No se acredita ningún P0 ni pérdida real de datos.** Las prioridades expresan impacto potencial, no frecuencia medida.

Cada referencia E enlaza al archivo y línea inicial relevante del commit congelado; los nombres y claves de texto identifican la evidencia dentro del archivo. Las propuestas son especificaciones pendientes, no funciones ya implementadas.

## 2. Correcciones y matices a la auditoría recibida

1. **Unidades en Proyecto:** el formulario existe en el archivo de esa pantalla, pero se retira y se inserta en Centros o Nodos. No debe contarse como formulario visible en Proyecto. Sí hay ayudas que todavía dicen que las unidades se crean allí. [E03] [E04] [E26]
2. **Nombre del proyecto abierto:** ya se presenta por separado y el formulario de creación se oculta al abrir un proyecto. No se considera vigente la incidencia antigua del nombre vacío. [E03]
3. **Importación de nuevas divisas:** el recorrido normal fija la moneda del análisis; conserva lógica histórica de conversiones, pero no ofrece sus antiguos controles. No se propone reintroducirlos. [E08]
4. **Servicios auxiliares:** hay tres estados de tratamiento, incluido «Pendiente de evaluación», además de las dos alternativas de tratamiento. No debe perderse esa posibilidad ni elegirse una política por el usuario. [E20] [E25]
5. **Estructura y tarifas:** ya existen árbol de revisión, explicaciones de finalizar, unidades monetarias por actividad, siete preguntas y aviso de conciliación parcial. La mejora consiste en jerarquizar y precisar; no en añadirlos como si faltaran. [E05] [E22] [E23]
6. **Capacidad normal:** el asistente ya muestra cálculo, frecuencia de paradas, ejemplos, actualización de propuesta y advertencia de guardado posterior. Debe conservarse. [E18]
7. **Resultados históricos:** ya hay distinción vigente/histórico y consulta sin recalcular. Lo contradictorio está en otro apartado: el historial textual del análisis. [E06] [E28] [E29]
8. **Reciprocidad:** hay explicación de circulación bruta, reparación y comprobación automática. No añadir una nueva autorización de autoasignación ni un paso obligatorio de viabilidad. [E14] [E15]
9. **Conciliación secundaria:** la directriz vigente retiró la tabla específica de conciliación por recurso de esa pantalla. Proponer un resumen y acceso a la conciliación existente de Resultados; no restaurar automáticamente la tabla retirada. [E34]
10. **Teclado y errores:** el commit incluye mejoras de tabulación y diagnóstico contextual. Su existencia se reconoce; su eficacia en toda combinación de pantalla, idioma y tamaño queda pendiente de prueba interactiva.

## 3. Inventario del recorrido accesible

Condiciones comunes: tener permiso de escritura y análisis abierto para las mutaciones que lo exigen; no estar procesando; resolver cambios pendientes. Un acceso a consulta no implica permiso para editar. No se evalúan como defectos las funciones que están deshabilitadas correctamente por estos requisitos. [E01] [E02] [E06]

| Entrada → superficie | Apartados y operaciones existentes | Condición y límite relevante |
|---|---|---|
| Proyecto | Copiar ejemplo; iniciar preparación para importar; abrir; crear con nombre y moneda propuesta | Disponible al inicio; abrir ejemplo guarda una copia elegida por el usuario |
| Proyecto abierto | Ver nombre; nuevo proyecto; ir a estructura, periodo o continuar cuentas | La continuación depende del análisis/importaciones disponibles |
| Proyecto → operaciones de archivo | Cerrar; copia de seguridad; restaurar; actualizar formato; cancelar operación; detalles técnicos | Cada comando depende del estado del archivo; actualización puede aparecer también como recomendación |
| Diseñar estructura → Estructura | Seleccionar/cargar; crear; nueva versión editable; recargar | Proyecto abierto; la estructura finalizada no se modifica directamente |
| → Centros | Alta/edición; tabla; activar/desactivar; catálogo y creación/activación de unidades | Mantener relación con usos históricos y reglas existentes |
| → Nodos de cálculo | Selección; centro, código, descripción de grupo, papel, unidad; funciones; nueva unidad; guardar/nuevo/eliminar | Edición condicionada por versión y análisis; la identidad de la estructura y las adaptaciones del análisis no son lo mismo |
| → Revisar y finalizar | Guardar; comprobar; finalizar; árbol; detalle técnico; descartar/cancelar | Se preserva la validación anterior a finalizar |
| Periodo / análisis | Abrir/actualizar lista; crear otro periodo; editar contexto; guardar; acceder a actividad | Requiere estructura finalizada para crear un análisis útil |
| → otras operaciones | Revisar archivo sin incorporarlo; punto de control técnico; cerrar/reabrir análisis; descartar/cancelar; instantáneas y definiciones históricas | Una instantánea de entrada no equivale a una ejecución económica |
| Importar saldos → Elegir archivo | CSV/Excel, hoja, significado económico; filas iniciales, separador/codificación; sustitución; comparación acumulada | Destino de importación guardado. La revisión sin destino no permite incorporar datos |
| → Indicar columnas | Cuenta, descripción, importe con signo o Debe/Haber; formato numérico; revisar errores y muestra | Tras lectura del archivo; mantiene códigos textuales y procedencia |
| → Elegir registros | Marcar/excluir, todas/ninguna, resumen y conciliación de selección | Tras comprobación de columnas; agrupación conserva detalle de origen |
| → Revisar y finalizar | Totales, grupos, diferencias acumuladas si corresponde, aprobación, guardar datos, volver/cancelar | Guardado explícito y aprobación; primario cerrado impide nuevas incorporaciones |
| Preparar cuentas | Tabla de cuentas/costes analíticos; corrección manual; actualizar; historial | Tras importación confirmada. Aquí no se muestra el editor de destinos |
| → reglas extracontables | Fichas con nombre/patrón/código/operación/valor/motivo; guardar, aplicar, previsualizar/confirmar, deshacer, duplicar, retirar | Guardar ficha y aplicar no son la misma operación |
| Reparto primario → claves | Biblioteca; cargar/nueva/nueva versión/eliminar/recargar; criterio; destinos/pesos/unidad; guardar | Tras importaciones; edición de claves usadas restringida |
| → comportamiento | Fijo/variable/mixto; porcentaje variable; selección/búsqueda/filtro; guardar cambios | Porcentaje fijo mixto calculado; se conserva hipótesis fija por defecto |
| → reglas y asignaciones | Regla por cuenta/prefijo; guardar/aplicar; excepciones; importes absolutos; nombre visible y procedencia | Estas pestañas pertenecen al primario; las del secundario se separan por código |
| → asignar claves a cuentas | Selección directa por fila, asignación múltiple a pendientes y cobertura | La selección directa persiste la asignación; no equivale a editar una regla |
| → detalle de cuentas | Partes/destinos; directo a objeto y alta de objeto; centro/grupo; exclusión; pendiente; comportamiento/contexto/capacidad; revisión/motivo; guardar/descartar | Apartado especial accesible desde primario; al abrirlo se desactiva el editor de claves |
| → mantenimiento de preparación | Revalorización y reinicio con motivo/confirmación; historial | Solo en el contexto que muestra herramientas de distribución; no son nuevos controles de divisas de la importación normal |
| → resultados y cierre | Cuenta→destinos, totales por nodo, explicación gráfica; cerrar/reabrir | Secundario requiere primario cerrado; reabrir está sujeto a los cierres posteriores |
| Reutilizar reglas | Origen; previsualización; selección; revisión de conflictos; adoptar/cancelar | Análisis compatible, permisos y fases editables; adopta/aplica reglas, no saldos ni horas |
| Reparto secundario | Claves secundarias; asignación por auxiliar; grupos por origen; completar ausentes; calcular/cancelar; reparar; liquidación, aportaciones, origen y gráficos; cerrar/reabrir | Primario cerrado; conservar rutas recíprocas y autoasignación permitida |
| Actividad y capacidades | Selección de análisis; tabla normal/real/utilización/coste/estado; fuentes, calidad, motivos; capacidades adicionales; guardar borrador/confirmar/anular por fila; historial | Acceso desde menú y análisis; escritura según estado del análisis; no guarda todas las filas con una sola acción |
| → calculadora | Calendario, histórico representativo o cifra declarada; previsualizar; adoptar/cancelar | La adopción cambia la fila en edición; todavía necesita guardado |
| → importación física | Archivo, hoja/separador, previsualización, selección sustitutiva, motivos, importar borradores | Guarda borradores físicos; no confirma observaciones |
| Servicios auxiliares | Selección, tratamiento, servicios por destino/grupo, preguntas; guardar, validar, calcular, importar, actividad, tarifas, reabrir, actualizar, exportar | Edición depende del cierre secundario; cálculo guarda una ejecución; exportación actual es archivo de datos |
| Tarifas de centros | Lista, filtro de incidencias, siete respuestas, detalle por componentes/procedencia; actualizar, actividad, origen de costes, exportar | Consulta con análisis guardado; el resultado depende de costes vigentes y actividad confirmada |
| Productos y servicios → Catálogo | Alta, quitar elemento nuevo, editar campos permitidos, activar/desactivar, guardar | Mantener identidades guardadas y referencias |
| → Composición | Producto/versión; abrir/nueva; lote/nota; líneas/componentes/unidades/factor/aplicación/merma; guardar/publicar; calcular necesidades; preguntas y detalle; recargar/volver | Versión publicada protegida; necesidades físicas, sin valoración ni consumo real |
| Resultados y trazabilidad | Calcular y guardar; cancelar; recargar; seleccionar ejecución; vigencia, estado y totales | Análisis existente; los requisitos económicos siguen aplicándose al cálculo |
| → cinco pestañas | Primario gráfico; secundario gráfico; coste por centro y desglose por nodo; detalle comprobable; conciliación | Solo lectura de ejecución seleccionada; selección/zoom/etiquetas no recalculan |
| Preferencias | Idioma, formato regional, zona horaria, reintentar guardado si falla | Disponible sin proyecto |
| → herramientas de desarrollo | Ejemplos de formato/fechas, divisas, redondeo/metadatos monetarios y edición numérica | Desplegable realmente accesible desde Preferencias; no confundir con el modelo económico abierto |
| Ayuda y diálogos | F1, ayuda contextual, ayuda de campo; guardar/descartar/permanecer; confirmaciones económicas; selección de archivos; historial físico | Son parte del recorrido y deben probarse con teclado y textos largos |

## 4. Matriz de problemas y propuesta por pantalla

En cada fila, el hecho C es comprobado; la dificultad y el error posible son hipótesis razonadas, no incidentes observados. El criterio de aceptación es la comprobación futura concreta.

### 4.1 Navegación, contexto y acciones generales

| ID / gravedad / certeza | Evidencia y situación actual | Dificultad / error posible | Propuesta concreta y aceptación |
|---|---|---|---|
| UX01 · P2 · C-P | Menú sin números, títulos con números parciales, avance lineal y accesos por tareas coexisten. [E01] [E02] | Creer que actividad, tarifas y composiciones son pasos sucesivos obligatorios | Cabecera con ruta económica y siguiente requisito real. Mantener menú por tareas, sin nueva numeración global. Aceptación: explicar qué falta y poder volver a un análisis sin recorrer pasos ya terminados |
| UX02 · P1 · C-P | Contexto global contiene proyecto/estructura/análisis, pero no siempre periodo, moneda o estado; las páginas integradas añaden contextos distintos. [E01] [E02] [E17] | Introducir datos en otro periodo o leer un resultado antiguo como actual | Contexto común: proyecto · análisis · fechas · moneda · estado y versión consultada cuando corresponda. Aceptación: visible sin desplegar detalles; al cambiar análisis cambian todos sus elementos |
| UX03 · P1 · C-T | «Continuar a claves y reglas», «Reparto primario», «Flujos y ejecuciones» y «Resultados y trazabilidad» nombran destinos relacionados de distintas maneras. [E02] [E24] [E25] | Buscar una pantalla con un nombre que no aparece en el menú | Usar el nombre visible de destino en cada ayuda y botón. «Ir a Reparto primario» / «Ver Resultados y trazabilidad». Aceptación: ninguna instrucción remite a una entrada inexistente |
| UX04 · P1 · C-P | Sí guarda en los diálogos de estructura/composición; Sí descarta en actividad y auxiliares. [E30] [E31] | Repetir una respuesta aprendida y descartar trabajo | Botones con verbos: «Guardar y continuar», «Descartar cambios», «Seguir editando», solo donde ya exista esa operación. Donde no haya guardado conjunto: «Volver a guardar las filas» y «Descartar y salir». Aceptación: nunca hace falta interpretar Sí/No para saber si se pierde un borrador |

### 4.2 Proyecto, archivo y estructura

| ID / gravedad / certeza | Evidencia y situación actual | Dificultad / error posible | Propuesta concreta y aceptación |
|---|---|---|---|
| UX05 · P2 · C-T | «Importar contabilidad» en inicio prepara nombre/moneda y encadena estructura/periodo, no abre el archivo contable. [E03] [E24] | Esperar seleccionar un Excel y pensar que el botón no funciona | «Preparar un proyecto para importar contabilidad». Mostrar tres decisiones principales: abrir, crear, explorar ejemplo. Aceptación: el usuario anticipa que primero necesita proyecto, estructura y periodo |
| UX06 · P2 · C-P | Copia, restauración, actualización y cierre comparten operaciones de archivo. Ya están desplegadas aparte. [E03] | Confundir restaurar una copia con reemplazar el proyecto activo o actualizar datos con actualizar formato | Mantener apartado secundario, con «Crear copia de seguridad», «Restaurar copia en un archivo…», «Actualizar formato del archivo». Resumen de origen/destino y efecto real antes de confirmar; no inventar reversibilidad. Aceptación: identificar el archivo afectado y cancelar sin escritura |
| UX07 · P1 · C-P | Centro, nodo, grupo de costes y unidad requieren decisiones diferenciadas; «pool» persiste en encabezados y mensajes. [E05] [E24] | Crear un centro por cada grupo de costes o confundir función auxiliar con naturaleza fija | Conservar entidades; explicar «Centro: Taller. Unidad de cálculo: Corte. Actividad: horas. Grupo de costes: costes de Corte». Usar «Unidad de cálculo (nodo)» en primera ayuda y «Grupo de costes» para pool. Aceptación: construir dos nodos en un centro sin duplicarlo |
| UX08 · P2 · C-P | Descripción opcional del grupo aparece antes del código obligatorio; listas y formularios separados en centros/nodos. [E05] | Empezar por un campo opcional o no saber qué fila se edita | Obligatorios primero, contexto de selección claro; propuesta de edición en tabla para valores editables, preservando guardado y altas. No duplicar los mismos campos en tabla y formulario. Aceptación: identificar fila/campos pendientes y guardar el registro correcto |
| UX09 · P1 · C-P | Guardar nodo ya persiste el borrador; guardar estructura, comprobar y finalizar siguen existiendo. Hay además adaptación de nodos del análisis. [E04] [E05] [E32] | Creer que un nodo solo se guarda al finalizar o que una modificación del análisis cambia la plantilla reutilizable | Etiqueta de ámbito «Estructura reutilizable» / «Nodos de este análisis» y mensajes de guardado específicos. Aceptación: distinguir guardar un nodo, finalizar una estructura y modificar su uso en un análisis; no prometer identidad nueva donde no se permite |
| UX10 · P2 · C-T | Alta de unidad pide «Dimensión» y ayuda con `time`/`batch`; «Activar / desactivar unidad» es una acción dual. [E03] [E24] | Introducir traducciones incompatibles o no prever el cambio de estado | «Magnitud que mide: tiempo, cantidad…» con correspondencia visible a valores admitidos, conservando códigos existentes; botón según estado. Aceptación: crear una unidad compatible sin conocer vocabulario interno ni modificar unidades previas |

### 4.3 Periodo, instantáneas y consulta anterior

| ID / gravedad / certeza | Evidencia y situación actual | Dificultad / error posible | Propuesta concreta y aceptación |
|---|---|---|---|
| UX11 · P1 · C-T | `RunHistory` devuelve siempre `Study.RunHistoryEmpty`: «Ejecuciones: ninguna. El cálculo de costes se entrega en una fase posterior». Se muestra en Otras operaciones. [E06] [E07] | Concluir que se perdieron cálculos o que VALDRIO todavía no calcula | Sustituir por información real si está disponible o texto neutral «Consulta los cálculos guardados en Resultados y trazabilidad» con navegación. Aceptación: con ejecuciones existentes nunca afirmar que no existen o que el cálculo es futuro |
| UX12 · P2 · C-P | Punto de control técnico, instantáneas, revisión de entrada y cierre de análisis se agrupan. [E06] [E07] | Confundir capturar datos con calcular o cerrar una ventana con cerrar el análisis | «Historial de datos de entrada» y «Crear punto de control de datos (avanzado)»; explicación «Conserva entradas; no calcula costes». Botón de cierre «Cerrar este análisis para edición». Aceptación: consulta histórica no cambia entradas ni publica resultados |
| UX13 · P2 · C-P | Abrir análisis, nuevo periodo con estructura y edición del contexto comparten página; la estructura seleccionada condiciona el destino. [E07] | Cambiar fechas de un análisis anterior pensando que crea otro mes | «Abrir periodo existente» / «Crear otro periodo» como acciones separadas; resumen del análisis actual antes de editar. Aceptación: crear segundo mes conserva íntegro el primero y no implica copia automática de saldos |

### 4.4 Importación contable

| ID / gravedad / certeza | Evidencia y situación actual | Dificultad / error posible | Propuesta concreta y aceptación |
|---|---|---|---|
| UX14 · P1 · C-P | El significado de los importes se elige antes de revisar columnas; acumulados requieren conservar una comparación. [E08] [E09] | Importar saldo acumulado como gasto del mes o invertir anterior/actual | Tres ejemplos junto al selector y resumen persistente: «Movimientos de este periodo», «Saldo al cierre», «Diferencia entre dos acumulados». Ejemplo didáctico: 12.000 − 9.000 = 3.000, sin insertar cifras en el proyecto. Aceptación: usuario explica qué representa el total antes de confirmar |
| UX15 · P1 · C-P | Debe/Haber, importe con signo, formatos y cuentas numéricas de Excel son decisiones distintas. [E09] | Duplicar abonos, cambiar signos o perder ceros iniciales | Muestra «Texto original → importe interpretado» con cuenta/descripcion, signos y unidad. Ejemplo Debe 100/Haber 20 → 80, solo bajo la interpretación aplicable. Mantener advertencia de cuenta numérica. Aceptación: negativos y códigos `006005` se conservan; no interpretar moneda por apariencia |
| UX16 · P1 · C-P | Sustitución está en desplegables; el selector de lote concatena archivo, fecha y un identificador. [E08] [E09] | Añadir dos veces el mismo periodo o sustituir el archivo equivocado | Al activarse, tarjeta fija «Sustituir [archivo/fecha] por [archivo] en [análisis]»; ID en procedencia. Confirmación muestra incorporación/sustitución, seleccionados/excluidos, positivos/negativos/neto. Aceptación: el lote reemplazado sigue trazable y no se suma dos veces |
| UX17 · P2 · C-P | Confirmación devuelve ID de lote, número de recursos y revisión; el recorrido ya permite continuar a cuentas. [E08] | No saber si los datos quedaron guardados y dónde encontrarlos | «Importación guardada en [análisis]. [n] cuentas incorporadas. Siguiente: revisar correcciones». Identificadores en detalles. Aceptación: reapertura muestra mismo lote y origen, sin repetir importación |

### 4.5 Preparar cuentas, reglas extracontables y detalle especial

| ID / gravedad / certeza | Evidencia y situación actual | Dificultad / error posible | Propuesta concreta y aceptación |
|---|---|---|---|
| UX18 · P2 · C-P | Fichas de reglas extracontables y sus expanders empiezan abiertos antes de la tabla. [E10] | Pensar que siempre hay que crear una regla antes de revisar cuentas | Tabla y conciliación primero; «Crear un ajuste reutilizable» como acceso secundario. Una ficha con cambios o revisión pendiente permanece visible. Aceptación: completar caso sin ajustes sin entrar en reglas |
| UX19 · P1 · C-P | Corrección manual es un total que se guarda al terminar la edición; reglas y partes tienen confirmaciones/guardados diferentes. [E10] [E11] | Teclear 20 pensando añadir 20 al ajuste anterior, o salir suponiendo que nada se guardó | Encabezado «Corrección manual total» y microtexto «Se guarda al terminar la edición»; mostrar anterior/nuevo/efecto. No cambiarlo a incremento ni añadir un segundo guardado. Aceptación: dos ediciones sucesivas dejan el total esperado y mantienen historial |
| UX20 · P1 · C-P | Aplicar regla puede compensar cuentas y crear una línea analítica; deshacer restaura preparación previa con tratamiento específico de correcciones posteriores. La vista previa ya lo explica. [E10] [E25] | Interpretar porcentaje como reparto o deshacer como borrar solo la línea nueva | Conservar confirmación, convertir párrafo en antes/después por cuenta y nueva línea; «Guardar definición», «Revisar aplicación», «Confirmar ajuste»; advertencia específica al deshacer. Aceptación: explicar qué cuentas se neutralizan y qué sucede con correcciones posteriores |
| UX21 · P2 · C-P | Editor de partes y destinos reside en Detalle de cuentas del primario; al abrirlo se bloquea el editor general. [E02] [E10] | Creer que las claves dejaron de funcionar o que perdió el contexto | Cabecera «Tratamiento especial de [cuenta]» y «Volver a claves de reparto» siempre accesible; distinguir parte, destino y exclusión. Aceptación: volver conserva/descarta solo tras resolver pendientes y no cambia de cuenta inadvertidamente |
| UX22 · P1 · C-P | Revalorización, reinicio, motivos y confirmación existen en herramientas del detalle, con efectos sobre la preparación actual. [E10] | Usar reinicio como refresco o cambiar importes esperando modificar el Excel | Separar «Volver a leer datos guardados» de «Reiniciar preparación»; resumen de cuentas/ajustes/destinos afectados y preservación del historial. Aceptación: cancelar no cambia proyecto; ejecutar conserva trazabilidad y respeta reglas activas |

### 4.6 Primario: claves, comportamiento, reglas, excepciones y reutilización

| ID / gravedad / certeza | Evidencia y situación actual | Dificultad / error posible | Propuesta concreta y aceptación |
|---|---|---|---|
| UX23 · P1 · C-P | «Guardar clave» publica si está completa y guarda borrador si no lo está; los mensajes/estado posteriores distinguen disponible e incompleta. [E12] [E13] | Esperar un guardado siempre editable o intentar asignar una clave incompleta | Mantener operación: etiqueta contextual «Guardar clave disponible» / «Guardar borrador incompleto», con efecto y limitación antes de actuar. No imponer publicación adicional. Aceptación: predecir disponibilidad y restricciones de clave usada |
| UX24 · P1 · C-P | Criterio por porcentaje o unidad/ponderación comparte editor de destinos. [E12] | Entender un peso de 30 como 30 % sin comprobar total/base | Encabezado según criterio y porcentaje resultante de solo lectura cuando el motor lo proporciona; explicar base. Ejemplo: pesos 30/70 de total 100 equivalen a 30/70 %, pero pesos 30/30 equivalen a 50/50 %. Aceptación: distinguir medición de porcentaje y comprobar suma exigida |
| UX25 · P1 · C-P | Guardar regla no aplica; asignación directa en tabla sí guarda; excepciones prevalecen sobre reglas. [E12] [E13] [E25] | Cambiar una regla y creer que cambió el reparto o no entender por qué una cuenta no cambia | Tres zonas: claves, asignación a cuentas, automatización. Por fila: origen de asignación «Manual / Regla exacta / Prefijo / Importe». Aceptación: explicar una excepción y quitarla conscientemente para volver a reglas |
| UX26 · P1 · C-P | «Composición y comportamiento del coste», hipótesis fija por defecto y mixto conviven; «Composición» también se usa para productos. [E12] [E25] | Tomar fijo por defecto como clasificación revisada o mezclar porcentaje variable y porcentaje de reparto | «Comportamiento del coste»; estado «Fijo por defecto: pendiente de revisión» donde corresponda; en mixto solo variable editable, fijo calculado. Aceptación: ejemplo 1.000, 30 % variable → 300 variable/700 fijo sin cambiar destinos; no endurecer reglas de cierre ni cambiar hipótesis por diseño |
| UX27 · P1 · C-P | Cierre bloquea composición, nodos y asignaciones; existen resultados automáticos y cierre condicionado. [E12] [E13] [E32] | Confundir cerrar la fase con volver o con obtener todos los resultados finales | Resumen anterior al cierre de base repartible, asignado, pendiente, clasificación e incidencias, usando magnitudes existentes y ámbito explícito. Texto «Cerrar reparto primario para edición». Aceptación: conocer qué queda bloqueado y el efecto real de reabrir |
| UX28 · P1 · C-P | Adoptar reglas aplica automáticamente; no copia saldos, horas, ajustes, importes absolutos ni cierres. [E16] [E25] | Interpretar «reutilizar» como duplicar el mes o creer que solo copia una definición | «Adoptar y aplicar reglas seleccionadas»; resumen origen/destino, compatibles/conflictos/omitidas y cambios antes de adoptar. Aceptación: segundo mes mantiene sus saldos; ninguna medición antigua se acepta como actual sin revisión |
| UX29 · P2 · C-P | Nombres de cuenta pueden elegirse entre descripciones de procedencia en una pestaña avanzada. [E12] | Cambiar nombre esperando cambiar identidad o reparto | «Nombre mostrado de la cuenta» y aviso «No cambia el código, el saldo ni la asignación». Aceptación: resolver conflicto de nombres conserva todas las procedencias |

### 4.7 Secundario y reciprocidad

| ID / gravedad / certeza | Evidencia y situación actual | Dificultad / error posible | Propuesta concreta y aceptación |
|---|---|---|---|
| UX30 · P1 · C-P | Reglas por auxiliar, grupos, resultados, viabilidad, liquidación y explicación comparten una página extensa; varios bloques se abren inicialmente. [E12] [E14] [E15] | Buscar cómo repartir y encontrar primero demasiadas lecturas del resultado | Antes del cálculo: asignaciones y faltantes. Después: coste final por principal y conciliación resumida, con procedencia seleccionable. Detalles de viabilidad/circulación accesibles sin competir con totales. Aceptación: realizar reparto simple sin configurar grupos especiales |
| UX31 · P1 · C-P | Circulación bruta y aportaciones netas ya se distinguen en ayudas, pero se presentan junto a liquidación y gráficos. [E15] [E25] | Sumar circulación al coste de empresa o interpretar un saldo de verificación como gasto residual | Etiquetar cada total por ámbito: «Transferencias internas: no sumar» y «Coste final recibido». Diferenciar variación de capacidad retenida del descuadre. Aceptación: explicar un caso recíproco sin doble contar costes; no asumir que todo auxiliar termina siempre a cero |
| UX32 · P2 · C-T | Ayuda usa grupo «all» y términos de grafo; hay ya botón de reparación y viabilidad automática. [E15] [E25] | No saber qué auxiliar corregir ni qué significa el grupo general | «Todos los componentes» en presentación; nombres concretos de auxiliares sin salida y acción para revisar su clave. Conservar identificador interno. Aceptación: corregir circuito cerrado sin eliminar claves usadas ni añadir autorización de autoasignación |

### 4.8 Actividad, calculadora, importación e historial físico

| ID / gravedad / certeza | Evidencia y situación actual | Dificultad / error posible | Propuesta concreta y aceptación |
|---|---|---|---|
| UX33 · P1 · C-P | Se guardan/confirman filas individualmente; fuentes/calidad están en detalle de la seleccionada. Ya existe ayuda de alcance. [E17] [E19] | Creer que «Guardar borrador» guarda toda la tabla o confirmar la fila equivocada | Acción «Guardar fila [centro/nodo]» y «Confirmar datos de [centro/nodo]», contador de otras filas pendientes y selección persistente. Aceptación: editar dos filas y comprobar que guardar una no borra ni confirma la otra |
| UX34 · P2 · C-P | Capacidad normal/real en tabla; teórica/práctica en detalle; uso calculado. [E17] [E19] | Confundir capacidad normal con máximo físico o leer vacío como cero | Microdefiniciones y unidad/periodo; «Utilización = actividad real ÷ capacidad normal». Cero y sin dato distintos; motivos condicionales. Aceptación: normal 100/real 80 se interpreta como 80 %, no como un coste; normal cero no muestra división ficticia |
| UX35 · P2 · C-P | Calculadora ya distingue tres métodos, fórmula y adoptar sin guardar; historial tiene revisado/incluir/anomalía. [E18] [E25] | Adoptar creyendo confirmar o excluir histórico sin justificar | Conservar fórmula y ejemplo de paradas; estado al volver «Propuesta trasladada a [fila], sin guardar». Método histórico muestra periodo/unidad y motivo de exclusión. Aceptación: propuesta adoptada y cancelación no alteran observaciones confirmadas hasta la operación correspondiente |
| UX36 · P1 · C-T | Importación física requiere cabeceras `node`, `unit`, `period_start`, `period_end`, `normal` o `real`; ayuda general no enumera el contrato y aparece en error. Título dice «horas físicas» aunque las filas tienen unidad. [E19] [E25] | Preparar un Excel convencional y descubrir requisitos tras fallar; pensar que solo admite horas | «Importar actividad y capacidad»; formato esperado y ejemplo de archivo accesible antes de elegirlo, sin alterar el importador. Vista previa con «Nuevo / Sustituye versión…», periodo, unidad y estado borrador. Aceptación: preparar archivo sin ayuda técnica y confirmar después cada observación |
| UX37 · P2 · C-P | Historial físico es un mensaje con líneas versión/fecha/estado y dos cantidades separadas por `/`. [E19] [E30] | No distinguir normal/real ni comparar versiones extensas | Vista consultable con columnas versión, fecha, normal, real, unidad, estado y motivo; conservar contenido completo/copia y consulta sin cambios. Aceptación: identificar qué cifra cambió y por qué en diez versiones |

### 4.9 Servicios auxiliares y tarifas

| ID / gravedad / certeza | Evidencia y situación actual | Dificultad / error posible | Propuesta concreta y aceptación |
|---|---|---|---|
| UX38 · P1 · C-P | Diez botones; validar evalúa propuesta sin publicar, calcular guarda ejecución y exige cambios guardados; también existe estado pendiente. [E20] [E21] | Validar pensando guardar o calcular pensando solo previsualizar; ignorar evaluación pendiente | «Guardar borrador», «Comprobar servicios sin guardar resultados», «Calcular y guardar resultados». Un siguiente paso destacado según estado, conservando accesos. Aceptación: predecir efecto y distinguir pendiente, sin evaluación y evaluado |
| UX39 · P1 · C-P | Servicios por grupo/destino; cantidades iniciales de líneas ausentes se muestran como cero; grupos no se suman entre sí. [E20] [E21] [E25] | Tomar valores iniciales como evidencia comprobada o sumar las horas de todos los grupos | Marcar preparación pendiente y mostrar total de cada grupo frente a real. No alterar la semántica persistida de ceros sin decisión funcional. Aceptación: explicar por qué dos grupos completos no duplican actividad y no confundir sin evaluar con ociosidad cero |
| UX40 · P1 · C-T | CSV exige `Center;Group;Destination;Quantity`, pero resuelve Center y Destination contra códigos de nodo. [E21] [E25] | Usar código de centro físico válido y recibir rechazo | Ayuda «Código de unidad de cálculo auxiliar y de destino»; ejemplo ligado a códigos visibles. Mantener cabeceras compatibles y ofrecer tabla de correspondencia. Aceptación: importar con un centro que tiene varios nodos sin confundir identidades |
| UX41 · P2 · C-T | Auxiliares remite a T14; tarifas a T14/T19 y a «Flujos y ejecuciones». [E23] [E25] | Buscar módulos inexistentes o creer que necesita comprender el plan técnico | «Actividad y capacidades», «Resultados y trazabilidad» y «El consumo real por producto u orden no está disponible en esta versión». Aceptación: toda instrucción apunta a una tarea existente o declara limitación sin prometer fecha |
| UX42 · P1 · C-P | Tarifa con unidad, política y conciliación parcial ya presentes, pero la lectura principal es tabla más respuestas largas. [E22] [E23] | Confundir tarifa con precio, coste realizado con coste aplicado a órdenes, o ausencia con cero | Tarjeta del centro: tarifa/unidad, normal/real, coste propio/recibido y alcance; mantener siete respuestas y bases por componente. Aviso persistente «Conciliación parcial; no acredita consumo por orden». Aceptación: explicar denominadores distintos y variación negativa sin llamarla ahorro |
| UX43 · P2 · C-P | Exportaciones de auxiliares y tarifas son archivos de datos; auxiliar propone `VALDRIO-T16.json`. [E30] [E33] | Esperar un informe contable o un Excel al pulsar exportar | «Exportar datos de la ejecución (JSON)» / «Exportar datos de tarifas (JSON)» y nombre económico. Conservar exportación exacta; no prometer PDF/Excel inexistentes. Aceptación: anticipar formato y versión exportada |

### 4.10 Productos, recetas y necesidades

| ID / gravedad / certeza | Evidencia y situación actual | Dificultad / error posible | Propuesta concreta y aceptación |
|---|---|---|---|
| UX44 · P2 · C-T | Catálogo ayuda: «Las unidades se crean en Proyecto»; el acceso real está en estructura. [E04] [E26] | Salir a buscar un alta que no está allí | «Crea las unidades en Diseñar estructura → Centros → Catálogo de unidades, o al configurar un nodo». Aceptación: texto y ayuda llevan al acceso real preservando borrador |
| UX45 · P1 · C-P | Cantidad, factor, aplicación, merma/rendimiento se editan en líneas; ayuda conjunta larga. [E26] [E27] | Introducir 90 como factor, aplicar merma al revés o prorratear lote iniciado | Etiquetas «Conversión a unidad del componente», «Por unidad / por lote iniciado / por orden» y ejemplo contextual por fila. Exceso 10 % de 10 = 11; rendimiento 80 % para 10 útiles requiere 12,5 antes de conversión/redondeo aplicable. Aceptación: explicar las dos fórmulas y conservar reglas de unidades discretas |
| UX46 · P1 · C-T | Pantalla y respuestas nombran T18/T19; preguntas de coste/consumo muestran indisponibilidad. [E26] | Interpretar necesidades como consumo real o creer que hay valoración disponible | «Composición física y necesidades previstas»; «Esta pantalla calcula cantidades. La valoración monetaria y el consumo real no están disponibles aquí». Mantener respuestas de límites sin navegación futura. Aceptación: ningún resultado se presenta como importe o consumo real |
| UX47 · P1 · C-P | Catálogo y versión comparten Guardar; publicación protege versión; preview puede usar composición actual. [E26] [E27] | Confundir abrir selector con abrir versión; cálculo de borrador con composición publicada | Contexto «Producto · versión · borrador/publicada · cambios sin guardar» y simulación con cantidad/version utilizada. «Guardar catálogo» en catálogo y «Guardar borrador de composición» en receta. Aceptación: cálculo siempre identifica sus entradas y no publica al previsualizar |

### 4.11 Resultados, preferencias y ayuda

| ID / gravedad / certeza | Evidencia y situación actual | Dificultad / error posible | Propuesta concreta y aceptación |
|---|---|---|---|
| UX48 · P1 · C-P | Botón de nuevo cálculo y selector histórico conviven; ya hay aviso de vigencia y no recálculo. [E28] [E29] | Pulsar calcular para abrir histórico | Bloques «Consultar cálculo guardado» y «Crear/obtener resultado con las entradas actuales»; conservar reutilización de ejecución si no cambió revisión. Aceptación: consultar histórico no genera ejecución ni modifica entradas |
| UX49 · P1 · C-P | Tarjetas de coste efectivo y final aparecen antes de conciliación detallada; directo/excluido/pendiente pueden explicar diferencias entre magnitudes. [E28] [E29] [E25] | Exigir que coste final en centros sea igual a coste efectivo o considerar «Sin diferencias» igual a «Todo asignado» | Junto a totales: alcance de cada cifra y pendientes separados del estado de cuadre. Conciliación con todos los destinos terminales aplicables, incluidas variaciones. Aceptación: un saldo pendiente explícito puede cuadrar pero no etiquetarse como trabajo terminado |
| UX50 · P2 · C-P | Cinco pestañas, dos gráficos, leyendas, selección y zoom; detalle ancho con columnas de origen/destino/importes. [E28] | Perder selección o confundir etapas con sumandos | Resumen económico primero; gráficos como explicación con leyenda y selección ligada a tabla; dato exacto accesible sin ratón. Aceptación: seguir una cuenta hasta su destino y distinguir resumen redondeado del valor conservado |
| UX51 · P2 · C-P | Preferencias expone códigos regionales/zonas y un desplegable de herramientas de desarrollo. [E01] [E02] | Creer que el ejemplo de divisa cambia tipos del análisis | Preferencias con nombres comprensibles y muestra de formato; «Diagnóstico y ejemplos técnicos (avanzado)», aviso «No modifica los costes del análisis». Conservar herramientas; no crear un permiso técnico nuevo ni eliminarlas. Aceptación: distinguir ajustes personales de datos económicos |
| UX52 · P2 · C-P | Ayuda inferior tiene altura 48 y recorte, aunque hay tooltip y F1. [E01] [E35] | No leer el final de un requisito, especialmente sin ratón | Mantener altura estable; enlace accesible «Leer explicación completa» y errores junto al campo. No agrandar automáticamente al pasar el cursor. Aceptación: texto completo con teclado a 200 % y retorno al foco anterior |

## 5. Propuesta de interfaz y textos

### 5.1 Composición común de pantalla

Respetar verde profundo, superficies blancas, fondo claro, acento amarillo y jerarquía del sistema visual aprobado. No rehacer identidad ni añadir paneles sin propósito. [E36]

Maqueta textual de la propuesta; los corchetes indican datos existentes que se mostrarían, no valores inventados:

```text
[Proyecto] · [Análisis] · [Desde–hasta] · [Moneda] · [Estado]
Cálculo de costes > Reparto primario
Distribuir las cuentas entre unidades de cálculo
Objetivo: comprobar quién recibe cada coste y qué falta por asignar.

[Estado de guardado]   [Incidencias]   [Siguiente requisito real]

Tabla principal editable / resultado principal según estado
Cuenta y descripción | Importe | Destino o clave | Estado
Detalle de la fila seleccionada, con su origen y efecto

Resumen económico con ámbito y moneda
[Asignado] [Pendiente] [Diferencia de comprobación]

Opciones avanzadas: reglas, excepciones, procedencia, historial

[Volver a …]                  [Acción principal con efecto explícito]
Ayuda breve estable                                 [Leer explicación]
```

No forzar esta tabla en todas las vistas. Importación mantiene asistente; estructura conserva sus cuatro pestañas; resultados conserva cinco; actividad sigue en tabla; receta conserva catálogo y composición. Conservar posiciones de acciones locales junto a datos y barra inferior fija de cerrar/reabrir repartos.

### 5.2 Vocabulario de acciones: no son sinónimos

| Acción | Qué debe comunicar | Texto propuesto según caso |
|---|---|---|
| Introducir | Editar un dato; aún no implica persistencia salvo guardado automático declarado | «Cambios sin guardar» o «Se guarda al terminar de editar esta celda» |
| Previsualizar | Mostrar efecto sin incorporar el cambio que se está revisando | «Revisar importación sin guardar» / «Previsualizar ajuste» |
| Adoptar propuesta | Trasladar resultado a un borrador | «Usar esta capacidad en la fila; después debes guardarla» |
| Guardar borrador | Conservar entradas editables; no confirmar ni prometer resultado definitivo | «Borrador guardado; falta confirmar» |
| Guardar clave | Su efecto actual depende de integridad | «Guardar clave disponible» / «Guardar borrador incompleto» |
| Confirmar importación | Aceptar significado e incorporar datos al análisis | «Confirmar e incorporar datos a [análisis]» |
| Confirmar dato físico | Guardar observación como confirmada tras comprobar fuentes | «Confirmar datos de [nodo]» |
| Comprobar/validar | Revisar requisitos; no equivale a publicar resultado | «Comprobar servicios sin guardar resultados» |
| Calcular necesidades | Obtener cantidades previstas | «Calcular necesidades físicas» |
| Calcular y guardar | Crear resultado económico persistido, o reutilizar el vigente conforme al comportamiento existente | «Calcular y guardar resultados»; informar expresamente si ya existía sin cambios |
| Finalizar estructura | Fijar una versión reutilizable dentro del proyecto | «Finalizar estructura para utilizarla» |
| Publicar composición | Fijar esa versión y conservarla | «Publicar esta versión de la composición» |
| Cerrar fase/análisis | Bloquear la edición correspondiente | «Cerrar reparto secundario para edición» / «Cerrar análisis» |
| Volver/cerrar pantalla | Navegar; resolver pendientes sin cerrar la fase económica | «Volver a [pantalla]» |
| Consultar histórico | Leer entradas/resultados conservados, sin recalcular | «Consultar cálculo del [fecha] · histórico» |
| Recargar | Leer datos guardados; no guardar implícitamente | «Volver a leer datos guardados» |

Los estados son dimensiones distintas. Un dato puede estar guardado pero sin confirmar; una ejecución puede estar completada y ser histórica; una fase puede estar cerrada y requerir otra operación posterior. No inventar un único estado lineal «borrador→validado→vigente» para todas las entidades, ni persistir una validación que actualmente no existe como estado.

### 5.3 Textos exactos de ayuda, validación y confirmación

Aplicar solo si corresponden a las condiciones reales. Sustituir los campos entre corchetes por datos comprobados; omitir una frase si su dato o garantía no existe.

| Situación | Texto propuesto |
|---|---|
| Inicio de periodo | «Este análisis reúne los datos de [periodo] en [moneda]. Las nuevas importaciones se incorporarán aquí.» |
| Nueva importación | «Los importes del archivo deben estar expresados en [moneda]. VALDRIO no deduce su moneda ni realiza una conversión en este paso.» |
| Selección contable | «Seleccionadas: [n] filas. Positivos: [importe]. Negativos: [importe]. Neto que se incorporará: [importe]. Las filas excluidas no se incorporarán.» |
| Descripción ausente | «[código] · Sin descripción en el archivo de origen». No inventar nombres |
| Sustitución | «Vas a sustituir [archivo/fecha] dentro de [análisis]. Revisa los importes anteriores y nuevos. La importación anterior permanecerá identificada en el historial.» |
| Corrección manual | «Introduce el total de la corrección manual, no el importe final de la cuenta ni un incremento sobre la corrección anterior.» |
| Clave incompleta | «Borrador guardado. Todavía no puede asignarse: [requisito concreto].» |
| Regla guardada | «Regla guardada sin aplicar. Revisa su efecto y aplícala para actualizar las asignaciones.» |
| Base de reparto | «Este total incluye solo las partes destinadas a unidades de cálculo. Los importes directos, excluidos y pendientes se muestran aparte.» |
| Mixto incorrecto | «Para un coste mixto, introduce un porcentaje variable mayor que 0 y menor que 100. La parte fija será el resto. Para todo fijo o todo variable, cambia el comportamiento.» |
| Observación sin fuente | «Completa la fuente de la capacidad normal y de la actividad real de [nodo] antes de confirmar. Por ejemplo: calendario aprobado y partes de trabajo.» |
| Dato ausente | «Sin dato: no puede calcularse [resultado]. Completa [campo] en [pantalla].» |
| Normal cero | «La capacidad normal es cero. No puede calcularse la utilización dividiendo entre esa referencia. Revisa el dato y su justificación.» |
| Servicios incompletos | «En [grupo], has distribuido [cantidad] de [actividad real] [unidad]. Revisa los destinos. El trabajo sin destino no equivale a capacidad no utilizada.» |
| Sin evaluación de auxiliar | «Este auxiliar reparte costes sin evaluar su capacidad propia. No permite concluir que la ociosidad sea cero.» |
| Tarifa | «Tarifa de gestión: [importe/unidad]. No es un precio de venta. Consulta las bases de cada componente.» |
| Consumos no disponibles | «No se puede determinar el coste aplicado a productos u órdenes porque esa información no está disponible en esta versión.» |
| Publicar receta | «Vas a publicar la versión [n] de [producto]. Esta versión quedará protegida frente a cambios. Para modificarla tendrás que crear otra versión.» Botones «Publicar versión» / «Seguir revisando» |
| Consulta histórica | «Estás consultando un cálculo guardado del [fecha], con las entradas de aquella revisión. Consultarlo no recalcula ni sustituye el resultado actual.» |
| Cuadre con pendientes | «Sin diferencias de conciliación. Quedan [importe] identificados como pendientes; revisa su destino antes de dar el trabajo por terminado.» Solo si ese estado está permitido y calculado |
| Acción fallida | «No se ha podido [acción]. [Causa comprensible]. [Remedio concreto].» No afirmar que nada se guardó cuando la escritura sí terminó |
| Escritura confirmada y refresco fallido | «Los cambios se han guardado, pero la pantalla no pudo actualizarse. Vuelve a leer los datos guardados antes de repetir la operación.» |

### 5.4 Contrato de cada resultado

Todo resultado debe responder a origen, cantidad/importe, destino, estado y comprobación. No todas las pantallas son monetarias; usar «No aplicable» cuando no exista importe o destino económico y nunca inventarlo.

| Resultado | Origen y magnitud | Destino y estado | Comprobación disponible |
|---|---|---|---|
| Importación | Archivo/hoja/filas, significado, moneda, seleccionados y neto | Análisis y lote; revisión o confirmado | Totales seleccionados, agrupación y exclusiones |
| Corrección/ajuste | Cuenta, saldo convertido, manual y reglas separados | Misma cuenta o nuevo coste analítico según operación | Convertido + manual + reglas = efectivo, conservando signos |
| Reparto primario | Cuenta y parte, clave/versión o importe directo | Nodos, importe; borrador/cerrado según estado | Base asignable = asignado + pendiente de esa base; otras rutas separadas |
| Reparto secundario | Costes primarios y servicios recibidos, reglas/grupos | Principales y variaciones que correspondan | Usar conciliaciones existentes; excluir circulación interna de la suma final |
| Actividad | Fuente, periodo, nodo, unidad y magnitudes físicas | Dato del análisis; sin guardar/borrador/confirmado/anulado | Utilización y coherencia de magnitudes; no es conciliación monetaria |
| Servicios auxiliares | Coste propio/recibido y servicios medidos por grupo | Destinos, variación y ejecución | Propio + recibido = transmitido + variación conforme al resultado; horas por grupo frente a real |
| Tarifas | Ejecución y observaciones confirmadas, coste por componente y base | Tarifa del centro; aplicación a órdenes no disponible | Conciliación parcial explícita; no convertirla en conciliación total |
| Necesidades físicas | Producto/versión/cantidad objetivo, líneas y conversiones | Componentes finales y unidades | Desarrollo por unidad/lote/orden y merma; no sumar magnitudes incompatibles |
| Ejecución | Entradas congeladas, fecha, revisión, periodo y moneda | Costes finales, directos, excluidos, pendientes y variaciones aplicables | Conciliación por origen y diferencia exacta; indicar vigencia y fallos |

Un indicador de conciliación no acredita por sí solo pertinencia económica, clasificación revisada, evaluación de capacidad completa ni consumo real por orden. Todos los mensajes deben delimitar qué se ha comprobado.

## 6. Recorridos propuestos

### A. Primer proyecto

1. Crear proyecto → nombre y moneda propuesta → elegir archivo.
2. Diseñar estructura → crear centros → nodos/papeles/unidades → revisar árbol → comprobar/finalizar.
3. Crear análisis → periodo/escenario/moneda/estructura → guardar.
4. Importar → significado → columnas → selección → revisar/confirmar.
5. Revisar cuentas → corregir o aplicar ajustes solo si hacen falta → comprobar base.
6. Primario → claves → comportamiento → asignaciones/reglas/excepciones → comprobar resultados → cerrar.
7. Secundario, cuando corresponda → claves de auxiliares y destinos → calcular/revisar/cerrar según requisitos.
8. Resultados y trazabilidad → obtener o consultar ejecución → revisar conciliación.

Ramas de trabajo, no pasos finales obligatorios: actividad puede prepararse desde un análisis guardado; evaluar servicios auxiliares requiere sus datos y distribución; tarifas requiere costes vigentes y datos físicos confirmados; productos/recetas tiene su propio catálogo/versionado y calcula necesidades físicas. Tras cambios que alteran entradas, revisar vigencia antes de usar resultados. La implementación debe conservar exactamente las dependencias actuales, sin convertir este esquema orientativo en nuevas restricciones.

### B. Segundo mes

Abrir proyecto → crear otro análisis con estructura compatible → introducir fechas y guardar → importar datos del nuevo periodo → revisar correcciones → previsualizar reglas del análisis anterior → adoptar y aplicar solo las compatibles → revisar mediciones y excepciones del mes → preparar/confirmar actividad correspondiente → calcular y conciliar. El mes anterior se conserva. Reutilizar metodología no debe interpretarse como copiar sus importes, sus horas ni sus cierres.

### C. Consulta histórica

Abrir proyecto → abrir análisis → Resultados y trazabilidad → seleccionar cálculo por fecha/revisión → leer marca histórico/vigente → consultar saldos, flechas, detalle y conciliación. Para entradas conservadas, ir al historial de datos del análisis. Ningún paso de consulta necesita reabrir, guardar, calcular ni publicar. Si un resultado no terminó, indicarlo y no presentar totales finales aparentes.

## 7. Riesgos pendientes de prueba interactiva

No se presentan como fallos reproducidos. Las siguientes pruebas deben ejecutarse sobre copias en Windows local.

| Riesgo | Evidencia que justifica probarlo | Prueba y resultado exigido |
|---|---|---|
| R01: controles fuera de alcance en ventana pequeña | Formularios de varias columnas, anchos mínimos y tablas con columnas fijas | 1280×720, 1366×768 y mínimo real de ventana, 100/125/150/200 %; todas las acciones alcanzables sin solaparse; desplazamiento explícito cuando proceda |
| R02: lectura extensa o recorte ES/EN | Altura fija de ayuda; nombres/descripciones largos y cabeceras | Texto completo accesible con teclado, sin expandir el pie al pasar cursor; filas/etiquetas críticas comprensibles en ambos idiomas |
| R03: pérdida de foco o selección | Páginas integradas, reconstrucción de tablas, navegación a detalle | Tab/Mayús+Tab, Intro/Espacio/Escape, F1; retorno a registro y celda reconocibles; ninguna acción irreversible con tecla accidental |
| R04: cambios pendientes al saltar entre entradas | Guardas distintas en navegar, abrir auxiliares/composiciones y cerrar páginas | Editar estructura, análisis, regla, parte, fila física y composición; usar cada salida; conservar o descartar solo la elección explícita; no afirmar pérdida sin reproducir |
| R05: confusión de guardado por celda/fila/pantalla | Correcciones inmediatas y borradores por fila | Editar varios registros, cambiar foco y reapertura; demostrar qué quedó guardado sin instrucciones externas |
| R06: escala de datos | Tablas con alturas máximas y múltiples expanders | 1, 50 y 500 cuentas; varios nodos/grupos y nombres largos; encontrar pendiente, conservar selección, ordenar importes negativos numéricamente |
| R07: representación de estados y totales históricos | Selector de ejecución, cuadros de resumen y mensajes | Casos completado vigente, histórico, fallido, cancelado, sin datos y revisión modificada; no mezclar entradas actuales con salida antigua |
| R08: comprensión económica | Términos y ejemplos de la matriz | Observación con analistas: signos, pesos, mixto, reciprocidad, capacidad, tarifa y receta. Registrar respuestas y errores, no solo clics |
| R09: idiomas de diálogos y formatos | Diálogos nativos, preferencias regionales y campos que conservan formato de edición | Windows/VALDRIO con idiomas diferentes; coma/punto; fechas; cambio regional con borrador. No reinterpretar silenciosamente números pendientes |
| R10: historial y confirmaciones largas | Mensaje de historial físico y confirmaciones con detalle | Diez versiones, motivos extensos y múltiples cuentas; lectura/copia/cancelación accesibles |

## 8. Plan de correcciones incremental

Las prioridades siguientes no autorizan implementación en esta fase. Cada lote debe conservar el inventario de operaciones, condiciones y trazabilidad, y probarse antes de ampliar alcance.

| Orden | Lote y hallazgos | Resultado verificable y dependencia |
|---|---|---|
| 1 · P1 | Veracidad de textos y destinos: UX03, UX11, UX40, UX41, UX44, UX46 | Eliminar contradicciones, Txx operativos y enlaces nominales erróneos. Sin tocar cálculos. Base para toda la ayuda |
| 2 · P1 | Efectos/ámbito de acciones: UX04, UX09, UX19, UX23, UX25, UX27, UX28, UX33, UX38, UX47, UX48 | Etiquetas antes de actuar y mensajes después. Respetar guardados actuales, variantes de confirmación y operaciones automáticas |
| 3 · P1 | Interpretación económica: UX02, UX07, UX14–16, UX20, UX22, UX24, UX26, UX30–31, UX36, UX39, UX42, UX45, UX49 | Conciliaciones y estados con ámbito. Reutilizar magnitudes existentes; revisión conjunta de negocio y diseño antes de tocar presentación de totales |
| 4 · P2 | Orden de trabajo y contenido secundario: UX01, UX05–06, UX08, UX10, UX12–13, UX17–18, UX21, UX29, UX32, UX34–35, UX37, UX43, UX50–52 | Menos competencia visual, acceso completo a avanzadas y mantenimiento. Requiere pruebas de ventana/teclado |
| 5 · P3 | Afinado tras observación | Acortar textos redundantes, ajustar densidad y disposición sin añadir nuevas funciones ni quitar explicaciones necesarias |

Dependencias transversales: matriz ES/EN de textos; semántica común de estados; contexto de análisis; inventario de guardas y comandos; datos de prueba de cada escenario; evidencia anterior/posterior del mismo commit de implementación. No estimar días sin acordar alcance técnico. No mezclar este trabajo con ampliaciones de valoración, consumo real o rentabilidad.

## 9. Criterios de aceptación y validación posterior

### 9.1 Integridad y conservación

- Trabajar sobre copias `.valdrio`, identificadas con versión de aplicación y caso. Conservar una copia original inmutable para comparar; nunca utilizar el proyecto de producción como ensayo.
- Comparar antes/después importes, signos, precisión, redondeos, asignaciones, estados, reglas, referencias y versiones. Una mejora textual/de presentación no puede cambiar el resultado económico.
- Reabrir proyecto tras cada operación persistente de muestra. Verificar compatibilidad de archivos existentes, históricos internacionales, análisis cerrados y consultas sin escritura.
- No confundir «sin diferencias», «sin pendientes», «capacidad evaluada» y «resultado vigente».
- Guardar evidencia real de ejecución local. Los tests históricos registrados por el repositorio no cuentan como pruebas de esta propuesta.

### 9.2 Casos de aceptación

| Caso | Tarea | Éxito exigido |
|---|---|---|
| A01 | Proyecto vacío y primer análisis | Usuario identifica los tres niveles proyecto/estructura/análisis y completa el recorrido sin comandos técnicos |
| A02 | Centros con varios nodos y unidades | No duplica centro para representar otro nodo; identifica ámbito de edición y finalidad de finalizar |
| A03 | CSV/Excel con cuenta textual, negativos y cero | No pierde ceros; explica signos y neto; cero sigue siendo dato conocido |
| A04 | Acumulados anterior/actual | Explica intervalo y diferencia; no importa acumulado completo como mes por error |
| A05 | Sustituir lote y cancelar | Identifica lote/periodo afectados; cancelar no incorpora ni sustituye |
| A06 | Corrección manual + regla extracontable + deshacer | Distingue total manual de incremento y nuevo coste analítico; conserva historial y correcciones conforme a reglas existentes |
| A07 | Clave incompleta/completa/usada | Entiende cuándo se puede asignar y cuándo necesita nueva versión; no se exige un botón de publicación inexistente |
| A08 | Regla exacta/prefijo/excepción/importes absolutos | Explica precedencia y modifica solo la asignación pretendida; sumas y signos correctos |
| A09 | Fijo/variable/mixto y cierre | Solo edita porcentaje variable mixto; reconoce fijo por defecto y bloqueo de cierre; no cambian destinos |
| A10 | Segundo mes y reutilización | Mantiene datos de ambos meses; selecciona reglas compatibles, revisa mediciones y conflictos |
| A11 | Reciprocidad, autoprestación y circuito sin salida | Distingue transferencias de costes nuevos; corrige ruta inviable sin perder historial |
| A12 | Dos filas físicas, cálculo y adopción | Guardar/confirmar una fila no afecta a otra; propuesta no equivale a confirmación |
| A13 | Normal/real sin dato, cero y sobreactividad | No convierte ausencia a cero; explica motivos y utilización indefinida; no llama ahorro a variación negativa |
| A14 | Importaciones físicas y de servicios | Prepara formatos con la ayuda; identifica nodos/unidades/periodos; distingue borradores guardados de valores sin guardar |
| A15 | Tres tratamientos auxiliares y grupos | Entiende pendiente, sin evaluar y evaluar; concilia cada grupo sin sumar actividades incompatibles |
| A16 | Tarifas con datos completos e incompletos | Explica tarifa/unidad y bases distintas; no confunde parcial con total ni falta de consumo con cero |
| A17 | Receta por unidad/lote iniciado/orden y semielaborado | Distingue cantidades, factor, exceso y rendimiento; identifica versión; no lo interpreta como consumo real |
| A18 | Histórico, cálculo fallido y entradas modificadas | Consulta sin recalcular, ve vigencia real y no usa resultado incompleto como definitivo |
| A19 | Preferencias y ejemplos técnicos | Cambiar formato no cambia moneda económica; distingue ejemplo técnico de datos del análisis |
| A20 | Todos los anteriores en ES/EN, teclado y ventana reducida | Acciones y texto esencial accesibles; foco estable; mismas reglas y cifras salvo formato de presentación |

### 9.3 Prueba de comprensión

Propuesta de evaluación: cinco analistas representativos, al menos tres sin uso previo de VALDRIO. Presentar objetivos y archivos de prueba, no instrucciones de clic. Registrar éxito, ayuda solicitada, errores de interpretación y recuperación.

En cada pantalla deben responder: «Estoy en…», «Debo…», «Porque…», «Al continuar ocurrirá…» y «Lo compruebo mediante…». Objetivo de aceptación propuesto: al menos cuatro de cinco completan cada tarea esencial sin guía externa; ninguno acepta un resultado erróneo por confundir histórico/vigente, tarifa/precio, bruto/neto, vacío/cero o borrador/confirmado. Si falla, corregir diseño/texto antes de darlo por aprobado. Es un objetivo de prueba, no un resultado obtenido.

No ejecutar GitHub Actions ni generar costes. La validación interactiva y las modificaciones pertenecen a una fase posterior; este documento entrega diagnóstico y especificación.

## 10. Prompt de continuación

> Implementa, solo cuando se autorice esa fase, la propuesta UX de este diagnóstico sobre una rama derivada de la referencia acordada. Antes de editar, comprueba diferencias respecto a e198d2873396e505b96b692d0169b6f4ba644822. Conserva el inventario de funciones, validaciones, fórmulas, formatos, guardados, versiones y trazabilidad. Empieza por los lotes 1 y 2, con textos ES/EN y criterios de aceptación asociados. No elimines operaciones avanzadas ni introduzcas valoración o consumos futuros. Distingue observaciones comprobadas de riesgos pendientes. Prueba en Windows local sobre copias y aporta evidencia por hallazgo corregido. No ejecutes GitHub Actions. No marques como resuelto un problema de interacción basándote solo en una compilación o lectura de código.

## 11. Evidencias del commit

Las referencias se completan con enlaces inmutables y líneas verificadas en la copia revisada.


| Ref. | Fuente y línea | Contenido |
|---|---|---|
| E01 | [src/Valdrio.Desktop/MainWindow.xaml:180](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/MainWindow.xaml#L180) | Cabecera, ayuda fija y controles del espacio de trabajo |
| E02 | [src/Valdrio.Desktop/MainWindow.Guidance.cs:75](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/MainWindow.Guidance.cs#L75) | Visibilidad, requisitos, etiquetas y navegación |
| E03 | [src/Valdrio.Desktop/CompanyFilePanel.xaml:17](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/CompanyFilePanel.xaml#L17) | Proyecto vacío/abierto y operaciones de archivo |
| E04 | [src/Valdrio.Desktop/ModelWorkspacePanel.Guidance.cs:13](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ModelWorkspacePanel.Guidance.cs#L13) | Traslado del formulario de unidades y avance guiado |
| E05 | [src/Valdrio.Desktop/ModelWorkspacePanel.xaml:83](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ModelWorkspacePanel.xaml#L83) | Cuatro pestañas de estructura y controles reales |
| E06 | [src/Valdrio.Desktop/StudyWorkspaceViewModel.cs:179](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/StudyWorkspaceViewModel.cs#L179) | Historial textual fijo; contexto y condiciones del análisis |
| E07 | [src/Valdrio.Desktop/StudyWorkspacePanel.xaml:87](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/StudyWorkspacePanel.xaml#L87) | Historial, punto de control y cierre de análisis |
| E08 | [src/Valdrio.Desktop/ImportColumnMappingViewModel.Confirmation.cs:34](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ImportColumnMappingViewModel.Confirmation.cs#L34) | Lotes, moneda fijada al vincular destino y confirmación |
| E09 | [src/Valdrio.Desktop/ImportColumnMappingWindow.xaml:79](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ImportColumnMappingWindow.xaml#L79) | Cuatro pestañas y operaciones de importación |
| E10 | [src/Valdrio.Desktop/ResourceWorkspacePanel.xaml:36](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ResourceWorkspacePanel.xaml#L36) | Reglas inicialmente expandidas, tabla, partes y mantenimiento |
| E11 | [src/Valdrio.Desktop/ResourceWorkspaceViewModel.Inline.cs:41](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ResourceWorkspaceViewModel.Inline.cs#L41) | Corrección manual total y persistencia |
| E12 | [src/Valdrio.Desktop/AllocationWorkspacePanel.xaml:99](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AllocationWorkspacePanel.xaml#L99) | Guardar clave; comportamiento, reglas, excepciones, resultados y cierre |
| E13 | [src/Valdrio.Desktop/AllocationWorkspaceViewModel.cs:109](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AllocationWorkspaceViewModel.cs#L109) | Guardado condicional como borrador o clave publicada |
| E14 | [src/Valdrio.Desktop/AllocationWorkspacePanel.xaml.cs:72](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AllocationWorkspacePanel.xaml.cs#L72) | Separación real de primario/secundario y pestañas |
| E15 | [src/Valdrio.Localization/Resources/Strings.es.resx:1229](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1229) | Terminología, liquidación, grupos y ayudas del reparto |
| E16 | [src/Valdrio.Desktop/AllocationReuseWindow.xaml:30](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AllocationReuseWindow.xaml#L30) | Reutilización, selección y autorización específica de conflictos |
| E17 | [src/Valdrio.Desktop/PhysicalActivityWindow.xaml:51](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PhysicalActivityWindow.xaml#L51) | Tabla, selección, evidencia y operaciones por fila |
| E18 | [src/Valdrio.Desktop/PhysicalCapacityAssistantWindow.xaml:20](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PhysicalCapacityAssistantWindow.xaml#L20) | Métodos, propuesta, fórmula y adopción de capacidad |
| E19 | [src/Valdrio.Desktop/PhysicalActivityEditor.cs:321](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PhysicalActivityEditor.cs#L321) | Guardado, anulación e historial físico |
| E20 | [src/Valdrio.Desktop/AuxiliaryCapacityWindow.xaml:44](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AuxiliaryCapacityWindow.xaml#L44) | Acciones visibles y tablas de servicios auxiliares |
| E21 | [src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs:112](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs#L112) | Guardar, validar, calcular, importar y preguntas de auxiliares |
| E22 | [src/Valdrio.Desktop/PrincipalRatesWindow.xaml:20](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PrincipalRatesWindow.xaml#L20) | Tarifas, respuestas y detalle de componentes |
| E23 | [src/Valdrio.Desktop/PrincipalRatesEditor.cs:100](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PrincipalRatesEditor.cs#L100) | Construcción de respuestas y conciliación parcial |
| E24 | [src/Valdrio.Localization/Resources/Strings.es.resx:478](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L478) | Etiquetas y ayudas del recorrido de proyecto/estructura/análisis |
| E25 | [src/Valdrio.Localization/Resources/Strings.es.resx:1882](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1882) | Textos ES de auxiliares; véanse enlaces específicos complementarios |
| E26 | [src/Valdrio.Localization/Resources/Strings.es.resx:1938](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1938) | Límites funcionales, catálogo, receta, ejemplos y mensajes |
| E27 | [src/Valdrio.Desktop/CompositionWindow.xaml.cs:17](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/CompositionWindow.xaml.cs#L17) | Columnas reales de catálogo/receta; guardar, publicar y previsualizar |
| E28 | [src/Valdrio.Desktop/CostFlowPanel.xaml:3](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/CostFlowPanel.xaml#L3) | Consulta/cálculo, tarjetas y cinco pestañas de resultados |
| E29 | [src/Valdrio.Desktop/CostFlowViewModel.cs:211](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/CostFlowViewModel.cs#L211) | Vigencia de ejecución y construcción de totales |
| E30 | [src/Valdrio.Desktop/PhysicalActivityWindow.xaml.cs:73](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PhysicalActivityWindow.xaml.cs#L73) | Salida con pendientes e historial físico |
| E31 | [src/Valdrio.Desktop/ModelEditorDialogs.cs:8](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ModelEditorDialogs.cs#L8) | Semántica de Sí/No/Cancelar en edición |
| E32 | [src/Valdrio.Desktop/ModelWorkspaceViewModel.StudyNodes.cs:11](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ModelWorkspaceViewModel.StudyNodes.cs#L11) | Ámbito específico de nodos adaptados al análisis |
| E33 | [src/Valdrio.Desktop/PrincipalRatesWindow.xaml.cs:31](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PrincipalRatesWindow.xaml.cs#L31) | Formato y nombre de exportación de tarifas |
| E34 | [docs/directrices/USABILIDAD.md:77](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/docs/directrices/USABILIDAD.md#L77) | Directrices: edición, cierre, conciliación, moneda y preservación |
| E35 | [src/Valdrio.Desktop/ContextHelpWindow.cs:73](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ContextHelpWindow.cs#L73) | Ayuda completa, idioma y lectura accesible |
| E36 | [docs/diseno/SISTEMA_VISUAL_VALDRIO_V1.md:17](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/docs/diseno/SISTEMA_VISUAL_VALDRIO_V1.md#L17) | Identidad visual y jerarquía aprobadas |

[E01]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/MainWindow.xaml#L180
[E02]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/MainWindow.Guidance.cs#L75
[E03]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/CompanyFilePanel.xaml#L17
[E04]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ModelWorkspacePanel.Guidance.cs#L13
[E05]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ModelWorkspacePanel.xaml#L83
[E06]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/StudyWorkspaceViewModel.cs#L179
[E07]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/StudyWorkspacePanel.xaml#L87
[E08]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ImportColumnMappingViewModel.Confirmation.cs#L34
[E09]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ImportColumnMappingWindow.xaml#L79
[E10]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ResourceWorkspacePanel.xaml#L36
[E11]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ResourceWorkspaceViewModel.Inline.cs#L41
[E12]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AllocationWorkspacePanel.xaml#L99
[E13]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AllocationWorkspaceViewModel.cs#L109
[E14]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AllocationWorkspacePanel.xaml.cs#L72
[E15]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1229
[E16]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AllocationReuseWindow.xaml#L30
[E17]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PhysicalActivityWindow.xaml#L51
[E18]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PhysicalCapacityAssistantWindow.xaml#L20
[E19]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PhysicalActivityEditor.cs#L321
[E20]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AuxiliaryCapacityWindow.xaml#L44
[E21]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs#L112
[E22]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PrincipalRatesWindow.xaml#L20
[E23]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PrincipalRatesEditor.cs#L100
[E24]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L478
[E25]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1882
[E26]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1938
[E27]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/CompositionWindow.xaml.cs#L17
[E28]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/CostFlowPanel.xaml#L3
[E29]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/CostFlowViewModel.cs#L211
[E30]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PhysicalActivityWindow.xaml.cs#L73
[E31]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ModelEditorDialogs.cs#L8
[E32]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ModelWorkspaceViewModel.StudyNodes.cs#L11
[E33]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PrincipalRatesWindow.xaml.cs#L31
[E34]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/docs/directrices/USABILIDAD.md#L77
[E35]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ContextHelpWindow.cs#L73
[E36]: https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/docs/diseno/SISTEMA_VISUAL_VALDRIO_V1.md#L17


### Localizador específico por hallazgo

Estos puntos complementan las referencias de contexto de la matriz; la prioridad y el perjuicio previsto siguen siendo valoración UX, no pruebas de usuario.

| Hallazgo | Evidencia puntual | Control, expresión o clave de texto |
|---|---|---|
| UX01 | [src/Valdrio.Desktop/MainWindow.Guidance.cs:95](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/MainWindow.Guidance.cs#L95) | var keys = new[] |
| UX02 | [src/Valdrio.Desktop/MainWindow.Guidance.cs:201](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/MainWindow.Guidance.cs#L201) | ProjectContextText.Text = |
| UX03 | [src/Valdrio.Localization/Resources/Strings.es.resx:1055](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1055) | Guide.ContinueAllocations |
| UX04 | [src/Valdrio.Desktop/PhysicalActivityWindow.xaml.cs:46](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PhysicalActivityWindow.xaml.cs#L46) | MessageBoxButton.YesNo, |
| UX05 | [src/Valdrio.Localization/Resources/Strings.es.resx:1503](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1503) | Guide.ImportStart |
| UX06 | [src/Valdrio.Desktop/CompanyFilePanel.xaml:59](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/CompanyFilePanel.xaml#L59) | FileOperationsLabel |
| UX07 | [src/Valdrio.Localization/Resources/Strings.es.resx:913](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L913) | Model.ColumnPool |
| UX08 | [src/Valdrio.Desktop/ModelWorkspacePanel.xaml:231](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ModelWorkspacePanel.xaml#L231) | x:Name="PoolBox" |
| UX09 | [src/Valdrio.Localization/Resources/Strings.es.resx:903](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L903) | Model.NodePersistenceHelp |
| UX10 | [src/Valdrio.Localization/Resources/Strings.es.resx:545](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L545) | Guide.Help.UnitDimension |
| UX11 | [src/Valdrio.Localization/Resources/Strings.es.resx:268](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L268) | Study.RunHistoryEmpty |
| UX12 | [src/Valdrio.Localization/Resources/Strings.es.resx:254](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L254) | Study.Capture |
| UX13 | [src/Valdrio.Desktop/StudyWorkspacePanel.xaml:71](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/StudyWorkspacePanel.xaml#L71) | NewStudyButton |
| UX14 | [src/Valdrio.Desktop/ImportColumnMappingWindow.xaml:139](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ImportColumnMappingWindow.xaml#L139) | ComparisonPanel |
| UX15 | [src/Valdrio.Desktop/ImportColumnMappingWindow.xaml:224](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ImportColumnMappingWindow.xaml#L224) | NumberFormatPanel |
| UX16 | [src/Valdrio.Desktop/ImportColumnMappingViewModel.Confirmation.cs:35](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ImportColumnMappingViewModel.Confirmation.cs#L35) | b.Prepared.Source.FileName |
| UX17 | [src/Valdrio.Desktop/ImportColumnMappingViewModel.Confirmation.cs:141](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ImportColumnMappingViewModel.Confirmation.cs#L141) | this["ConfirmedSummary"] |
| UX18 | [src/Valdrio.Desktop/ResourceWorkspacePanel.xaml:36](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ResourceWorkspacePanel.xaml#L36) | ReusableAdjustmentsSection |
| UX19 | [src/Valdrio.Desktop/ResourceWorkspaceViewModel.Inline.cs:41](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ResourceWorkspaceViewModel.Inline.cs#L41) | ResourceCorrectionCommand.Prepare |
| UX20 | [src/Valdrio.Localization/Resources/Strings.es.resx:1533](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1533) | Adjustment.UndoPreview |
| UX21 | [src/Valdrio.Desktop/MainWindow.Guidance.cs:171](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/MainWindow.Guidance.cs#L171) | AllocationPanel.IsEnabled = !DistributionDetails.IsExpanded |
| UX22 | [src/Valdrio.Desktop/ResourceWorkspacePanel.xaml:125](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/ResourceWorkspacePanel.xaml#L125) | ResetButton |
| UX23 | [src/Valdrio.Desktop/AllocationWorkspaceViewModel.cs:109](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AllocationWorkspaceViewModel.cs#L109) | SaveKeyCommand = |
| UX24 | [src/Valdrio.Desktop/AllocationWorkspacePanel.xaml:77](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AllocationWorkspacePanel.xaml#L77) | ModeBox |
| UX25 | [src/Valdrio.Localization/Resources/Strings.es.resx:1124](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1124) | AllocationUI.RuleHelp |
| UX26 | [src/Valdrio.Localization/Resources/Strings.es.resx:1439](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1439) | AllocationUI.CompositionWeightsHelp |
| UX27 | [src/Valdrio.Localization/Resources/Strings.es.resx:1416](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1416) | AllocationUI.CompositionCloseHelp |
| UX28 | [src/Valdrio.Localization/Resources/Strings.es.resx:1722](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1722) | Reuse.Help |
| UX29 | [src/Valdrio.Localization/Resources/Strings.es.resx:1150](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1150) | AllocationUI.NamesHelp |
| UX30 | [src/Valdrio.Desktop/AllocationWorkspacePanel.xaml:411](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AllocationWorkspacePanel.xaml#L411) | AuxiliarySettlementSection |
| UX31 | [src/Valdrio.Localization/Resources/Strings.es.resx:1266](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1266) | AllocationUI.SecondaryGross |
| UX32 | [src/Valdrio.Localization/Resources/Strings.es.resx:1256](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1256) | AllocationUI.SecondaryRouteHelp |
| UX33 | [src/Valdrio.Localization/Resources/Strings.es.resx:1751](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1751) | Activity.SelectionHelp |
| UX34 | [src/Valdrio.Desktop/PhysicalActivityWindow.xaml:74](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PhysicalActivityWindow.xaml#L74) | UtilizationColumn |
| UX35 | [src/Valdrio.Localization/Resources/Strings.es.resx:1742](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1742) | Activity.Assistant.Help |
| UX36 | [src/Valdrio.Localization/Resources/Strings.es.resx:1710](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1710) | Activity.ImportHeader |
| UX37 | [src/Valdrio.Desktop/PhysicalActivityEditor.cs:381](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PhysicalActivityEditor.cs#L381) | public string History() |
| UX38 | [src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs:141](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs#L141) | public void Calculate() |
| UX39 | [src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs:82](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs#L82) | Quantity = Qty(saved? |
| UX40 | [src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs:167](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs#L167) | n.Code == c[0] |
| UX41 | [src/Valdrio.Localization/Resources/Strings.es.resx:1825](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1825) | Rates.AssignmentLater |
| UX42 | [src/Valdrio.Desktop/PrincipalRatesEditor.cs:100](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/PrincipalRatesEditor.cs#L100) | var reconciliation = |
| UX43 | [src/Valdrio.Desktop/AuxiliaryCapacityWindow.xaml.cs:36](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/AuxiliaryCapacityWindow.xaml.cs#L36) | VALDRIO-T16.json |
| UX44 | [src/Valdrio.Localization/Resources/Strings.es.resx:1940](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1940) | Composition.CatalogHelp |
| UX45 | [src/Valdrio.Localization/Resources/Strings.es.resx:1954](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1954) | Composition.LineHelp |
| UX46 | [src/Valdrio.Localization/Resources/Strings.es.resx:1938](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1938) | Composition.Scope |
| UX47 | [src/Valdrio.Desktop/CompositionWindow.xaml:27](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/CompositionWindow.xaml#L27) | SaveDraft |
| UX48 | [src/Valdrio.Desktop/CostFlowPanel.xaml:3](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/CostFlowPanel.xaml#L3) | ExecutionCard |
| UX49 | [src/Valdrio.Localization/Resources/Strings.es.resx:1578](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Localization/Resources/Strings.es.resx#L1578) | Run.ReconcileHelp |
| UX50 | [src/Valdrio.Desktop/CostFlowPanel.xaml:22](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/CostFlowPanel.xaml#L22) | AnalysisTabs |
| UX51 | [src/Valdrio.Desktop/MainWindow.xaml:285](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/MainWindow.xaml#L285) | x:Name="DevelopmentTools" |
| UX52 | [src/Valdrio.Desktop/MainWindow.xaml:180](https://github.com/JavierRB35/VALDRIO/blob/e198d2873396e505b96b692d0169b6f4ba644822/src/Valdrio.Desktop/MainWindow.xaml#L180) | x:Name="FieldHelpText" |

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

- En primario y secundario, agrupar nombre, criterio y guardar en una fila.
  Las tablas de claves y destinos deben aprovechar la altura de la ventana;
  evitar alturas fijas pequeñas y grandes espacios vacíos en la cabecera.
- «Asignar claves a cuentas» permite ordenar cada columna en ambos sentidos.
  Los importes se ordenan por valor numérico, incluidos los abonos negativos.
- Mostrar la viabilidad automáticamente con el cálculo secundario y al consultar
  un cierre guardado. No exigir una comprobación adicional. Si el cálculo falla,
  mostrar el diagnóstico en rojo; no confundir cualquier error con falta de
  viabilidad matemática de las rutas. Invalidar la información al cambiar datos.
- Situar cerrar/reabrir al comienzo de las pantallas 6 y 7, en el mismo lugar.
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

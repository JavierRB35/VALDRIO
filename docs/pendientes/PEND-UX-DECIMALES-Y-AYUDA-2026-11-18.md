# Pendientes transversales obligatorios — VALDRIO (18/11/2026)

Origen: corrección UX del módulo **Productos y servicios**, catálogo y composición.

## PEND-UX-DEC-01 · Estandarizar entrada decimal en es-ES

**Prioridad: alta. Estado: pendiente en el resto de VALDRIO.**

En Catálogo se introduce un buffer textual validado: la coma y el punto del teclado numérico representan el separador decimal, sin separador de miles. Los valores se validan como `decimal` y se guardan en los contratos actuales; un texto inválido permanece editable y no llega a SQLite.

Auditar **todas las demás pantallas con números editables**, incluyendo importes, actividades, bases, coeficientes, porcentajes, tipos de cambio y cantidades. Definir una política homogénea que distinga claramente la entrada manual de la interpretación de CSV/XLSX y de la exportación canónica. No convertir silenciosamente puntos de miles en decimales ni sustituir `decimal` por `double`.

**Aceptación:** pruebas focalizadas ES/EN con teclado normal y numérico, casos 12,5/12.5, negativos según campo, ceros, vacíos, valores inválidos, precisión decimal y conservación antes/después de guardar y reabrir. Sin cambios económicos ni errores de binding.

## PEND-UX-HELP-01 · Cobertura completa de ayuda contextual

**Prioridad: obligatoria. Estado: pendiente en pantallas distintas de Productos y servicios.**

Inventariar campos, botones con efectos, selectores y columnas editables de **todas las pantallas**. Cada elemento debe explicar con lenguaje para analistas qué significa, cuándo se utiliza, qué dato admite y sus consecuencias; donde exista un cálculo, incorporar fórmula y ejemplo proporcionado (especialmente mermas, inductores, repartos y capacidad). Mostrar la misma explicación mediante foco de teclado, hover, tooltip y tecnologías de apoyo, reutilizando `FieldHelp` y la barra inferior.

**Aceptación:** cero controles relevantes sin explicación válida en ES/EN; pruebas con teclado, ratón, textos largos, ventana estrecha y escalado; sin mostrar claves técnicas, identificadores internos ni fórmulas incorrectas.

## Notas de compatibilidad de esta entrega

- Los registros de catálogo publicados conservan su identidad; quitar una referencia usada significa desactivarla para no romper históricos.
- Los controles de versiones de Composición pueden simplificarse, pero los snapshots publicados empleados por presupuestos y órdenes **no se eliminan**. Una definición editable se guarda como nueva revisión interna cuando corresponde.
- El coste calculado de la composición solo sustituye un coste manual cuando se pueden valorar **todos** sus componentes directos con los datos disponibles. Si existe actividad sin tarifa, precio incompleto o una base dependiente de la orden, se mantiene el valor manual y el perímetro parcial se comunica explícitamente.
- No se garantiza a partir de una revisión estática una validación WPF real ni la integridad de un archivo de empresa concreto; requieren pruebas Windows/locales.

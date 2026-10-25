# Ayuda contextual: Capacidad y servicios auxiliares
**ID Vista UI:** `View_AuxiliaryServices`  
**Última actualización:** `2026-10-21`  
**Estado:** borrador documental contrastado con código de `main@211af0d`; pendiente de prueba interactiva Windows.

## 1. Objetivo y función financiera
Definir tratamiento y cantidades de servicios prestados por auxiliares, evaluando capacidad propia y transferencias sin duplicar coste recibido. Permite comprobar cobertura, reciprocidad y efecto terminal al publicar la ejecución.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Centro auxiliar, unidad, normal y real | Magnitudes de Actividad y capacidades | Lectura | Las horas se consultan desde observaciones físicas, no se duplican aquí. |
| Tratamiento de auxiliar | Selector | Sí, si se evalúa | Distingue propietario de capacidad, ThroughFlow y pendiente. |
| Grupo de servicios | Tabla | Según centro | Diferencia servicios o rutas del auxiliar. |
| Destino de servicio | Tabla | Según grupo | Identifica otro auxiliar o principal receptor. |
| Cantidad a destino | Unidad de actividad | Sí, según tratamiento | Cobertura física medida de servicios; no porcentajes inventados. |
| Importación de servicios | CSV UTF-8 | Opcional | Cabecera exacta `Center;Group;Destination;Quantity`; decimal regional y sin miles. |
| Guardar, validar, calcular | Acciones | Según estado | Guardar cambios no publica coste; validar comprueba; calcular puede publicar T12. |
| Reabrir secundario | Acción | Si cerrado | Desbloquea cambios conforme a permisos y control de revisiones. |
| Actualizar/exportar | Acciones | Según estado | Recarga información protegida y exporta ejecución JSON. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Preguntas y respuestas | Coste propio/recibido, actividad, variación, transmisión | Facilitan interpretar al auxiliar seleccionado. |
| Cobertura de servicios | Cantidades medidas y distribuidas | Detecta trabajo realizado sin destino. |
| Resultado terminal y conciliación | Reparto secundario T11/T12 según configuración | Explica coste retenido/transferido sin sumar doble recíprocos. |
| Versión y diagnósticos | Fuentes físicas y estados | Distinguen evaluación parcial de medición integral. |

## 4. Casos de uso, validaciones y errores frecuentes
- Tratamiento pendiente bloquea cálculo: nunca se cambia automáticamente a ThroughFlow.
- Actividad realizada sin servicio asignado no es sinónimo de ociosidad.
- No sumar circulación bruta de auxiliares como coste de empresa.
- Modificar los datos empleados de Actividad y capacidades puede invalidar ejecuciones y exigir reevaluación.
- El CSV de servicios no soporta XLSX en esta vista; la importación de Actividad y capacidades sí dispone de ambos formatos.

### Referencias al código
- [`src/Valdrio.Desktop/AuxiliaryCapacityWindow.xaml`](../../src/Valdrio.Desktop/AuxiliaryCapacityWindow.xaml)
- [`src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs`](../../src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs)
- [`src/Valdrio.Application/SecondaryAllocation.cs`](../../src/Valdrio.Application/SecondaryAllocation.cs)

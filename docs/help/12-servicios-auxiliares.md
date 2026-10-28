# Ayuda contextual: Capacidad y servicios auxiliares
**ID Vista UI:** `View_AuxiliaryServices`  
**Última actualización:** `2026-10-28`
**Estado:** guía revisada sobre `2599188` y la auditoría contextual de 28/10/2026. Las evidencias de validación se registran por separado.

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

### Elegir el tratamiento
**Repartir sin evaluar** conserva las claves secundarias publicadas. No calcula la ociosidad propia del auxiliar: ausencia de evaluación no equivale a variación cero.

**Evaluar capacidad propia** exige normal y real confirmadas. Cada grupo de servicios debe sumar la actividad real; sus cantidades determinan las proporciones de destino y sustituyen los pesos de la clave para ese auxiliar evaluado. No sumes grupos entre sí como si fueran horas adicionales.

La capacidad se evalúa una sola vez en el propietario del coste fijo que corresponde evaluar. El coste recibido de otros auxiliares se transmite sin volver a evaluar su capacidad. Para corregir normal o real, vuelve a Actividad y capacidades; aquí se registran los servicios a destinos.

### Referencias al código
- [`src/Valdrio.Desktop/AuxiliaryCapacityWindow.xaml`](../../src/Valdrio.Desktop/AuxiliaryCapacityWindow.xaml)
- [`src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs`](../../src/Valdrio.Desktop/AuxiliaryCapacityEditor.cs)
- [`src/Valdrio.Application/SecondaryAllocation.cs`](../../src/Valdrio.Application/SecondaryAllocation.cs)

## Recorrido recomendado
1. Selecciona el auxiliar y su tratamiento; consulta la capacidad confirmada y registra los servicios a destinos en la unidad del auxiliar.
2. Comprueba que las transferencias y reciprocidades permiten llegar a centros principales sin contar dos veces costes recibidos.
3. Revisa diagnósticos y resultados de la ejecución antes de utilizar los servicios auxiliares para análisis de tarifas o productos.

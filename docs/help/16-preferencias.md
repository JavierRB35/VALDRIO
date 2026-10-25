# Ayuda contextual: Preferencias y formatos
**ID Vista UI:** `View_Preferences`  
**Última actualización:** `2026-10-25`
**Estado:** guía funcional mantenida para `main@6885eca` (25/10/2026). Describe el código de esta pantalla; las pruebas visuales interactivas se registran por separado y solo aplican al SHA indicado en cada evidencia.

## 1. Objetivo y función financiera
Configurar presentación de idioma, números, fechas y zona horaria de la aplicación. Los ajustes de visualización no sustituyen conversiones monetarias de negocio ni modifican el coste económico calculado.

## 2. Guía de campos y entradas (Inputs)
| Campo / parámetro | Tipo / unidad | Requerido | Descripción e impacto en el cálculo |
| :--- | :--- | :--- | :--- |
| Idioma de interfaz | Selector ES/EN | No | Traduce etiquetas disponibles de la aplicación. |
| Región / formato cultural | Selector | No | Da formato a cantidades, fechas y monedas sin convertir su valor. |
| Zona horaria de visualización | Selector | No | Representa instantes en la zona elegida sin modificar fechas de negocio. |
| Reintentar guardado de preferencias | Acción | Si error | Permite persistir selección si hubo fallo. |
| Herramientas experimentales | Controles de prueba | Solo desarrollo | Ejemplos de redondeo, metadatos monetarios y FX; no flujos productivos generalizados. |

## 3. Interpretación de resultados (Outputs)
| Métrica / resultado | Fórmula u origen | Significado / decisión asociada |
| :--- | :--- | :--- |
| Vista previa de formatos | Moneda, fecha e instante | Comprueba legibilidad regional. |
| Estado de persistencia | Aviso o confirmación de preferencia | Indica si quedó guardada la configuración. |
| Ayuda de campos | Tooltip, foco y texto accesible | Proporciona explicaciones breves en controles instrumentados. |
| Ayuda de esta pantalla (F1) | Catálogo local versionado de vistas | Abre una guía de lectura independiente sin modificar el proyecto ni los cálculos. |

## 4. Casos de uso, validaciones y errores frecuentes
- El botón **Ayuda (F1)** de la barra superior abre la ficha de la vista activa, también en asistentes integrados. Se puede cerrar con **Escape**.
- Cambiar región de formato no convierte saldos de un estudio.
- La zona horaria no altera fechas contables fijadas por el análisis.
- Herramientas de desarrollo no deben confundirse con funciones comerciales de gestión.
- Si se muestra error de guardado, reintentar sin asumir que el cambio quedó persistido.

### Referencias al código
- [`src/Valdrio.Desktop/MainWindow.xaml`](../../src/Valdrio.Desktop/MainWindow.xaml)
- [`src/Valdrio.Desktop/MainWindowViewModel.cs`](../../src/Valdrio.Desktop/MainWindowViewModel.cs)
- [`src/Valdrio.Desktop/FieldHelp.cs`](../../src/Valdrio.Desktop/FieldHelp.cs)

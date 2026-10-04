# VALDRIO — Índice y secuencia de tareas

Versión 0.4. 38 fichas, T00–T33 y T01A/T02A/T06A/T22A. Se avanza por dependencias y evidencia. Las cuatro fichas nuevas introducen internacionalización sin renumerar las tareas económicas.

| ID | Edición | Dependencias | Ficha |
| --- | --- | --- | --- |
| T00 | R0 | — | Acordar el producto y sus políticas |
| T01 | R0 | T00 | Repositorio, solución y compilación Windows |
| T01A | R0 | T01 | Interfaz bilingüe y preferencias independientes |
| T02 | R0 | T01A | Unidades, decimal, política y redondeo |
| T02A | R0 | T02 T01A | Money, monedas, fechas canónicas y tipos congelados |
| T03 | R0 | T02A | Archivo de empresa y SQLite real |
| T04 | R0 | T03 | Modelos versionados y centros |
| T05 | R0 | T04 | Estudios, revisiones y snapshots |
| T06 | R0 | T05 | Importación verificable de sumas y saldos |
| T06A | R0 | T06 T02A | Importación internacional y puente de moneda |
| T07 | R0 | T06A | Ajustes, clasificación y particiones |
| T08 | R0 | T04 T07 | Claves y reglas de asignación |
| T09 | R0 | T08 | Reparto primario con trazabilidad |
| T10 | R0 | T02 T08 | Solver recíproco y prueba de viabilidad |
| T11 | R0 | T09 T10 | Secundario y contribución efectiva |
| T12 | R0 | T05 T11 | Ejecuciones completas, revisión y consulta |
| T13 | R0 | T12 | Aceptación del demostrador R0 |
| T14 | R1 | T13 | Actividad y capacidades por pool |
| T15 | R1 | T14 T11 | Tarifas y absorción de principales |
| T16 | R1 | T15 | Capacidad auxiliar por propietario |
| T17 | R1 | T04 T15 | Materiales y recetas versionadas |
| T18 | R1 | T17 T12 | Estimaciones y órdenes con tarifas congeladas |
| T19 | R1 | T18 T16 | Consumo real y frontera de valoración |
| T20 | R1 | T19 | Ventas y márgenes con perímetro |
| T21 | R1 | T20 | Escenarios y comparación |
| T22 | R1 | T21 | Informes, explicación y exportación |
| T22A | R1 | T22 T01A T02A T06A | Informes internacionales y cierre de localización R1 |
| T23 | R1 | T12 T19 | Copias, evolución de archivo y recuperación |
| T24 | R1 | T22A T23 | Instalación, protección y licencia offline |
| T25 | R1 | T24 | Piloto y entrega profesional R1 |
| T26 | R2 | T25 | ABC con actividades y pools |
| T27 | R2 | T26 | Objetos y rentabilidad multidimensional |
| T28 | R3 | T26 T27 | TDABC y ecuaciones de tiempo |
| T29 | R3 | T25 T26 | Estándares y desviaciones aditivas |
| T30 | R4 | T21 T25 | Métodos alternativos y simulación |
| T31 | R4 | T29 T30 | Presupuesto, previsión e integraciones elegidas |
| T32 | Opcional | T25 | Valoración productiva e inventario |
| T33 | Opcional | T25 | Fuentes contables adicionales |

La fuente machine-readable completa de las 38 fichas se conserva en `indice_tareas.json`.

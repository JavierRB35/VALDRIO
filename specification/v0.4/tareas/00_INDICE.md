# VALDRIO — Índice y secuencia de tareas

Versión 0.4. 38 fichas, T00–T33 y T01A/T02A/T06A/T22A. Se avanza por dependencias y evidencia. Las cuatro fichas nuevas introducen internacionalización sin renumerar las tareas económicas.

| ID | Edición | Dependencias | Ficha |
| --- | --- | --- | --- |
| T00 | R0 | — | [Acordar el producto y sus políticas](T00.md) |
| T01 | R0 | T00 | [Repositorio, solución y compilación Windows](T01.md) |
| T01A | R0 | T01 | [Interfaz bilingüe y preferencias independientes](T01A.md) |
| T02 | R0 | T01A | [Unidades, decimal, política y redondeo](T02.md) |
| T02A | R0 | T02 T01A | [Money, monedas, fechas canónicas y tipos congelados](T02A.md) |
| T03 | R0 | T02A | [Archivo de empresa y SQLite real](T03.md) |
| T04 | R0 | T03 | [Modelos versionados y centros](T04.md) |
| T05 | R0 | T04 | [Estudios, revisiones y snapshots](T05.md) |
| T06 | R0 | T05 | [Importación verificable de sumas y saldos](T06.md) |
| T06A | R0 | T06 T02A | [Importación internacional y puente de moneda](T06A.md) |
| T07 | R0 | T06A | [Ajustes, clasificación y particiones](T07.md) |
| T08 | R0 | T04 T07 | [Claves y reglas de asignación](T08.md) |
| T09 | R0 | T08 | [Reparto primario con trazabilidad](T09.md) |
| T10 | R0 | T02 T08 | [Solver recíproco y prueba de viabilidad](T10.md) |
| T11 | R0 | T09 T10 | [Secundario y contribución efectiva](T11.md) |
| T12 | R0 | T05 T11 | [Ejecuciones completas, revisión y consulta](T12.md) |
| T13 | R0 | T12 | [Aceptación del demostrador R0](T13.md) |
| T14 | R1 | T13 | [Actividad y capacidades por pool](T14.md) |
| T15 | R1 | T14 T11 | [Tarifas y absorción de principales](T15.md) |
| T16 | R1 | T15 | [Capacidad auxiliar por propietario](T16.md) |
| T17 | R1 | T04 T15 | [Materiales y recetas versionadas](T17.md) |
| T18 | R1 | T17 T12 | [Estimaciones y órdenes con tarifas congeladas](T18.md) |
| T19 | R1 | T18 T16 | [Consumo real y frontera de valoración](T19.md) |
| T20 | R1 | T19 | [Ventas y márgenes con perímetro](T20.md) |
| T21 | R1 | T20 | [Escenarios y comparación](T21.md) |
| T22 | R1 | T21 | [Informes, explicación y exportación](T22.md) |
| T22A | R1 | T22 T01A T02A T06A | [Informes internacionales y cierre de localización R1](T22A.md) |
| T23 | R1 | T12 T19 | [Copias, evolución de archivo y recuperación](T23.md) |
| T24 | R1 | T22A T23 | [Instalación, protección y licencia offline](T24.md) |
| T25 | R1 | T24 | [Piloto y entrega profesional R1](T25.md) |
| T26 | R2 | T25 | [ABC con actividades y pools](T26.md) |
| T27 | R2 | T26 | [Objetos y rentabilidad multidimensional](T27.md) |
| T28 | R3 | T26 T27 | [TDABC y ecuaciones de tiempo](T28.md) |
| T29 | R3 | T25 T26 | [Estándares y desviaciones aditivas](T29.md) |
| T30 | R4 | T21 T25 | [Métodos alternativos y simulación](T30.md) |
| T31 | R4 | T29 T30 | [Presupuesto, previsión e integraciones elegidas](T31.md) |
| T32 | Opcional | T25 | [Valoración productiva e inventario](T32.md) |
| T33 | Opcional | T25 | [Fuentes contables adicionales](T33.md) |

R0 incluye T01A/T02A/T06A desde los fundamentos. R1 incluye T22A antes de T24. Cada ficha aplica el documento internacional, incluso los módulos posteriores.

T32 y T33 son opcionales. Si D01 adelanta inventario, replanificar y conservar sus dependencias. T33 añade fuentes contables concretas mediante adaptadores.

T10 es puerta de viabilidad numérica; T24 contiene cuatro subentregas. Una compilación no sustituye pruebas ni recorrido de usuario.

El corpus contiene 39 referencias numéricas verificadas por un oráculo racional y 15 criterios internacionales para software futuro. Las 38 tareas de desarrollo empiezan pendientes.

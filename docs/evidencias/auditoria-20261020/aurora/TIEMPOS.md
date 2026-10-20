# Tiempos AURORA

Segundos. Medición Debug local; cinco recorridos y veinte clics de calculadora.
Apertura de empresa y carga de análisis tienen una muestra por versión; no se estima su P95.

| Operación | n | Antes mediana | Después mediana | Antes P95 | Después P95 | Primera después |
|---|---:|---:|---:|---:|---:|---:|
| Abrir empresa | 1 | 35.673 | 11.166 | — | — | 11.166 |
| Cargar análisis | 1 | 32.350 | 1.098 | — | — | 1.098 |
| Estructura | 5 | 9.671 | 0.242 | 10.219 | 0.251 | 0.251 |
| Análisis | 5 | 19.267 | 0.912 | 19.798 | 1.143 | 1.143 |
| Recursos | 5 | 0.050 | 0.012 | 18.906 | 0.429 | 0.429 |
| Reparto primario | 5 | 0.779 | 0.818 | 0.909 | 1.241 | 0.647 |
| Reparto secundario | 5 | 0.839 | 1.062 | 0.980 | 1.525 | 1.525 |
| Flujos | 5 | 0.590 | 0.608 | 0.652 | 0.736 | 0.736 |
| Actividad | 5 | 14.134 | 0.462 | 14.718 | 0.515 | 0.515 |
| Tarifas | 5 | 14.385 | 0.599 | 14.576 | 0.703 | 0.703 |
| Auxiliares | 5 | 9.795 | 0.580 | 10.241 | 0.616 | 0.614 |
| Calculadora: clic hasta lista | 20 | 0.049 | 0.096 | 0.083 | 0.127 | 0.194 |

Las pantallas suman preparación y presentación. En navegación asíncrona, la preparación
incluye el trabajo de interfaz intercalado durante las esperas; no representa exclusivamente E/S.
P95 por rango más próximo. Con cinco muestras coincide con el máximo; no es una garantía estadística.
Los JSON conservan cada medición y las repeticiones posteriores a la primera.

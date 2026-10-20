# Comparativa local de rendimiento

Milisegundos. N=20; misma copia sintética. Ver README para condiciones y límites.

| Operación | Mediana antes | Mediana después | P95 antes | P95 después | Reducción mediana |
|---|---:|---:|---:|---:|---:|
| calculator-constructor | 121,95 | 5,63 | 145,90 | 6,85 | 95,4 % |
| calculator-render | 137,91 | 99,69 | 175,41 | 129,06 | 27,7 % |
| calculator-total | 259,52 | 105,35 | 307,99 | 134,99 | 59,4 % |
| structure-read | 39,11 | 0,00 | 49,04 | 0,00 | 100,0 % |
| history-read | 32,11 | 25,12 | 49,90 | 32,52 | 21,8 % |


| Operación | Primera antes | Primera después | Mediana sucesivas antes | Mediana sucesivas después | P95 sucesivas antes | P95 sucesivas después |
|---|---:|---:|---:|---:|---:|---:|
| calculator-constructor | 127,38 | 13,03 | 119,66 | 5,59 | 147,67 | 6,85 |
| calculator-render | 264,62 | 190,40 | 133,77 | 99,59 | 175,41 | 129,06 |
| calculator-total | 392,00 | 203,43 | 258,34 | 105,33 | 307,99 | 134,99 |
| structure-read | 27,76 | 0,07 | 39,45 | 0,00 | 54,26 | 0,00 |
| history-read | 37,11 | 32,96 | 31,99 | 25,04 | 54,14 | 32,52 |

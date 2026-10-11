# ADR-0014 — Estado del reparto secundario y sello reproducible (propuesto)

Fecha: 2026-10-09 · Estado: **candidato sujeto a validación nativa**.

## Contexto

VALDRIO permite publicar claves de reparto en T08, visualizar hechos primarios en T09 y resolver la circulación recíproca en T10. El usuario confirma que puede haber rutas auxiliar → auxiliar y auxiliar → principal y que el cierre secundario debe bloquear el primario sin impedir reabrir fases posteriormente.

## Decisión

El usuario lanza un cálculo explícito cuando hay cobertura de reglas publicadas. No se confunde *cobertura* con *resolubilidad*: el botón se habilita con reglas completas, y al ejecutarlo T10 diagnostica los circuitos cerrados, deriva, pivotes y demás bloqueos.

El resultado del cálculo de T11 distingue importes efectivos adjudicados por origen a cada principal de flujo bruto auxiliar informativo. La fase secundaria cerrada conserva un `SecondaryCalculationHash` canónico y la fase primaria bloqueada. El servicio de persistencia recalcula y contrasta el sello antes de aceptar el cierre. Un primario que se reabra elimina el sello y el cierre secundarios; el secundario reabierto conserva cerrado el primario.

Se mantiene el almacenamiento histórico de cambios en `AllocationChangeReceipt`, sin migración SQLite adicional. La presentación reconstruye resultados deterministas desde los hechos históricos, contrastando la huella si la fase está cerrada.

## Riesgos y consecuencias

- El hash es un **sello reproducible de estado**, complementado ahora por un recibo persistido de hechos terminales y explicaciones; falta ejecutar y aceptar las pruebas nativas de cierre/replay antes de T12.
- Se ha incorporado mapeo explícito `SecondaryPartRoutingGroup(PartId, ComponentRoutingGroup)` a la configuración, opcional y omitido en snapshots históricos; la ausencia equivale a `all`. Cada grupo utilizado se resuelve con claves completas para todos los auxiliares, y los importes finales se agregan por parte y destino; nunca se confunden las matrices de factores entre grupos.
- No se admite el cierre si T09 no está completo o si T10 rechaza el grafo.
- El cálculo y los tests C# deben ejecutarse en Windows/.NET 10, con métricas de estabilidad y memoria. Sin ello, ADR no pasa a Accepted.
- Los workflows de GitHub Actions continúan manuales y sin consumo autorizado.


## Extensión de decisión — publicación íntegra, 2026-10-09

El sello aislado resulta insuficiente para la auditoría económica. Se materializa `SecondaryCalculationPublication` como propiedad opcional del `AllocationChangeReceipt` de cierre (ausente en todos los recibos históricos T08–T10). Incluye hechos finales y enlaces, totalizaciones, conciliación de partes, bruto auxiliar, revisión fuente y versiones metodológicas. Se verifica en la transacción de cierre y por replay histórico, usando entrada y claves publicadas en el recibo; el resto de cambios conserva `SecondaryPublication=null`. El estado activo expone únicamente la última publicación cuando `SecondaryClosed=true`.

La interfaz cerrada consume el recibo inmutable. Reabrir secundario deja el histórico publicado disponible para auditoría, pero retira la publicación activa; reabrir primario invalida además su sello. La lectura de un estudio cerrado no es una orden de recálculo.

**Estado del ADR: propuesto** hasta compilación, pruebas de persistencia/reapertura nativas, rendimiento y validación A05. No confundir publicación persistida con aceptación técnica.


## Extensión: grupos por parte y puente de integridad

Se conserva el contrato legado all cuando SecondaryPartRoutingGroups es null. La asignación de grupo es explícita por PartId; ninguna etiqueta Fixed/Variable se infiere a partir del nombre de un grupo. Cada Q/B se calcula y diagnostica de forma autónoma con la misma versión de modelo, y los hechos finales se agregan después sin multiplicar transferencias internas. No existe una matriz única de factores cuando distintas partes usan matrices diferentes: GroupCirculations conserva el detalle por grupo y la circulación total agregada no pretende ser una factorización única.

SecondaryEconomicBridge integra las fronteras del reparto: directo, excluido, pendiente y costes terminales. No se incorpora el bruto auxiliar al total monetario. El recibo histórico guarda el puente y sus enlaces; una diferencia no nula bloquea el cierre. Las claves referenciadas y sus grupos forman parte de la huella de resultado.

Se añade limpieza de asignaciones a partes desaparecidas y se protege la compatibilidad canónica de configuraciones históricas. ADR sigue propuesta hasta compilación, smoke WPF, replay real y A05.

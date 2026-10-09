# ADR-0013 — Solver recíproco PNUM-1 (candidato)

Fecha: 2026-10-09. **Estado:** propuesta técnica desarrollada, aceptación y validación de runtime pendientes. No acepta automáticamente D04/D05 ni sustituye la puerta A05.

## Modelo determinista

Q contiene fracciones de auxiliar emisor (fila) a auxiliar receptor (columna); B, de auxiliar a principal. Ambos provienen exclusivamente de versiones publicadas T08 y usan IDs ordinales. Cada origen auxiliar debe cubrir exactamente 1 en Q+B y alcanzar algún principal mediante rutas positivas, incluso si su coste es cero. Autorreparto prohibido bloquea; no existe asignación residual a un destino inventado.

M=I-Qᵀ y LU en decimal con pivoteo parcial reproducible. Los términos independientes económicos admiten importes negativos o cero. Una misma factorización resuelve varios RHS y produce factores Bᵀ M⁻¹ por origen resolviendo vectores unitarios, no calculando una inversa explícita. Normalización por pesos para DriverUnits/EqualShares asigna a la última línea ordenada el resto decimal, con conservación exacta de 1.

## Política numérica propuesta (CM06)

| Comprobación | Umbral | Respuesta |
|---|---|---|
| Pivote nulo | = 0 | SOLVER_SINGULAR |
| Pivote / norma de fila | < 10⁻¹² | SOLVER_SENSITIVE |
| Residuo absoluto | > max(10⁻⁸, 10⁻¹² × norma∞(b)) | SOLVER_RESIDUAL |
| Residuo relativo | > 10⁻¹² | SOLVER_RESIDUAL |
| Deriva por origen de factores finales | > 10⁻¹⁸ | SOLVER_FACTOR_DRIFT |
| Factor final negativo | < -10⁻¹⁸ | SOLVER_NEGATIVE_FACTOR |
| Amplificación interna | > 10⁶ | aviso |
| Amplificación interna | > 10¹² | SOLVER_SENSITIVE |

Se calculan y exponen residual absoluto/relativo, pivote normalizado mínimo y amplificación. No se permite un fallback silencioso a reparto directo ni redondeo monetario en el solver. Overflow decimal bloquea de forma explícita. La factorización no debe reutilizarse después de un cambio de matriz o reglas.

## Prueba de viabilidad y continuidad

Existe test sintético de anillo con 100 auxiliares, reloj monotónico, bytes asignados en hilo y cancelación. **No se ha ejecutado en .NET**. Objetivo de revisión propuesto para equipo Windows: completar 100 auxiliares en 10 segundos o menos y registrar uso de memoria y CPU; el umbral final A05 depende de aceptación y mediciones reales. No se debe alterar un expected para acomodar la implementación.

El verificador Python 39/39 coteja únicamente referencias racionales; no demuestra el resultado del solver C#. Se ha añadido un diagnóstico WPF de solo lectura con botón explícito, estados y grupos. Faltan compilación/test nativos, benchmark y prueba visual/funcional real del diagnóstico WPF. GitHub Actions no ha sido ejecutado ni se ha autorizado gasto potencial. La persistencia/publicación secundaria corresponde a T11.

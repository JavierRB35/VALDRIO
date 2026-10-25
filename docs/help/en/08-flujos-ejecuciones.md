# Contextual help: Cost flows and calculation runs
**Last updated:** `2026-10-25`


## 1. Purpose and financial function
Inspect and compare persisted calculation runs, their diagrams and the provenance of distributed costs without changing the meaning of historical results.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Selected calculation run | Run selector | As needed | Distinguishes current and historical runs. |
| Calculate, cancel and refresh | Actions | By state | Generate, interrupt or reload calculations. |
| Graph stage | Primary or secondary | Optional | Restricts displayed flows to the chosen stage. |
| Zoom and labels | Graph controls | Optional | Improve legibility without changing calculations. |
| Centre, node or connection selection | Graph or table | Optional | Filters origins, destinations and reconciliation detail. |
| Clear selection | Action | Optional | Returns to the full graph. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Effective and terminal cost | Persisted calculation totals | Check the economic cost base and final destination. |
| Node and connection detail | Contributions in selected run | Explains where amounts came from. |
| Run version and state | Saved execution and revisions | Distinguishes published, historical and stale results. |
| Reconciliation | Inflows, outflows and terminal amounts | Detects discrepancies without double-counting reciprocal transit. |

## 4. Use cases, validation and common errors
- Do not confuse old runs with the latest valid result.
- Graphic scale does not change monetary totals.
- Gross reciprocal movement is not a fresh cost.
- A calculation view is not evidence that a later production-consumption stage is available.

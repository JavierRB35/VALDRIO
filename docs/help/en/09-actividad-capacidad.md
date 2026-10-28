# Contextual help: Activity and capacity
**Last updated:** `2026-10-28`


## 1. Purpose and financial function
Record normal capacity and actual activity for each analytical node, study and period. Observations are versioned and must be confirmed with identifiable sources; they are not inferred from each other.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Project, analysis and period | Selection and context | Yes | Scope the observations; switching study does not copy them. |
| Node and measurement unit | Read-only context | Yes | Show what activity is measured and in which unit. |
| Normal capacity | Quantity per period | On confirmation | Reference denominator for utilisation and management rates. |
| Actual activity | Quantity per period | On confirmation | Measured activity for that exact period. |
| Normal and actual sources | Text | On confirmation | Identify verifiable provenance of both measurements. |
| Quality and description | Choice and text | Quality on confirmation; description optional | Distinguish recorded, declared or calculated values; describe the source further when useful. |
| Reason for zero normal capacity | Text | If normal equals zero | Explain a verified zero instead of missing data. |
| Exceptional activity reason | Text | If activity exceeds normal or exceptional | Explain physical anomalies without clipping values. |
| Theoretical and practical measures | Advanced quantities | Optional | Supporting physical context, not confirmed normal/actual values. |
| Save draft, confirm and annul | Actions | By state | Preserve distinct observation versions. |
| Normal-capacity assistant and import | Actions | Optional | Propose/import values, still subject to explicit confirmation. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Physical utilisation | Actual / normal if normal is positive | Compares workload to reference without proving efficiency. |
| Published related cost | Compatible T12 run | Read-only cost context, not a rate created by this screen. |
| Observation state and revision | Versioned physical history | Distinguishes missing, draft, confirmed and annulled. |
| Diagnostics | Validation and source rules | Explain corrections before confirmation. |

## 4. Use cases, validation and common errors
- A missing observation is not a measured zero.
- Normal capacity of zero makes utilisation undefined, even if actual activity is positive.
- Utilisation above 100% may be valid if justified; never truncate it.
- Resolve unsaved edits before switching studies.
- Closed studies permit consultation but not editing.

### Where to edit
The overview table is read-only. Open **Edit activity and capacity** for the selected node: it shares normal, actual and source values with this screen. Explicitly save a draft or confirm; adopting a normal-capacity proposal does not confirm the observation.

## Recommended workflow
1. Select the node, unit and period for actual activity and normal capacity observations.
2. Enter or import observations with their sources, then confirm units, dates and status before comparing.
3. Review activity and capacity differences; rates also require a compatible saved economic run.

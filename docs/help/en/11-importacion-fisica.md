# Contextual help: Import physical activity
**Last updated:** `2026-10-28`


## 1. Purpose and financial function
Import normal and actual observations from CSV or XLSX into the selected analysis, with source-node/unit matching, previews and explicit replacement choices. This is separate from accounting-balance import.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Source file | CSV or XLSX path | Yes | Identifies the origin of physical observations. |
| Sheet | Selector | XLSX only | Chooses the source records. |
| Delimiter | Selector | CSV only | Reads columns using the original format. |
| Replacement selection | Checkbox per row | To apply | Specifies which draft observations may be replaced. |
| Node and unit | Read-only mapping | Yes | Confirm compatibility with the active model. |
| Normal and actual values | Quantities per period | When supplied | Physical measurements, not costs or rates. |
| Correction reason | Text | For replacement | Explains changes to existing values. |
| Zero-normal reason | Text | If applicable | Documents a real zero capacity. |
| Exceptional reason | Text | If applicable | Explains above-normal activity or special cases. |
| Preview and apply | Actions | After validation | Review potential conflicts before writing selected data. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Source preview | Node, unit, normal, actual and existing data | Shows what would change. |
| Import diagnostics | Matching and business rules | Expose errors before applying. |
| Imported observations | Applied selected rows | Remain governed by Activity and capacities state and version rules. |

## 4. Use cases, validation and common errors
- Accounting balances are imported through a different screen.
- Do not move hours between periods or incompatible units automatically.
- A row without a valid current node or pool must not be applied.
- Preview is not saving or confirming every observation.
- Respect protected revisions when replacing or annulling.

### Template and effect of Apply
Use headers `node;unit;period_start;period_end;normal;real;normal_source;real_source`. Node and unit are existing codes; dates use `yyyy-MM-dd` and must match the full analysis period. Text quantities use the regional decimal separator without thousands separators. Supply at least normal or actual quantity; blank is not zero.

**Apply saves drafts**, not confirmed observations. When only normal or actual is supplied, the other previous quantity is retained if available. Review selected rows and then confirm in Activity and capacities. Do not select two rows for the same node; they are not automatically summed.

## Recommended workflow
1. Generate the template and fill its node, unit, period and quantity columns; this assistant has no manual column-mapping selector.
2. Review the preview and fix incompatible or incomplete rows; preview alone does not apply the import.
3. Apply valid rows and confirm the observations; inspect their values and sources in Activity and capacity.

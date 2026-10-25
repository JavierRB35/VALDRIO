# Contextual help: Prepare accounts and resources
**Last updated:** `2026-10-25`


## 1. Purpose and financial function
Review imported account balances, apply explicit extra-accounting adjustments, reconcile effective amounts and classify resources into destinations, exclusions or outstanding items.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Account, description and provenance | Read-only text | Review | Preserve the imported account identity and source. |
| Imported and converted amount | Study currency | Review | Original base for reconciliation. |
| Manual correction | Signed amount | Optional | Explicit adjustment in the existing table. |
| Rule adjustment | Monetary amount | If a rule is applied | Distinct from manual correction. |
| Adjustment rule name and account/prefix | Text | Per rule | Select matched accounts or a supplementary cost with no source account. |
| Analytical code | Text | When creating a line | Identifies analytical resources independently from imported accounting. |
| Rule mode and value | Fixed amount, percentage or signed sum | Per rule | Calculates target amounts using supported modes only. |
| Reason | Text | Yes for rules | Records financial justification. |
| Parts and values | Study currency | By resource | Reconcile effective value among destinations and exclusions. |
| Destination | Selector | By part | Select pool, direct object, pending or exclusion. |
| Direct-object identity | Code, name and type | If direct | Identifies a destination outside pools. |
| Pool, behaviour and layer | Selectors | By destination | Classify fixed/variable cost and its assignment. |
| Advanced unit, horizon and physical measures | Units and dates | When relevant | Do not equal a confirmed Activity and capacities physical observation. |
| FX revaluation details | Rate, date, source and reason | On revaluation | Preserve monetary traceability. |
| Save, preview, apply and undo | Actions | By operation | A saved rule is not applied automatically. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Effective account amount | Original plus or minus authorised corrections and adjustments | Forms the amount available for classification. |
| Totals, pending and difference | Reconciliation of resources and parts | Identify unallocated values and discrepancies. |
| Rule preview | Matching accounts and proposed adjustment | Review impact before applying. |
| Change history | Versions and justifications | Trace corrections, revaluations and reversals. |

## 4. Use cases, validation and common errors
- Two active rules must not conflict over the same account.
- Editing accounts governed by active rules may need further confirmation.
- Previewing or saving a rule does not apply it to the study.
- Outstanding and excluded amounts must not be silently redistributed.
- Resource-preparation diagnostics use red text, unlike informational messages. Economic confirmation dialogs are unchanged. Read-only fields are skipped by Tab without hiding their content.

## Recommended workflow
1. Review accounts from confirmed batches, their descriptions and opening balances.
2. Classify, adjust or exclude amounts only as appropriate; check fixed, variable or mixed treatment and save table changes.
3. Review totals and accounting-to-analytical reconciliation; proceed to primary allocation once amounts have been handled.

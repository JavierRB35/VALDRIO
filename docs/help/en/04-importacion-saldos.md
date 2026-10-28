# Contextual help: Import accounting balances
**Last updated:** `2026-10-28`


## 1. Purpose and financial function
Read source accounting balances from CSV or XLSX, interpret codes, descriptions and monetary amounts, review their provenance and confirm the import into the chosen analysis.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| File, sheet and encoding | Source options | By format | Define where rows are read from. |
| Header and starting row | Row indices | By source | Avoid importing headings as transactions. |
| CSV delimiter | Choice | CSV only | Separates source columns correctly. |
| Amount basis | Choice | Yes | Distinguishes period movement, full ending balance and cumulative basis. |
| Previous lot and replacement | Selector | Optional | Replacement must be deliberate and its scope reviewed. |
| Fiscal period comparison | Dates and choice | When comparing cumulatives | Establishes the basis for period differences. |
| Amount layout | Selector | Yes | Choose signed amount or debit and credit columns. |
| Account code | Text column | Yes | Preserve identity as text, including leading zeros. |
| Account description | Source column | When supplied | Shows meaning and conflicts without changing account identity. |
| Amount, debit and credit | Numeric columns | Per layout | Carry source amounts without arbitrary sign changes. |
| Decimal, thousands and negative syntax | Parsing options | Per source | Parse the source representation explicitly. |
| Numeric account permission | Toggle | Per source | Enables numeric-coded accounts where appropriate. |
| Selected rows | Table checkboxes | Yes | Include or exclude rows before grouping. |
| Analysis currency | Read-only | Always | Amounts must already use this currency. This import does not convert currencies. |
| Approval reference and confirmation | Text and action | Before confirmation | Provide evidence that interpretation was reviewed. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Row preview | Source row, account, description and amount | Detects parsing, sign and mapping errors. |
| Grouped preview | Source rows aggregated by account | Verifies the financial base entering Resource preparation. |
| Comparison and reconciliation | Amounts and differences for the selected balance basis | Supports an explicit approval decision. |
| Confirmed import | Saved lot in current analysis | Only then can the imported balances feed later stages. |

## 4. Use cases, validation and common errors
- A preview never means the import has been confirmed.
- Do not drop leading zeros from account identifiers.
- Inspect descriptions, signs, missing or inconsistent columns and source provenance.
- Replacing an imported lot is not automatic; review the exact selection and approval.

### Currency and signs before confirmation
If the source uses another currency, convert amounts outside VALDRIO before importing. The application cannot verify the actual currency of a file that does not identify it. Historical international batches retain their conversions for consultation; they do not enable conversion in a new import.

With debit/credit columns the result is **debit minus credit**. Negative credits reduce cost; do not reverse their sign merely to obtain a positive result. Cumulative balances become period movements only through comparison with an explicitly prepared, comparable earlier closing balance.

## Recommended workflow
1. Choose a CSV or XLSX file and explicitly map columns, dates, delimiters and numeric separators.
2. Review the preview, account codes/descriptions and ambiguous amounts; correct source data or mappings where necessary.
3. Confirm the batch for the correct analysis and verify it is saved before preparing accounts.

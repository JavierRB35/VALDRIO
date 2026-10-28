# Contextual help: Cost centre rates
**Last updated:** `2026-10-28`


## 1. Purpose and financial function
Review normalised management rates for principal cost centres from compatible published cost runs and confirmed physical capacity/activity. These are not automatically inventory valuation rates.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Selected centre | Table selection | For centre detail | Scopes questions to the centre, study and measurement unit. |
| Only issues | Yes/no | Optional | Displays situations requiring attention. |
| Refresh | Action | Optional | Re-reads the currently valid calculation and physical state. |
| View activity / view costs | Actions | Optional | Go to the source of Activity and capacities observations or T12 runs. |
| Export | Action | When results exist | Saves report and provenance JSON. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Combined cost and rate | Sum of component rates, each using its own denominator | Gives monetary cost per physical unit when data is complete. |
| Fixed, variable and received components | Cost breakdown and provenance | Explain rate composition. |
| Component base and rate | Normal or actual activity by approved policy | Clarify the denominator chosen for each component. |
| Capacity variance | Difference between normalised normal and actual activity | Measures utilisation effects, not realised savings. |
| Question/answer table | Seven management questions | Distinguishes known, pending and inapplicable data. |
| Provenance | T12 run ID and Activity and capacities observation versions | Supports audit and comparison. |

## 4. Use cases, validation and common errors
- Do not show rates when compatible T12 runs or confirmed physical denominators are missing.
- Do not compare rates across incompatible units or scopes.
- Cost of performed activity is not an amount allocated to orders without actual consumption (unavailable in this version).
- Unused capacity is not equivalent to unassigned completed service.
- Management normalisation does not by itself establish IAS 2 inventory valuation.

### Interpreting rates and capacity variance
**Own fixed cost subject to capacity assessment** is divided by normal capacity. Variable and consumed components use actual activity. Period costs stay separate, outside the combined rate. Do not divide all cost by a single base or normalise an already assessed auxiliary cost again.

Example: own fixed cost 1,000, normal capacity 100 h and actual activity 80 h give a rate of 10/h, performed cost 800 and capacity variance 200. With positive costs, a negative variance indicates above-normal activity, not cash savings. Without actual consumption by product or order, applied cost and unassigned performed cost remain unknown; an estimated order does not complete this reconciliation.

## Recommended workflow
1. Save an economic run compatible with the analysis revision and refresh; this screen uses the current run without a historical-run selector.
2. Check the principal centre has confirmed activity and capacity observations; missing data is not zero.
3. Review the rate, capacity variance, unit and provenance; make corrections in source screens, not this analysis table.

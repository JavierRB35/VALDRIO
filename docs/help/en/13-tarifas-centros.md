# Contextual help: Cost centre rates
**Last updated:** `2026-11-03`


## 1. Purpose and financial function
Review normalised management rates for principal cost centres from compatible published cost runs and confirmed physical capacity/activity. These are not automatically inventory valuation rates.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Selected centre | Table selection | For centre detail | Scopes questions to the centre, study and measurement unit. |
| Only issues | Yes/no | Optional | Displays situations requiring attention. |
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

Rates load automatically from the current closed costs and confirmed physical data. Updating activity does not reopen allocations; frozen auxiliary assessments remain traceable.

## Extension: rates and unit costs (candidate 2026-11-04)

The screen is now called **Rates and unit costs**. The **Centre rates** tab presents general centres; **Direct factors** lists explicitly configured homogeneous direct-labour groups and procurement groups. Both use the **same economic run**, frozen sources and calculation; changing tabs never posts costs.

Each node's purpose is selected when editing the cost model. Historic nodes without an explicit purpose remain under general centres until the model is explicitly adapted. Own fixed, variable and previously assessed auxiliary service components continue to follow CM08/CM09. A direct-labour tariff does not prove that hours have been consumed by orders; actual order consumption belongs to T19. Estimates do not substitute real hours.

**Status:** candidate feature branch, not yet validated or accepted on Windows.
## Reading the rate at a glance (T15 improvement)

The selected centre now shows a **rate breakdown**: own fixed, observed variable, received/consumed and period costs, with each component's amount, denominator and rate. The capacity variance is shown separately; it is not an additional expense.

- **Own fixed cost subject to capacity assessment:** divided by normal activity. Its unabsorbed portion remains a capacity variance.
- **Observed variable:** divided by actual activity.
- **Received or consumed:** divided by actual activity; capacity already assessed elsewhere must not be assessed twice. A fixed received cost without identified assessment triggers a **review notice**, not an automatic error or reclassification.
- **Period costs:** excluded from the combined rate.

**Needs attention** also includes centres with this methodological review notice. Use **View cost origin** and **Prepare accounts** to check classification; this screen never modifies the calculation.

**Purpose:** management cost rate, not a validated inventory valuation. Inventory costing requires the separate T32 scope and rules. No fabricated inventory rate or actual order consumption is shown.


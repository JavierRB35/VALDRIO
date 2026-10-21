# Contextual help: Cost centre rates

## 1. Purpose and financial function
Review normalised management rates for principal cost centres from compatible published cost runs and confirmed physical capacity/activity. These are not automatically inventory valuation rates.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Selected centre | Table selection | For centre detail | Scopes questions to the centre, study and measurement unit. |
| Only issues | Yes/no | Optional | Displays situations requiring attention. |
| Refresh | Action | Optional | Re-reads the currently valid calculation and physical state. |
| View activity / view costs | Actions | Optional | Go to the source of T14 observations or T12 runs. |
| Export | Action | When results exist | Saves report and provenance JSON. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Combined cost and rate | Normalised cost over approved base | Gives monetary cost per physical unit when data is complete. |
| Fixed, variable and received components | Cost breakdown and provenance | Explain rate composition. |
| Component base and rate | Normal or actual activity by approved policy | Clarify the denominator chosen for each component. |
| Capacity variance | Difference between normalised normal and actual activity | Measures utilisation effects, not realised savings. |
| Question/answer table | Seven management questions | Distinguishes known, pending and inapplicable data. |
| Provenance | T12 run ID and T14 observation versions | Supports audit and comparison. |

## 4. Use cases, validation and common errors
- Do not show rates when compatible T12 runs or confirmed physical denominators are missing.
- Do not compare rates across incompatible units or scopes.
- Cost of performed activity is not an amount allocated to orders without T19.
- Unused capacity is not equivalent to unassigned completed service.
- Management normalisation does not by itself establish IAS 2 inventory valuation.

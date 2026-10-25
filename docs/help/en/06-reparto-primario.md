# Contextual help: Primary cost allocation
**Last updated:** `2026-10-25`


## 1. Purpose and financial function
Distribute effective resource costs to the analytical cost nodes by published allocation keys, while preserving the fixed and variable behaviour of each item and reconciling the financial base.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Allocation-key library and version | Selector | According to allocation | Choose published reusable keys and trace versions. |
| Key name | Text | On creation | Identifies a reusable rule. |
| Criterion | Percentage, unit quantity or equal share | Yes | Defines destination weights. |
| Driver unit | Selector | For quantity-based rules | Specifies the physical allocation unit. |
| Included destinations and values | Checkboxes and numbers | Yes | Define the allocation coverage and distribution. |
| Fixed/variable split | Percentages | By account | Classifies cost behaviour; mixed cost has derived fixed complement. |
| Matching account code or prefix | Text | Optional | Associates accounts to a saved key. |
| Published key | Selector | When defining rule | Establishes the applied distribution method. |
| Exceptions and individual assignments | Account, part and key | By coverage | Handle cases not covered by general rules. |
| Absolute monetary amounts | Study currency | When applicable | Support explicit monetary distribution. |
| Bulk assignment | Selected rows and key | Optional | Assign many accounts with reviewed scope. |
| Calculate, close and reopen | Actions | By stage state | Validate and preserve allocation results. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Coverage | Assigned and pending accounts | Shows resources awaiting an allocation key. |
| Primary totals | Effective, allocable, assigned, excluded and direct amounts | Reconcile the primary allocation. |
| Account detail | Destination, driver, share and amount | Explains each resource-to-node path. |
| Primary diagram | Relationships for the selected account | Visualises actual allocation paths. |
| Node totals | Sum of assigned amounts | Feed the secondary stage. |

## 4. Use cases, validation and common errors
- Saving a key is not the same as calculating or closing allocation.
- Do not close while allocable parts or keys remain invalid.
- Historically referenced published keys require dependency checks before deletion.
- Account descriptions support identification but do not change code identity.
- While editing an allocation key, Tab and Shift+Tab traverse enabled editors in key lines, weights, classification and coverage, skipping informational columns. Contextual validation errors appear in red; existing modal messages remain available.

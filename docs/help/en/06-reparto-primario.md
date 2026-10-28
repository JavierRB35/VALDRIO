# Contextual help: Primary cost allocation
**Last updated:** `2026-10-28`


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

### Pareto chart
The chart sorts all nodes receiving primary costs by descending cost. Bars show each node’s aggregate cost; the line shows cumulative cost as a percentage of positive costs. An exact-value table and horizontal scrolling are available. Credits keep their sign and are excluded from the cumulative curve. Positive costs plus credits reconcile to net cost. A zero or negative net total has no percentage; missing results are shown as unavailable.

### Choosing weights and resolving matching rules
- **Percentages:** included destinations must total exactly 100%.
- **Driver units:** enter non-negative quantities of one measurement; their total must be positive. Each destination receives its quantity divided by the total: 30 and 30 hours mean 50% each, not 30% each.
- **Equal shares:** each included destination receives the same proportion.

Choose a driver that represents resource consumption or benefit. The application validates weights, not their economic suitability. Key quantities do not replace activity and capacity observations.

An explicit individual key overrides automatic rules. Among rules, exact account wins, then longest prefix, then highest priority; unresolved ties produce a diagnostic. Fixed/variable composition splits the amount by behaviour, not the allocation percentage assigned to each node.

### Direct keys
**Generate direct node keys** prepares single-destination keys. It reports created keys, existing equivalents and skipped nodes; it does not automatically assign accounts. Review the destination and assign the appropriate key.

## Recommended workflow
1. Create or select allocation keys and verify each account or resource's driver and destinations.
2. Assign keys in the tables, save changes and calculate only after coverage and percentages are valid.
3. Inspect cost by node and the Pareto (cost bars and cumulative-percentage line); review details and close primary allocation when satisfied.

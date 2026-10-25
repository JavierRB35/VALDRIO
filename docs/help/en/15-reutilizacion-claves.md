# Contextual help: Reuse allocation methodology
**Last updated:** `2026-10-25`


## 1. Purpose and financial function
Review compatible saved allocation keys and rules from another analysis, preview conflicts and explicitly adopt selected methodology. This does not transfer balances or historic cost results.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Source analysis | Selector | Yes | Determines which study supplies candidate methodology. |
| Candidate allocation keys | Selection table | To adopt | Shows compatible rules, status and version conflicts. |
| Replace conflicts | Yes/no | Optional | Explicitly authorises replacement of clashing definitions. |
| Preview and adopt | Actions | To apply | Compare before copying selected keys. |
| Cancel | Action | Optional | Exit without changing destination definitions. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Candidate keys and states | Comparison of source and target | Supports a selective adoption decision. |
| Adoption preview | Compatibility checks and conflicts | Reveals intervention needed before copying. |
| Adopted methodology | Keys and rules in destination analysis | Must be reviewed before running allocation. |

## 4. Use cases, validation and common errors
- Adopting methodology does not copy balances, hours, closed allocations or runs.
- Verify structure and version compatibility first.
- Conflict replacement must be explicitly chosen.
- Cancel must leave the destination unchanged.

# Contextual help: Secondary allocation and closing

## 1. Purpose and financial function
Redistribute costs of auxiliary nodes into final principal destinations, including supported reciprocal flows. A closed primary allocation is a prerequisite.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Auxiliary node or origin group | Selector and table | Yes | Identifies costs awaiting secondary distribution. |
| Published secondary key | Selector | Per allocation | Defines the destination shares. |
| Node or group assignment | Selector | By model | Completes missing key choices. |
| Repair pending assignments | Action | When diagnosed | Resolves missing rules without hiding obsolete groups. |
| Calculate and cancel | Actions | After primary closing | Execute and control reciprocal allocation. |
| Close and reopen | Actions | By state | Protect final results and allow explicitly authorised revisions. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Reciprocal feasibility | Diagnostics by group | Detects cycles without valid exits. |
| Auxiliary settlement | Initial, incoming, outgoing and residual cost | Shows where each auxiliary cost goes. |
| Final principal cost | Primary plus net secondary contributions | Provides final cost per principal node. |
| Principal provenance | Contributions and flow diagram | Trace costs without adding transit twice. |
| Gross auxiliary circulation | Reciprocal flows | Diagnostic volume, not an additional company cost. |

## 4. Use cases, validation and common errors
- Do not calculate secondary distribution before the primary stage is closed.
- Fix reciprocal cycles with no viable terminal exit.
- Do not add gross auxiliary circulation to total company cost.
- The reciprocal solver does not imply a user-selectable ABC or TDABC method.
- T16 auxiliary capacity requires its own explicit treatment and verified data.
- When assigning keys to auxiliary nodes or absolute amounts, Tab and Shift+Tab move across row editors and skip read-only values. Assignment errors are shown in red and existing dialogs are not replaced.

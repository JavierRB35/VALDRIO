# Contextual help: Secondary allocation and closing
**Last updated:** `2026-10-28`


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

### Pareto chart
The chart sorts principal nodes at their final cost, without counting internal auxiliary circulation again by descending cost. Bars show each node’s aggregate cost; the line shows cumulative cost as a percentage of positive costs. An exact-value table and horizontal scrolling are available. Credits keep their sign and are excluded from the cumulative curve. Positive costs plus credits reconcile to net cost. A zero or negative net total has no percentage; missing results are shown as unavailable.

### What reciprocal allocation solves
An auxiliary may receive services from another auxiliary and provide services back. VALDRIO solves these relationships together: each auxiliary redistributes its initial cost plus received services. Do not repeat allocations manually or add internal flows to final cost again.

For example, an auxiliary with initial cost 100, 20% self-service and 80% to a principal redistributes gross cost 125: 25 returns to itself and 100 reaches the principal. Final cost remains 100. A 100% self-loop without an exit is not viable. Every auxiliary chain needs a path to principals; validation also checks numerical stability.

When auxiliary capacity is assessed, some cost may remain as capacity variance. Check this split in reconciliation before comparing initial cost with final principal costs.

## Recommended workflow
1. Check auxiliary nodes carrying costs and define service transfer keys towards principal nodes; self-allocation is allowed.
2. Save destinations and percentages, run the calculation and inspect route viability, including closed loops.
3. Review contributions, principal totals and the Pareto; resolve diagnostics before closing secondary allocation.

Tables grow with their rows: use the outer page scroll. Save key and closure actions remain in the bottom bar and follow the active tab. Horizontal scrolling keeps columns accessible when the window is narrow.

## Tab order

Secondary allocation uses the same order: **Allocation keys → Assign accounts / nodes → Check and close**. Define or select auxiliary service keys, assign them to nodes/groups, and check mathematical feasibility before closing. Self-service remains subject to the existing solver validation.

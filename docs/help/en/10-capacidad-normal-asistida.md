# Contextual help: Normal-capacity assistant
**Last updated:** `2026-10-28`


## 1. Purpose and financial function
Propose a normal-capacity figure from calendar availability, comparable history or an explicit declaration. Adopting a proposal is not the same as saving or confirming the physical observation.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Calculation method | Calendar, history or declared | Yes | Defines the procedure and source. |
| Operating days | Whole days | Calendar method | Days inside the selected period. |
| Shifts per day | Count | Calendar method | Frequency of operations. |
| Hours per shift | Hours | Calendar method | Duration of each shift. |
| Equivalent resources | Count | Calendar method | Comparable persons or equipment. |
| Downtime and deduction basis | Hours and policy | Calendar method | Reduce available time without exceeding the base. |
| Normalisation factor | Percent | Calendar method | Convert availability to normal expected activity. |
| Factor reason | Text | If factor is nontrivial | Explain the normal-capacity assumption. |
| Historical observations | Comparable periods | Historical method | Supply a reference only when periods and units are comparable. |
| Include/exclude anomalies | Selector and reason | Historical method | Explicitly review unusual records. |
| Declared capacity | Node-unit quantity | Declared method | Propose a documented responsible-person estimate. |
| Declared source and description | Text | Source on confirmation; description optional | Record provenance; a numeric proposal may be adopted before completing its source. |
| Preview, adopt, cancel | Actions | By state | Preview is informative; adoption copies the proposal to a draft. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Normal-capacity proposal | Formula for selected method | Candidate reference pending Activity and capacities validation. |
| Theoretical and practical capacity | Calendar intermediate values | Explain the derivation without changing cost amounts. |
| Calculation steps and warnings | Inputs, operations and sources | Support review before adoption. |

## 4. Use cases, validation and common errors
- Do not import unrelated months, units or historical anomalies by assumption.
- A normal-capacity proposal never determines actual activity.
- Closing without adoption does not transfer the proposal; separately review any edits made to the shared observation.
- After adoption, explicitly save or confirm in Activity and capacity.

### Proposal and shared observation
**Edit activity and capacity** combines the calculator and the selected node's data. Normal, actual and their sources are shared with Activity and capacities. You can save a draft or confirm here.

**Adopt** copies the proposal to the shared normal value; saving or confirmation is still required. Additional source description is optional; retain the source and quality required for confirmation. Closing the calculator does not undo a completed save or adoption.

## Recommended workflow
1. Choose an available normal-capacity basis: operating calendar, comparable history or declared amount.
2. Inspect the proposed result and its assumptions; a proposal is not yet a confirmed observation.
3. Adopt a suitable proposal, complete its source and confirm the shared observation from this editor or Activity and capacities.

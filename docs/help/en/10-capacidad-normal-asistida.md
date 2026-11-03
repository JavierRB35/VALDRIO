# Contextual help: Edit activity and capacity
**ID Vista UI:** `View_NormalCapacityAssistant`
**Last updated:** `2026-11-03`

## 1. Purpose and financial function
Review normal capacity and record actual activity for the selected node and period. These are independent quantities: actual activity is never inferred from shifts or copied from another month. Proposals do not change published costs or runs.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Normal capacity for the period | Quantity in displayed unit | To confirm | Usual reference. May display an unconfirmed proposal. Editing selects manual input. |
| Actual activity for the period | Quantity in the same unit | To confirm | Actual total. Blank means unknown; 0 means no activity. |
| Capacity calculation | Visible primary panel | Depends on method | Days, shifts, hours per shift, resources, stoppages and factor remain accessible. The breakdown and proposal stay beside the save actions. |
| How to obtain normal capacity | Calendar / historical / manual | For a proposal | Manual input uses the same primary quantity field. |
| Operating days | Days within period | Calendar | Days from a different month are not copied. Complete when no reliable calendar exists. |
| Shifts per day | Daily shifts | Calendar | May be proposed from a compatible previous calendar; review validity. |
| Hours per shift | Hours | Calendar | Effective duration. Do not subtract breaks twice. |
| Equivalent simultaneous resources | Equivalent machines/people | Calendar | Simultaneous homogeneous resources; do not double count machines and people representing the same capacity. |
| Unavailable time (h) / How stoppages are expressed | Hours and frequency | Calendar | Frequency converts the entered hours into a period total. Do not reuse another period's total. |
| Usual activity level (%) | Percentage | Calendar | Converts available capacity to normal capacity; not actual activity. |
| Note about usual activity (optional) | Secondary text | No | Additional context; never required to calculate. |
| Historical periods / include / exceptional | Selection | Historical | Confirmed periods with matching model, node, unit and duration; no future, overlapping or duplicate periods. Review selection. Notes are optional. |
| Use this proposal when saving | Checkbox | To replace with proposal | Saves the calculation and its source in the next save operation. Existing values are not silently replaced. |
| Normal capacity source | Sources, notes and history | No | Generated from the method or identified as manual entry. |
| Actual activity source | Sources, notes and history | No | Preserves existing import/record provenance; manual edits are identified. |
| Additional notes (optional) | Text | No | Normal/actual context; do not repeat formulas or sources. |
| Note about zero capacity (optional) | Conditional text | No | Visible in details when normal = 0. |
| Note about the difference (optional) | Conditional text | No | Visible when actual exceeds normal; review quantities without a required narrative. |
| Change note (optional) | Conditional text | No | Visible when editing an existing version. Actor, timestamp and versions are recorded automatically. |

## 3. Results and interpretation
| Metric / result | Formula or source | Meaning and decision |
| :--- | :--- | :--- |
| What is missing | Validation of both quantities | Identifies missing or invalid values. Incomplete drafts are allowed. |
| Calendar capacity | Days × shifts × hours × resources | Before stoppages; no duplicate entry. |
| Available capacity | Calendar − total stoppages | Capacity after stoppages. |
| Proposed normal capacity | Available × factor, or mean of selected periods | Full precision, separate from confirmation. |
| Source and data type | Stored evidence / method | Manual entry is not recorded evidence. |
| History | Immutable versions | Review changes without changing past periods. |

## 4. Use cases, validation and common errors
- Example: 22 days × 1 shift × 8 h × 3 resources = 528 h. Stoppages of 1 h per shift and resource = 66 h. Available 462 h; at 90 %, normal is 415.8 h. The field and proposal keep the same precision.
- Save draft allows unknown actual activity. Confirm requires both valid quantities but no mandatory narrative.
- Calendars, holidays, shifts and actual records are never invented. Unavailable inputs remain pending.
- Parameters recalculate the proposal. Select Use this proposal when saving to replace an existing quantity. Editing the primary quantity switches to manual input.
- Stoppages cannot exceed calendar capacity; shifts cannot exceed 24 hours per day; quantities cannot be negative.
- Zero normal allows exceptional actual activity but no division by zero. Activity above normal is not capped.
- Closed studies cannot be edited. Version conflicts must be resolved by reloading; concurrent work is not overwritten.

## Recommended workflow
1. Review node, period and both quantities. Complete only missing information.
2. Review the calculation panel; review parameters or periods and select the proposal before saving.
3. Save a draft or confirm. Open Sources, notes and history only when needed. Manually adjusting capacity preserves calendar parameters; reopening restores them without replacing your quantity.

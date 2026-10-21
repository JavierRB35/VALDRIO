# Contextual help: Period and analysis

## 1. Purpose and financial function
Create or reopen an analysis for a defined accounting period, scenario and published model. This scope governs subsequent imports and calculations.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Existing analysis | Selector | No | Reopens saved analysis, imports and state. |
| Analysis name | Text | Yes | User-readable identifier of the study. |
| Published model | Selector | Yes | Fixes the structural version used by the study. |
| Period start and end | Dates | Yes | Establish the reporting period; start must not exceed end. |
| Currency | Currency code | Yes | Functional currency of costs and reporting. |
| Scenario | Selector | As applicable | Context for the study; not an automatic economic simulation. |
| Create, save and refresh | Actions | By state | Separate creation, persistence and reload. |
| Snapshot | Action | Optional | Preserves an auditable reference point. |
| Close and reopen | Advanced actions | By state | Control edit permissions; do not close the full financial period. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Active context | Analysis, period, scenario and model | Identifies which study feeds subsequent modules. |
| History and snapshots | Recorded versions | Allow previous states to be reviewed. |
| Readiness messages | Prerequisite checks | Explain whether import and later stages can proceed. |

## 4. Use cases, validation and common errors
- A published model is required before preparing a usable analysis.
- Save the study and verify dates before importing amounts.
- Closed analyses remain viewable until explicitly reopened.
- Changing periods does not transfer prior amounts or physical hours automatically.

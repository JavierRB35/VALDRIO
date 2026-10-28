# Contextual help: Project
**Last updated:** `2026-10-28`


## 1. Purpose and financial function
Create, open and protect the company project file (.valdrio). This establishes the monetary and physical-unit context for cost accounting but does not itself calculate costs.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Project name | Text | On creation | Identifies the company or project; it is not an accounting amount. |
| Project currency | ISO currency code | On creation | Sets the project's monetary context; does not convert imported balances. |
| Project file | .valdrio file path | According to operation | Selects the local project file; backup and recovery use separate files. |
| Activity unit code | Text identifier | On unit creation | Identifies physical measurement drivers and observations. |
| Unit dimension | Choice | On unit creation | Defines compatible measurement quantities. |
| Unit symbol and scale | Text and scale | As applicable | Symbol identifies the unit; scale is the allowed number of decimal places (0–12), not a unit conversion factor. |
| Discrete unit | Yes/no | As applicable | Identifies quantities treated as whole items. |
| Open, copy, recover and upgrade | Actions | According to operation | Handle files and upgrades without overwriting the original. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Current project and status | Opened file and metadata | Confirms the destination of work; does not imply financial closing. |
| Unit catalogue | Saved unit definitions | Supports consistent selection of pool and activity units. |
| File diagnostics | Read and write validation | Explains recovery or upgrade requirements. |

## 4. Use cases, validation and common errors
- A name and a valid path are required for project creation.
- Upgrade older project formats using the separate copy, not the original.
- Examples are opened as working copies; do not alter reference examples.
- Incompatible physical units make later activity comparisons invalid.
- Errors while creating, opening or recovering a project are highlighted in red next to the operation status; informational messages remain neutral. Existing confirmation dialogs are retained.

### Estimated orders and file format
Accepted estimated orders are stored inside the company file in schema 13. Upgrade older files into a copy, retaining the original. Backups include the frozen order history.

## Recommended workflow
1. Create a project or open a working copy of an existing file; check its name and location.
2. Select the currency and define the physical units needed by your cost centres; check dimension, scale and whether each unit is discrete.
3. Confirm the project is open and saved before moving to Structure design; upgrade old file formats into a copy while keeping the original.

# Contextual help: Auxiliary capacity and services
**Last updated:** `2026-10-25`


## 1. Purpose and financial function
Evaluate measured auxiliary services and the treatment of auxiliary capacity, distinguishing owner-once capacity, throughput and pending treatment. Do not add reciprocal gross circulation to company cost.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Auxiliary centre, unit, normal and actual | Activity and capacities read-only quantities | Review | Reuse physical observations; do not re-enter them here. |
| Auxiliary treatment | Selector | To evaluate | Choose capacity-owner, through-flow or pending explicitly. |
| Service group | Table | By centre | Differentiates services and output routes. |
| Service destination | Table | By group | Identifies the receiving auxiliary or principal node. |
| Destination quantity | Activity units | By treatment | Measured physical service coverage; not invented shares. |
| Service import | UTF-8 CSV | Optional | Requires Center;Group;Destination;Quantity headers and ungrouped decimal values. |
| Save, validate and calculate | Actions | By state | Saving does not automatically publish cost; calculation can publish T12. |
| Reopen secondary stage | Action | If closed | Makes authorised changes possible under version controls. |
| Refresh and export | Actions | By state | Retrieve saved data and export run provenance. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Questions and answers | Own cost, received cost and measured activity | Help interpret the selected auxiliary centre. |
| Service coverage | Delivered and assigned physical units | Identifies activity lacking measured service destinations. |
| Terminal result and reconciliation | Secondary allocation and T12 | Explain retained and transferred cost without reciprocal duplication. |
| Version and diagnostics | Activity and capacities sources and state | Separate partial evaluation from complete measurement. |

## 4. Use cases, validation and common errors
- Pending treatment blocks calculation and is never silently converted to through-flow.
- Unassigned realised service is not automatically idle capacity.
- Gross circulation through auxiliaries does not create extra company cost.
- Changing the related Activity and capacities observation can invalidate existing runs.
- This services import accepts CSV, while Activity and capacities physical observations also allow XLSX.

## Recommended workflow
1. Select auxiliary services and enter quantities provided and relevant capacity in consistent units.
2. Check transfers and reciprocal flows can reach principal centres without counting received costs twice.
3. Review diagnostics and economic-run results before using auxiliary services in rates or product analysis.

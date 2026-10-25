# Context help: Estimated orders
**UI View ID:** `View_Estimates`  
**Last updated:** `2026-10-25`

## 1. Purpose and financial function
Value an order using a published composition, material reference prices and activity rates from a saved economic run. Each accepted version retains its inputs. Open **Products and results → Estimated orders**.

This is a management estimate. It does not consume materials, change allocations, record actual production or value accounting inventory.

## 2. Fields and inputs
| Field | Meaning |
| --- | --- |
| Order reference | Identifies the order within the analysis. |
| Published recipe | Defines quantities, bases and units. |
| Quantity | Order output using the configured decimal separator. |
| Saved run | Economic source of the activity rates. |

1. Publish a composition in **Products and services**, then import and save reference prices for its final materials.
2. Save a completed economic calculation and confirm the activity observations needed for its rates.
3. Enter a unique order reference within the analysis, then choose a recipe, quantity and saved run.
4. Select **Calculate estimate**. Review the seven answers and the quantity, price/rate, amount and source breakdown.
5. Select **Accept and freeze version**. The study must be open and the file writable. Confirmation means the version has been saved.

Changing an input invalidates the preview. If prices, observations or the latest order version change before acceptance, calculate again. Unaccepted drafts are not saved on exit; you can keep editing or explicitly discard them.

## 3. Results and interpretation
Select a version and choose **View frozen version**. Current prices, recipes and observations do not replace its saved values. To revise an order, select its latest version and choose **Prepare new version**. Its reference and product remain unchanged; recipe version, quantity and run may change.

Choose two versions of the same order and select **Compare versions**. The comparison shows quantities, totals, unit costs and sources, plus the total cost difference. Differences may combine volume, recipe, price and rate changes; they are not actual consumption variances.

## 4. Use cases, validation and common errors
For 120 units, batch size 50, setup of 2 hours per batch at 30/hour and processing of 0.1 hours per unit at 20/hour, three batches are started: setup costs 180, processing costs 240, total cost is 420 and unit cost is 3.5. A partial final batch still requires its full setup.

Rates retain their full precision before amount rounding. Semi-finished items are expanded and final components are counted once. The finished product's reference price is not added again.

## 5. Missing data and scope
- A missing price or rate blocks acceptance. An explicit zero differs from missing data.
- Reference prices and study functional currency must match; there is no automatic currency conversion.
- Preview uses the chosen economic run and current activity observations, retaining their identities in the accepted version.
- Older files upgrade to schema 13 in a copy, preserving the original.
- Actual consumption and variances belong to T19; accounting valuation belongs to T32.

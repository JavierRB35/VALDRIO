# Contextual help: Products, services and reference costs
**Last updated:** `2026-10-25`

## 1. Purpose and financial function
This screen defines the physical components needed for a product or service and estimates a management cost from reference unit costs. Use it to review a composition or compare its priced component subtotal with an imported unit-cost reference for the output product.

A reference amount is management data. **It is not actual consumption, an observed cost, or accounting valuation of inventory or work in progress.** Actual consumption and variances belong to T19; accounting valuation is reserved for T32. Imported unit costs use the company's default currency.

## 2. Import products and unit costs
On the **Catalogue** tab, select **Import products and unit costs** and choose a CSV/TSV or XLSX file. Include a header row and these four columns:

| Column | What to enter | Example |
| :--- | :--- | :--- |
| Reference | Stable code identifying the product or material | `P-104` |
| Name | Description shown to the analyst | `Stainless screw` |
| Unit of measure | Code or symbol already present in the company's unit catalogue | `ea` |
| Unit cost | Cost of one unit, without a currency symbol | `0.12` |

Comma, semicolon, and tab delimiters are supported. Decimal formatting follows the selected regional format; with a comma decimal mark, semicolons are usually the simplest delimiter. Headers may be in Spanish or English. The unit must match one existing unit code or symbol; import does not create or convert units. Unit cost must be zero or greater.

Choose **Import as** to classify new references as product, material, or service. Select **Material** for direct components in a composition. Products and services represent outputs whose imported reference can be compared with the calculated component subtotal. Import does not create composition lines.

When importing again, VALDRIO matches by reference. An existing item keeps its identity, unit, and type, while its name and reference unit cost are updated. A new reference is added using the selected type. If an existing reference is imported with a different unit, the entire file is rejected so a price cannot be attached to an incompatible unit. Review the result and select **Save catalogue** to persist it; reloading or closing before saving discards the pending changes.

The catalogue shows reference unit cost and the source filename with the import date. The company change log records the edits. Imported prices use the company's default currency; no currency conversion is applied.

## 3. Edit the active composition

1. Create a product, material or service with reference, name, kind and unit, then **Save catalogue**. The standard **Unit (u)** is available in newly created projects; older companies can explicitly install missing default units.
2. A **manual reference unit cost** accepts `12,50` and `12.50` (without thousands grouping); its origin is determined automatically. A warning explains priority before opening a composition for a product with a non-zero manual cost.
3. Under **Composition**, choose the product and press **Open**. No draft/version selector or separate publishing step is required in the normal workflow. Edit materials and activities, then press **Save composition and catalogue**.
4. Select active **principal-node activities** (including direct labour) and active catalogue materials. **Auxiliary centres are not selectable**: their costs belong in the secondary allocation.
5. Enter quantity, physical unit, conversion and loss/yield. **Extra input** of 10% turns 10 into 11; **useful yield** of 80% requires 10 / 0.80 = 12.5. The grid displays the multiplier as you edit.

Only a **fully priced, material-only** composition with an unambiguous unit basis may replace the manual reference unit cost. If a component price or activity rate is missing, the current cost stays in place and the calculation is marked partial.

**Historical compatibility:** version controls are removed from the everyday screen, but previously published definitions remain immutable internally because accepted estimates and orders reference them. Changing a composition never silently replaces historical inputs.

Saved catalogue identities are **deactivated**, not physically deleted, to preserve historical links. They can be reactivated. Unit conversions stay within the same dimension and discrete quantities are rounded up after conversion.

## 4. Use cases and cost estimate interpretation
Enter an output quantity and select **Calculate physical requirements**. The results table shows components, base and adjusted amounts, and required amounts after conversion and rounding. The summary expands semi-finished items and shows only final materials and activities.

The cost answer separates:

- **Priced component subtotal:** required quantity × reference unit cost for final materials with a price.
- **Missing reference prices:** lists physical components without a unit cost. The visible subtotal excludes them and may therefore be partial.
- **Indirect activity not included:** activity quantities appear in the physical result, but this screen does not automatically apply T15 rates or duplicate centre amounts. Activities are excluded from this subtotal.
- **Imported output reference:** imported unit cost for the output multiplied by the entered quantity. This is displayed separately; it is not subtracted from the subtotal or used as manufacturing cost.

Example: a recipe needs 3 kg of material at €4/kg and 2 hours of activity. VALDRIO shows €12 as the priced component subtotal, warns that the 2 hours are not valued, and displays the output's imported unit-cost reference separately if available. Do not interpret €12 as the total cost while prices or activities are missing.

## 5. Limits and validation
- A reference cost is a unit amount provided by the user. It is not necessarily a purchase price, actual consumption, approved rate, or accounting cost.
- Reference costs use the company's default currency and are not converted automatically. Check that the import file uses that currency.
- With missing material prices or indirect activity, the result is not a complete cost. VALDRIO identifies the missing data instead of treating it as zero.
- Loss and yield values affect planned quantities. The analyst must decide their normality and economic treatment.
- Indirect materials may remain in cost centres. Do not include the same consumption both as a direct material and as indirect cost.
- Do not infer direct materials from accounting codes 600/601.
- Published versions cannot be edited in place; create a new version to change components or quantities.

## Complete estimated orders
To include activity costs and retain an accepted version, open **Products and results → Estimated orders**. Use a published composition and a saved economic run. This screen retains its reference subtotal preview. See the [estimated order guide](16-ordenes-estimadas.md).

## Recommended workflow
1. Import or review the catalogue and reference unit costs in the project currency.
2. Build the component list, quantities, units and bases of a composition; validate and publish the recipe.
3. Review the estimated subtotal and breakdown, then use Estimated orders for a specific order without confusing estimates with actual consumption.

### Reference price and direct composition
Edit the reference unit cost in its catalogue cell, including existing items; the manual origin is recorded. The first column opens the selected product composition by identity, preferring its draft and preserving published versions. Import previews changes in the catalogue; Save persists the complete validated catalogue atomically. No new material valuation policy is introduced.

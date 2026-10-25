# Contextual help: Products, services and reference costs
**Last updated:** `2026-10-25`

## 1. Purpose and financial function
This screen defines the physical components needed for a product or service and estimates a management cost from reference unit costs. Use it to review a composition or compare its priced component subtotal with an imported unit-cost reference for the output product.

A reference amount is management data. **It is not actual consumption, an observed cost, or accounting valuation of inventory or work in progress.** Actual consumption and variances belong to T19; accounting valuation is reserved for T32. Imported unit costs use the company's default currency.

## 2. Import products and unit costs
On the **Catalogue** tab, select **Import products and unit costs** and choose a CSV or TSV file. Include a header row and these four columns:

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

## 3. Build and publish a composition
1. Save or import catalogue items before composing.
2. In **Composition**, select a product or service and open a version or create a draft.
3. Enter batch size when preparation is shared across a batch. Add each component in the table with quantity, input unit, conversion factor, and whether it applies per unit, started batch, or order.
4. Use **Extra input** for additional physical input over the base quantity. Use **Useful yield** for the share of input that produces useful output. These are physical assumptions; they do not determine whether a loss is normal or its accounting treatment.
5. A semi-finished component must point to a published version of the same model. VALDRIO expands it and totals only final components, avoiding a second charge for the semi-finished intermediate itself.
6. Save a draft to continue editing. **Publish** fixes that version; create a new version to change it.

Conversions are explicit and allowed only between units of the same dimension. The factor means component units per entered input unit; when units match, the factor must be 1. Discrete units are rounded up after conversion. A partial batch still requires full preparation for each started batch (for example, 120 units with a batch size of 50 require 3 preparations).

## 4. Use cases and cost estimate interpretation
Enter an output quantity and select **Calculate physical requirements**. The results table shows components, base and adjusted amounts, and required amounts after conversion and rounding. The summary expands semi-finished items and shows only final materials and activities.

The cost answer separates:

- **Priced component subtotal:** required quantity × reference unit cost for final materials with a price.
- **Missing reference prices:** lists physical components without a unit cost. The visible subtotal excludes them and may therefore be partial.
- **Indirect activity not included:** activity quantities appear in the physical result, but this phase does not automatically apply T15 rates or duplicate centre amounts. Activities are excluded from this subtotal.
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

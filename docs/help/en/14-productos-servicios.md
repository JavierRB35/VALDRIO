# Contextual help: Products, services and reference costs
**Last updated:** `2026-11-18`
**State:** functional guide for T17/T18 reviewed against the behaviour implemented in the products branch. The amount shown is a management estimate, not an accounting valuation.

## 1. Purpose
This screen maintains the catalogue of products, services, semi-finished items and materials; defines the physical components required by each output; and calculates requirements and a management subtotal from reference unit costs.

A reference amount **is not actual consumption, an observed cost, or an accounting valuation of inventory or work in progress**. Actual consumption and variances belong to T19; accounting valuation belongs to T32. Unit costs are expressed in the company default currency and are never converted to another currency.

## 2. Import products and unit costs
On **Catalogue**, select **Import products and unit costs**. The file needs a header row and these four columns, in any order:

| Column | What to enter | Example |
| :--- | :--- | :--- |
| Reference | Stable code entered as text; case and leading zeros are preserved | `0012` |
| Name | Description shown to the analyst | `Stainless screw` |
| Unit of measure | Code or symbol already present in the unit catalogue | `ea` |
| Unit cost | Cost of one unit, without currency symbol; zero or greater | `0.12` |

Formats and real limits:

- **CSV/TSV/TXT:** UTF-8; the first line determines the delimiter among `;`, `,` or tab. Double quotes protect delimiters inside a value. Unclosed quotes are rejected.
- **XLSX:** only the **first worksheet** is read; headers must be in **row 1** and data starts in **row 2**. The Reference cell must be text: a number such as `0012` loses its leading zeros and the row is rejected. Formulas are not evaluated: the last cached value saved by Excel is used. A formula without a cached value or an error cell provides no usable value and the row is rejected.
- The XLSX adapter applies technical limits of 32 MiB per file, 100,000 rows, 16,384 columns, 262,144 characters per cell and 250,000 shared strings. A file exceeding them is rejected.
- Headers may be in Spanish or English and are compared without case, accent or space distinctions. A repeated or ambiguous header is rejected.
- Cost follows the regional format: in es-ES use `0,12`; `12.50` is also read as a decimal when the dot cannot be a thousands group (three final digits). Currency symbols are not accepted.

**Import as** classifies only new references as product, material or service. Choose **Material** for direct components. On re-import, an existing reference keeps its identity, unit and type; name, cost and provenance are updated. A mismatched unit or duplicate references reject the whole file. Review the result and select **Save catalog and pending changes** to persist it atomically; closing or reloading before saving discards those changes.

## 3. Edit the active composition

1. Create a product, material or service with reference, name, kind and unit, then save it. The **Unit (u)** preset is available in new projects and can be installed from the screen button in older projects.
2. The reference cost may be empty or manual, with a comma or dot decimal (`12.50` or `12,50`) and no thousands grouping. Invalid text stays in the cell and is not saved. A non-zero manual cost triggers an explanation of cost precedence when the composition is opened.
3. Under **Composition**, choose the product and select **Open**. There are no draft, version-number or publish controls in the normal workflow. Edit the components and select **Save composition and catalogue**.
4. Allowed components are active catalogue materials, published semi-finished items from the same structure and activities from **active principal nodes**. Those nodes may represent production, direct labour or other direct factors. Auxiliary nodes are not offered: their costs arrive through secondary allocation and must not be counted twice.
5. A new composition cannot introduce auxiliary nodes, inactive references or inactive semi-finished items, including through a nested semi-finished child. Historical definitions can still be opened and reviewed; an inactive reference appears in the selector marked as inactive, is opened with **Open** and the view is read-only: it cannot edit, save or publish. Non-selectable components are identified as such.
6. On each line enter the basis, quantity, input unit, conversion factor and loss or yield.

### Basis, conversion and rounding
- **Per unit:** multiply by the simulated output units.
- **Per started batch:** multiply by whole batches, rounded up. With 120 units and batch size 50, three batches are started; the last partial batch requires its full setup.
- **Per order:** apply once in the calculation. This screen does not know a specific order quantity, so a line with this basis prevents automatic unit-cost updates.
- Loss is applied before conversion and rounding. **Extra input** of 10% gives `10 × 1.10 = 11`. **Useful yield** of 80% requires `10 / 0.80 = 12.5`. Basis, conversion factor and, for discrete units, rounding up are applied afterwards.
- Zero is a declared, valid value; an empty cell means missing data and is never converted to zero.
- Conversions are only allowed between units of the same dimension. If the input and component units are the same, the factor must be `1`.

## 4. Manual cost, calculated cost and partial results
When a composition is saved with every final material priced, no activity and no per-order basis, VALDRIO calculates the output unit cost and updates its reference cost. The calculation uses the saved batch size, so changing the batch size may change the unit cost when setup is consumed per batch. The origin becomes composition and retains the internal reference of the definition used.

If a price is missing, activity is present —including direct labour or other direct factors— without a rate in this screen, or a per-order basis exists at any expanded level, the breakdown is **partial**:

- any manual reference is retained;
- a cost calculated by an earlier composition is cleared, so an incomplete breakdown cannot keep appearing as current;
- a warning explains what must be reviewed;
- a missing value never becomes an invented price.

Activity appears in the physical requirements, but this screen applies no T15 rates. To value principal nodes with rates and retain an accepted order estimate, continue in **Estimated orders** (T18).

## 5. Interpreting the preview
When physical requirements are calculated:

- the table shows base quantity, loss-adjusted quantity, converted quantity and required quantity for each component;
- semi-finished items are expanded and their final components are summed once;
- the summary shows final materials and activities;
- the **priced component subtotal** sums required quantity × reference unit cost for final materials with a price;
- components without a price are listed separately and excluded from that subtotal;
- unpriced activity is shown separately and excluded from the subtotal;
- the **imported output reference** is a separate comparison: unit cost × simulated quantity. It is not subtracted from the subtotal or used as manufacturing cost.

Example: 3 kg of material at €4/kg and 2 hours of activity give a €12 subtotal with an unpriced-activity warning. While a price or rate is missing, €12 is not a total cost.

### Accounting interpretation limits
- A reference cost is a unit amount provided by the user. It is not necessarily a purchase price, actual consumption, an approved rate or an accounting cost.
- Indirect materials may remain in cost centres. Do not include the same consumption both as a direct material and as indirect cost.
- Do not infer direct materials from accounting codes 600/601.
- Loss and yield affect planned quantities; the analyst decides their normality and economic treatment. This screen does not approve that policy.

## 6. Historical compatibility and validation
- There are no version controls in the ordinary interface. Saving the active composition creates a new internal published definition; definitions used by accepted estimates or orders remain immutable.
- Removing a saved reference **deactivates** it without deleting its identity or relationships. Tick **Active** to restore it.
- If an inactive reference still has a saved composition, it can be **opened in read-only mode** to review its history: its components and data are shown, but it cannot be edited, saved or published, no new version or clone is created and it is not offered as a component of other compositions. The product selector includes these references marked as inactive. Selectors for new components remain limited to active materials and active principal nodes.
- Discarding changes restores the saved version that was open in a clean state and keeps the selected product. If the open composition was never saved (for example, a new unsaved version), it is not recreated: the page is explicitly left without a loaded composition and states that **Open** must be pressed.
- The catalogue requires unique references, name, valid unit and non-negative cost. Quantities, factors and percentages are validated before saving; yield must be greater than 0 and at most 100, and extra input cannot be negative.
- Batch size must be positive; for a discrete output unit it must be an integer.
- Decimal values use `decimal`; amounts and quantities keep their precision and are rounded only where the physical rule requires it.

### Code references
- [`src/Valdrio.Desktop/CompositionWindow.xaml`](../../src/Valdrio.Desktop/CompositionWindow.xaml)
- [`src/Valdrio.Desktop/CompositionEditor.cs`](../../src/Valdrio.Desktop/CompositionEditor.cs)
- [`src/Valdrio.Desktop/ProductCostImport.cs`](../../src/Valdrio.Desktop/ProductCostImport.cs)
- [`src/Valdrio.Calculation/CompositionCalculator.cs`](../../src/Valdrio.Calculation/CompositionCalculator.cs)
- [`src/Valdrio.Calculation/CompositionCostEstimator.cs`](../../src/Valdrio.Calculation/CompositionCostEstimator.cs)

To include activity valuation and retain an accepted version, open **Products and results → Estimated orders** and see its [guide](16-ordenes-estimadas.md).

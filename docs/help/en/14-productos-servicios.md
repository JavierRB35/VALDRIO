# Contextual help: Products, services and compositions

## 1. Purpose and financial function
Define and publish physical bills of materials or service compositions, including nested semi-finished items. This view computes physical needs; it does not value them with prices or record actual consumption.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Item code, name, type and unit | Text and selectors | By item | Identify materials, products, services and semi-finished items. |
| Product and version | Selectors | Yes | Open a draft or an immutable published recipe. |
| Batch size | Physical quantity | By recipe | Sets the shared preparation quantity. |
| Note | Text | Optional | Documents assumptions and recipe scope. |
| Composition lines | Table | By recipe | Select components, required quantity, unit and conditions. |
| Unit conversion factor | Number | If units differ | Explicitly converts only compatible dimensions. |
| Loss or yield | Percent | By line | Changes physical requirement, not automatically capitalisable waste. |
| Preparation per batch | Quantity | When relevant | Partial batches still require full preparation under D06. |
| Publish and new version | Actions | By state | Preserve published recipe history. |
| Preview quantity | Physical quantity | To simulate | Scales physical needs to a stated batch or quantity. |
| Save and preview | Actions | By state | Distinguish draft persistence from physical calculation. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Recipe version state | Draft or published revision | Shows which assumptions apply. |
| Physical needs | Converted quantities adjusted for yield and discrete units | Plan future requirements, not monetary valuation. |
| Analyst questions | Batches, dependencies, losses and versions | Expose missing or changing assumptions. |
| Expanded composition | Final components without duplicating semi-finished intermediates | Review physical requirements. Monetary valuation is unavailable in this view. |

## 4. Use cases, validation and common errors
- This view does not include prices, monetary rates or actual consumption records.
- Never silently convert unrelated measurement dimensions.
- Semi-finished dependencies must reference published versions and not form cycles.
- Published recipes require a new version for changes.
- Do not infer direct materials merely from accounting codes 600/601.

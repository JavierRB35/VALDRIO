# Contextual help: Preferences and presentation formats

## 1. Purpose and financial function
Choose the UI language, number and date formatting and display time zone. Presentation preferences do not change economic cost values or carry out foreign-exchange conversions.

## 2. Fields and inputs
| Field / parameter | Type / unit | Required | Purpose and cost impact |
| :--- | :--- | :--- | :--- |
| Interface language | Spanish/English selector | Optional | Changes available labels and contextual help language. |
| Format culture | Region selector | Optional | Formats values and dates without converting amounts. |
| Display time zone | Selector | Optional | Changes how instants are displayed, not accounting dates. |
| Retry saving preferences | Action | On error | Attempts to persist changes that were not saved. |
| Development tools | Test controls | Development only | Examples of rounding, currency metadata and FX, not general production flows. |

## 3. Results and interpretation
| Result | Formula or source | Interpretation / decision |
| :--- | :--- | :--- |
| Format preview | Monetary, date and instant values | Checks readability of selected presentation. |
| Preference persistence | Saved status or warning | Reports whether settings were stored. |
| Field help | Tooltips, keyboard focus and assistive text | Supplies short contextual descriptions. |
| Help for this screen (F1) | Offline documented view catalogue | Opens a read-only guide without changing the company project or calculations. |

## 4. Use cases, validation and common errors
- Regional formatting does not convert a study's balances.
- Changing display time zone does not change business accounting dates.
- Do not mistake developer-only tools for production controlling features.
- Retry failed preference storage rather than assuming it persisted.
- Use Help (F1) from the header and close the reader with Escape.

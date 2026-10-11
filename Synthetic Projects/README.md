# VALDRIO synthetic acceptance projects — T13

This folder contains **fictional**, executable business scenarios; it is not production accounting data.

- `Service Company/` — professional service costing, EUR 17,500; 2 principal nodes, 2 auxiliary nodes, 6 accounts, negative credit, zero row, mixed costs, self-service and reciprocal allocations.
- `Industrial Company/` — production cost model, EUR 36,000; 3 production nodes, 2 auxiliaries, lot/hour units, 7 accounts, negative purchase rebate, zero row, mixed costs and mutual services.
- `verify_scenarios.py` — independent Python 3 exact-fraction calculation and negative controls.
- `Generated/` — **untracked, local output** after the Windows integration tests run: actual `.valdrio` SQLite projects and snapshot JSON. It is not included in source control.

Every case defines a `scenario.json` (business structure, 100%-reconciled keys, expected primary/terminal totals, numerical tolerance) and `accounts.csv` (semicolons, decimal comma, signed amount, descriptive account code). Codes are strings, not numeric identifiers.

## Run

```powershell
python "Synthetic Projects/verify_scenarios.py" --self-test
$env:VALDRIO_T13_EXPORT_DIR = Join-Path (Get-Location) "Synthetic Projects/Generated"
dotnet test tests/Valdrio.Tests/Valdrio.Tests.csproj -c Release --filter "FullyQualifiedName~T13_"
Remove-Item Env:VALDRIO_T13_EXPORT_DIR
```

The .NET tests use the **real** file, model, import, resource, allocation, closure, snapshot and reopen services. They do not emulate the accounting engine. Consult [T13 acceptance specification](../docs/tareas/T13.md).

**Status:** the Python references are independently specified; neither the .NET tests nor the Windows UI have been run for this branch. T12 history and flow graph acceptance is blocked by PR #36 still being open, with its files absent from main.

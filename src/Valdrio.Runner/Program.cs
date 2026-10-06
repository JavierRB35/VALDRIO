using System.Text.Json;
using Valdrio.Runner;
using Valdrio.Application;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

// Portable subprocess probe: real session/OS lock, no economic execution.
if (args.Length == 2 && args[0] == "--study-snapshot")
{
    try
    {
        var receipt = CanonicalSnapshotJson.Deserialize<StudySnapshotReceipt>(File.ReadAllText(args[1]));
        receipt.EnsureValid();
        var preparation = StudyPreparationService.Evaluate(receipt.Input);
        var input = receipt.Input;
        using var output = JsonDocument.Parse(CanonicalSnapshotJson.Serialize(new { status = "verified", outputSchema = "valdrio-study-verification-v2",
            task = input is ImportedStudySnapshotInput ? "T06-period-import" : "T05-context-only", receipt.Id,
            receipt.StudyId, receipt.InputRevision, receipt.PayloadSchema, receipt.EconomicInputHash, receipt.FullSnapshotHash,
            periodStart = receipt.Input.StudyContext.PeriodStart.ToString(), periodEnd = receipt.Input.StudyContext.PeriodEnd.ToString(),
            currency = receipt.Input.StudyContext.Currency.FunctionalCurrencyCode.ToString(), preparation.ReadyForCalculation,
            resourceCount = input.Resources.Count,
            imports = input is ImportedStudySnapshotInput imported ? imported.Imports.Select(b =>
                new { b.Id, b.Prepared.Reconciliation }).ToArray() : [],
            diagnostics = preparation.Diagnostics.Select(d => d.Code) }));
        Console.WriteLine(output.RootElement.GetProperty("Value").GetRawText());
        return 0;
    }
    catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or Valdrio.Domain.DomainRuleException)
    {
        Console.Error.WriteLine("STUDY_SNAPSHOT_INVALID: " + ex.GetType().Name); return 3;
    }
}

if (args.Length == 2 && args[0] == "--company-lock-probe")
{
    try
    {
        var catalog = new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json"));
        using var session = new SqliteCompanyFileService(catalog, new SystemClock()).Open(args[1]);
        Console.WriteLine("READY " + session.Read().Id);
        Console.Out.Flush();
        Console.ReadLine();
        return 0;
    }
    catch (CompanyFileException ex) { Console.WriteLine(ex.Diagnostic.Code); return 3; }
}

if (args.Contains("--verify-t02-reference", StringComparer.Ordinal))
{
    var path = Path.Combine(
        AppContext.BaseDirectory,
        "reference",
        "t02-rounding-cases.json");

    var verified = T02ReferenceVerifier.Verify(path);

    Console.WriteLine(
        JsonSerializer.Serialize(
            new
            {
                status = "passed",
                task = "T02",
                verified_count = verified.Count,
                cases = verified
            }));

    return 0;
}

if (args.Contains("--verify-t02a-currency", StringComparer.Ordinal))
{
    var path = Path.Combine(
        AppContext.BaseDirectory,
        "reference",
        "currency-catalog-example.json");

    var result = T02ACurrencyVerifier.Verify(path);

    Console.WriteLine(
        JsonSerializer.Serialize(
            new
            {
                status = "passed",
                task = "T02A-sub1",
                checks = result
            }));

    return 0;
}

if (args.Contains("--verify-t02a-time", StringComparer.Ordinal))
{
    var result = T02ATimeVerifier.Verify();

    Console.WriteLine(
        JsonSerializer.Serialize(
            new
            {
                status = "passed",
                task = "T02A-sub2",
                checks = result
            }));

    return 0;
}

if (args.Contains("--verify-t02a-fx", StringComparer.Ordinal))
{
    var result = T02AFxVerifier.Verify();

    Console.WriteLine(
        JsonSerializer.Serialize(
            new
            {
                status = "passed",
                task = "T02A-sub3",
                checks = result
            }));

    return 0;
}

Console.WriteLine(
    "VALDRIO Runner ready. Economic snapshot execution is not implemented yet.");

return 0;

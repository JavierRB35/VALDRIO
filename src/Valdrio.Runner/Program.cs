using System.Text.Json;
using Valdrio.Runner;

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

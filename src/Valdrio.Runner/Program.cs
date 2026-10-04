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

Console.WriteLine(
    "VALDRIO Runner ready. Economic snapshot execution is not implemented yet.");

return 0;

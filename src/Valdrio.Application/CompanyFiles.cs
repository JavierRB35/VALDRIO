using Valdrio.Domain;

namespace Valdrio.Application;

public enum CompanyFileOpenMode { Edit, ReadOnly }

public sealed record CreateCompanyFile(
    string Path, string Name, CurrencyCode DefaultCurrency, PrecisionPolicy PrecisionPolicy);

// DefaultCurrency is a suggestion for future studies, not their functional currency.
public sealed class CompanyFileSnapshot
{
    public CompanyFileSnapshot(EntityId id, string name, CurrencyCode defaultCurrency,
        UtcInstant createdAt, long revision, PrecisionPolicy precisionPolicy,
        IEnumerable<UnitDefinition> units, int schemaVersion)
    {
        Id = id; Name = name; DefaultCurrency = defaultCurrency; CreatedAt = createdAt;
        Revision = revision; PrecisionPolicy = precisionPolicy;
        Units = FrozenList.Copy(units); SchemaVersion = schemaVersion;
    }
    public EntityId Id { get; }
    public string Name { get; }
    public CurrencyCode DefaultCurrency { get; }
    public UtcInstant CreatedAt { get; }
    public long Revision { get; }
    public PrecisionPolicy PrecisionPolicy { get; }
    public IReadOnlyList<UnitDefinition> Units { get; }
    public int SchemaVersion { get; }
}

public interface ICompanyFileSession : IDisposable
{
    IEstimateWorkspace Estimates => throw new NotSupportedException();
    ICompositionWorkspace Compositions => throw new NotSupportedException();
    IRunStore Runs => throw new NotSupportedException();
    IPhysicalActivityWorkspace PhysicalActivity => throw new NotSupportedException();
    IResourceWorkspace Resources { get; }
    IAllocationWorkspace Allocations => throw new NotSupportedException();
    IImportWorkspace Imports { get; }
    IStudyWorkspace Studies { get; }
    IModelWorkspace Models { get; }
    string Path { get; }
    CompanyFileOpenMode Mode { get; }
    CompanyFileSnapshot Read(CancellationToken cancellationToken = default);
    CompanyFileSnapshot AddUnits(IEnumerable<UnitDefinition> units, long expectedRevision,
        string actor, CancellationToken cancellationToken = default);
    void Backup(string destination, CancellationToken cancellationToken = default);
}

public interface ICompanyFileService
{
    // Read-only compatibility probe. Older files are a normal user choice, not an Open() exception.
    // Existing alternate service implementations can keep relying on their legacy Open() behavior.
    bool RequiresUpgrade(string path, CancellationToken cancellationToken = default) => false;
    ICompanyFileSession Upgrade(string source, string destination, string actor, CancellationToken cancellationToken = default);
    ICompanyFileSession Create(CreateCompanyFile command, CancellationToken cancellationToken = default);
    ICompanyFileSession Open(string path, CompanyFileOpenMode mode = CompanyFileOpenMode.Edit,
        CancellationToken cancellationToken = default);
    // Restore always creates a new file. The source and existing destinations remain untouched.
    ICompanyFileSession Restore(string source, string destination, CancellationToken cancellationToken = default);
}

public sealed class CompanyFileException(string code, string messageKey, Exception? inner = null)
    : Exception(code, inner)
{
    public DomainDiagnostic Diagnostic { get; } = new(code, messageKey, []);
}

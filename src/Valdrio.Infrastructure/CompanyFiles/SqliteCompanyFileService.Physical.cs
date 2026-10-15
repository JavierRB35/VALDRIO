using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure.CompanyFiles;

public sealed partial class SqliteCompanyFileService
{
    private static PhysicalObservation RestorePhysical(PhysicalObservationRow row)
    {
        var observation = CanonicalSnapshotJson.Deserialize<PhysicalObservation>(row.CanonicalJson);
        Invalid(observation.Id.ToString() != row.Id ||
            observation.CompanyId.ToString() != row.CompanyId ||
            observation.StudyId.ToString() != row.StudyId ||
            observation.ModelVersionId.ToString() != row.ModelVersionId ||
            observation.NodeId.ToString() != row.NodeId ||
            observation.PoolId.ToString() != row.PoolId ||
            observation.UnitId.ToString() != row.UnitId ||
            observation.PeriodStart.ToString() != row.PeriodStart ||
            observation.PeriodEnd.ToString() != row.PeriodEnd ||
            observation.Version != row.Version || observation.State.ToString() != row.State ||
            Json(observation) != row.CanonicalJson ||
            PublishedModelStructure.Hash(row.CanonicalJson) != row.ContentHash,
            "Physical observation columns/payload differ");
        Invalid(observation.Version < 1 || observation.PeriodStart.Value > observation.PeriodEnd.Value ||
            !Enum.IsDefined(observation.State) || string.IsNullOrWhiteSpace(observation.Actor),
            "Physical observation metadata invalid");
        return observation;
    }

    private static PhysicalObservationRow ToPhysicalRow(PhysicalObservation value)
    {
        var json = Json(value);
        return new()
        {
            Id = value.Id.ToString(), CompanyId = value.CompanyId.ToString(),
            StudyId = value.StudyId.ToString(), ModelVersionId = value.ModelVersionId.ToString(),
            NodeId = value.NodeId.ToString(), PoolId = value.PoolId.ToString(), UnitId = value.UnitId.ToString(),
            PeriodStart = value.PeriodStart.ToString(), PeriodEnd = value.PeriodEnd.ToString(),
            Version = value.Version, State = value.State.ToString(),
            CanonicalJson = json, ContentHash = PublishedModelStructure.Hash(json)
        };
    }

    private static void ValidatePhysical(PhysicalDbContext db, CompanyFileSnapshot file)
    {
        var records = db.PhysicalObservations.AsNoTracking().ToArray().Select(RestorePhysical).ToArray();
        var studies = db.Studies.AsNoTracking().ToArray().ToDictionary(s => s.Id);
        var events = db.Audit.AsNoTracking()
            .Where(a => EF.Property<string>(a, "AggregateType") == "PhysicalObservation")
            .Select(a => new { Row = a, AggregateId = EF.Property<string>(a, "AggregateId") })
            .ToArray();
        Invalid(events.Length != records.Length, "Physical audit count differs from the persisted versions");
        var auditByVersion = events.ToDictionary(e => e.AggregateId, e => e.Row);
        var scopes = records.GroupBy(r => (r.CompanyId, r.StudyId, r.ModelVersionId, r.PoolId, r.PeriodStart, r.PeriodEnd));
        foreach (var chain in scopes)
        {
            PhysicalObservation? prior = null;
            foreach (var observation in chain.OrderBy(r => r.Version))
            {
                Invalid(observation.CompanyId != file.Id || !studies.TryGetValue(observation.StudyId.ToString(), out var study) ||
                    observation.ModelVersionId.ToString() != study.SelectedModelVersionId &&
                    !db.Models.AsNoTracking().Any(m => m.Id == observation.ModelVersionId.ToString() &&
                        m.CompanyId == file.Id.ToString()), "Physical scope invalid");
                Invalid(observation.Version != (prior?.Version ?? 0L) + 1L, "Physical version sequence invalid");
                // Historical model/period are frozen even if the study subsequently changes.
                var pool = db.Pools.AsNoTracking().SingleOrDefault(p => p.Id == observation.PoolId.ToString() &&
                    p.ModelVersionId == observation.ModelVersionId.ToString());
                Invalid(pool is null || pool.NodeId != observation.NodeId.ToString() ||
                    pool.DriverUnitId != observation.UnitId.ToString(), "Physical pool/unit association invalid");
                Invalid(!auditByVersion.TryGetValue(observation.Id.ToString(), out var audit) ||
                    audit.Revision != 0 || audit.CompanyId != file.Id.ToString() ||
                    audit.Actor != observation.Actor ||
                    audit.Operation != (observation.State == PhysicalObservationState.Annulled
                        ? "AnnulPhysicalVersion" : "RecordPhysicalVersion") ||
                    audit.BeforeJson != (prior is null ? "null" : Json(prior)) ||
                    audit.AfterJson != Json(observation), "Physical audit/version chain invalid");
                var source = new SavePhysicalObservation(
                    observation.CompanyId, observation.StudyId, 0L, observation.NodeId, observation.PoolId,
                    observation.Version - 1, observation.State == PhysicalObservationState.Annulled
                        ? PhysicalObservationState.Draft : observation.State,
                    observation.TheoreticalQuantity, observation.PracticalQuantity,
                    observation.NormalQuantity, observation.RealQuantity,
                    observation.NormalEvidence, observation.RealEvidence,
                    observation.NormalZeroReason, observation.ExceptionReason,
                    observation.CorrectionReason, observation.Actor);
                PhysicalActivityRules.Validate(source, file.PrecisionPolicy);
                Invalid(prior is not null && prior.State == PhysicalObservationState.Confirmed &&
                    string.IsNullOrWhiteSpace(observation.CorrectionReason),
                    "Confirmed physical version lacks a correction reason");
                prior = observation;
            }
        }
    }

    private sealed partial class Session
    {
        public IPhysicalActivityWorkspace PhysicalActivity => this;

        private T PhysicalWorkspace<T>(bool write, CancellationToken token,
            Func<PhysicalDbContext, CompanyFileSnapshot, T> action)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (write && Mode != CompanyFileOpenMode.Edit) throw Error("FILE_READ_ONLY", "File.ReadOnly");
                return Guard(() =>
                {
                    var file = Validate(Path, token);
                    token.ThrowIfCancellationRequested();
                    using var connection = Connect(Path, write ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly);
                    using var db = new PhysicalDbContext(connection);
                    using var transaction = db.Database.BeginTransaction();
                    var result = action(db, file);
                    if (write)
                    {
                        db.SaveChanges();
                        owner.onCheckpoint?.Invoke(CompanyWriteCheckpoint.BeforeCommit);
                    }
                    token.ThrowIfCancellationRequested();
                    if (write) transaction.Commit();
                    return result;
                });
            }
        }

        private static PhysicalObservation? Latest(PhysicalDbContext db, EntityId company, StudyDefinition study,
            EntityId poolId) => db.PhysicalObservations.AsNoTracking()
            .Where(r => r.CompanyId == company.ToString() && r.StudyId == study.Id.ToString() &&
                r.ModelVersionId == study.SelectedModelVersionId.ToString() &&
                r.PoolId == poolId.ToString() &&
                r.PeriodStart == study.PeriodStart.ToString() && r.PeriodEnd == study.PeriodEnd.ToString())
            .OrderByDescending(r => r.Version).Select(r => r).AsEnumerable()
            .Select(RestorePhysical).FirstOrDefault();

        public IReadOnlyList<PhysicalObservation> ReadCurrent(EntityId companyId, EntityId studyId,
            CancellationToken cancellationToken = default) => PhysicalWorkspace(false, cancellationToken, (db, file) =>
        {
            Scope(companyId, file);
            var study = Study(RequiredStudy(db, companyId, studyId));
            var rows = db.PhysicalObservations.AsNoTracking().Where(r => r.CompanyId == companyId.ToString() &&
                r.StudyId == studyId.ToString() &&
                r.ModelVersionId == study.SelectedModelVersionId.ToString() &&
                r.PeriodStart == study.PeriodStart.ToString() &&
                r.PeriodEnd == study.PeriodEnd.ToString()).ToArray();
            // An annulled latest version remains visible as annulled, never as the preceding valid value.
            return FrozenList.Copy(rows.Select(RestorePhysical).GroupBy(r => r.PoolId)
                .Select(g => g.OrderByDescending(v => v.Version).First()).OrderBy(v => v.NodeId));
        });

        public IReadOnlyList<PhysicalObservation> ReadHistory(EntityId companyId, EntityId studyId,
            EntityId poolId, CancellationToken cancellationToken = default) => PhysicalWorkspace(false, cancellationToken, (db, file) =>
        {
            Scope(companyId, file);
            _ = RequiredStudy(db, companyId, studyId);
            return FrozenList.Copy(db.PhysicalObservations.AsNoTracking().Where(r =>
                r.CompanyId == companyId.ToString() && r.StudyId == studyId.ToString() &&
                r.PoolId == poolId.ToString()).ToArray().Select(RestorePhysical)
                .OrderBy(v => v.PeriodStart.Value).ThenBy(v => v.ModelVersionId).ThenBy(v => v.Version));
        });

        public IReadOnlyList<PhysicalObservation> ReadReferences(EntityId companyId, EntityId studyId,
            CancellationToken cancellationToken = default) => PhysicalWorkspace(false, cancellationToken, (db, file) =>
        {
            Scope(companyId, file);
            _ = RequiredStudy(db, companyId, studyId);
            var all = db.PhysicalObservations.AsNoTracking()
                .Where(r => r.CompanyId == companyId.ToString()).ToArray().Select(RestorePhysical);
            return FrozenList.Copy(all.GroupBy(r => (r.StudyId, r.ModelVersionId, r.PoolId, r.PeriodStart, r.PeriodEnd))
                .Select(g => g.OrderByDescending(r => r.Version).First())
                .Where(r => r.State == PhysicalObservationState.Confirmed &&
                    (r.StudyId != studyId || r.PeriodStart != Study(RequiredStudy(db, companyId, studyId)).PeriodStart))
                .OrderBy(r => r.PeriodStart.Value).ThenBy(r => r.PoolId));
        });

        private PhysicalObservation InsertPhysical(PhysicalDbContext db, CompanyFileSnapshot file,
            SavePhysicalObservation command)
        {
            Scope(command.CompanyId, file);
            PhysicalActivityRules.Validate(command, file.PrecisionPolicy);
            var study = Study(RequiredStudy(db, file.Id, command.StudyId));
            if (study.State != StudyState.Open)
                throw PhysicalActivityRules.Error("ACT_STUDY_CLOSED", "Activity.StudyClosed");
            StudyRules.CheckRevision(command.ExpectedStudyRevision, study.Revision, study.Id);
            var model = RequiredPublication(db, file.Id, study.SelectedModelVersionId);
            var pool = model.Structure.Pools.SingleOrDefault(p => p.Id == command.PoolId);
            var node = model.Structure.Nodes.SingleOrDefault(n => n.Id == command.NodeId &&
                n.Role == NodeRole.Principal && n.IsActive);
            if (pool is null || node is null || pool.NodeId != command.NodeId)
                throw PhysicalActivityRules.Error("ACT_SCOPE_INVALID", "Activity.ScopeInvalid");

            var before = Latest(db, file.Id, study, pool.Id);
            if ((before?.Version ?? 0) != command.ExpectedPhysicalVersion)
                throw PhysicalActivityRules.Error("ACT_CONFLICT", "Activity.Conflict");
            PhysicalActivityRules.EnsureCorrectionReason(before, command.CorrectionReason);
            var after = new PhysicalObservation(new EntityId(Guid.NewGuid()), file.Id, study.Id,
                study.SelectedModelVersionId, command.NodeId, pool.Id, pool.DriverUnitId,
                study.PeriodStart, study.PeriodEnd, checked((before?.Version ?? 0) + 1),
                command.State, command.TheoreticalQuantity, command.PracticalQuantity,
                command.NormalQuantity, command.RealQuantity, command.NormalEvidence, command.RealEvidence,
                command.NormalZeroReason, command.ExceptionReason, command.CorrectionReason,
                command.Actor, owner.time.GetUtcNow());
            db.PhysicalObservations.Add(ToPhysicalRow(after));
            Audit<PhysicalObservation>(db, file, "PhysicalObservation", after.Id, 0,
                "RecordPhysicalVersion", command.Actor, before, after);
            return after;
        }

        public PhysicalObservation Save(SavePhysicalObservation command, CancellationToken cancellationToken = default) =>
            PhysicalWorkspace(true, cancellationToken, (db, file) => InsertPhysical(db, file, command));

        public IReadOnlyList<PhysicalObservation> ApplyImport(ApplyPhysicalImport command,
            CancellationToken cancellationToken = default) =>
            PhysicalWorkspace(true, cancellationToken, (db, file) =>
            {
                ArgumentNullException.ThrowIfNull(command);
                ArgumentNullException.ThrowIfNull(command.Operations);
                if (command.Operations.Count == 0 || command.Operations.Count > 10_000 ||
                    command.Operations.Any(x => x is null || !Enum.IsDefined(x.Choice)))
                    throw PhysicalActivityRules.Error("ACT_IMPORT_INVALID", "Activity.ImportInvalid");
                var replace = command.Operations.Where(x => x.Choice == PhysicalImportChoice.Replace)
                    .Select(x => x.Draft).ToArray();
                if (replace.Length == 0)
                    return FrozenList.Copy(Array.Empty<PhysicalObservation>());
                if (replace.Any(x => x is null || x.State != PhysicalObservationState.Draft) ||
                    replace.Select(x => (x.CompanyId, x.StudyId, x.PoolId)).Distinct().Count() != replace.Length)
                    throw PhysicalActivityRules.Error("ACT_IMPORT_DUPLICATE", "Activity.ImportDuplicate");
                var updates = new List<PhysicalObservation>(replace.Length);
                foreach (var entry in replace)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    updates.Add(InsertPhysical(db, file, entry));
                }
                // PhysicalWorkspace issues exactly one SaveChanges and one SQLite COMMIT.
                // Any conflict rolls back *every* imported version and audit envelope.
                return FrozenList.Copy(updates);
            });

        public PhysicalObservation Annul(AnnulPhysicalObservation command, CancellationToken cancellationToken = default) =>
            PhysicalWorkspace(true, cancellationToken, (db, file) =>
            {
                Scope(command.CompanyId, file);
                if (string.IsNullOrWhiteSpace(command.Actor) || command.Actor.Length > 300 ||
                    command.Reason?.Length > 4000 || command.ExpectedStudyRevision < 0)
                    throw PhysicalActivityRules.Error("ACT_INPUT_INVALID", "Activity.InputInvalid");
                var study = Study(RequiredStudy(db, file.Id, command.StudyId));
                if (study.State != StudyState.Open)
                    throw PhysicalActivityRules.Error("ACT_STUDY_CLOSED", "Activity.StudyClosed");
                StudyRules.CheckRevision(command.ExpectedStudyRevision, study.Revision, study.Id);
                var before = Latest(db, file.Id, study, command.PoolId);
                if ((before?.Version ?? 0) != command.ExpectedPhysicalVersion)
                    throw PhysicalActivityRules.Error("ACT_CONFLICT", "Activity.Conflict");
                if (before is null || before.NodeId != command.NodeId || before.State == PhysicalObservationState.Annulled)
                    throw PhysicalActivityRules.Error("ACT_SCOPE_INVALID", "Activity.ScopeInvalid");
                if (before.State == PhysicalObservationState.Confirmed && string.IsNullOrWhiteSpace(command.Reason))
                    throw PhysicalActivityRules.Error("ACT_ANNUL_REASON", "Activity.AnnulReason");
                var after = before with
                {
                    Id = new EntityId(Guid.NewGuid()),
                    Version = checked(before.Version + 1),
                    State = PhysicalObservationState.Annulled,
                    ExceptionReason = command.Reason,
                    CorrectionReason = command.Reason,
                    Actor = command.Actor,
                    RecordedAtUtc = owner.time.GetUtcNow()
                };
                db.PhysicalObservations.Add(ToPhysicalRow(after));
                Audit<PhysicalObservation>(db, file, "PhysicalObservation", after.Id, 0, "AnnulPhysicalVersion",
                    command.Actor, before, after);
                return after;
            });
    }
}

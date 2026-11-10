using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure.CompanyFiles;
public sealed partial class SqliteCompanyFileService
{
    private static void Invalid(bool condition, string reason)
    {
        if (condition)
            throw new InvalidDataException(reason);
    }

    private static string Json<T>(T value) => CanonicalSnapshotJson.Serialize(value);
    private static void ValidateWorkspace(ModelDbContext db, CompanyFileSnapshot file)
    {
        var centers = Centers(db);
        var units = DriverUnits(db);
        var models = Models(db);
        Invalid(db.UnitStates.Count() != file.Units.Count, "Unit states incomplete");
        foreach (var c in centers)
            Invalid(c.Id.ToString() != db.Centers.Single(r => r.Id == c.Id.ToString()).Id || c.CompanyId != file.Id, "Center scope invalid");
        foreach (var u in units)
            Invalid(u.Revision < 0 || !file.Units.Contains(u.Definition), "Unit state invalid");
        var families = db.Families.AsNoTracking().ToArray();
        Invalid(families.Any(f => f.CompanyId != file.Id.ToString() || EntityId.Parse(f.Id).ToString() != f.Id || !models.Any(m => m.Structure.FamilyId.ToString() == f.Id)), "Family scope invalid");
        foreach (var group in models.GroupBy(x => x.Structure.FamilyId))
        {
            var ordered = group.OrderBy(x => x.Structure.VersionNumber).ToArray();
            Invalid(!ordered.Select(x => x.Structure.VersionNumber).SequenceEqual(Enumerable.Range(1, ordered.Length)), "Version sequence invalid");
            Invalid(ordered[0].Structure.ParentVersionId is not null, "Initial model has parent");
        }

        foreach (var model in models)
        {
            var s = model.Structure;
            Invalid(model.Revision < 0 || s.CompanyId != file.Id || s.PrecisionPolicyVersion != file.PrecisionPolicy.Version || !families.Any(f => f.Id == s.FamilyId.ToString()), "Model metadata invalid");
            if (s.ParentVersionId is { } parentId)
            {
                var parent = models.SingleOrDefault(x => x.Structure.Id == parentId);
                Invalid(parent is null || parent.Status != ModelStatus.Published || parent.Structure.FamilyId != s.FamilyId || parent.Structure.VersionNumber >= s.VersionNumber, "Model parent invalid");
            }

            foreach (var n in s.Nodes)
                Invalid(!centers.Any(c => c.Id == n.PhysicalCenterId), "Node center invalid");
            foreach (var p in s.Pools)
                Invalid(!units.Any(u => u.Definition.Id == p.DriverUnitId), "Pool unit invalid");
            if (model.Status == ModelStatus.Draft)
                Invalid(model.PublishedAtUtc is not null || model.StructuralHash is not null || model.PublishedCanonicalJson is not null, "Draft has publication");
            else
            {
                Invalid(model.PublishedAtUtc is null || model.PublishedCanonicalJson is null || model.StructuralHash is null, "Publication incomplete");
                var frozen = model.Publication!;
                Invalid(Json(frozen.Structure) != Json(s) || model.StructuralHash != PublishedModelStructure.Hash(model.PublishedCanonicalJson!) || Json(frozen) != model.PublishedCanonicalJson, "Publication differs from structure");
                Invalid(ModelRules.ValidateStructure(frozen.Structure, frozen.PhysicalCenters, frozen.DriverUnits.Select(u => new DriverUnitState(u, true, 0)).ToArray()).Count != 0, "Published structure invalid");
            }
        }

        var audit = db.Audit.AsNoTracking().Select(a => new { Row = a, Type = EF.Property<string>(a, "AggregateType"), AggregateId = EF.Property<string>(a, "AggregateId"), Schema = EF.Property<string>(a, "PayloadSchema"), TransactionId = EF.Property<string>(a, "TransactionId") }).ToArray();
        foreach (var a in audit)
        {
            Invalid(a.Row.CompanyId != file.Id.ToString() || EntityId.Parse(a.Row.Id).ToString() != a.Row.Id || EntityId.Parse(a.AggregateId).ToString() != a.AggregateId || EntityId.Parse(a.TransactionId).ToString() != a.TransactionId || string.IsNullOrWhiteSpace(a.Row.Actor) || UtcInstant.ParseIso(a.Row.AtUtc).ToCanonicalString() != a.Row.AtUtc || a.Schema != CanonicalSnapshotJson.SchemaVersion, "Audit envelope invalid");
            Invalid(a.Type is not ("Company" or "CompanyFile" or "PhysicalCenter" or "ModelVersion" or "ModelArchive" or "UnitState" or "Study" or "StudySnapshot" or "ImportBatch" or "ResourceChange" or "ResourceAdjustment" or "AllocationKey" or "AllocationChange" or "PhysicalObservation" or "CompositionCatalog" or "Estimate"), "Unknown audit aggregate");
            if (a.Type == "Estimate") Invalid(file.SchemaVersion < 13, "Estimate audit scope invalid");
            if (a.Type == "CompositionCatalog")
                Invalid(file.SchemaVersion < 12 || a.AggregateId != file.Id.ToString(), "Composition audit scope invalid");
            if (a.Type == "Company")
                Invalid(a.AggregateId != file.Id.ToString(), "Company audit scope invalid");
        }

        foreach (var center in centers)
        {
            var rows = audit.Where(a => a.Type == "PhysicalCenter" && a.AggregateId == center.Id.ToString()).OrderBy(a => a.Row.Revision).ToArray();
            Invalid(rows.LongLength != center.Revision + 1, "Center audit count invalid");
            string previous = "null";
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i].Row;
                var after = CanonicalSnapshotJson.Deserialize<PhysicalCenter>(row.AfterJson);
                after.EnsureValid();
                Invalid(row.Revision != i || after.Revision != i || after.Id != center.Id || after.CompanyId != file.Id || row.Operation != (i == 0 ? "CreateCenter" : "ChangeCenter") || row.BeforeJson != previous, "Center audit chain invalid");
                previous = Json(after);
            }

            Invalid(previous != Json(center), "Center audit/current state mismatch");
        }

        foreach (var unit in units)
        {
            var rows = audit.Where(a => a.Type == "UnitState" && a.AggregateId == unit.Definition.Id.ToString()).OrderBy(a => a.Row.Revision).ToArray();
            Invalid(rows.LongLength != unit.Revision, "Unit state audit count invalid");
            string previous = Json(new DriverUnitState(unit.Definition, true, 0));
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i].Row;
                var after = CanonicalSnapshotJson.Deserialize<DriverUnitState>(row.AfterJson);
                Invalid(row.Revision != i + 1 || after.Revision != i + 1 || after.Definition != unit.Definition || row.Operation != "ChangeUnitState" || row.BeforeJson != previous, "Unit state audit chain invalid");
                previous = Json(after);
            }

            Invalid(previous != Json(unit), "Unit state audit/current state mismatch");
        }

        foreach (var model in models)
        {
            var rows = audit.Where(a => a.Type == "ModelVersion" && a.AggregateId == model.Structure.Id.ToString()).OrderBy(a => a.Row.Revision).ToArray();
            Invalid(rows.LongLength != model.Revision + 1, "Model audit count invalid");
            string previous = "null";
            ModelVersionSnapshot? prior = null;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i].Row;
                var after = CanonicalSnapshotJson.Deserialize<ModelVersionSnapshot>(row.AfterJson);
                Invalid(!Enum.IsDefined(after.Status) || row.Revision != i || after.Revision != i || after.Structure.Id != model.Structure.Id || row.BeforeJson != previous || after.Structure.CompanyId != file.Id || after.Structure.FamilyId != model.Structure.FamilyId || after.Structure.VersionNumber != model.Structure.VersionNumber || after.Structure.ParentVersionId != model.Structure.ParentVersionId || (prior is not null && prior.Status != ModelStatus.Draft), "Model audit chain invalid");
                Invalid(i == 0 ? row.Operation != (after.Structure.ParentVersionId is null ? "CreateDraft" : "Clone") || after.Status != ModelStatus.Draft : row.Operation != (after.Status == ModelStatus.Published ? "Publish" : "SaveDraft"), "Model audit operation invalid");
                previous = Json(after);
                prior = after;
            }

            Invalid(previous != Json(model), "Model audit/current state mismatch");
        }

        // Independent archive stream is allowed without modifying a published
        // ModelVersion row (the schema explicitly rejects such UPDATEs).
        foreach (var group in audit.Where(a => a.Type == "ModelArchive").GroupBy(a => a.AggregateId))
        {
            Invalid(!models.Any(m => m.Structure.Id.ToString() == group.Key &&
                m.Status == ModelStatus.Published), "Archive references an absent/unpublished model");
            string previous = "null";
            ModelArchiveState? prior = null;
            var events = group.OrderBy(a => a.Row.Revision).ToArray();
            for (var i = 0; i < events.Length; i++)
            {
                var entry = events[i].Row;
                var after = CanonicalSnapshotJson.Deserialize<ModelArchiveState>(entry.AfterJson);
                Invalid(entry.Revision != i || after.Revision != i ||
                    after.ModelId.ToString() != group.Key ||
                    entry.BeforeJson != previous ||
                    entry.Operation != (after.Archived ? "ArchiveModel" : "RestoreModel") ||
                    (prior is not null && prior.Archived == after.Archived),
                    "Model archive audit chain invalid");
                previous = Json(after);
                prior = after;
            }
        }

        // Deleted drafts must leave a terminal audit event, while their rows
        // and children are removed only after dependency and version checks.
        var deletedModelIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in audit.Where(a => a.Type == "ModelVersion" &&
            !models.Any(m => m.Structure.Id.ToString() == a.AggregateId)).GroupBy(a => a.AggregateId))
        {
            var events = group.OrderBy(a => a.Row.Revision).ToArray();
            Invalid(events.Length < 2 || events[^1].Row.Operation != "DeleteDraft",
                "Deleted model without a terminal event");
            string previous = "null";
            for (var i = 0; i < events.Length - 1; i++)
            {
                var entry = events[i].Row;
                var after = CanonicalSnapshotJson.Deserialize<ModelVersionSnapshot>(entry.AfterJson);
                Invalid(entry.Revision != i || after.Revision != i ||
                    after.Status != ModelStatus.Draft || after.Structure.Id.ToString() != group.Key ||
                    entry.BeforeJson != previous ||
                    entry.Operation != (i == 0
                        ? after.Structure.ParentVersionId is null ? "CreateDraft" : "Clone"
                        : "SaveDraft"), "Deleted model audit chain invalid");
                previous = Json(after);
            }
            var deletion = events[^1].Row;
            Invalid(deletion.Revision != events.Length - 1 ||
                deletion.BeforeJson != previous ||
                deletion.AfterJson != Json(new { Deleted = true }),
                "Deleted model audit terminal invalid");
            // ValidateWorkspace also runs against older schema contexts,
            // which deliberately do not expose later study/allocation tables.
            Invalid((db is StudyDbContext studies &&
                        studies.Studies.Any(st => st.SelectedModelVersionId == group.Key)) ||
                db.Models.Any(m => m.ParentVersionId == group.Key) ||
                (db is AllocationDbContext allocations &&
                        allocations.AllocationKeyEvents.Any(k => k.ModelVersionId == group.Key)) ||
                audit.Any(a => a.Type == "Study" &&
                    (a.Row.BeforeJson.Contains(group.Key, StringComparison.Ordinal) ||
                     a.Row.AfterJson.Contains(group.Key, StringComparison.Ordinal))),
                "Deleted model retains references");
            deletedModelIds.Add(group.Key);
        }

        var deletedCenterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in audit.Where(a => a.Type == "PhysicalCenter" && !centers.Any(c => c.Id.ToString() == a.AggregateId)).GroupBy(a => a.AggregateId))
        {
            var rows = group.OrderBy(a => a.Row.Revision).ToArray();
            Invalid(rows.Length < 2 || rows[^1].Row.Operation != "DeleteCenter", "Missing center without deletion");
            string previous = "null";
            for (var i = 0; i < rows.Length - 1; i++)
            {
                var row = rows[i].Row;
                var after = CanonicalSnapshotJson.Deserialize<PhysicalCenter>(row.AfterJson); after.EnsureValid();
                Invalid(row.Revision != i || after.Revision != i || after.Id.ToString() != group.Key || after.CompanyId != file.Id ||
                    row.Operation != (i == 0 ? "CreateCenter" : "ChangeCenter") || row.BeforeJson != previous, "Deleted center audit chain invalid");
                previous = Json(after);
            }
            var deletion = rows[^1].Row;
            Invalid(deletion.Revision != rows.Length - 1 || deletion.BeforeJson != previous || deletion.AfterJson != Json(new { Deleted = true }), "Invalid center deletion");
            Invalid(models.Any(m => m.Structure.Nodes.Any(n => n.PhysicalCenterId.ToString() == group.Key)) ||
                audit.Any(a => a.Type == "ModelVersion" && (a.Row.BeforeJson.Contains(group.Key, StringComparison.Ordinal) || a.Row.AfterJson.Contains(group.Key, StringComparison.Ordinal))), "Deleted center has historical dependencies");
            deletedCenterIds.Add(group.Key);
        }

        Invalid(audit.Any(a => a.Type == "PhysicalCenter" && !centers.Any(c => c.Id.ToString() == a.AggregateId) && !deletedCenterIds.Contains(a.AggregateId) || a.Type == "UnitState" && !units.Any(u => u.Definition.Id.ToString() == a.AggregateId) || a.Type == "ModelVersion" && !models.Any(m => m.Structure.Id.ToString() == a.AggregateId) && !deletedModelIds.Contains(a.AggregateId)), "Orphan aggregate audit");
        Invalid(file.SchemaVersion < 3 && audit.Any(a => a.Type is "Study" or "StudySnapshot"), "Study audit in old schema");
        var upgrades = audit.Where(a => a.Type == "CompanyFile").OrderBy(a => a.Row.Revision).ToArray();
        Invalid(file.SchemaVersion < 4 && audit.Any(a => a.Type == "ImportBatch"), "Import audit in old schema");
        Invalid(file.SchemaVersion < 8 && audit.Any(a => a.Type is "ResourceChange" or "ResourceAdjustment" or "AllocationKey" or "AllocationChange"), "Resource audit in old schema");
        Invalid(file.SchemaVersion < 9 && audit.Any(a => a.Type is "AllocationKey" or "AllocationChange"), "Allocation audit in old schema");
        Invalid(file.SchemaVersion < 11 && audit.Any(a => a.Type == "PhysicalObservation"), "Physical audit in old schema");
        Invalid(upgrades.Length > SchemaVersion - 1, "Too many format upgrades");
        int? previousSchema = null;
        for (var i = 0; i < upgrades.Length; i++)
        {
            var a = upgrades[i];
            var before = CanonicalSnapshotJson.Deserialize<FormatUpgrade>(a.Row.BeforeJson);
            var after = CanonicalSnapshotJson.Deserialize<FormatUpgrade>(a.Row.AfterJson);
            Invalid(a.AggregateId != file.Id.ToString() || a.Row.Revision != i || a.Row.Operation != "UpgradeFormat" ||
                before.FromSchema is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12) || before.FromSchema != before.ToSchema || after.FromSchema != before.FromSchema ||
                after.ToSchema != before.FromSchema + 1 || (previousSchema.HasValue && before.FromSchema != previousSchema) ||
                before.MigrationId != (before.FromSchema switch { 1 => MigrationId, 2 => UpgradeMigrationId, 3 => StudyMigrationId, 4 => ImportMigrationId, 5 => GroupedImportMigrationId, 6 => ImportLedgerMigrationId, 7 => InternationalImportMigrationId, 8 => ResourceMigrationId, 9 => AllocationMigrationId, 10 => RunMigrationId, 11 => PhysicalMigrationId, _ => CompositionMigrationId }) ||
                after.MigrationId != (after.ToSchema switch { 2 => UpgradeMigrationId, 3 => StudyMigrationId, 4 => ImportMigrationId, 5 => GroupedImportMigrationId, 6 => ImportLedgerMigrationId, 7 => InternationalImportMigrationId, 8 => ResourceMigrationId, 9 => AllocationMigrationId, 10 => RunMigrationId, 11 => PhysicalMigrationId, 12 => CompositionMigrationId, _ => EstimateMigrationId }), "Format upgrade audit invalid");
            previousSchema = after.ToSchema;
        }
        Invalid(previousSchema.HasValue && previousSchema.Value != file.SchemaVersion, "Format upgrade history incomplete");
    }
}

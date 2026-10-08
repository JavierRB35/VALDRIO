using Microsoft.EntityFrameworkCore;
using Valdrio.Application;
using Valdrio.Domain;

namespace Valdrio.Infrastructure.CompanyFiles;
public sealed partial class SqliteCompanyFileService
{
    private static PhysicalCenter Center(PhysicalCenterRow r)
    {
        var center = new PhysicalCenter(EntityId.Parse(r.Id), EntityId.Parse(r.CompanyId), r.Code, r.Name, r.IsActive, r.Revision);
        center.EnsureValid();
        return center;
    }

    private static IReadOnlyList<PhysicalCenter> Centers(ModelDbContext db) => FrozenList.Copy(db.Centers.AsNoTracking().ToArray().Select(Center).OrderBy(x => x.Id));
    private static IReadOnlyList<DriverUnitState> DriverUnits(ModelDbContext db)
    {
        var states = db.UnitStates.AsNoTracking().ToArray();
        return FrozenList.Copy(db.Units.AsNoTracking().ToArray().Select(u =>
        {
            var state = states.Single(x => x.CompanyId == u.CompanyId && x.UnitId == u.Id);
            return new DriverUnitState(new(UnitId.Parse(u.Id), u.Code, u.Dimension, u.Symbol, u.DecimalScale, u.IsDiscrete), state.IsActive, state.Revision);
        }).OrderBy(x => x.Definition.Id));
    }

    private static ModelVersionSnapshot Model(ModelDbContext db, ModelVersionRow r)
    {
        var nodes = db.Nodes.AsNoTracking().Where(x => x.ModelVersionId == r.Id).ToArray().Select(n => new CalculationNodeDefinition(EntityId.Parse(n.Id), EntityId.Parse(n.ModelVersionId), EntityId.Parse(n.PhysicalCenterId), n.Code, Enum.Parse<NodeRole>(n.Role), CanonicalSnapshotJson.Deserialize<string[]>(n.FunctionTagsJson), n.IsActive)).ToArray();
        var pools = db.Pools.AsNoTracking().Where(x => x.ModelVersionId == r.Id).ToArray().Select(p => new CostPoolDefinition(EntityId.Parse(p.Id), EntityId.Parse(p.ModelVersionId), EntityId.Parse(p.NodeId), UnitId.Parse(p.DriverUnitId), p.Description, Enum.Parse<CostLayer>(p.CostLayer), CanonicalSnapshotJson.Deserialize<ModelPolicyReference>(p.CapacityPolicyJson))).ToArray();
        var structure = new ModelStructure(EntityId.Parse(r.Id), EntityId.Parse(r.CompanyId), EntityId.Parse(r.FamilyId), r.VersionNumber, r.ParentVersionId is null ? null : EntityId.Parse(r.ParentVersionId), r.Description, r.PrecisionPolicyVersion, CanonicalSnapshotJson.Deserialize<ModelPolicyReference>(r.MethodPolicyJson), nodes, pools);
        var publication = db.Publications.AsNoTracking().SingleOrDefault(x => x.ModelVersionId == r.Id);
        if (publication is not null && publication.PayloadSchema != PublishedModelStructure.PayloadSchema)
            throw new InvalidDataException("Publication schema invalid");
        return new(structure, Enum.Parse<ModelStatus>(r.Status), r.Revision, r.PublishedAtUtc is null ? null : UtcInstant.ParseIso(r.PublishedAtUtc), publication?.StructuralHash, publication?.CanonicalJson);
    }

    private static IReadOnlyList<ModelVersionSnapshot> Models(ModelDbContext db) => FrozenList.Copy(db.Models.AsNoTracking().ToArray().Select(r => Model(db, r)).OrderBy(x => x.Structure.FamilyId).ThenBy(x => x.Structure.VersionNumber));
    private static ModelVersionRow Row(ModelStructure m) => new()
    {
        Id = m.Id.ToString(),
        CompanyId = m.CompanyId.ToString(),
        FamilyId = m.FamilyId.ToString(),
        VersionNumber = m.VersionNumber,
        ParentVersionId = m.ParentVersionId?.ToString(),
        Description = m.Description,
        Status = "Draft",
        Revision = 0,
        PrecisionPolicyVersion = m.PrecisionPolicyVersion,
        MethodPolicyJson = CanonicalSnapshotJson.Serialize(m.MethodPolicy)
    };
    private static void AddStructure(ModelDbContext db, ModelStructure m)
    {
        db.Nodes.AddRange(m.Nodes.Select(n => new NodeRow { Id = n.Id.ToString(), CompanyId = m.CompanyId.ToString(), ModelVersionId = m.Id.ToString(), PhysicalCenterId = n.PhysicalCenterId.ToString(), Code = n.Code, Role = n.Role.ToString(), FunctionTagsJson = CanonicalSnapshotJson.Serialize(n.FunctionTags), IsActive = n.IsActive }));
        db.Pools.AddRange(m.Pools.Select(p => new PoolRow { Id = p.Id.ToString(), CompanyId = m.CompanyId.ToString(), ModelVersionId = m.Id.ToString(), NodeId = p.NodeId.ToString(), DriverUnitId = p.DriverUnitId.ToString(), Description = p.Description, CostLayer = p.CostLayer.ToString(), CapacityPolicyJson = CanonicalSnapshotJson.Serialize(p.CapacityPolicy) }));
    }

    private static void Scope(EntityId company, CompanyFileSnapshot file)
    {
        company.EnsureValid();
        if (company != file.Id)
            throw ModelRules.Error("MODEL_SCOPE_INVALID", "Model.ScopeInvalid", company);
    }

    private static void Revision(long expected, long actual, EntityId id)
    {
        if (expected != actual)
            throw ModelRules.Error("MODEL_REVISION_CHANGED", "Model.RevisionChanged", id);
    }

    private static void Editable(ModelVersionSnapshot m)
    {
        if (m.Status != ModelStatus.Draft)
            throw ModelRules.Error("MODEL_PUBLISHED_IMMUTABLE", "Model.PublishedImmutable", m.Structure.Id);
    }

    private static ModelVersionRow Required(ModelDbContext db, EntityId company, EntityId id)
    {
        id.EnsureValid();
        return db.Models.SingleOrDefault(x => x.Id == id.ToString() && x.CompanyId == company.ToString()) ?? throw ModelRules.Error("MODEL_NOT_FOUND", "Model.NotFound", id);
    }

    private sealed partial class Session
    {
        private T Workspace<T>(bool write, CancellationToken token, Func<ModelDbContext, CompanyFileSnapshot, T> action)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (write && Mode != CompanyFileOpenMode.Edit)
                    throw Error("FILE_READ_ONLY", "File.ReadOnly");
                return Guard(() =>
                {
                    var file = Validate(Path, token);
                    using var connection = Connect(Path, write ? Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite : Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly);
                    using var db = new AllocationDbContext(connection);
                    using var tx = db.Database.BeginTransaction();
                    var result = action(db, file);
                    if (write)
                    {
                        db.SaveChanges();
                        owner.onCheckpoint?.Invoke(CompanyWriteCheckpoint.BeforeCommit);
                        token.ThrowIfCancellationRequested();
                        tx.Commit();
                    }
                    else
                        token.ThrowIfCancellationRequested();
                    return result;
                });
            }
        }

        private void FirstWrite(ModelDbContext db)
        {
            db.SaveChanges();
            owner.onCheckpoint?.Invoke(CompanyWriteCheckpoint.AfterFirstWrite);
        }

        private void Audit<T>(ModelDbContext db, CompanyFileSnapshot file, string type, EntityId id, long revision, string operation, string actor, T? before, T after, string? requestId = null)
        {
            if (string.IsNullOrWhiteSpace(actor))
                throw ModelRules.Error("MODEL_ACTOR_REQUIRED", "Model.ActorRequired", id);
            AddAudit(db, new() { Id = NewId(), CompanyId = file.Id.ToString(), Revision = revision, Operation = operation, Actor = actor, AtUtc = owner.time.GetUtcNow().ToCanonicalString(), BeforeJson = before is null ? "null" : CanonicalSnapshotJson.Serialize(before), AfterJson = CanonicalSnapshotJson.Serialize(after) }, type, id.ToString(), CanonicalSnapshotJson.SchemaVersion, requestId);
        }

        public ModelWorkspaceData ReadWorkspace(CancellationToken token = default) =>
            Workspace(false, token, (db, _) => new ModelWorkspaceData(Centers(db), DriverUnits(db), Models(db)));
        public IReadOnlyList<PhysicalCenter> ReadCenters(CancellationToken token = default) => Workspace(false, token, (db, _) => Centers(db));
        public IReadOnlyList<DriverUnitState> ReadDriverUnits(CancellationToken token = default) => Workspace(false, token, (db, _) => DriverUnits(db));
        public IReadOnlyList<ModelVersionSnapshot> ReadModels(CancellationToken token = default) => Workspace(false, token, (db, _) => Models(db));
        public PhysicalCenter CreateCenter(CreatePhysicalCenter command, CancellationToken token = default) => Workspace(true, token, (db, file) =>
        {
            Scope(command.CompanyId, file);
            var center = new PhysicalCenter(EntityId.Parse(NewId()), file.Id, command.Code, command.Name, true, 0);
            center.EnsureValid();
            if (db.Centers.Any(x => x.Code == command.Code))
                throw ModelRules.Error("MODEL_DUPLICATE", "Model.Duplicate", center.Id);
            db.Centers.Add(new() { Id = center.Id.ToString(), CompanyId = file.Id.ToString(), Code = center.Code, Name = center.Name, IsActive = true, Revision = 0 });
            FirstWrite(db);
            Audit(db, file, "PhysicalCenter", center.Id, 0, "CreateCenter", command.Actor, (PhysicalCenter? )null, center);
            return center;
        });
        public PhysicalCenter ChangeCenter(ChangePhysicalCenter command, CancellationToken token = default) => Workspace(true, token, (db, file) =>
        {
            Scope(command.CompanyId, file);
            var row = db.Centers.SingleOrDefault(x => x.Id == command.Id.ToString() && x.CompanyId == file.Id.ToString()) ?? throw ModelRules.Error("MODEL_CENTER_UNAVAILABLE", "Model.CenterUnavailable", command.Id);
            var before = Center(row);
            Revision(command.ExpectedRevision, before.Revision, command.Id);
            var after = new PhysicalCenter(before.Id, before.CompanyId, command.Code, command.Name, command.IsActive, checked(before.Revision + 1));
            after.EnsureValid();
            if (db.Centers.Any(x => x.Code == after.Code && x.Id != row.Id))
                throw ModelRules.Error("MODEL_DUPLICATE", "Model.Duplicate", command.Id);
            row.Code = after.Code;
            row.Name = after.Name;
            row.IsActive = after.IsActive;
            row.Revision = after.Revision;
            FirstWrite(db);
            Audit(db, file, "PhysicalCenter", after.Id, after.Revision, "ChangeCenter", command.Actor, before, after);
            return after;
        });
        public DriverUnitState ChangeUnitState(ChangeDriverUnitState command, CancellationToken token = default) => Workspace(true, token, (db, file) =>
        {
            Scope(command.CompanyId, file);
            command.Id.EnsureValid();
            var row = db.UnitStates.SingleOrDefault(x => x.CompanyId == file.Id.ToString() && x.UnitId == command.Id.ToString()) ?? throw ModelRules.Error("MODEL_DRIVER_UNAVAILABLE", "Model.DriverUnavailable");
            var before = DriverUnits(db).Single(x => x.Definition.Id == command.Id);
            Revision(command.ExpectedRevision, row.Revision, command.Id.Value);
            row.IsActive = command.IsActive;
            row.Revision = checked(row.Revision + 1);
            var after = new DriverUnitState(before.Definition, row.IsActive, row.Revision);
            FirstWrite(db);
            Audit(db, file, "UnitState", command.Id.Value, row.Revision, "ChangeUnitState", command.Actor, before, after);
            return after;
        });
        public ModelVersionSnapshot CreateDraft(CreateModelDraft command, CancellationToken token = default) => Workspace(true, token, (db, file) =>
        {
            Scope(command.CompanyId, file);
            var model = new ModelStructure(EntityId.Parse(NewId()), file.Id, EntityId.Parse(NewId()), 1, null, command.Description, file.PrecisionPolicy.Version, ModelPolicyReference.PendingMethod, [], []);
            db.Families.Add(new() { Id = model.FamilyId.ToString(), CompanyId = file.Id.ToString() });
            db.Models.Add(Row(model));
            FirstWrite(db);
            var after = new ModelVersionSnapshot(model, ModelStatus.Draft, 0, null, null, null);
            Audit(db, file, "ModelVersion", model.Id, 0, "CreateDraft", command.Actor, (ModelVersionSnapshot? )null, after);
            return after;
        });
        public ModelVersionSnapshot SaveDraft(SaveModelDraft command, CancellationToken token = default) => Workspace(true, token, (db, file) =>
        {
            var m = command.Structure;
            Scope(m.CompanyId, file);
            m.EnsureScope();
            var row = Required(db, file.Id, m.Id);
            var before = Model(db, row);
            Editable(before);
            Revision(command.ExpectedRevision, row.Revision, m.Id);
            if (before.Structure.FamilyId != m.FamilyId || before.Structure.VersionNumber != m.VersionNumber || before.Structure.ParentVersionId != m.ParentVersionId || m.PrecisionPolicyVersion != file.PrecisionPolicy.Version || m.MethodPolicy != before.Structure.MethodPolicy)
                throw ModelRules.Error("MODEL_SCOPE_INVALID", "Model.ScopeInvalid", m.Id);
            var centers = Centers(db);
            var units = DriverUnits(db);
            foreach (var n in m.Nodes)
            {
                var old = before.Structure.Nodes.SingleOrDefault(x => x.Id == n.Id);
                if (!centers.Any(c => c.Id == n.PhysicalCenterId && c.CompanyId == file.Id && (c.IsActive || old?.PhysicalCenterId == c.Id)))
                    throw ModelRules.Error("MODEL_CENTER_UNAVAILABLE", "Model.CenterUnavailable", n.Id);
            }

            foreach (var p in m.Pools)
            {
                var old = before.Structure.Pools.SingleOrDefault(x => x.Id == p.Id);
                if (!units.Any(u => u.Definition.Id == p.DriverUnitId && (u.IsActive || old?.DriverUnitId == u.Definition.Id)))
                    throw ModelRules.Error("MODEL_DRIVER_UNAVAILABLE", "Model.DriverUnavailable", p.Id);
            }

            // Replace only this draft's graph, within the same transaction. No historical cascade.
            db.Pools.RemoveRange(db.Pools.Where(x => x.ModelVersionId == row.Id));
            db.SaveChanges();
            db.Nodes.RemoveRange(db.Nodes.Where(x => x.ModelVersionId == row.Id));
            db.SaveChanges();
            db.ChangeTracker.Clear();
            row = Required(db, file.Id, m.Id);
            row.Description = m.Description;
            row.Revision = checked(row.Revision + 1);
            AddStructure(db, m);
            FirstWrite(db);
            var after = new ModelVersionSnapshot(m, ModelStatus.Draft, row.Revision, null, null, null);
            Audit(db, file, "ModelVersion", m.Id, row.Revision, "SaveDraft", command.Actor, before, after);
            return after;
        });
        public IReadOnlyList<DomainDiagnostic> ValidateModel(EntityId companyId, EntityId modelId, CancellationToken token = default) => Workspace(false, token, (db, file) =>
        {
            Scope(companyId, file);
            var model = Model(db, Required(db, companyId, modelId));
            return model.Status == ModelStatus.Published ? FrozenList.Copy(Array.Empty<DomainDiagnostic>()) : ModelRules.ValidateStructure(model.Structure, Centers(db), DriverUnits(db));
        });
        public ModelVersionSnapshot Publish(PublishModel command, CancellationToken token = default) => Workspace(true, token, (db, file) =>
        {
            Scope(command.CompanyId, file);
            var row = Required(db, file.Id, command.Id);
            var before = Model(db, row);
            Editable(before);
            Revision(command.ExpectedRevision, row.Revision, command.Id);
            var centers = Centers(db);
            var units = DriverUnits(db);
            var issues = ModelRules.ValidateStructure(before.Structure, centers, units);
            if (issues.Count != 0)
                throw new DomainRuleException(issues[0]);
            var frozen = new PublishedModelStructure(before.Structure, centers.Where(c => before.Structure.Nodes.Any(n => n.PhysicalCenterId == c.Id)).ToArray(), units.Where(u => before.Structure.Pools.Any(p => p.DriverUnitId == u.Definition.Id)).Select(u => u.Definition).ToArray());
            var json = CanonicalSnapshotJson.Serialize(frozen);
            var hash = PublishedModelStructure.Hash(json);
            db.Publications.Add(new() { ModelVersionId = row.Id, PayloadSchema = PublishedModelStructure.PayloadSchema, CanonicalJson = json, StructuralHash = hash });
            FirstWrite(db);
            row.Status = "Published";
            row.Revision = checked(row.Revision + 1);
            row.PublishedAtUtc = owner.time.GetUtcNow().ToCanonicalString();
            var after = new ModelVersionSnapshot(before.Structure, ModelStatus.Published, row.Revision, UtcInstant.ParseIso(row.PublishedAtUtc), hash, json);
            Audit(db, file, "ModelVersion", command.Id, row.Revision, "Publish", command.Actor, before, after);
            return after;
        });
        public ModelVersionSnapshot Clone(CloneModel command, CancellationToken token = default) => Workspace(true, token, (db, file) =>
        {
            Scope(command.CompanyId, file);
            command.RequestId.EnsureValid();
            var parent = Model(db, Required(db, file.Id, command.Id));
            Revision(command.ExpectedRevision, parent.Revision, command.Id);
            if (parent.Status != ModelStatus.Published)
                throw ModelRules.Error("MODEL_PARENT_NOT_PUBLISHED", "Model.ParentNotPublished", command.Id);
            var repeated = db.Audit.AsNoTracking().SingleOrDefault(a => EF.Property<string>(a, "TransactionId") == command.RequestId.ToString());
            if (repeated is not null)
            {
                if (repeated.Operation != "Clone")
                    throw ModelRules.Error("MODEL_REQUEST_CONFLICT", "Model.RequestConflict", command.RequestId);
                var original = CanonicalSnapshotJson.Deserialize<ModelVersionSnapshot>(repeated.AfterJson);
                if (original.Structure.ParentVersionId != command.Id || original.Structure.Description != command.Description || repeated.Actor != command.Actor)
                    throw ModelRules.Error("MODEL_REQUEST_CONFLICT", "Model.RequestConflict", command.RequestId);
                return Model(db, Required(db, file.Id, original.Structure.Id));
            }

            var p = parent.Publication!.Structure;
            var id = EntityId.Parse(NewId());
            var remap = p.Nodes.ToDictionary(x => x.Id, _ => EntityId.Parse(NewId()));
            var m = new ModelStructure(id, file.Id, p.FamilyId, checked(db.Models.Where(x => x.FamilyId == p.FamilyId.ToString()).Max(x => x.VersionNumber) + 1), p.Id, command.Description, p.PrecisionPolicyVersion, p.MethodPolicy, p.Nodes.Select(n => new CalculationNodeDefinition(remap[n.Id], id, n.PhysicalCenterId, n.Code, n.Role, n.FunctionTags, n.IsActive)).ToArray(), p.Pools.Select(pool => new CostPoolDefinition(EntityId.Parse(NewId()), id, remap[pool.NodeId], pool.DriverUnitId, pool.Description, pool.CostLayer, pool.CapacityPolicy)).ToArray());
            db.Models.Add(Row(m));
            AddStructure(db, m);
            FirstWrite(db);
            var after = new ModelVersionSnapshot(m, ModelStatus.Draft, 0, null, null, null);
            Audit(db, file, "ModelVersion", m.Id, 0, "Clone", command.Actor, (ModelVersionSnapshot? )null, after, command.RequestId.ToString());
            return after;
        });
    }
}

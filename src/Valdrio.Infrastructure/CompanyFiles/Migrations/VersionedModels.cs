using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Valdrio.Infrastructure.CompanyFiles.Migrations;
[DbContext(typeof(ModelDbContext))]
[Migration("202610050002_VersionedModels")]
internal sealed class VersionedModels : Migration
{
    public VersionedModels()
    {
    }

    protected override void BuildTargetModel(ModelBuilder model) => V2Model.Configure(model);
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("""
            DROP INDEX IX_AuditEvents_CompanyId_Revision;
            ALTER TABLE AuditEvents ADD COLUMN AggregateType TEXT NOT NULL DEFAULT 'Company';
            ALTER TABLE AuditEvents ADD COLUMN AggregateId TEXT NOT NULL DEFAULT '';
            ALTER TABLE AuditEvents ADD COLUMN PayloadSchema TEXT NOT NULL DEFAULT 'valdrio-values-v1';
            ALTER TABLE AuditEvents ADD COLUMN TransactionId TEXT NOT NULL DEFAULT '';
            UPDATE AuditEvents SET AggregateId=CompanyId, TransactionId=Id;
            CREATE UNIQUE INDEX IX_AuditEvents_CompanyId_AggregateType_AggregateId_Revision ON AuditEvents(CompanyId,AggregateType,AggregateId,Revision);
            CREATE UNIQUE INDEX IX_AuditEvents_TransactionId ON AuditEvents(TransactionId);
            CREATE UNIQUE INDEX AK_Units_CompanyId_Id ON Units(CompanyId,Id);
            CREATE TABLE UnitStates (CompanyId TEXT NOT NULL, UnitId TEXT NOT NULL, IsActive INTEGER NOT NULL CHECK(IsActive IN(0,1)), Revision INTEGER NOT NULL CHECK(Revision>=0), PRIMARY KEY(CompanyId,UnitId), FOREIGN KEY(CompanyId,UnitId) REFERENCES Units(CompanyId,Id) ON DELETE RESTRICT);
            INSERT INTO UnitStates SELECT CompanyId,Id,1,0 FROM Units;
            CREATE TABLE PhysicalCenters (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL REFERENCES Companies(Id) ON DELETE RESTRICT, Code TEXT COLLATE BINARY NOT NULL, Name TEXT NOT NULL, IsActive INTEGER NOT NULL CHECK(IsActive IN(0,1)), Revision INTEGER NOT NULL CHECK(Revision>=0), UNIQUE(CompanyId,Id));
            CREATE UNIQUE INDEX IX_PhysicalCenters_CompanyId_Code ON PhysicalCenters(CompanyId,Code);
            CREATE TABLE ModelFamilies (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL REFERENCES Companies(Id) ON DELETE RESTRICT, UNIQUE(CompanyId,Id));
            CREATE TABLE ModelVersions (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, FamilyId TEXT NOT NULL, VersionNumber INTEGER NOT NULL CHECK(VersionNumber>=1), ParentVersionId TEXT NULL, Description TEXT NOT NULL, Status TEXT NOT NULL CHECK(Status IN('Draft','Published')), Revision INTEGER NOT NULL CHECK(Revision>=0), PublishedAtUtc TEXT NULL, PrecisionPolicyVersion TEXT NOT NULL, MethodPolicyJson TEXT NOT NULL, UNIQUE(CompanyId,Id), FOREIGN KEY(CompanyId,FamilyId) REFERENCES ModelFamilies(CompanyId,Id) ON DELETE RESTRICT, FOREIGN KEY(CompanyId,ParentVersionId) REFERENCES ModelVersions(CompanyId,Id) ON DELETE RESTRICT);
            CREATE UNIQUE INDEX IX_ModelVersions_FamilyId_VersionNumber ON ModelVersions(FamilyId,VersionNumber);
            CREATE TABLE CalculationNodes (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, ModelVersionId TEXT NOT NULL, PhysicalCenterId TEXT NOT NULL, Code TEXT COLLATE BINARY NOT NULL, Role TEXT NOT NULL CHECK(Role IN('Principal','Auxiliary')), FunctionTagsJson TEXT NOT NULL, IsActive INTEGER NOT NULL CHECK(IsActive IN(0,1)), UNIQUE(ModelVersionId,Id), FOREIGN KEY(CompanyId,ModelVersionId) REFERENCES ModelVersions(CompanyId,Id) ON DELETE RESTRICT, FOREIGN KEY(CompanyId,PhysicalCenterId) REFERENCES PhysicalCenters(CompanyId,Id) ON DELETE RESTRICT);
            CREATE UNIQUE INDEX IX_CalculationNodes_ModelVersionId_Code ON CalculationNodes(ModelVersionId,Code);
            CREATE TABLE CostPools (Id TEXT NOT NULL PRIMARY KEY, CompanyId TEXT NOT NULL, ModelVersionId TEXT NOT NULL, NodeId TEXT NOT NULL, DriverUnitId TEXT NOT NULL, Description TEXT NOT NULL, CostLayer TEXT NOT NULL CHECK(CostLayer IN('Unclassified','Fixed','Variable','Mixed')), CapacityPolicyJson TEXT NOT NULL, FOREIGN KEY(CompanyId,ModelVersionId) REFERENCES ModelVersions(CompanyId,Id) ON DELETE RESTRICT, FOREIGN KEY(ModelVersionId,NodeId) REFERENCES CalculationNodes(ModelVersionId,Id) ON DELETE RESTRICT, FOREIGN KEY(CompanyId,DriverUnitId) REFERENCES Units(CompanyId,Id) ON DELETE RESTRICT);
            CREATE UNIQUE INDEX IX_CostPools_ModelVersionId_NodeId ON CostPools(ModelVersionId,NodeId);
            CREATE INDEX IX_ModelVersions_CompanyId_FamilyId ON ModelVersions(CompanyId,FamilyId);
            CREATE INDEX IX_ModelVersions_CompanyId_ParentVersionId ON ModelVersions(CompanyId,ParentVersionId);
            CREATE INDEX IX_CalculationNodes_CompanyId_ModelVersionId ON CalculationNodes(CompanyId,ModelVersionId);
            CREATE INDEX IX_CalculationNodes_CompanyId_PhysicalCenterId ON CalculationNodes(CompanyId,PhysicalCenterId);
            CREATE INDEX IX_CostPools_CompanyId_DriverUnitId ON CostPools(CompanyId,DriverUnitId);
            CREATE INDEX IX_CostPools_CompanyId_ModelVersionId ON CostPools(CompanyId,ModelVersionId);
            CREATE TABLE ModelPublications (ModelVersionId TEXT NOT NULL PRIMARY KEY REFERENCES ModelVersions(Id) ON DELETE RESTRICT, PayloadSchema TEXT NOT NULL, CanonicalJson TEXT NOT NULL, StructuralHash TEXT NOT NULL);
            CREATE TRIGGER PublishRequiresStructure BEFORE UPDATE OF Status ON ModelVersions WHEN NEW.Status='Published' AND OLD.Status='Draft' BEGIN
                SELECT CASE WHEN NEW.PublishedAtUtc IS NULL OR NOT EXISTS(SELECT 1 FROM ModelPublications WHERE ModelVersionId=NEW.Id) THEN RAISE(ABORT,'Publication required') END;
                SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM CalculationNodes WHERE ModelVersionId=NEW.Id AND IsActive=1 AND Role='Principal') THEN RAISE(ABORT,'Principal required') END;
                SELECT CASE WHEN EXISTS(SELECT 1 FROM CalculationNodes n WHERE n.ModelVersionId=NEW.Id AND n.IsActive=1 AND NOT EXISTS(SELECT 1 FROM CostPools p WHERE p.ModelVersionId=n.ModelVersionId AND p.NodeId=n.Id)) THEN RAISE(ABORT,'Pool required') END;
            END;
            CREATE TRIGGER PublishedModelUpdate BEFORE UPDATE ON ModelVersions WHEN OLD.Status='Published' BEGIN SELECT RAISE(ABORT,'Published model is immutable'); END;
            CREATE TRIGGER PublishedModelDelete BEFORE DELETE ON ModelVersions WHEN OLD.Status='Published' BEGIN SELECT RAISE(ABORT,'Published model is immutable'); END;
            CREATE TRIGGER PublicationUpdate BEFORE UPDATE ON ModelPublications BEGIN SELECT RAISE(ABORT,'Publication is immutable'); END;
            CREATE TRIGGER PublicationDelete BEFORE DELETE ON ModelPublications BEGIN SELECT RAISE(ABORT,'Publication is immutable'); END;
            CREATE TRIGGER ModelIdentityUpdate BEFORE UPDATE ON ModelVersions WHEN NEW.Id<>OLD.Id OR NEW.CompanyId<>OLD.CompanyId OR NEW.FamilyId<>OLD.FamilyId OR NEW.VersionNumber<>OLD.VersionNumber OR NEW.ParentVersionId IS NOT OLD.ParentVersionId BEGIN SELECT RAISE(ABORT,'Model identity is immutable'); END;
            """);
        foreach (var table in new[]
        {
            "CalculationNodes",
            "CostPools"
        }

        )
        {
            m.Sql($"CREATE TRIGGER {table}PublishedInsert BEFORE INSERT ON {table} WHEN EXISTS(SELECT 1 FROM ModelVersions WHERE Id=NEW.ModelVersionId AND Status='Published') BEGIN SELECT RAISE(ABORT,'Published model is immutable'); END;");
            m.Sql($"CREATE TRIGGER {table}PublishedUpdate BEFORE UPDATE ON {table} WHEN EXISTS(SELECT 1 FROM ModelVersions WHERE Id IN(OLD.ModelVersionId,NEW.ModelVersionId) AND Status='Published') BEGIN SELECT RAISE(ABORT,'Published model is immutable'); END;");
            m.Sql($"CREATE TRIGGER {table}PublishedDelete BEFORE DELETE ON {table} WHEN EXISTS(SELECT 1 FROM ModelVersions WHERE Id=OLD.ModelVersionId AND Status='Published') BEGIN SELECT RAISE(ABORT,'Published model is immutable'); END;");
        }
    }

    protected override void Down(MigrationBuilder m) => throw new NotSupportedException("Downgrading a company file is not supported. Keep the original v1 file.");
}

[DbContext(typeof(ModelDbContext))]
internal sealed class ModelDbContextSnapshot : ModelSnapshot
{
    public ModelDbContextSnapshot()
    {
    }

    protected override void BuildModel(ModelBuilder model) => V2Model.Configure(model);
}

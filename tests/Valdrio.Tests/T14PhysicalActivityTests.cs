using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;

namespace Valdrio.Tests;

[TestClass]
public sealed class T14PhysicalActivityTests
{
    private string directory = null!;
    private sealed class Clock : IClock
    {
        public UtcInstant GetUtcNow() => UtcInstant.ParseIso("2026-10-15T08:15:00Z");
    }
    private static EntityId Id() => new(Guid.NewGuid());
    private SqliteCompanyFileService Service(Action<CompanyWriteCheckpoint>? hook = null) =>
        new(new JsonCurrencyCatalog(Path.Combine(AppContext.BaseDirectory, "reference", "currency-catalog-example.json")), new Clock(), hook);
    [TestInitialize] public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "valdrio-t14-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
    }
    [TestCleanup] public void TearDown() => Directory.Delete(directory, true);

    private (ICompanyFileSession File, StudyDefinition Study, CalculationNodeDefinition Node, CostPoolDefinition Pool) Fixture()
    {
        var file = Service().Create(new(Path.Combine(directory, "physical.valdrio"), "T14 test", new("EUR"), PrecisionPolicy.Pdec1Proposed));
        var company = file.Read();
        var unit = new UnitDefinition(new UnitId(Id()), "labor_hour", "time", "h", 12, false);
        file.AddUnits([unit], company.Revision, "test");
        var center = file.Models.CreateCenter(new(company.Id, "MONT", "Montaje", "test"));
        var draft = file.Models.CreateDraft(new(company.Id, "T14 model", "test"));
        var node = new CalculationNodeDefinition(Id(), draft.Structure.Id, center.Id, "MONT",
            NodeRole.Principal, ["production"], true);
        var pool = new CostPoolDefinition(Id(), draft.Structure.Id, node.Id, unit.Id, "Montaje horas",
            CostLayer.Unclassified, ModelPolicyReference.PendingCapacity);
        var m = draft.Structure;
        var structure = new ModelStructure(m.Id, m.CompanyId, m.FamilyId, m.VersionNumber, m.ParentVersionId,
            m.Description, m.PrecisionPolicyVersion, m.MethodPolicy, [node], [pool]);
        var saved = file.Models.SaveDraft(new(structure, draft.Revision, "test"));
        file.Models.Publish(new(company.Id, structure.Id, saved.Revision, "test"));
        var study = file.Studies.Create(new(company.Id, Id(), "Octubre", BusinessDate.ParseIso("2026-10-01"),
            BusinessDate.ParseIso("2026-10-31"), StudyPurpose.Management, StudyScenario.Actual,
            structure.Id, new("EUR"), "test"));
        return (file, study, node, pool);
    }

    private static PhysicalEvidence Source(PhysicalEvidenceQuality quality = PhysicalEvidenceQuality.Recorded) =>
        new(quality, "Partes de Montaje", "Registro mensual identificado");

    private static SavePhysicalObservation Save(StudyDefinition study, CalculationNodeDefinition node,
        CostPoolDefinition pool, long revision, decimal? normal, decimal? real,
        PhysicalObservationState state = PhysicalObservationState.Confirmed, string? reason = null,
        string? zero = null, string? exception = null) =>
        new(study.CompanyId, study.Id, study.Revision, node.Id, pool.Id, revision, state,
            null, null, normal, real, normal is null ? null : Source(PhysicalEvidenceQuality.Calculated),
            real is null ? null : Source(), zero, exception, reason, "test");

    [TestMethod]
    public void T14_ImmutableHistoryCorrectionConflictAndAnnulmentPreserveStudyRevision()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            var first = file.PhysicalActivity.Save(Save(study, node, pool, 0, 700, 610));
            Assert.AreEqual(1L, first.Version);
            Assert.AreEqual(PhysicalObservationState.Confirmed, first.State);
            Assert.AreEqual(610m / 700m * 100m, first.UtilizationPercent);
            Assert.AreEqual(study.Revision, file.Studies.ReadStudies().Single().Revision);

            Assert.AreEqual("ACT_CORRECTION_REASON", Assert.ThrowsExactly<DomainRuleException>(
                () => file.PhysicalActivity.Save(Save(study, node, pool, 1, 700, 625))).Diagnostic.Code);
            var second = file.PhysicalActivity.Save(Save(study, node, pool, 1, 700, 625, reason: "Parte omitido"));
            Assert.AreEqual(2L, second.Version);
            Assert.AreEqual("Parte omitido", second.CorrectionReason);
            Assert.AreEqual("ACT_CONFLICT", Assert.ThrowsExactly<DomainRuleException>(
                () => file.PhysicalActivity.Save(Save(study, node, pool, 1, 700, 630, reason: "Ventana antigua"))).Diagnostic.Code);

            var annulled = file.PhysicalActivity.Annul(new(study.CompanyId, study.Id, study.Revision, node.Id, pool.Id, 2,
                "El parte pertenece a otro nodo", "test"));
            Assert.AreEqual(3L, annulled.Version);
            Assert.AreEqual(PhysicalObservationState.Annulled, annulled.State);
            Assert.IsNull(annulled.UtilizationPercent);
            Assert.AreEqual(3L, file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single().Version);
            Assert.AreEqual(3, file.PhysicalActivity.ReadHistory(study.CompanyId, study.Id, pool.Id).Count);
            Assert.AreEqual(610m, file.PhysicalActivity.ReadHistory(study.CompanyId, study.Id, pool.Id)[0].RealQuantity);
            Assert.AreEqual(study.Revision, file.Studies.ReadStudies().Single().Revision);
        }
        using var reopened = Service().Open(Path.Combine(directory, "physical.valdrio"));
        Assert.AreEqual(3, reopened.PhysicalActivity.ReadHistory(study.CompanyId, study.Id, pool.Id).Count);
    }

    [TestMethod]
    public void T14_ZeroIsPresent_RealAboveNormalIsNotClamped()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            var draft = file.PhysicalActivity.Save(Save(study, node, pool, 0, 0, null, PhysicalObservationState.Draft));
            Assert.IsNull(draft.RealQuantity);
            Assert.AreEqual("ACT_ZERO_NORMAL_REASON", Assert.ThrowsExactly<DomainRuleException>(
                () => file.PhysicalActivity.Save(Save(study, node, pool, 1, 0, 20,
                    PhysicalObservationState.Confirmed, exception: "Intervencion imprevista"))).Diagnostic.Code);
            var special = file.PhysicalActivity.Save(Save(study, node, pool, 1, 0, 20,
                zero: "Cierre previsto", exception: "Intervencion imprevista"));
            Assert.IsNull(special.UtilizationPercent);
            Assert.IsTrue(special.ExceptionalActivity);
            var over = file.PhysicalActivity.Save(Save(study, node, pool, 2, 700, 770,
                reason: "Cambio de calendario", exception: "Horas extraordinarias"));
            Assert.AreEqual(110m, over.UtilizationPercent);
            Assert.AreEqual(770m, over.RealQuantity);
            Assert.IsTrue(over.ExceedsNormal);
        }
    }

    [TestMethod]
    public void T14_ClosedStudyRejectsChanges_AndMigrationUsesImmutableTables()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            var first = file.PhysicalActivity.Save(Save(study, node, pool, 0, 700, 610));
            file.Studies.ChangeState(new(study.CompanyId, study.Id, study.Revision, StudyState.Closed, "test"));
            Assert.AreEqual("ACT_STUDY_CLOSED", Assert.ThrowsExactly<DomainRuleException>(
                () => file.PhysicalActivity.Save(Save(study, node, pool, 1, 700, 625, reason: "Late"))).Diagnostic.Code);
            Assert.AreEqual("ACT_STUDY_CLOSED", Assert.ThrowsExactly<DomainRuleException>(
                () => file.PhysicalActivity.Annul(new(study.CompanyId, study.Id, study.Revision, node.Id, pool.Id, 1, "Wrong", "test"))).Diagnostic.Code);
            using var connection = new SqliteConnection("Data Source=" + file.Path + ";Pooling=False");
            connection.Open();
            using var sql = connection.CreateCommand();
            sql.CommandText = "UPDATE PhysicalObservations SET State='Draft'";
            Assert.ThrowsExactly<SqliteException>(() => sql.ExecuteNonQuery());
            Assert.AreEqual(SqliteCompanyFileService.SchemaVersion, file.Read().SchemaVersion);
            Assert.AreEqual(first.Id, file.PhysicalActivity.ReadHistory(study.CompanyId, study.Id, pool.Id).Single().Id);
        }
    }

    [TestMethod]
    public void T14_CalculatorSeparatesIdealAvailableNormalAndRequiresComparableHistory()
    {
        var calendar = PhysicalNormalCalculator.Calendar(new(22, 1m, 8m, 4m, 20m, 1m, "Turno homogeneo"));
        Assert.AreEqual(704m, calendar.Theoretical);
        Assert.AreEqual(684m, calendar.Practical);
        Assert.AreEqual(684m, calendar.Normal);
        var october = new PhysicalHistoryPeriod(BusinessDate.ParseIso("2026-08-01"), BusinessDate.ParseIso("2026-08-31"), 600m, true, null);
        var september = new PhysicalHistoryPeriod(BusinessDate.ParseIso("2026-09-01"), BusinessDate.ParseIso("2026-09-30"), 620m, true, null);
        Assert.AreEqual("ACT_HISTORY_NOT_COMPARABLE", Assert.ThrowsExactly<DomainRuleException>(
            () => PhysicalNormalCalculator.History([october, september])).Diagnostic.Code);
        var july = october with { Start = BusinessDate.ParseIso("2026-07-01"), End = BusinessDate.ParseIso("2026-07-31"), RealQuantity = 640m };
        Assert.AreEqual(620m, PhysicalNormalCalculator.History([october, july]));
    }
    [TestMethod]
    public void T14_AfterAnnulmentNewObservationDoesNotRevalidateOldHours()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            file.PhysicalActivity.Save(Save(study, node, pool, 0, 700m, 610m));
            file.PhysicalActivity.Annul(new(study.CompanyId, study.Id, study.Revision, node.Id, pool.Id, 1L,
                "Wrong production log", "test"));
            var replacement = file.PhysicalActivity.Save(Save(study, node, pool, 2L, 680m, 600m));
            Assert.AreEqual(3L, replacement.Version);
            Assert.AreEqual(600m, file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single().RealQuantity);
            var old = file.PhysicalActivity.ReadHistory(study.CompanyId, study.Id, pool.Id);
            Assert.AreEqual(PhysicalObservationState.Annulled, old[1].State);
            Assert.AreEqual(610m, old[0].RealQuantity);
        }
    }

    [TestMethod]
    public void T14_CancelledTransactionDoesNotWriteObservationOrAudit()
    {
        var (file, study, node, pool) = Fixture();
        var path = file.Path;
        file.Dispose();
        using (var faulty = Service(p =>
        {
            if (p == CompanyWriteCheckpoint.BeforeCommit)
                throw new InvalidOperationException("Injected T14 failure");
        }).Open(path))
        {
            Assert.ThrowsExactly<InvalidOperationException>(
                () => faulty.PhysicalActivity.Save(Save(study, node, pool, 0, 700m, 610m)));
            Assert.AreEqual(0, faulty.PhysicalActivity.ReadHistory(study.CompanyId, study.Id, pool.Id).Count);
        }
        using var reopened = Service().Open(path);
        Assert.AreEqual(0, reopened.PhysicalActivity.ReadHistory(study.CompanyId, study.Id, pool.Id).Count);
    }

    [TestMethod]
    public void T14_DraftCanKeepPartialSourceWithoutClaimingConfirmation()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            var draft = Save(study, node, pool, 0L, 684m, null, PhysicalObservationState.Draft) with
            {
                NormalEvidence = new(PhysicalEvidenceQuality.Declared, "", "")
            };
            var saved = file.PhysicalActivity.Save(draft);
            Assert.AreEqual(PhysicalObservationState.Draft, saved.State);
            Assert.AreEqual("ACT_INCOMPLETE", Assert.ThrowsExactly<DomainRuleException>(
                () => file.PhysicalActivity.Save(draft with
                {
                    ExpectedPhysicalVersion = 1L,
                    State = PhysicalObservationState.Confirmed
                })).Diagnostic.Code);
        }
    }


    [TestMethod]
    public void T14_ChangingStudyPeriodRejectsStaleEditorWithoutTransferringOldHours()
    {
        var (file, study, node, pool) = Fixture();
        using (file)
        {
            var initial = file.PhysicalActivity.Save(Save(study, node, pool, 0L, 700m, 610m));
            var November = BusinessDate.ParseIso("2026-11-01");
            var monthEnd = BusinessDate.ParseIso("2026-11-30");
            var changed = file.Studies.UpdateContext(new(study.CompanyId, study.Id, study.Revision,
                study.Name, November, monthEnd, study.Purpose, study.ScenarioKind,
                study.SelectedModelVersionId, study.Currency.FunctionalCurrencyCode, "test"));
            Assert.AreEqual("STUDY_REVISION_CHANGED", Assert.ThrowsExactly<DomainRuleException>(
                () => file.PhysicalActivity.Save(Save(study, node, pool, 0L, 700m, 610m))).Diagnostic.Code);
            Assert.AreEqual(0, file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Count);
            var history = file.PhysicalActivity.ReadHistory(study.CompanyId, study.Id, pool.Id);
            Assert.AreEqual(initial.Id, history.Single().Id);
            Assert.AreEqual(study.PeriodStart, history.Single().PeriodStart);
            Assert.AreEqual(changed.Revision, file.Studies.ReadStudies().Single(s => s.Id == study.Id).Revision);
        }
    }
}

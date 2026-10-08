using System.IO.Compression;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Infrastructure;
using Valdrio.Infrastructure.CompanyFiles;
namespace Valdrio.Tests;
public sealed partial class T06ImportConfirmationTests
{
    [TestMethod]
    public async Task T08XlsxExplicitDescriptionPersistsWithoutSourceAndKeepsLeadingZeros()
    {
        EntityId studyId;
        var sourcePath = FileOf("names.xlsx");
        using (var file = Baseline())
        {
            var study = Study(file); studyId = study.Id;
            static Cell Cell(string reference, string value) => new() { CellReference = reference, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(value)) };
            using (var doc = SpreadsheetDocument.Create(sourcePath, SpreadsheetDocumentType.Workbook))
            {
                var book = doc.AddWorkbookPart(); book.Workbook = new Workbook(new Sheets()); var sheet = book.AddNewPart<WorksheetPart>();
                sheet.Worksheet = new Worksheet(new SheetData(new Row(Cell("A1","Cuenta"),Cell("B1","Nombre"),Cell("C1","Importe")) {RowIndex=1},
                    new Row(Cell("A2","00123"),Cell("B2","Papelería 日本"),new Cell { CellReference="C2",DataType=CellValues.Number,CellValue=new CellValue("160.25") }) {RowIndex=2}));
                book.Workbook.GetFirstChild<Sheets>()!.Append(new Sheet { Name="Datos",SheetId=1,Id=book.GetIdOfPart(sheet) });
            }
            using var session = await new ImportSourceSessionFactory().OpenAsync(sourcePath);
            var source = await session.ReadAsync(new(ImportSourceKind.Xlsx,"Datos",1,2,null));
            var profile = new SourceParseProfileVersion(SourceParseProfileVersion.CurrentSchema,Id(),1,"XLSX description",ImportSourceKind.Xlsx,"Datos",1,2,
                new(1,description:2,amount:3),null,new(",",null,[],ImportNegativeSyntax.LeadingMinus),"yyyy-MM-dd",ImportAmountMode.PeriodAmount,
                ImportSignConvention.SignedAmount,study.PeriodStart,study.PeriodEnd,new("EUR"),null,ImportDuplicatePolicy.Reject,ImportZeroPolicy.Keep,
                ImportAccountSelection.All,new(ImportProfileApprovalState.ApprovedBusiness,"00123=160.25"),true);
            file.Imports.ConfirmImport(Command(Prepare(file,study,source,profile)));
        }
        File.Delete(sourcePath);
        using var reopened=Service().Open(FileOf("current.valdrio"));
        var origin=reopened.Resources.ReadResources(reopened.Read().Id,studyId).Origins.Single();
        Assert.AreEqual("00123",origin.Code); Assert.AreEqual("Papelería 日本",origin.Descriptions.Single());
        Assert.AreEqual(2,origin.Rows.Single().DescriptionColumn); Assert.AreEqual(160.25m,origin.Rows.Single().Conversion.ConvertedOriginalMoney.Amount);
    }
    [TestMethod]
    public async Task T08AtomicRollbackAndStaleKeyEditNeverWritePartialHistory()
    {
        SaveAllocationKey command;
        using(var file=Baseline()) command=KeyCommand(await ImportedResources(file),publish:false);
        var path=FileOf("current.valdrio"); var before=Hash(path);
        foreach(var point in Enum.GetValues<CompanyWriteCheckpoint>())
        {
            using(var file=Service(p => { if(p==point) throw new IOException("Injected local failure"); }).Open(path))
                Assert.ThrowsExactly<CompanyFileException>(() => file.Allocations.SaveAllocationKey(command));
            Assert.AreEqual(before,Hash(path));
        }
        using var current=Service().Open(path);
        var receipt=current.Allocations.SaveAllocationKey(command); var saved=Hash(path);
        Assert.AreEqual("KEY_REVISION_CHANGED",Assert.ThrowsExactly<DomainRuleException>(() => current.Allocations.SaveAllocationKey(command with {RequestId=Id()})).Diagnostic.Code);
        Assert.AreEqual(saved,Hash(path));
        using var read=Service().Open(path,CompanyFileOpenMode.ReadOnly);
        Assert.AreEqual("FILE_READ_ONLY",Assert.ThrowsExactly<CompanyFileException>(() => read.Allocations.SaveAllocationKey(command)).Diagnostic.Code);
        var lines=new List<AllocationKeyLine>(command.Lines); var immutable=command with {Lines=lines};lines.Clear();Assert.AreEqual(1,immutable.Lines.Count);
        Assert.AreEqual(1,CanonicalSnapshotJson.Deserialize<AllocationKeyReceipt>(CanonicalSnapshotJson.Serialize(receipt)).Request.Lines.Count);
    }
    [TestMethod]
    public async Task T08RemovedPartOverridesStayVisibleUntilExplicitlyCleared()
    {
        using var file=Baseline(); var w=RouteForKey(file,await ImportedResources(file));
        var part=w.Resources.Single().Parts.Single();var key=file.Allocations.SaveAllocationKey(KeyCommand(w)).After;
        var saved=file.Allocations.ChangeAllocations(AllocationCommand(w,new([],[],[new(part.Id,key.Id)],[],[])));
        w=file.Resources.ReadResources(w.Study.CompanyId,w.Study.Id);
        file.Resources.ChangeResources(Mutation(w,ResourceMutationKind.EditResource,w.Resources.Single().Id,[new ResourcePartDraft(Id(),Eur(1000),ResourceRoutingKind.Excluded,exclusionReason:"Excluded explicitly")]));
        var current=file.Allocations.ReadAllocations(w.Study.CompanyId,w.Study.Id);
        Assert.IsTrue(current.Coverage.Any(c => c.SourceId==part.Id && c.Diagnostic?.Code=="KEY_PART_CHANGED"));
        file.Allocations.ChangeAllocations(AllocationCommand(current.Resources,AllocationConfiguration.Empty));
        Assert.IsFalse(file.Allocations.ReadAllocations(w.Study.CompanyId,w.Study.Id).Coverage.Any(c => c.SourceId==part.Id));
    }
    [TestMethod]
    public void T08GenuineV8UpgradePreservesT07HistoryAndSnapshotsByteForByte()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","t07-v8.json")));
        using var stream=new GZipStream(new MemoryStream(Convert.FromBase64String(doc.RootElement.GetProperty("GzipBase64").GetString()!)),CompressionMode.Decompress);
        var source=FileOf("v8.valdrio");using(var output=File.Create(source)) stream.CopyTo(output);
        var hash=Hash(source); Assert.AreEqual(doc.RootElement.GetProperty("Sha256").GetString(),hash);
        var history=Query(source,"SELECT group_concat(CanonicalJson) FROM ResourceChanges ORDER BY Id");
        var photos=Query(source,"SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id");
        using var file=Service().Upgrade(source,FileOf("v9.valdrio"),"test");
        Assert.AreEqual(9,file.Read().SchemaVersion);Assert.AreEqual(hash,Hash(source));
        Assert.AreEqual(history,Query(file.Path,"SELECT group_concat(CanonicalJson) FROM ResourceChanges ORDER BY Id"));
        Assert.AreEqual(photos,Query(file.Path,"SELECT group_concat(CanonicalJson) FROM StudySnapshots ORDER BY Id"));
        Assert.AreEqual("0",Query(file.Path,"SELECT COUNT(*) FROM AllocationKeyEvents"));
    }
}

using System.Globalization;
using Valdrio.Application;
using Valdrio.Desktop;
using Valdrio.Domain;

namespace Valdrio.Tests;

public sealed partial class T14PhysicalActivityTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AsyncConfirmation_PreventsDuplicateAndKeepsEditsOnFailure(bool fail)
    {
        var (file, study, _, pool) = Fixture();
        using (file)
        using (var gate = new GatedPhysicalWorkspace(file.PhysicalActivity) { Fail = fail })
        {
            var host = new PhysicalActivityEditor(study, file.Studies, gate, file.Models, file.Runs,
                file.Read(), true, CultureInfo.GetCultureInfo("es"), CultureInfo.GetCultureInfo("es-ES"));
            var active = 0;
            host.BeginActivity = () => { active++; return new CallbackScope(() => active--); };
            var assistant = new PhysicalCapacityAssistantEditor(host) { NormalValue = "100", RealValue = "80" };
            var saving = assistant.SaveAsync(true);
            try
            {
                await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.IsFalse(saving.IsCompleted, "Persistence must yield to the caller.");
                Assert.IsTrue(host.IsBusy);
                Assert.IsFalse(assistant.CanConfirm);
                Assert.IsFalse(assistant.CanSave);
                Assert.AreEqual(1, active);
                Assert.IsFalse(await assistant.SaveAsync(true), "A second invocation must not write.");
                Assert.AreEqual(1, gate.SaveCount);
            }
            finally { gate.Release.Set(); }
            Assert.AreEqual(!fail, await saving);
            Assert.IsFalse(host.IsBusy);
            Assert.AreEqual(0, active);
            Assert.AreEqual(fail, host.SelectedRow!.HasChanges);
            Assert.AreEqual(fail ? 0 : 1, file.PhysicalActivity.ReadHistory(study.CompanyId, study.Id, pool.Id).Count);
            if (fail)
            {
                Assert.AreEqual("100", assistant.NormalValue);
                Assert.AreEqual("80", assistant.RealValue);
                Assert.IsTrue(assistant.CanConfirm);
                gate.Fail = false;
                Assert.IsTrue(await assistant.SaveAsync(true), "Retry after failure must succeed.");
            }
            var persisted = file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Single();
            Assert.AreEqual(100m, persisted.NormalQuantity);
            Assert.AreEqual(80m, persisted.RealQuantity);
            Assert.AreEqual(PhysicalObservationState.Confirmed, persisted.State);
            assistant.Detach();
        }
    }

    [TestMethod]
    public async Task AsyncConfirmation_ValidationDoesNotWriteOrLoseEdits()
    {
        var (file, study, _, _) = Fixture();
        using (file)
        {
            var host = Editor(file, study);
            var assistant = new PhysicalCapacityAssistantEditor(host) { NormalValue = "-1", RealValue = "20" };
            Assert.IsFalse(await assistant.SaveAsync(true));
            Assert.IsFalse(host.IsBusy);
            Assert.IsTrue(host.HasPendingEdits);
            Assert.AreEqual(0, file.PhysicalActivity.ReadCurrent(study.CompanyId, study.Id).Count);
        }
    }

    private sealed class CallbackScope(Action release) : IDisposable
    {
        public void Dispose() => release();
    }
    private sealed class GatedPhysicalWorkspace(IPhysicalActivityWorkspace inner) : IPhysicalActivityWorkspace, IDisposable
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new(false);
        public int SaveCount { get; private set; }
        public bool Fail { get; set; }
        public PhysicalObservation Save(SavePhysicalObservation command, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Test did not release persistence.");
            if (Fail) throw new IOException("Simulated write failure.");
            return inner.Save(command, cancellationToken);
        }
        public IReadOnlyList<PhysicalObservation> ReadCurrent(EntityId c, EntityId s, CancellationToken t = default) => inner.ReadCurrent(c, s, t);
        public IReadOnlyList<PhysicalObservation> ReadReferences(EntityId c, EntityId s, CancellationToken t = default) => inner.ReadReferences(c, s, t);
        public IReadOnlyList<PhysicalObservation> ReadHistory(EntityId c, EntityId s, EntityId p, CancellationToken t = default) => inner.ReadHistory(c, s, p, t);
        public IReadOnlyList<PhysicalObservation> ApplyImport(ApplyPhysicalImport command, CancellationToken t = default) => inner.ApplyImport(command, t);
        public PhysicalObservation Annul(AnnulPhysicalObservation command, CancellationToken t = default) => inner.Annul(command, t);
        public void Dispose() => Release.Dispose();
    }
}

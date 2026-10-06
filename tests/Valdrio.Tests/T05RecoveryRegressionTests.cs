using Valdrio.Application;
using Valdrio.Localization;

namespace Valdrio.Tests;

[TestClass]
public sealed class T05RecoveryRegressionTests
{
    [TestMethod]
    public async Task B01_ConfirmedValueIsAppliedBeforeRefreshFailure()
    {
        var visible = "revision-5";
        var refreshObserved = "";
        var outcome = await ConfirmedCommitRefresh.ApplyThenRefreshAsync(
            "revision-6",
            value => visible = value,
            () =>
            {
                refreshObserved = visible;
                throw new IOException("injected post-commit read failure");
            });

        Assert.AreEqual("revision-6", visible);
        Assert.AreEqual("revision-6", refreshObserved);
        Assert.IsFalse(outcome.RefreshSucceeded);
        Assert.IsNotNull(outcome.RefreshFailure);
        Assert.AreEqual(typeof(IOException), outcome.RefreshFailure.GetType());
    }

    [TestMethod]
    public async Task B01_SuccessfulRefreshKeepsConfirmedValue()
    {
        var visible = "revision-5";
        var refreshed = false;
        var outcome = await ConfirmedCommitRefresh.ApplyThenRefreshAsync(
            "revision-6",
            value => visible = value,
            () =>
            {
                refreshed = true;
                return Task.CompletedTask;
            });

        Assert.AreEqual("revision-6", visible);
        Assert.IsTrue(refreshed);
        Assert.IsTrue(outcome.RefreshSucceeded);
        Assert.IsNull(outcome.RefreshFailure);
    }

    [TestMethod]
    public void B02_IoFailurePersistingPreferencesDoesNotInterruptLaterSubscribers()
    {
        AssertPersistenceFailureDoesNotInterrupt(
            new IOException("injected write failure"),
            "PREFERENCES_SAVE_IO_FAILED");
    }

    [TestMethod]
    public void B02_AccessFailurePersistingPreferencesDoesNotInterruptLaterSubscribers()
    {
        AssertPersistenceFailureDoesNotInterrupt(
            new UnauthorizedAccessException("injected access failure"),
            "PREFERENCES_SAVE_ACCESS_DENIED");
    }

    [TestMethod]
    public void B02_JsonStoreCleansUniqueTemporaryFileAfterFailedReplacement()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "valdrio-preference-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var destinationDirectory = Path.Combine(root, "presentation.json");
        Directory.CreateDirectory(destinationDirectory);

        try
        {
            var store = new JsonUserPresentationPreferencesStore(destinationDirectory);
            Exception? failure = null;
            try
            {
                store.Save(UserPresentationPreferences.Default);
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            Assert.IsNotNull(failure);
            Assert.IsTrue(
                failure is IOException or UnauthorizedAccessException,
                $"Expected an IO/access failure but caught {failure.GetType().Name}.");

            Assert.AreEqual(
                0,
                Directory.GetFiles(root, "presentation.json.*.tmp").Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertPersistenceFailureDoesNotInterrupt(
        Exception exception,
        string expectedCode)
    {
        var service = new PresentationPreferencesService(
            new LanguageCatalog(),
            UserPresentationPreferences.Default);
        var persistence = new PresentationPreferencesPersistence(
            new ThrowingPreferencesStore(exception));
        PresentationPreferencesPersistenceFailure? failure = null;
        var laterNotifications = 0;

        service.Changed += (_, args) =>
        {
            Assert.IsFalse(persistence.TrySave(args.Current, out failure));
        };
        service.Changed += (_, _) => laterNotifications++;

        service.Update(service.Current with { UiLanguageTag = "es" });

        Assert.AreEqual("es", service.Current.UiLanguageTag);
        Assert.AreEqual(1, laterNotifications);
        Assert.IsNotNull(failure);
        Assert.AreEqual(expectedCode, failure.Code);
        Assert.AreEqual("Preferences.SaveFailed", failure.MessageKey);
    }

    private sealed class ThrowingPreferencesStore(Exception exception)
        : IUserPresentationPreferencesStore
    {
        public bool TryLoad(out UserPresentationPreferences? preferences)
        {
            preferences = null;
            return false;
        }

        public bool TryLoad(
            out UserPresentationPreferences? preferences,
            out string? failureCode)
        {
            preferences = null;
            failureCode = "PREFERENCES_NOT_FOUND";
            return false;
        }

        public void Save(UserPresentationPreferences preferences) =>
            throw exception;
    }
}

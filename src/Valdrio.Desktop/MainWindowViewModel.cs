using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Application;
using Valdrio.Domain;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private const string ExpectedKind = "valdrio-development-smoke-v1";

    private readonly UiTextCatalog texts;
    private readonly PresentationPreferencesService preferences;
    private readonly PresentationPreferencesPersistence? preferencesPersistence;
    private PresentationPreferencesPersistenceFailure? preferencesSaveFailure;
    private readonly PresentationFormatter formatter;
    private readonly T02RoundingExample roundingExample;
    private readonly TemporalContractExampleService temporalContractExampleService;
    private readonly FxConversionExampleService fxConversionExampleService;
    private DecimalEditSession? editSession;
    private SampleState sampleState = SampleState.None;
    private EditState editState = EditState.NotStarted;
    private decimal? committedValue;
    private bool pseudoLocalizationEnabled;
    private bool forceRtlForTest;
    private string sampleName = "—";
    private string samplePurpose = "—";
    private string sourcePath = "—";
    private string pendingEditText = "1.234,50";
    private string fxQuoteText = "";
    private DecimalEditSession? fxEditSession;
    private DomainDiagnostic? fxDiagnostic;
    private string fxRateDateText = "2026-10-04";
    private string fxRateSourceText = "manual-demo";
    private string fxStatusKey = "FxEditorReady";
    private FxRateApplication fxExample = null!;

    public MainWindowViewModel(
        UiTextCatalog texts,
        LanguageCatalog languages,
        PresentationPreferencesService preferences,
        PresentationFormatter formatter,
        T02RoundingExampleService roundingExampleService,
        CurrencyMetadataExampleService currencyMetadataExampleService,
        TemporalContractExampleService temporalContractExampleService,
        FxConversionExampleService fxConversionExampleService,
        PresentationPreferencesPersistence? preferencesPersistence = null)
    {
        this.texts = texts;
        this.preferences = preferences;
        this.preferencesPersistence = preferencesPersistence;
        this.formatter = formatter;
        roundingExample = roundingExampleService.BuildC07();
        RoundingRows = roundingExample.Rows;
        CurrencyMetadataRows = currencyMetadataExampleService.Build();
        this.temporalContractExampleService =
            temporalContractExampleService;
        this.fxConversionExampleService =
            fxConversionExampleService;
        fxExample = fxConversionExampleService.Build(
            0.9m, BusinessDate.ParseIso(fxRateDateText), fxRateSourceText);
        fxQuoteText = FormatQuote(fxExample.Quote);

        UiLanguageOptions = languages.Published
            .Select(language => language.Tag)
            .ToArray();

        FormatCultureOptions = ["es-ES", "en-US", "en-GB", "de-DE"];
        TimeZoneOptions = ["Europe/Madrid", "UTC", "America/Los_Angeles"];

        StartEditCommand = new RelayCommand(StartEdit);
        CommitEditCommand = new RelayCommand(CommitEdit);
        ApplyFxRateCommand = new RelayCommand(ApplyFxRate);
        RetrySavePreferencesCommand = new RelayCommand(PersistPreferences, () => preferencesSaveFailure is not null);

        preferences.Changed += (_, _) =>
        {
            PersistPreferences();
            RefreshPresentation();
            PresentationChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? PresentationChanged;
    public CompanyFileViewModel? Company { get; init; }
    public ModelWorkspaceViewModel? Models { get; init; }
    public StudyWorkspaceViewModel? Studies { get; init; }
    public ResourceWorkspaceViewModel? Resources { get; init; }
    public AllocationWorkspaceViewModel? Allocations { get; init; }

    public IReadOnlyList<string> UiLanguageOptions { get; }
    public IReadOnlyList<string> FormatCultureOptions { get; }
    public IReadOnlyList<string> TimeZoneOptions { get; }
    public IReadOnlyList<T02RoundingExampleRow> RoundingRows { get; }
    public IReadOnlyList<CurrencyMetadataExampleRow> CurrencyMetadataRows { get; }

    public IRelayCommand StartEditCommand { get; }
    public IRelayCommand CommitEditCommand { get; }
    public IRelayCommand ApplyFxRateCommand { get; }
    public IRelayCommand RetrySavePreferencesCommand { get; }
    public bool ShowPreferencesSaveWarning => preferencesSaveFailure is not null;
    public string PreferencesSaveWarning => preferencesSaveFailure is null ? "" : Text(preferencesSaveFailure.MessageKey);
    public string RetrySavePreferencesLabel => Text("Preferences.RetrySave");

    private void PersistPreferences()
    {
        if (preferencesPersistence is null) return;
        if (!preferencesPersistence.TrySave(preferences.Current, out preferencesSaveFailure))
            Trace.TraceWarning("{0}: {1}", preferencesSaveFailure!.Code, PreferencesSaveWarning);
        OnPropertyChanged(nameof(ShowPreferencesSaveWarning));
        OnPropertyChanged(nameof(PreferencesSaveWarning));
        RetrySavePreferencesCommand.NotifyCanExecuteChanged();
    }

    public UserPresentationPreferences CurrentPreferences => preferences.Current;
    public bool ForceRtlForTest => forceRtlForTest;

    public string SelectedUiLanguageTag
    {
        get => preferences.Current.UiLanguageTag;
        set
        {
            if (string.Equals(value, preferences.Current.UiLanguageTag, StringComparison.Ordinal))
            {
                return;
            }

            preferences.Update(preferences.Current with { UiLanguageTag = value });
        }
    }

    public string SelectedFormatCultureTag
    {
        get => preferences.Current.FormatCultureTag;
        set
        {
            if (string.Equals(value, preferences.Current.FormatCultureTag, StringComparison.Ordinal))
            {
                return;
            }

            preferences.Update(preferences.Current with { FormatCultureTag = value });
        }
    }

    public string SelectedTimeZoneId
    {
        get => preferences.Current.DisplayTimeZoneId;
        set
        {
            if (string.Equals(value, preferences.Current.DisplayTimeZoneId, StringComparison.Ordinal))
            {
                return;
            }

            preferences.Update(preferences.Current with { DisplayTimeZoneId = value });
        }
    }

    public string PendingEditText
    {
        get => pendingEditText;
        set => SetProperty(ref pendingEditText, value);
    }

    public string FxQuoteText
    {
        get => fxQuoteText;
        set
        {
            if (SetProperty(ref fxQuoteText, value)) BeginFxEdit();
        }
    }

    public string FxRateDateText
    {
        get => fxRateDateText;
        set => SetProperty(ref fxRateDateText, value);
    }

    public string FxRateSourceText
    {
        get => fxRateSourceText;
        set => SetProperty(ref fxRateSourceText, value);
    }

    public string SampleName
    {
        get => sampleName;
        private set => SetProperty(ref sampleName, value);
    }

    public string SamplePurpose
    {
        get => samplePurpose;
        private set => SetProperty(ref samplePurpose, value);
    }

    public string SourcePath
    {
        get => sourcePath;
        private set => SetProperty(ref sourcePath, value);
    }

    public string AppTitle => Text("AppTitle");
    public string DevelopmentToolsTitle => Text("DevelopmentToolsTitle");
    public string WindowTitle => Text("WindowTitle");
    public string SpecificationVersion => Text("SpecificationVersion");
    public string HeaderSubtitle => Text("HeaderSubtitle");
    public string PreferencesTitle => Text("PreferencesTitle");
    public string LabelUiLanguage => Text("LabelUiLanguage");
    public string LabelFormatCulture => Text("LabelFormatCulture");
    public string LabelTimeZone => Text("LabelTimeZone");
    public string PreviewTitle => Text("PreviewTitle");
    public string LabelMoneyPreview => Text("LabelMoneyPreview");
    public string LabelDatePreview => Text("LabelDatePreview");
    public string LabelInstantPreview => Text("LabelInstantPreview");
    public string LabelCanonicalBusinessDate => Text("LabelCanonicalBusinessDate");
    public string LabelCanonicalUtcInstant => Text("LabelCanonicalUtcInstant");
    public string LabelResolvedLocalInstant => Text("LabelResolvedLocalInstant");
    public string EditTitle => Text("EditTitle");
    public string LabelCapturedCulture => Text("LabelCapturedCulture");
    public string LabelPendingValue => Text("LabelPendingValue");
    public string LabelEditStatus => Text("LabelEditStatus");
    public string ActionStartEdit => Text("ActionStartEdit");
    public string ActionCommitEdit => Text("ActionCommitEdit");
    public string LabelStatus => Text("LabelStatus");
    public string LabelSample => Text("LabelSample");
    public string LabelPurpose => Text("LabelPurpose");
    public string LabelSource => Text("LabelSource");
    public string FooterDevelopmentOnly => Text("FooterDevelopmentOnly");
    public string RoundingTitle => Text("RoundingTitle");
    public string LabelRoundingReference => Text("LabelRoundingReference");
    public string LabelRoundingSource => Text("LabelRoundingSource");
    public string LabelRoundingQuantum => Text("LabelRoundingQuantum");
    public string LabelRoundingId => Text("LabelRoundingId");
    public string LabelRoundingRaw => Text("LabelRoundingRaw");
    public string LabelRoundingAdjustment => Text("LabelRoundingAdjustment");
    public string LabelRoundingPosted => Text("LabelRoundingPosted");
    public string RoundingReferenceCase => roundingExample.ReferenceCase;
    public string RoundingSourceTotal => roundingExample.SourceTotal;
    public string RoundingQuantum => roundingExample.Quantum;
    public string RoundingPostedTotal => roundingExample.PostedTotal;
    public string RoundingExplanation =>
        FormatText("RoundingExplanation", roundingExample.PostedTotal);
    public string CurrencyMetadataTitle => Text("CurrencyMetadataTitle");
    public string LabelCurrencyCode => Text("LabelCurrencyCode");
    public string LabelNumericCode => Text("LabelNumericCode");
    public string LabelMinorUnitDigits => Text("LabelMinorUnitDigits");
    public string LabelCurrencyQuantum => Text("LabelCurrencyQuantum");
    public string LabelCurrencyProjection => Text("LabelCurrencyProjection");
    public string CurrencyMetadataExplanation =>
        Text("CurrencyMetadataExplanation");
    public string FxEditorTitle => Text("FxEditorTitle");
    public string LabelFxOriginal => Text("LabelFxOriginal");
    public string LabelFxOrientation => Text("LabelFxOrientation");
    public string LabelFxQuote => Text("LabelFxQuote");
    public string LabelFxRateDate => Text("LabelFxRateDate");
    public string LabelFxRateSource => Text("LabelFxRateSource");
    public string LabelFxFunctional => Text("LabelFxFunctional");
    public string LabelFxRounding => Text("LabelFxRounding");
    public string LabelFxRateSet => Text("LabelFxRateSet");
    public string ActionApplyFxRate => Text("ActionApplyFxRate");
    public string FxEditorExplanation => Text("FxEditorExplanation");
    public string FxStatus => fxDiagnostic is null
        ? Text(fxStatusKey)
        : fxDiagnostic.Code + " — " + new DomainDiagnosticFormatter(texts)
            .Format(fxDiagnostic, UiCulture, FormatCulture).Message;
    public string FxCapturedCulture => fxEditSession?.CapturedCulture.Name ?? FormatCulture.Name;
    public string FxOriginal => FormatMoney(fxExample.OriginalMoney);
    public string FxOrientation =>
        $"{fxExample.ConvertedOriginalMoney.Currency.Value}/{fxExample.OriginalMoney.Currency.Value}";
    public string FxFunctional => FormatMoney(fxExample.ConvertedOriginalMoney);
    public string FxRounding =>
        $"{fxExample.ConversionRoundingAdjustment.Amount.ToString("0.############################", FormatCulture)} {fxExample.ConvertedOriginalMoney.Currency.Value}";
    public string FxRateSetVersion => fxExample.RateSetVersionId ?? "IDENTITY";

    public string Status => Text(sampleState switch
    {
        SampleState.None => "StatusNoSample",
        SampleState.Opened => "StatusSampleOpened",
        SampleState.Failed => "StatusSampleOpenFailed",
        _ => throw new ArgumentOutOfRangeException()
    });

    public string PreviewMoney
    {
        get
        {
            var current = preferences.Current;
            return formatter.FormatCurrency(
                1234.50m,
                "USD",
                CultureInfo.GetCultureInfo(current.FormatCultureTag),
                current.NumberDisplayOptions);
        }
    }

    public string PreviewDate
    {
        get
        {
            var current = preferences.Current;
            return formatter.FormatDate(
                new DateOnly(2026, 2, 1),
                CultureInfo.GetCultureInfo(current.FormatCultureTag),
                current.DateDisplayPreset);
        }
    }

    public string PreviewInstant
    {
        get
        {
            var current = preferences.Current;
            return formatter.FormatInstant(
                new DateTimeOffset(2026, 10, 4, 0, 30, 0, TimeSpan.Zero),
                current.DisplayTimeZoneId,
                CultureInfo.GetCultureInfo(current.FormatCultureTag));
        }
    }

    public string CanonicalBusinessDate =>
        TemporalExample.BusinessDateIso;

    public string CanonicalUtcInstant =>
        TemporalExample.UtcInstant;

    public string ResolvedLocalInstant =>
        TemporalExample.LocalInstant;

    public string CapturedEditCulture => editSession?.CapturedCulture.Name ?? "—";

    public string EditStatus
    {
        get
        {
            return editState switch
            {
                EditState.NotStarted => Text("EditNotStarted"),
                EditState.Started => FormatText("EditStarted", CapturedEditCulture),
                EditState.Committed => FormatText(
                    "EditCommitted",
                    committedValue?.ToString(CultureInfo.InvariantCulture) ?? "—"),
                EditState.Invalid => FormatText("EditInvalid", CapturedEditCulture),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public bool TryLoadDevelopmentSample(string path, out string? diagnosticCode)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            var kind = root.GetProperty("kind").GetString();
            if (!string.Equals(kind, ExpectedKind, StringComparison.Ordinal))
            {
                sampleState = SampleState.Failed;
                OnPropertyChanged(nameof(Status));
                diagnosticCode = "T01_SAMPLE_KIND_INVALID";
                return false;
            }

            SampleName = root.GetProperty("name").GetString()
                ?? throw new InvalidDataException();
            SamplePurpose = root.GetProperty("purpose").GetString()
                ?? throw new InvalidDataException();
            SourcePath = Path.GetFullPath(path);
            sampleState = SampleState.Opened;
            OnPropertyChanged(nameof(Status));

            diagnosticCode = null;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
            or KeyNotFoundException)
        {
            sampleState = SampleState.Failed;
            OnPropertyChanged(nameof(Status));
            diagnosticCode = "T01_SAMPLE_OPEN_FAILED";
            return false;
        }
    }

    public void ApplyPresentationPreset(
        string uiLanguageTag,
        string formatCultureTag,
        string timeZoneId,
        bool pseudo,
        bool forceRtl)
    {
        pseudoLocalizationEnabled = pseudo;
        forceRtlForTest = forceRtl;

        preferences.Update(preferences.Current with
        {
            UiLanguageTag = uiLanguageTag,
            FormatCultureTag = formatCultureTag,
            DisplayTimeZoneId = timeZoneId
        });

        RefreshPresentation();
        PresentationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void BeginFxEdit()
    {
        fxEditSession ??= new DecimalEditSession(FormatCulture);
        OnPropertyChanged(nameof(FxCapturedCulture));
    }

    private void ApplyFxRate()
    {
        BeginFxEdit();
        try
        {
            if (!fxEditSession!.TryCommit(FxQuoteText, out var quote))
                throw new DomainRuleException(new DomainDiagnostic("FX_QUOTE_TEXT_INVALID", "Fx.QuoteTextInvalid",
                    [new("culture", DiagnosticArgumentType.Text, fxEditSession.CapturedCulture.Name)]));

            BusinessDate rateDate;
            try { rateDate = BusinessDate.ParseIso(FxRateDateText); }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                throw new DomainRuleException(new DomainDiagnostic("FX_DATE_INVALID", "Fx.DateInvalid", []));
            }
            if (string.IsNullOrWhiteSpace(FxRateSourceText))
                throw new DomainRuleException(new DomainDiagnostic("FX_SOURCE_REQUIRED", "Fx.SourceRequired", []));

            var next = fxConversionExampleService.Build(quote, rateDate, FxRateSourceText);
            fxExample = next;
            fxDiagnostic = next.Diagnostics.FirstOrDefault();
            fxStatusKey = "FxEditorReady";
            fxEditSession = null;
            fxQuoteText = FormatQuote(next.Quote);
            OnPropertyChanged(nameof(FxQuoteText));
        }
        catch (DomainRuleException ex) { fxDiagnostic = ex.Diagnostic; }
        catch (Exception ex)
        {
            Trace.TraceError("FX editor unexpected failure: {0}", ex);
            fxDiagnostic = null;
            fxStatusKey = "FxEditorUnexpected";
        }

        OnPropertyChanged(nameof(FxOriginal));
        OnPropertyChanged(nameof(FxOrientation));
        OnPropertyChanged(nameof(FxFunctional));
        OnPropertyChanged(nameof(FxRounding));
        OnPropertyChanged(nameof(FxRateSetVersion));
        OnPropertyChanged(nameof(FxStatus));
        OnPropertyChanged(nameof(FxCapturedCulture));
    }

    private CultureInfo FormatCulture =>
        CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag);
    private string FormatQuote(decimal quote) =>
        quote.ToString("0.############################", FormatCulture);
    private string FormatMoney(Money money) =>
        formatter.FormatCurrency(money.Amount, money.Currency.Value, FormatCulture,
            preferences.Current.NumberDisplayOptions);

    private TemporalContractExample TemporalExample =>
        temporalContractExampleService.Build(
            preferences.Current.DisplayTimeZoneId);

    private CultureInfo UiCulture =>
        CultureInfo.GetCultureInfo(preferences.Current.UiLanguageTag);

    private string Text(string key)
    {
        var value = texts.Get(key, UiCulture);
        return pseudoLocalizationEnabled
            ? PseudoLocalization.Expand(value)
            : value;
    }

    private string FormatText(string key, params object?[] arguments)
    {
        var value = string.Format(UiCulture, texts.Get(key, UiCulture), arguments);
        return pseudoLocalizationEnabled
            ? PseudoLocalization.Expand(value)
            : value;
    }

    private void StartEdit()
    {
        editSession = new DecimalEditSession(
            CultureInfo.GetCultureInfo(preferences.Current.FormatCultureTag));
        committedValue = null;
        editState = EditState.Started;
        OnPropertyChanged(nameof(CapturedEditCulture));
        OnPropertyChanged(nameof(EditStatus));
    }

    private void CommitEdit()
    {
        if (editSession is null)
        {
            StartEdit();
        }

        if (editSession!.TryCommit(PendingEditText, out var value))
        {
            committedValue = value;
            editState = EditState.Committed;
        }
        else
        {
            committedValue = null;
            editState = EditState.Invalid;
        }

        OnPropertyChanged(nameof(EditStatus));
    }

    private void RefreshPresentation()
    {
        Company?.RefreshPresentation(pseudoLocalizationEnabled);
        Models?.RefreshPresentation(pseudoLocalizationEnabled);
        Studies?.RefreshPresentation(pseudoLocalizationEnabled);
        Resources?.RefreshPresentation(pseudoLocalizationEnabled);
        Allocations?.RefreshPresentation(pseudoLocalizationEnabled);
        Studies?.ImportColumns?.RefreshPresentation(pseudoLocalizationEnabled);
        // Dirty edits retain their captured grammar; clean fields follow the selected region.
        if (fxEditSession is null)
        {
            fxQuoteText = FormatQuote(fxExample.Quote);
            OnPropertyChanged(nameof(FxQuoteText));
        }
        var names = new[]
        {
            nameof(SelectedUiLanguageTag),
            nameof(SelectedFormatCultureTag),
            nameof(SelectedTimeZoneId),
            nameof(AppTitle),
            nameof(WindowTitle),
            nameof(DevelopmentToolsTitle),
            nameof(SpecificationVersion),
            nameof(HeaderSubtitle),
            nameof(PreferencesTitle),
            nameof(PreferencesSaveWarning),
            nameof(RetrySavePreferencesLabel),
            nameof(LabelUiLanguage),
            nameof(LabelFormatCulture),
            nameof(LabelTimeZone),
            nameof(PreviewTitle),
            nameof(LabelMoneyPreview),
            nameof(LabelDatePreview),
            nameof(LabelInstantPreview),
            nameof(LabelCanonicalBusinessDate),
            nameof(LabelCanonicalUtcInstant),
            nameof(LabelResolvedLocalInstant),
            nameof(CanonicalBusinessDate),
            nameof(CanonicalUtcInstant),
            nameof(ResolvedLocalInstant),
            nameof(EditTitle),
            nameof(LabelCapturedCulture),
            nameof(LabelPendingValue),
            nameof(LabelEditStatus),
            nameof(ActionStartEdit),
            nameof(ActionCommitEdit),
            nameof(LabelStatus),
            nameof(LabelSample),
            nameof(LabelPurpose),
            nameof(LabelSource),
            nameof(FooterDevelopmentOnly),
            nameof(RoundingTitle),
            nameof(LabelRoundingReference),
            nameof(LabelRoundingSource),
            nameof(LabelRoundingQuantum),
            nameof(LabelRoundingId),
            nameof(LabelRoundingRaw),
            nameof(LabelRoundingAdjustment),
            nameof(LabelRoundingPosted),
            nameof(RoundingExplanation),
            nameof(CurrencyMetadataTitle),
            nameof(LabelCurrencyCode),
            nameof(LabelNumericCode),
            nameof(LabelMinorUnitDigits),
            nameof(LabelCurrencyQuantum),
            nameof(LabelCurrencyProjection),
            nameof(CurrencyMetadataExplanation),
            nameof(FxEditorTitle),
            nameof(LabelFxOriginal),
            nameof(LabelFxOrientation),
            nameof(LabelFxQuote),
            nameof(LabelFxRateDate),
            nameof(LabelFxRateSource),
            nameof(LabelFxFunctional),
            nameof(LabelFxRounding),
            nameof(LabelFxRateSet),
            nameof(ActionApplyFxRate),
            nameof(FxEditorExplanation),
            nameof(FxStatus),
            nameof(FxCapturedCulture),
            nameof(FxOriginal),
            nameof(FxFunctional),
            nameof(FxRounding),
            nameof(Status),
            nameof(PreviewMoney),
            nameof(PreviewDate),
            nameof(PreviewInstant),
            nameof(EditStatus)
        };

        foreach (var name in names)
        {
            OnPropertyChanged(name);
        }
    }

    private enum SampleState
    {
        None,
        Opened,
        Failed
    }

    private enum EditState
    {
        NotStarted,
        Started,
        Committed,
        Invalid
    }
}

using System.Globalization;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Valdrio.Localization;

namespace Valdrio.Desktop;

public sealed class MainWindowViewModel : ObservableObject
{
    private const string ExpectedKind = "valdrio-development-smoke-v1";

    private readonly UiTextCatalog texts;
    private readonly PresentationPreferencesService preferences;
    private readonly PresentationFormatter formatter;
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

    public MainWindowViewModel(
        UiTextCatalog texts,
        LanguageCatalog languages,
        PresentationPreferencesService preferences,
        PresentationFormatter formatter)
    {
        this.texts = texts;
        this.preferences = preferences;
        this.formatter = formatter;

        UiLanguageOptions = languages.Published
            .Select(language => language.Tag)
            .ToArray();

        FormatCultureOptions = ["es-ES", "en-US", "en-GB"];
        TimeZoneOptions = ["Europe/Madrid", "UTC", "America/Los_Angeles"];

        StartEditCommand = new RelayCommand(StartEdit);
        CommitEditCommand = new RelayCommand(CommitEdit);

        preferences.Changed += (_, _) =>
        {
            RefreshPresentation();
            PresentationChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? PresentationChanged;

    public IReadOnlyList<string> UiLanguageOptions { get; }
    public IReadOnlyList<string> FormatCultureOptions { get; }
    public IReadOnlyList<string> TimeZoneOptions { get; }

    public IRelayCommand StartEditCommand { get; }
    public IRelayCommand CommitEditCommand { get; }

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

    public string CapturedEditCulture => editSession?.CapturedCulture.Name ?? "—";

    public string EditStatus
    {
        get
        {
            var culture = UiCulture;
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
        var names = new[]
        {
            nameof(SelectedUiLanguageTag),
            nameof(SelectedFormatCultureTag),
            nameof(SelectedTimeZoneId),
            nameof(AppTitle),
            nameof(WindowTitle),
            nameof(SpecificationVersion),
            nameof(HeaderSubtitle),
            nameof(PreferencesTitle),
            nameof(LabelUiLanguage),
            nameof(LabelFormatCulture),
            nameof(LabelTimeZone),
            nameof(PreviewTitle),
            nameof(LabelMoneyPreview),
            nameof(LabelDatePreview),
            nameof(LabelInstantPreview),
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

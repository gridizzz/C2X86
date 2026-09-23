using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using JsToCSharp.Application;
using JsToCSharp.Domain;
using JsToCSharp.Infrastructure;
using System.Diagnostics;

namespace JsToCSharp.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly TranslationService _translationService;
    private readonly DispatcherTimer _waitTimer;
    private readonly Stopwatch _waitStopwatch = new();
    private readonly LocalCodeTranslator _localTranslator = new();
    private readonly IUserPreferencesStore _preferences = new JsonUserPreferencesStore(JsonUserPreferencesStore.DefaultPath);
    private readonly ILocalModelCatalog _modelCatalog = new OllamaModelCatalog();
    private CancellationTokenSource? _modelNameCancellation;
    private (string Source, string Result)? _clearedCode;
    private double _editorFontSize = 14;
    private bool _closing;
    private bool _canClose;
    private Size _normalSize;
    private int _animationFrame;
    private CancellationTokenSource? _translationCancellation;

    private const string OtherModel = "Other…";
    private static readonly string[] OpenAiModels =
        ["gpt-5-mini", "gpt-6-sol", "gpt-6-astra", "gpt-6-luna", "gpt-4.1", "gpt-4.1-mini", OtherModel];
    // Text-generation models from https://ai.google.dev/gemini-api/docs/models (2026-09-23).
    private static readonly string[] GeminiModels =
    [
        "gemini-3.8-flash", "gemini-3.7-flash", "gemini-3.6-flash", "gemini-3.5-flash",
        "gemini-3.5-flash-lite", "gemini-3.1-flash-lite",
        "gemini-3-flash-preview", "gemini-3.1-pro-preview",
        "gemini-2.5-flash", "gemini-2.5-pro", "gemini-2.5-flash-lite", OtherModel
    ];

    public MainWindow()
    {
        InitializeComponent();
        SourceLanguageBox.ItemsSource = LanguageCatalog.All;
        TargetLanguageBox.ItemsSource = LanguageCatalog.All;
        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        _translationService = new TranslationService([
            new OpenAiCodeTranslator(httpClient),
            new GeminiCodeTranslator(httpClient),
            _localTranslator
        ]);
        _waitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _waitTimer.Tick += WaitTimer_OnTick;
        SetModelOptions(AiProvider.OpenAi);
        var preferences = _preferences.Load();
        Width = preferences.WindowWidth;
        Height = preferences.WindowHeight;
        _normalSize = new Size(Width, Height);
        SizeChanged += (_, _) =>
        {
            if (WindowState == WindowState.Normal) _normalSize = Bounds.Size;
        };
        if (preferences.Maximized) WindowState = WindowState.Maximized;
        ProviderBox.SelectedIndex = (int)preferences.Provider;
        SourceLanguageBox.SelectedItem = LanguageCatalog.Get(preferences.SourceLanguage);
        TargetLanguageBox.SelectedItem = LanguageCatalog.Get(preferences.TargetLanguage);
        _editorFontSize = preferences.EditorFontSize;
        WordWrapBox.IsChecked = preferences.WordWrap;
        WordWrapBox.IsCheckedChanged += (_, _) => ApplyEditorOptions();
        ApplyEditorOptions();
        LocalModelPathBox.TextChanged += async (_, _) => await UpdateModelNameAsync();
        LocalModelPathBox.Text = preferences.LocalModelPath;
        SourceLanguageBox.SelectionChanged += (_, _) => ApplyHighlighting();
        TargetLanguageBox.SelectionChanged += (_, _) => ApplyHighlighting();
        ApplyHighlighting();
        InitializeEditorTools(preferences);
        Closing += async (_, args) =>
        {
            if (_canClose) return;
            args.Cancel = true;
            if (_closing) return;
            _closing = true;
            _draftTimer.Stop();
            SaveDraft();
            IsEnabled = false;
            _translationCancellation?.Cancel();
            _modelNameCancellation?.Cancel();
            try
            {
                _preferences.Save(CapturePreferences());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Preference failures must not prevent shutdown; credentials are never saved.
            }
            await _localTranslator.DisposeAsync();
            httpClient.Dispose();
            _canClose = true;
            Close();
        };
    }

    private async void Translate_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            _translationCancellation = new CancellationTokenSource();
            SetBusy(true, GetProvider() == AiProvider.LocalModel
                ? "Loading model and translating locally on your CPU…"
                : "Translating… Your code is being sent to the selected provider.");
            var request = new TranslationRequest(
                GetProvider(),
                (ModelBox.SelectedItem as string == OtherModel
                    ? CustomModelBox.Text
                    : ModelBox.SelectedItem?.ToString())?.Trim() ?? string.Empty,
                ApiKeyBox.Text?.Trim() ?? string.Empty,
                GetLanguage(SourceLanguageBox),
                GetLanguage(TargetLanguageBox),
                SourceCodeBox.Text ?? string.Empty,
                LocalModelPathBox.Text?.Trim());

            var progress = new Progress<TranslationStage>(stage =>
            {
                if (_translationCancellation is null || _translationCancellation.IsCancellationRequested) return;
                SetStatus(stage switch
                {
                    TranslationStage.LoadingModel => "Loading local model into memory…",
                    TranslationStage.PreparingInput => "Preparing input…",
                    _ => "Generating translated code…"
                });
                UpdateModelMemory();
            });
            var result = await _translationService.TranslateAsync(request, _translationCancellation.Token, progress);
            ResultCodeBox.Text = result.Code;
            SetStatus(result.Warning ?? "Translation complete. Review the generated code before using it.");
        }
        catch (OperationCanceledException)
        {
            var cancelled = _translationCancellation?.IsCancellationRequested == true;
            SetStatus(cancelled ? "Translation cancelled." : "The provider request timed out. Try again.", !cancelled);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or HttpRequestException or IOException or UnauthorizedAccessException)
        {
            SetStatus(exception.Message, true);
        }
        finally
        {
            SetBusy(false, null);
            _translationCancellation?.Dispose();
            _translationCancellation = null;
            UpdateModelMemory();
        }
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e)
    {
        _translationCancellation?.Cancel();
        CancelButton.IsEnabled = false;
        SetStatus("Cancelling… Waiting for the current inference step to finish.");
    }

    private async void BrowseModel_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a GGUF model or Ollama sha256 weights file",
            AllowMultiple = false
        });
        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path is null)
        {
            SetStatus("Choose a model stored on a local drive.", true);
            return;
        }
        LocalModelPathBox.Text = path;
        SetStatus("Local model selected. Select Translate to load it.");
    }

    private async void Import_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import source code",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Source code") { Patterns = LanguageCatalog.SourceExtensions.Select(extension => "*" + extension).ToArray() }]
        });

        if (files.Count == 0) return;
        await ImportSourceFileAsync(files[0]);
    }

    private async void Export_OnClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ResultCodeBox.Text))
        {
            SetStatus("There is no translated code to export.", true);
            return;
        }

        var language = LanguageCatalog.Get(GetLanguage(TargetLanguageBox));
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export translated code",
            SuggestedFileName = language.SuggestedFileName,
            DefaultExtension = language.FileExtension.TrimStart('.'),
            FileTypeChoices = [new FilePickerFileType(language.DisplayName + " code") { Patterns = ["*" + language.FileExtension] }]
        });

        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(ResultCodeBox.Text);
        SetStatus($"Exported {file.Name}.");
    }

    private void ClearSource_OnClick(object? sender, RoutedEventArgs e)
    {
        _clearedCode = (SourceCodeBox.Text ?? "", ResultCodeBox.Text ?? "");
        UndoClearButton.IsEnabled = true;
        SourceCodeBox.Text = string.Empty;
        ResultCodeBox.Text = string.Empty;
        SetStatus("Source and translated code cleared.");
    }

    private async void PasteSource_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SourceCodeBox.IsReadOnly || _closing) return;
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                SetStatus("Clipboard access is unavailable.", true);
                return;
            }

            var text = await clipboard.GetTextAsync();
            // Translation or shutdown may have started while the clipboard was read.
            if (SourceCodeBox.IsReadOnly || _closing) return;
            if (string.IsNullOrEmpty(text))
            {
                SetStatus("The clipboard contains no text to paste.", true);
                return;
            }

            var start = SourceCodeBox.SelectionStart;
            SourceCodeBox.Document.Replace(start, SourceCodeBox.SelectionLength, text);
            SourceCodeBox.Select(start + text.Length, 0);
            SourceCodeBox.Focus();
            SourceCodeBox.ScrollToLine(SourceCodeBox.TextArea.Caret.Line);
            SetStatus("Clipboard text pasted into source code.");
        }
        catch (Exception)
        {
            SetStatus("Could not paste from the clipboard. Try copying the text again.", true);
        }
    }

    private async void Copy_OnClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ResultCodeBox.Text)) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(ResultCodeBox.Text);
            SetStatus("Translated code copied to clipboard.");
        }
    }

    private void ProviderBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModelBox is not null)
        {
            SetModelOptions(GetProvider());
        }
    }

    private AiProvider GetProvider() => ProviderBox.SelectedIndex switch
    {
        1 => AiProvider.Gemini,
        2 => AiProvider.LocalModel,
        _ => AiProvider.OpenAi
    };

    private static Language GetLanguage(ComboBox box) => (box.SelectedItem as LanguageInfo)?.Language
        ?? throw new InvalidOperationException("Choose a source and target language.");

    private void SetBusy(bool isBusy, string? status)
    {
        SourceCodeBox.IsReadOnly = isBusy;
        UpdateSearchButtons();
        ImportButton.IsEnabled = !isBusy;
        ClearButton.IsEnabled = !isBusy;
        PasteButton.IsEnabled = !isBusy;
        UndoClearButton.IsEnabled = !isBusy && _clearedCode.HasValue;
        TranslateButton.IsEnabled = !isBusy;
        CancelButton.IsVisible = isBusy;
        CancelButton.IsEnabled = isBusy;
        ProviderBox.IsEnabled = !isBusy;
        SourceLanguageBox.IsEnabled = !isBusy;
        TargetLanguageBox.IsEnabled = !isBusy;
        ApiPanel.IsEnabled = !isBusy;
        ModelPanel.IsEnabled = !isBusy;
        LocalModelPanel.IsEnabled = !isBusy;
        TranslateButton.Content = isBusy ? "Translating…" : "Translate";
        ThinkingIcon.IsVisible = isBusy;
        WaitTimeText.IsVisible = isBusy;

        if (isBusy)
        {
            _animationFrame = 0;
            _waitStopwatch.Restart();
            _waitTimer.Start();
            WaitTimeText.Text = "Waiting: 00:00";
        }
        else
        {
            _waitTimer.Stop();
            _waitStopwatch.Stop();
        }

        if (status is not null) SetStatus(status);
    }

    private void SetModelOptions(AiProvider provider)
    {
        ApiPanel.IsVisible = provider != AiProvider.LocalModel;
        ModelPanel.IsVisible = provider != AiProvider.LocalModel;
        LocalModelPanel.IsVisible = provider == AiProvider.LocalModel;
        CustomModelBox.Text = string.Empty;
        CustomModelBox.Watermark = provider == AiProvider.Gemini
            ? "e.g. gemini-3.8-flash"
            : "e.g. gpt-6-sol";
        ModelBox.ItemsSource = provider == AiProvider.OpenAi ? OpenAiModels : GeminiModels;
        ModelBox.SelectedIndex = 0;
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusCard.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(isError ? "#3A1D1D" : "#252526"));
        StatusCard.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(isError ? "#F14C4C" : "#3C3C3C"));
        StatusText.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(isError ? "#F48771" : "#D4D4D4"));
    }

    private void ModelBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CustomModelPanel is not null)
        {
            CustomModelPanel.IsVisible = ModelBox.SelectedItem as string == OtherModel;
        }
    }

    private void ApplyHighlighting()
    {
        SourceCodeBox.SyntaxHighlighting = EditorHighlighting.Get(GetLanguage(SourceLanguageBox));
        ResultCodeBox.SyntaxHighlighting = EditorHighlighting.Get(GetLanguage(TargetLanguageBox));
    }

    private void ApplyEditorOptions()
    {
        SourceCodeBox.FontSize = ResultCodeBox.FontSize = _editorFontSize;
        SourceCodeBox.WordWrap = ResultCodeBox.WordWrap = WordWrapBox.IsChecked == true;
        FontSizeText.Text = _editorFontSize.ToString("0");
        SmallerFontButton.IsEnabled = _editorFontSize > 10;
        LargerFontButton.IsEnabled = _editorFontSize < 32;
    }

    private void SmallerFont_OnClick(object? sender, RoutedEventArgs e)
    {
        _editorFontSize = Math.Max(10, _editorFontSize - 1);
        ApplyEditorOptions();
    }

    private void LargerFont_OnClick(object? sender, RoutedEventArgs e)
    {
        _editorFontSize = Math.Min(32, _editorFontSize + 1);
        ApplyEditorOptions();
    }

    private void UndoClear_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_clearedCode is not { } code) return;
        SourceCodeBox.Text = code.Source;
        ResultCodeBox.Text = code.Result;
        _clearedCode = null;
        UndoClearButton.IsEnabled = false;
        SetStatus("Cleared code restored.");
    }

    private async Task UpdateModelNameAsync()
    {
        _modelNameCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _modelNameCancellation = cancellation;
        try
        {
            var name = await _modelCatalog.GetDisplayNameAsync(LocalModelPathBox.Text?.Trim() ?? "", cancellation.Token);
            if (!cancellation.IsCancellationRequested) LocalModelNameText.Text = name;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_modelNameCancellation, cancellation)) _modelNameCancellation = null;
        }
    }

    private void UpdateModelMemory()
    {
        var loaded = _localTranslator.LoadedModelPath;
        ModelMemoryText.Text = loaded is null ? "Model not loaded" : "Model loaded in memory for reuse";
        ModelMemoryText.SetValue(ToolTip.TipProperty, loaded);
        UnloadModelButton.IsEnabled = loaded is not null && _translationCancellation is null;
    }

    private async void UnloadModel_OnClick(object? sender, RoutedEventArgs e)
    {
        SetBusy(true, "Unloading local model…");
        CancelButton.IsVisible = false;
        try { await _localTranslator.UnloadAsync(); }
        finally { SetBusy(false, "Local model unloaded."); UpdateModelMemory(); }
    }

    private void WaitTimer_OnTick(object? sender, EventArgs e)
    {
        WaitTimeText.Text = $"Waiting: {_waitStopwatch.Elapsed:mm\\:ss}";
        ThinkingIcon.Text = new[] { "✦", "✧", "·", "✧" }[_animationFrame++ % 4];
    }
}

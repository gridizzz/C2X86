using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit;
using JsToCSharp.Application;
using JsToCSharp.Domain;
using JsToCSharp.Infrastructure;

namespace JsToCSharp.Desktop;

public sealed partial class MainWindow
{
    private readonly IDraftStore _draftStore = new JsonDraftStore(Path.Combine(
        Path.GetDirectoryName(JsonUserPreferencesStore.DefaultPath)!, "draft.json"));
    private readonly IInputFileService _inputFiles = new InputFileService();
    private readonly DispatcherTimer _draftTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _draftDirty;
    private bool _changingRecovery;

    private UserPreferences CapturePreferences() => new()
    {
        Provider = GetProvider(), LocalModelPath = LocalModelPathBox.Text,
        SourceLanguage = GetLanguage(SourceLanguageBox), TargetLanguage = GetLanguage(TargetLanguageBox),
        WindowWidth = _normalSize.Width, WindowHeight = _normalSize.Height,
        Maximized = WindowState == WindowState.Maximized,
        EditorFontSize = _editorFontSize, WordWrap = WordWrapBox.IsChecked == true,
        DraftRecoveryEnabled = RecoveryBox.IsChecked == true
    };

    private void InitializeEditorTools(UserPreferences preferences)
    {
        RecoveryBox.IsChecked = preferences.DraftRecoveryEnabled;
        if (preferences.DraftRecoveryEnabled)
        {
            var draft = _draftStore.Load();
            if (draft is not null)
            {
                SourceLanguageBox.SelectedItem = LanguageCatalog.Get(draft.SourceLanguage);
                TargetLanguageBox.SelectedItem = LanguageCatalog.Get(draft.TargetLanguage);
                SourceCodeBox.Text = draft.SourceCode;
                ResultCodeBox.Text = draft.ResultCode;
                RecoveryMessage("Saved draft restored. Draft recovery is enabled; code is saved locally.");
            }
            else RecoveryMessage("Draft recovery enabled. Source and result are saved locally as plaintext.");
        }
        else
        {
            try { _draftStore.Delete(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { RecoveryMessage("Recovery is off, but the old draft could not be deleted. Check local file permissions."); }
        }
        RecoveryBox.IsCheckedChanged += (_, _) => ChangeRecovery();
        SourceCodeBox.TextChanged += (_, _) => _draftDirty = true;
        ResultCodeBox.TextChanged += (_, _) => _draftDirty = true;
        SourceLanguageBox.SelectionChanged += (_, _) => _draftDirty = true;
        TargetLanguageBox.SelectionChanged += (_, _) => _draftDirty = true;
        _draftTimer.Tick += (_, _) => SaveDraft();
        _draftTimer.Start();
        SearchTargetBox.SelectionChanged += (_, _) => UpdateSearchButtons();
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.F && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
            {
                SearchTargetBox.SelectedIndex = ResultCodeBox.IsKeyboardFocusWithin ? 1 : 0;
                OpenSearch_OnClick(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && SearchPanel.IsVisible)
            { SearchPanel.IsVisible = false; e.Handled = true; }
        }, RoutingStrategies.Tunnel);
        ConfigureDrop(SourceDropArea, model: false);
        ConfigureDrop(LocalModelPanel, model: true);
        ConfigureDrop(ProviderBox, model: true);
    }

    private void RecoveryMessage(string message)
    {
        RecoveryStatusText.Text = message;
        RecoveryStatusText.IsVisible = true;
    }

    private void ChangeRecovery()
    {
        if (_changingRecovery) return;
        try { _preferences.Save(CapturePreferences()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _changingRecovery = true;
            RecoveryBox.IsChecked = RecoveryBox.IsChecked != true;
            _changingRecovery = false;
            RecoveryMessage("Could not save the recovery setting. Check local file permissions.");
            return;
        }
        if (RecoveryBox.IsChecked == true)
        {
            _draftDirty = true;
            SaveDraft();
        }
        else
        {
            try { _draftStore.Delete(); RecoveryMessage("Draft recovery disabled. Saved draft deleted."); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { RecoveryMessage("Recovery disabled, but the saved draft could not be deleted. Check local file permissions."); }
        }
    }

    private void SaveDraft()
    {
        if (RecoveryBox.IsChecked != true || !_draftDirty) return;
        try
        {
            _draftStore.Save(new RecoveryDraft(SourceCodeBox.Text ?? "", ResultCodeBox.Text ?? "",
                GetLanguage(SourceLanguageBox), GetLanguage(TargetLanguageBox)));
            _draftDirty = false;
            RecoveryMessage($"Draft saved locally at {DateTime.Now:t}. Recovery is enabled (plaintext code on disk).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { RecoveryMessage("Draft could not be saved. Check local disk space and permissions; recent edits are not protected."); }
    }

    private TextEditor SearchEditor => SearchTargetBox.SelectedIndex == 1 ? ResultCodeBox : SourceCodeBox;
    private void UpdateSearchButtons()
    {
        var editable = SearchTargetBox.SelectedIndex == 0 && !SourceCodeBox.IsReadOnly && !_closing;
        ReplaceButton.IsEnabled = ReplaceAllButton.IsEnabled = editable;
        ReplaceBox.IsEnabled = editable;
    }
    private void OpenSearch_OnClick(object? sender, RoutedEventArgs e)
    {
        SearchPanel.IsVisible = true;
        UpdateSearchButtons();
        FindBox.Focus();
    }
    private void CloseSearch_OnClick(object? sender, RoutedEventArgs e) => SearchPanel.IsVisible = false;
    private void FindNext_OnClick(object? sender, RoutedEventArgs e)
    {
        var editor = SearchEditor;
        var query = FindBox.Text ?? "";
        if (query.Length == 0) { SetStatus("Enter text to find."); return; }
        var index = EditorSearch.FindNext(editor.Text ?? "", query,
            editor.SelectionStart + editor.SelectionLength, MatchCaseBox.IsChecked == true);
        if (index < 0) { SetStatus("No matching text found."); return; }
        editor.Select(index, query.Length);
        editor.ScrollToLine(editor.Document.GetLineByOffset(index).LineNumber);
        SetStatus("Match selected. Find next wraps to the beginning.");
    }
    private void Replace_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ReplaceButton.IsEnabled) return;
        var query = FindBox.Text ?? "";
        if (query.Length == 0) return;
        var comparison = MatchCaseBox.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (!string.Equals(SourceCodeBox.SelectedText, query, comparison)) { FindNext_OnClick(sender, e); return; }
        var start = SourceCodeBox.SelectionStart;
        var replacement = ReplaceBox.Text ?? "";
        SourceCodeBox.Document.Replace(start, SourceCodeBox.SelectionLength, replacement);
        SourceCodeBox.Select(start + replacement.Length, 0);
        FindNext_OnClick(sender, e);
    }
    private void ReplaceAll_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ReplaceAllButton.IsEnabled) return;
        var original = SourceCodeBox.Text ?? "";
        var updated = EditorSearch.ReplaceAll(original, FindBox.Text ?? "", ReplaceBox.Text ?? "", MatchCaseBox.IsChecked == true);
        if (updated == original) { SetStatus("No text changed."); return; }
        SourceCodeBox.Document.Replace(0, SourceCodeBox.Document.TextLength, updated);
        SetStatus("All matching source text replaced. Use Undo to revert.");
    }

    private void ConfigureDrop(Control target, bool model)
    {
        DragDrop.SetAllowDrop(target, true);
        target.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            if (!e.Data.Contains(DataFormats.Files)) return;
            e.DragEffects = !SourceCodeBox.IsReadOnly && !_closing ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        target.AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            if (!e.Data.Contains(DataFormats.Files)) return;
            e.Handled = true;
            if (SourceCodeBox.IsReadOnly || _closing) return;
            var files = e.Data.GetFiles()?.Take(2).ToArray();
            if (files is not { Length: 1 } || files[0] is not IStorageFile file)
            { SetStatus("Drop one file at a time, not a folder.", true); return; }
            if (model)
            {
                var path = file.TryGetLocalPath();
                if (path is null) { SetStatus("Choose a model on a local drive.", true); return; }
                SetBusy(true, "Checking model file…");
                CancelButton.IsVisible = false;
                try
                {
                    await Task.Run(() => _inputFiles.ValidateModel(path));
                    if (_closing) return;
                    ProviderBox.SelectedIndex = (int)AiProvider.LocalModel;
                    LocalModelPathBox.Text = path;
                    SetStatus("Local model selected. Select Translate to load it.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
                { SetStatus("Could not select model: " + ex.Message, true); }
                finally { SetBusy(false, null); }
            }
            else await ImportSourceFileAsync(file);
        }, RoutingStrategies.Tunnel);
    }

    private async Task ImportSourceFileAsync(IStorageFile file)
    {
        if (SourceCodeBox.IsReadOnly || _closing) return;
        SetBusy(true, "Reading source file…");
        CancelButton.IsVisible = false;
        try
        {
            await using var stream = await file.OpenReadAsync();
            var text = await _inputFiles.ReadSourceAsync(stream, file.Name);
            if (_closing) return;
            SourceCodeBox.Document.Replace(0, SourceCodeBox.Document.TextLength, text);
            SetStatus($"Imported {file.Name}. Use Undo to restore previous source.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { SetStatus("Could not import source: " + ex.Message, true); }
        finally { SetBusy(false, null); }
    }
}

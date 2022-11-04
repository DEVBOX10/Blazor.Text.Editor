﻿using System.Collections.Immutable;
using System.Text;
using BlazorTextEditor.RazorLib.Character;
using BlazorTextEditor.RazorLib.Clipboard;
using BlazorTextEditor.RazorLib.Commands;
using BlazorTextEditor.RazorLib.Cursor;
using BlazorTextEditor.RazorLib.HelperComponents;
using BlazorTextEditor.RazorLib.JavaScriptObjects;
using BlazorTextEditor.RazorLib.Keyboard;
using BlazorTextEditor.RazorLib.Store.TextEditorCase;
using BlazorTextEditor.RazorLib.TextEditor;
using BlazorTextEditor.RazorLib.Virtualization;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorTextEditor.RazorLib;

public partial class TextEditorDisplay : ComponentBase
{
    [Inject]
    private IStateSelection<TextEditorStates, TextEditorBase> TextEditorStatesSelection { get; set; } = null!;
    [Inject]
    private ITextEditorService TextEditorService { get; set; } = null!;
    [Inject]
    private IDispatcher Dispatcher { get; set; } = null!;
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;
    [Inject]
    private IClipboardProvider ClipboardProvider { get; set; } = null!;

    [Parameter, EditorRequired]
    public TextEditorKey TextEditorKey { get; set; } = null!;
    [Parameter]
    public RenderFragment? OnContextMenuRenderFragment { get; set; }
    [Parameter]
    public RenderFragment? AutoCompleteMenuRenderFragment { get; set; }
    [Parameter]
    public Action<TextEditorBase>? OnSaveRequested { get; set; }
    /// <summary>
    ///     (TextEditorBase textEditor, ImmutableArray&lt;TextEditorCursorSnapshot&gt; textEditorCursorSnapshots,
    ///     KeyboardEventArgs keyboardEventArgs, Func&lt;TextEditorMenuKind, Task&gt; setTextEditorMenuKind), Task
    /// </summary>
    [Parameter]
    public Func<TextEditorBase, ImmutableArray<TextEditorCursorSnapshot>, KeyboardEventArgs,
        Func<TextEditorMenuKind, Task>, Task>? AfterOnKeyDownAsync { get; set; }
    [Parameter]
    public bool ShouldRemeasureFlag { get; set; }
    [Parameter]
    public string StyleCssString { get; set; } = null!;
    [Parameter]
    public string ClassCssString { get; set; } = null!;
    /// <summary>
    ///     TabIndex is used for the html attribute: 'tabindex'
    ///     <br /><br />
    ///     tabindex of -1 means one can only set focus to the
    ///     text editor by clicking on it.
    ///     <br /><br />
    ///     tabindex of 0 means one can both use the tab key to set focus to the
    ///     text editor or click on it.
    /// </summary>
    [Parameter]
    public int TabIndex { get; set; } = -1;
    
    private int? _previousGlobalFontSizeInPixels;
    private bool? _previousShouldRemeasureFlag;

    private TextEditorKey? _previousTextEditorKey;
    private string _testStringForMeasurement = "abcdefghijklmnopqrstuvwxyz0123456789";
    private int _testStringRepeatCount = 6;
    private TextEditorCursorDisplay? _textEditorCursorDisplay;
    private ElementReference _textEditorDisplayElementReference;

    private Guid _textEditorGuid = Guid.NewGuid();

    /// <summary>
    ///     Do not select text just because the user has the Left Mouse Button down.
    ///     They might hold down Left Mouse Button from outside the TextEditorDisplay's content div
    ///     then move their mouse over the content div while holding the Left Mouse Button down.
    ///     <br /><br />
    ///     Instead only select text if an @onmousedown event triggered <see cref="_thinksLeftMouseButtonIsDown" />
    ///     to be equal to true and the @onmousemove event followed afterwards.
    /// </summary>
    private bool _thinksLeftMouseButtonIsDown;

    private VirtualizationDisplay<List<RichCharacter>>? _virtualizationDisplay;

    public bool ShouldMeasureDimensions { get; set; } = true;
    public CharacterWidthAndRowHeight? CharacterWidthAndRowHeight { get; private set; }
    public RelativeCoordinates? RelativeCoordinatesOnClick { get; private set; }
    public WidthAndHeightOfTextEditor? TextEditorWidthAndHeight { get; private set; }

    public TextEditorBase MutableReferenceToTextEditor => TextEditorStatesSelection.Value;

    private string TextEditorContentId => $"bte_text-editor-content_{_textEditorGuid}";

    private string MeasureCharacterWidthAndRowHeightId =>
        $"bte_measure-character-width-and-row-height_{_textEditorGuid}";

    private MarkupString GetAllTextEscaped => (MarkupString)MutableReferenceToTextEditor
        .GetAllText()
        .Replace("\r\n", "\\r\\n<br/>")
        .Replace("\r", "\\r<br/>")
        .Replace("\n", "\\n<br/>")
        .Replace("\t", "--->")
        .Replace(" ", "·");

    private string GlobalThemeCssClassString => TextEditorService
                                                    .TextEditorStates
                                                    .GlobalTextEditorOptions
                                                    .Theme?
                                                    .CssClassString
                                                ?? string.Empty;

    private string GlobalFontSizeInPixelsStyling => "font-size: " + TextEditorService
                                                                      .TextEditorStates
                                                                      .GlobalTextEditorOptions
                                                                      .FontSizeInPixels!.Value
                                                                  + "px;";

    private bool GlobalShowNewlines => TextEditorService
        .TextEditorStates.GlobalTextEditorOptions.ShowNewlines!.Value;

    private bool GlobalShowWhitespace => TextEditorService
        .TextEditorStates.GlobalTextEditorOptions.ShowWhitespace!.Value;

    public TextEditorCursor PrimaryCursor { get; } = new(true);

    public event Action? CursorsChanged;

    protected override async Task OnParametersSetAsync()
    {
        var primaryCursorSnapshot = new TextEditorCursorSnapshot(PrimaryCursor);

        var currentGlobalFontSizeInPixels = TextEditorService
            .TextEditorStates
            .GlobalTextEditorOptions
            .FontSizeInPixels!
            .Value;

        var dirtyGlobalFontSizeInPixels =
            _previousGlobalFontSizeInPixels is null ||
            _previousGlobalFontSizeInPixels != currentGlobalFontSizeInPixels;

        var dirtyShouldRemeasureFlag = _previousShouldRemeasureFlag is null ||
                                       _previousShouldRemeasureFlag != ShouldRemeasureFlag;

        if (dirtyGlobalFontSizeInPixels || dirtyShouldRemeasureFlag)
        {
            _previousGlobalFontSizeInPixels = currentGlobalFontSizeInPixels;
            _previousShouldRemeasureFlag = ShouldRemeasureFlag;

            ShouldMeasureDimensions = true;
            await InvokeAsync(StateHasChanged);

            ReloadVirtualizationDisplay();
        }

        if (_previousTextEditorKey is null ||
            _previousTextEditorKey != TextEditorKey)
        {
            // Setting IndexCoordinates to (0, 0) twice in this block
            // due to a general feeling of unease
            // that something bad will happen otherwise.
            {
                primaryCursorSnapshot.UserCursor.IndexCoordinates = (0, 0);
                primaryCursorSnapshot
                    .UserCursor.TextEditorSelection.AnchorPositionIndex = null;

                _previousTextEditorKey = TextEditorKey;

                primaryCursorSnapshot.UserCursor.IndexCoordinates = (0, 0);
                primaryCursorSnapshot
                    .UserCursor.TextEditorSelection.AnchorPositionIndex = null;
            }

            ReloadVirtualizationDisplay();
        }

        await base.OnParametersSetAsync();
    }

    protected override void OnInitialized()
    {
        TextEditorStatesSelection
            .Select(textEditorStates => textEditorStates.TextEditorList
                .Single(x => x.Key == TextEditorKey));

        CursorsChanged?.Invoke();

        base.OnInitialized();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) ReloadVirtualizationDisplay();

        if (ShouldMeasureDimensions)
        {
            CharacterWidthAndRowHeight = await JsRuntime
                .InvokeAsync<CharacterWidthAndRowHeight>(
                    "blazorTextEditor.measureCharacterWidthAndRowHeight",
                    MeasureCharacterWidthAndRowHeightId,
                    _testStringRepeatCount * _testStringForMeasurement.Length);

            TextEditorWidthAndHeight = await JsRuntime
                .InvokeAsync<WidthAndHeightOfTextEditor>(
                    "blazorTextEditor.measureWidthAndHeightOfTextEditor",
                    TextEditorContentId);

            {
                ShouldMeasureDimensions = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    public void ReloadVirtualizationDisplay()
    {
        _virtualizationDisplay?.InvokeEntriesProviderFunc();
    }

    private async Task FocusTextEditorOnClickAsync()
    {
        if (_textEditorCursorDisplay is not null)
            await _textEditorCursorDisplay.FocusAsync();   
    }

    private async Task HandleOnKeyDownAsync(KeyboardEventArgs keyboardEventArgs)
    {
        var safeTextEditorReference = MutableReferenceToTextEditor;
        var primaryCursorSnapshot = new TextEditorCursorSnapshot(PrimaryCursor);

        var cursorSnapshots = new TextEditorCursorSnapshot[]
        {
            new(primaryCursorSnapshot.UserCursor),
        }.ToImmutableArray();

        _textEditorCursorDisplay?.SetShouldDisplayMenuAsync(TextEditorMenuKind.None);

        if (KeyboardKeyFacts.IsMovementKey(keyboardEventArgs.Key))
        {
            TextEditorCursor.MoveCursor(
                keyboardEventArgs,
                primaryCursorSnapshot.UserCursor,
                safeTextEditorReference);
        }
        else if (KeyboardKeyFacts.CheckIsContextMenuEvent(keyboardEventArgs))
            _textEditorCursorDisplay?.SetShouldDisplayMenuAsync(TextEditorMenuKind.ContextMenu);
        else
        {
            var command = safeTextEditorReference.TextEditorKeymap.KeymapFunc
                .Invoke(keyboardEventArgs);

            if (command is not null)
            {
                await command.DoAsyncFunc.Invoke(
                    new TextEditorCommandParameter(
                        safeTextEditorReference,
                        cursorSnapshots,
                        ClipboardProvider,
                        TextEditorService,
                        ReloadVirtualizationDisplay,
                        OnSaveRequested));
            }
            else
            {
                Dispatcher.Dispatch(
                    new EditTextEditorBaseAction(
                        TextEditorKey,
                        cursorSnapshots,
                        keyboardEventArgs,
                        CancellationToken.None));

                ReloadVirtualizationDisplay();
            }
        }

        CursorsChanged?.Invoke();

        primaryCursorSnapshot.UserCursor.ShouldRevealCursor = true;

        var afterOnKeyDownAsync = AfterOnKeyDownAsync;

        if (afterOnKeyDownAsync is not null)
        {
            var cursorDisplay = _textEditorCursorDisplay;

            if (cursorDisplay is not null)
            {
                var textEditor = safeTextEditorReference;

                // Do not block UI thread with long running AfterOnKeyDownAsync 
                _ = Task.Run(async () =>
                {
                    await afterOnKeyDownAsync.Invoke(
                        textEditor,
                        cursorSnapshots,
                        keyboardEventArgs,
                        cursorDisplay.SetShouldDisplayMenuAsync);
                });
            }
        }
    }

    private void HandleOnContextMenuAsync()
    {
        _textEditorCursorDisplay?.SetShouldDisplayMenuAsync(TextEditorMenuKind.ContextMenu);
    }

    private async Task HandleContentOnMouseDownAsync(MouseEventArgs mouseEventArgs)
    {
        var safeTextEditorReference = MutableReferenceToTextEditor;
        var primaryCursorSnapshot = new TextEditorCursorSnapshot(PrimaryCursor);

        if ((mouseEventArgs.Buttons & 1) != 1 &&
            TextEditorSelectionHelper.HasSelectedText(
                primaryCursorSnapshot.ImmutableCursor.ImmutableTextEditorSelection))
            // Not pressing the left mouse button
            // so assume ContextMenu is desired result.
            return;

        _textEditorCursorDisplay?.SetShouldDisplayMenuAsync(TextEditorMenuKind.None);

        var rowAndColumnIndex =
            await DetermineRowAndColumnIndex(mouseEventArgs);

        primaryCursorSnapshot.UserCursor.IndexCoordinates =
            (rowAndColumnIndex.rowIndex, rowAndColumnIndex.columnIndex);
        primaryCursorSnapshot.UserCursor.PreferredColumnIndex =
            rowAndColumnIndex.columnIndex;

        _textEditorCursorDisplay?.PauseBlinkAnimation();

        var cursorPositionIndex = safeTextEditorReference
            .GetCursorPositionIndex(
                new TextEditorCursor(rowAndColumnIndex, false));

        primaryCursorSnapshot.UserCursor.TextEditorSelection.AnchorPositionIndex =
            cursorPositionIndex;
        primaryCursorSnapshot.UserCursor.TextEditorSelection.EndingPositionIndex =
            cursorPositionIndex;

        _thinksLeftMouseButtonIsDown = true;

        CursorsChanged?.Invoke();
    }

    /// <summary>
    ///     OnMouseUp is unnecessary
    /// </summary>
    /// <param name="mouseEventArgs"></param>
    private async Task HandleContentOnMouseMoveAsync(MouseEventArgs mouseEventArgs)
    {
        var safeTextEditorReference = MutableReferenceToTextEditor;
        var primaryCursorSnapshot = new TextEditorCursorSnapshot(PrimaryCursor);

        // Buttons is a bit flag
        // '& 1' gets if left mouse button is held
        if (_thinksLeftMouseButtonIsDown &&
            (mouseEventArgs.Buttons & 1) == 1)
        {
            var rowAndColumnIndex =
                await DetermineRowAndColumnIndex(mouseEventArgs);

            primaryCursorSnapshot.UserCursor.IndexCoordinates =
                (rowAndColumnIndex.rowIndex, rowAndColumnIndex.columnIndex);
            primaryCursorSnapshot.UserCursor.PreferredColumnIndex =
                rowAndColumnIndex.columnIndex;

            _textEditorCursorDisplay?.PauseBlinkAnimation();

            primaryCursorSnapshot.UserCursor.TextEditorSelection.EndingPositionIndex =
                safeTextEditorReference
                    .GetCursorPositionIndex(
                        new TextEditorCursor(rowAndColumnIndex, false));
        }
        else
            _thinksLeftMouseButtonIsDown = false;

        CursorsChanged?.Invoke();
    }

    private async Task<(int rowIndex, int columnIndex)> DetermineRowAndColumnIndex(
        MouseEventArgs mouseEventArgs)
    {
        var safeTextEditorReference = MutableReferenceToTextEditor;

        RelativeCoordinatesOnClick = await JsRuntime
            .InvokeAsync<RelativeCoordinates>(
                "blazorTextEditor.getRelativePosition",
                TextEditorContentId,
                mouseEventArgs.ClientX,
                mouseEventArgs.ClientY);

        if (CharacterWidthAndRowHeight is null)
            return (0, 0);

        var positionX = RelativeCoordinatesOnClick.RelativeX;
        var positionY = RelativeCoordinatesOnClick.RelativeY;

        // Scroll position offset
        {
            positionX += RelativeCoordinatesOnClick.RelativeScrollLeft;
            positionY += RelativeCoordinatesOnClick.RelativeScrollTop;
        }

        // Gutter padding column offset
        {
            positionX -= TextEditorBase.GUTTER_PADDING_LEFT_IN_PIXELS +
                         TextEditorBase.GUTTER_PADDING_RIGHT_IN_PIXELS;
        }

        var columnIndexDouble = positionX / CharacterWidthAndRowHeight.CharacterWidthInPixels;

        var columnIndexInt = (int)Math.Round(
            columnIndexDouble,
            MidpointRounding.AwayFromZero);

        var rowIndex = (int)(positionY / CharacterWidthAndRowHeight.RowHeightInPixels);

        rowIndex = rowIndex > safeTextEditorReference.RowCount - 1
            ? safeTextEditorReference.RowCount - 1
            : rowIndex;

        var lengthOfRow = safeTextEditorReference.GetLengthOfRow(rowIndex);

        // Tab key column offset
        {
            var parameterForGetTabsCountOnSameRowBeforeCursor =
                columnIndexInt > lengthOfRow
                    ? lengthOfRow
                    : columnIndexInt;

            var tabsOnSameRowBeforeCursor = safeTextEditorReference
                .GetTabsCountOnSameRowBeforeCursor(
                    rowIndex,
                    parameterForGetTabsCountOnSameRowBeforeCursor);

            // 1 of the character width is already accounted for
            var extraWidthPerTabKey = TextEditorBase.TAB_WIDTH - 1;

            columnIndexInt -= extraWidthPerTabKey * tabsOnSameRowBeforeCursor;
        }

        // Line number column offset
        {
            var mostDigitsInARowLineNumber = safeTextEditorReference.RowCount
                .ToString()
                .Length;

            columnIndexInt -= mostDigitsInARowLineNumber;
        }

        columnIndexInt = columnIndexInt > lengthOfRow
            ? lengthOfRow
            : columnIndexInt;

        rowIndex = Math.Max(rowIndex, 0);
        columnIndexInt = Math.Max(columnIndexInt, 0);

        return (rowIndex, columnIndexInt);
    }

    private string GetCssClass(byte decorationByte)
    {
        var safeTextEditorReference = MutableReferenceToTextEditor;

        return safeTextEditorReference.DecorationMapper.Map(decorationByte);
    }

    private string GetRowStyleCss(int index, double? virtualizedRowLeftInPixels)
    {
        var safeTextEditorReference = MutableReferenceToTextEditor;

        if (CharacterWidthAndRowHeight is null)
            return string.Empty;

        var top =
            $"top:{index * CharacterWidthAndRowHeight.RowHeightInPixels}px;";
        var height =
            $"height: {CharacterWidthAndRowHeight.RowHeightInPixels}px;";

        var mostDigitsInARowLineNumber = safeTextEditorReference.RowCount
            .ToString()
            .Length;

        var widthOfGutterInPixels = mostDigitsInARowLineNumber *
                                    CharacterWidthAndRowHeight.CharacterWidthInPixels;

        var leftInPixels = widthOfGutterInPixels +
                           virtualizedRowLeftInPixels +
                           TextEditorBase.GUTTER_PADDING_LEFT_IN_PIXELS +
                           TextEditorBase.GUTTER_PADDING_RIGHT_IN_PIXELS;

        var left = $"left: {leftInPixels}px;";

        return $"{top} {height} {left}";
    }

    private string GetGutterStyleCss(int index, double? virtualizedRowLeftInPixels)
    {
        var safeTextEditorReference = MutableReferenceToTextEditor;

        if (CharacterWidthAndRowHeight is null)
            return string.Empty;

        var top =
            $"top: {index * CharacterWidthAndRowHeight.RowHeightInPixels}px;";
        var height =
            $"height: {CharacterWidthAndRowHeight.RowHeightInPixels}px;";

        var mostDigitsInARowLineNumber = safeTextEditorReference.RowCount
            .ToString()
            .Length;

        var widthInPixels = mostDigitsInARowLineNumber *
                            CharacterWidthAndRowHeight.CharacterWidthInPixels;

        widthInPixels += TextEditorBase.GUTTER_PADDING_LEFT_IN_PIXELS +
                         TextEditorBase.GUTTER_PADDING_RIGHT_IN_PIXELS;

        var width = $"width: {widthInPixels}px;";

        var paddingLeft =
            $"padding-left: {TextEditorBase.GUTTER_PADDING_LEFT_IN_PIXELS}px;";
        var paddingRight =
            $"padding-right: {TextEditorBase.GUTTER_PADDING_RIGHT_IN_PIXELS}px;";

        var left = $"left: {virtualizedRowLeftInPixels}px;";

        return $"{left} {top} {height} {width} {paddingLeft} {paddingRight}";
    }

    private string GetTextSelectionStyleCss(int lowerBound, int upperBound, int rowIndex)
    {
        var safeTextEditorReference = MutableReferenceToTextEditor;

        if (CharacterWidthAndRowHeight is null ||
            rowIndex >= safeTextEditorReference.RowEndingPositions.Length)
            return string.Empty;

        var startOfRowTuple = safeTextEditorReference.GetStartOfRowTuple(rowIndex);
        var endOfRowTuple = safeTextEditorReference.RowEndingPositions[rowIndex];

        var selectionStartingColumnIndex = 0;
        var selectionEndingColumnIndex =
            endOfRowTuple.positionIndex - 1;

        var fullWidthOfRowIsSelected = true;

        if (lowerBound > startOfRowTuple.positionIndex)
        {
            selectionStartingColumnIndex =
                lowerBound - startOfRowTuple.positionIndex;

            fullWidthOfRowIsSelected = false;
        }

        if (upperBound < endOfRowTuple.positionIndex)
        {
            selectionEndingColumnIndex =
                upperBound - startOfRowTuple.positionIndex;

            fullWidthOfRowIsSelected = false;
        }

        var top =
            $"top: {rowIndex * CharacterWidthAndRowHeight.RowHeightInPixels}px;";
        var height =
            $"height: {CharacterWidthAndRowHeight.RowHeightInPixels}px;";

        var mostDigitsInARowLineNumber = safeTextEditorReference.RowCount
            .ToString()
            .Length;

        var widthOfGutterInPixels = mostDigitsInARowLineNumber *
                                    CharacterWidthAndRowHeight.CharacterWidthInPixels;

        var gutterSizeInPixels =
            widthOfGutterInPixels +
            TextEditorBase.GUTTER_PADDING_LEFT_IN_PIXELS +
            TextEditorBase.GUTTER_PADDING_RIGHT_IN_PIXELS;

        var selectionStartInPixels =
            selectionStartingColumnIndex *
            CharacterWidthAndRowHeight.CharacterWidthInPixels;

        // selectionStartInPixels offset from Tab keys a width of many characters
        {
            var tabsOnSameRowBeforeCursor = safeTextEditorReference
                .GetTabsCountOnSameRowBeforeCursor(
                    rowIndex,
                    selectionStartingColumnIndex);

            // 1 of the character width is already accounted for

            var extraWidthPerTabKey = TextEditorBase.TAB_WIDTH - 1;

            selectionStartInPixels += extraWidthPerTabKey *
                                      tabsOnSameRowBeforeCursor *
                                      CharacterWidthAndRowHeight.CharacterWidthInPixels;
        }

        var left = $"left: {gutterSizeInPixels + selectionStartInPixels}px;";

        var selectionWidthInPixels =
            selectionEndingColumnIndex *
            CharacterWidthAndRowHeight.CharacterWidthInPixels -
            selectionStartInPixels;

        // Tab keys a width of many characters
        {
            var tabsOnSameRowBeforeCursor = safeTextEditorReference
                .GetTabsCountOnSameRowBeforeCursor(
                    rowIndex,
                    selectionEndingColumnIndex);

            // 1 of the character width is already accounted for

            var extraWidthPerTabKey = TextEditorBase.TAB_WIDTH - 1;

            selectionWidthInPixels += extraWidthPerTabKey *
                                      tabsOnSameRowBeforeCursor *
                                      CharacterWidthAndRowHeight.CharacterWidthInPixels;
        }

        var widthCssStyleString = "width: ";

        if (fullWidthOfRowIsSelected)
            widthCssStyleString += "100%";
        else if (selectionStartingColumnIndex != 0 &&
                 upperBound > endOfRowTuple.positionIndex - 1)
            widthCssStyleString += $"calc(100% - {selectionStartInPixels}px);";
        else
            widthCssStyleString += $"{selectionWidthInPixels}px;";

        return $"{top} {height} {left} {widthCssStyleString}";
    }

    private void AppendTextEscaped(
        StringBuilder spanBuilder,
        RichCharacter richCharacter,
        string tabKeyOutput,
        string spaceKeyOutput)
    {
        switch (richCharacter.Value)
        {
            case '\t':
                spanBuilder.Append(tabKeyOutput);
                break;
            case ' ':
                spanBuilder.Append(spaceKeyOutput);
                break;
            case '\r':
                break;
            case '\n':
                break;
            case '<':
                spanBuilder.Append("&lt;");
                break;
            case '>':
                spanBuilder.Append("&gt;");
                break;
            case '"':
                spanBuilder.Append("&quot;");
                break;
            case '\'':
                spanBuilder.Append("&#39;");
                break;
            case '&':
                spanBuilder.Append("&amp;");
                break;
            default:
                spanBuilder.Append(richCharacter.Value);
                break;
        }
    }

    private VirtualizationResult<List<RichCharacter>>? EntriesProvider(
        VirtualizationRequest request)
    {
        if (CharacterWidthAndRowHeight is null ||
            TextEditorWidthAndHeight is null ||
            request.CancellationToken.IsCancellationRequested)
            return null;

        var safeTextEditorReference = TextEditorStatesSelection.Value;

        var verticalStartingIndex = (int)Math.Floor(
            request.ScrollPosition.ScrollTopInPixels /
            CharacterWidthAndRowHeight.RowHeightInPixels);

        var verticalTake = (int)Math.Ceiling(
            TextEditorWidthAndHeight.HeightInPixels /
            CharacterWidthAndRowHeight.RowHeightInPixels);

        if (verticalStartingIndex + verticalTake >
            safeTextEditorReference.RowEndingPositions.Length)
        {
            verticalTake = safeTextEditorReference.RowEndingPositions.Length -
                           verticalStartingIndex;
        }

        verticalTake = Math.Max(0, verticalTake);

        var horizontalStartingIndex = (int)Math.Floor(
            request.ScrollPosition.ScrollLeftInPixels /
            CharacterWidthAndRowHeight.CharacterWidthInPixels);

        var horizontalTake = (int)Math.Ceiling(
            TextEditorWidthAndHeight.WidthInPixels /
            CharacterWidthAndRowHeight.CharacterWidthInPixels);

        var virtualizedEntries = safeTextEditorReference
            .GetRows(verticalStartingIndex, verticalTake)
            .Select((row, index) =>
            {
                index += verticalStartingIndex;

                var localHorizontalTake = horizontalTake;

                if (horizontalStartingIndex + localHorizontalTake > row.Count)
                    localHorizontalTake = row.Count - horizontalStartingIndex;

                localHorizontalTake = Math.Max(0, localHorizontalTake);

                var horizontallyVirtualizedRow = row
                    .Skip(horizontalStartingIndex)
                    .Take(localHorizontalTake)
                    .ToList();

                var widthInPixels =
                    horizontallyVirtualizedRow.Count *
                    CharacterWidthAndRowHeight.CharacterWidthInPixels;

                var leftInPixels =
                    horizontalStartingIndex *
                    CharacterWidthAndRowHeight.CharacterWidthInPixels;

                var topInPixels =
                    index *
                    CharacterWidthAndRowHeight.RowHeightInPixels;

                return new VirtualizationEntry<List<RichCharacter>>(
                    index,
                    horizontallyVirtualizedRow,
                    widthInPixels,
                    CharacterWidthAndRowHeight.RowHeightInPixels,
                    leftInPixels,
                    topInPixels);
            }).ToImmutableArray();

        var totalWidth =
            safeTextEditorReference.MostCharactersOnASingleRow *
            CharacterWidthAndRowHeight.CharacterWidthInPixels;

        var totalHeight =
            safeTextEditorReference.RowEndingPositions.Length *
            CharacterWidthAndRowHeight.RowHeightInPixels;

        var leftBoundaryWidthInPixels =
            horizontalStartingIndex *
            CharacterWidthAndRowHeight.CharacterWidthInPixels;

        var leftBoundary = new VirtualizationBoundary(
            leftBoundaryWidthInPixels,
            null,
            0,
            0);

        var rightBoundaryLeftInPixels =
            leftBoundary.WidthInPixels +
            CharacterWidthAndRowHeight.CharacterWidthInPixels *
            horizontalTake;

        var rightBoundaryWidthInPixels =
            totalWidth -
            rightBoundaryLeftInPixels;

        var rightBoundary = new VirtualizationBoundary(
            rightBoundaryWidthInPixels,
            null,
            rightBoundaryLeftInPixels,
            0);

        var topBoundaryHeightInPixels =
            verticalStartingIndex *
            CharacterWidthAndRowHeight.RowHeightInPixels;

        var topBoundary = new VirtualizationBoundary(
            null,
            topBoundaryHeightInPixels,
            0,
            0);

        var bottomBoundaryTopInPixels =
            topBoundary.HeightInPixels +
            CharacterWidthAndRowHeight.RowHeightInPixels *
            verticalTake;

        var bottomBoundaryHeightInPixels =
            totalHeight -
            bottomBoundaryTopInPixels;

        var bottomBoundary = new VirtualizationBoundary(
            null,
            bottomBoundaryHeightInPixels,
            0,
            bottomBoundaryTopInPixels);

        return new VirtualizationResult<List<RichCharacter>>(
            virtualizedEntries,
            leftBoundary,
            rightBoundary,
            topBoundary,
            bottomBoundary);
    }
}
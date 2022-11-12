﻿using BlazorTextEditor.RazorLib;
using BlazorTextEditor.RazorLib.Row;
using BlazorTextEditor.RazorLib.Store.TextEditorCase;
using BlazorTextEditor.RazorLib.TextEditor;
using Fluxor;
using Microsoft.AspNetCore.Components;

namespace BlazorTextEditor.RazorLib.HelperComponents;

public partial class TextEditorFooter : ComponentBase
{
    [Inject]
    private ITextEditorService TextEditorService { get; set; } = null!;
    
    [Parameter, EditorRequired]
    public string? FileExtension { get; set; }

    private TextEditorDisplay? _textEditorDisplay;
    private TextEditorBase? _textEditorBase;
    
    public async Task ReRenderTextEditorFooterAsync(
        TextEditorHelperComponentParameters textEditorHelperComponentParameters)
    {
        _textEditorBase = textEditorHelperComponentParameters.TextEditorBase;
        _textEditorDisplay = textEditorHelperComponentParameters.TextEditorDisplay;
        
        await InvokeAsync(StateHasChanged);
    }

    private void SelectRowEndingKindOnChange(ChangeEventArgs changeEventArgs)
    {
        if (_textEditorBase is null)
            return;
        
        var textEditorKey = _textEditorBase.Key;

        var rowEndingKindString = (string)(changeEventArgs.Value ?? string.Empty);

        if (Enum.TryParse<RowEndingKind>(rowEndingKindString, out var rowEndingKind))
            TextEditorService.SetUsingRowEndingKind(textEditorKey, rowEndingKind);
    }
}
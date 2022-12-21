﻿using BlazorTextEditor.RazorLib.TextEditor;

namespace BlazorTextEditor.RazorLib.Store.TextEditorCase.Rewrite.ViewModels;

public record RegisterTextEditorViewModelAction(TextEditorKey TextEditorKey, TextEditorViewModelKey TextEditorViewModelKey);
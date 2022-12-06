﻿namespace BlazorTextEditor.RazorLib.Analysis.Json.Decoration;

public enum JsonDecorationKind
{
    None,
    PropertyKey,
    String,
    Number,
    Integer,
    Keyword,
    LineComment,
    BlockComment,
    Document,
    Error,
    Null,
}
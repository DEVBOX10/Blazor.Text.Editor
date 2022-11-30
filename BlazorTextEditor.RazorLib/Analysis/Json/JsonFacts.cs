﻿using System.Collections.Immutable;

namespace BlazorTextEditor.RazorLib.Analysis.Json;

public static class JsonFacts
{
    public const string COMMENT_LINE_START = "//";
    public static readonly ImmutableArray<char> COMMENT_LINE_ENDINGS = new[]
    {
        WhitespaceFacts.CARRIAGE_RETURN,
        WhitespaceFacts.LINE_FEED,
    }.ToImmutableArray();
    
    public const string COMMENT_BLOCK_START = "/*";
    public const string COMMENT_BLOCK_END = "*/";
    
    public const char OBJECT_START = '{';
    public const char OBJECT_END = '}';
    
    public const char PROPERTY_KEY_START = '"';
    public const char PROPERTY_KEY_END = '"';
    
    public const char PROPERTY_DELIMITER_BETWEEN_KEY_AND_VALUE = ':';
    
    public const char STRING_START = '"';
    public const char STRING_END = '"';
    
    public const char ARRAY_START = '[';
    public const char ARRAY_END = ']';
 
    public const char PROPERTY_ENTRY_DELIMITER = ',';
    public const char ARRAY_ENTRY_DELIMITER = ',';
}
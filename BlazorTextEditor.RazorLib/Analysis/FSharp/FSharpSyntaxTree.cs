﻿using System.Text;
using BlazorTextEditor.RazorLib.Lexing;

namespace BlazorTextEditor.RazorLib.Analysis.FSharp;

public class FSharpSyntaxTree
{
    public static List<TextEditorTextSpan> ParseText(string content)
    {
        // Will contain the final result which will be returned.
        var textEditorTextSpans = new List<TextEditorTextSpan>();
        
        // A single while loop will go character by character
        // until the end of the file for this method.
        var stringWalker = new StringWalker(content);
        
        stringWalker.WhileNotEndOfFile(() =>
        {
            var wordTuple = stringWalker.ConsumeWord();
            
            var foundKeyword = FSharpKeywords.ALL
                .FirstOrDefault(keyword =>
                    keyword == wordTuple.value);
        
            if (foundKeyword is not null)
            {
                textEditorTextSpans.Add(
                    wordTuple.textSpan with
                    {
                        DecorationByte = 
                        (byte)FSharpDecorationKind.Keyword 
                    });
            }

            return false;
        });

        return textEditorTextSpans;
    }
}
using BlazorTextEditor.RazorLib.Keyboard;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorTextEditor.RazorLib.TextEditor;

public class TextEditorCursor
{
    public TextEditorCursor()
    {
    }
    
    public TextEditorCursor((int rowIndex, int columnIndex) rowAndColumnIndex)
        : this()
    {
        IndexCoordinates = rowAndColumnIndex;
    }
    
    public (int rowIndex, int columnIndex) IndexCoordinates { get; set; }
    public int PreferredColumnIndex { get; set; }
    public TextCursorKind TextCursorKind { get; set; }
    public TextEditorSelection TextEditorSelection { get; } = new();
    public bool ShouldRevealCursor { get; set; }
    
    /// <summary>
    /// TODO: handle control modifier
    /// </summary>
    public static void MoveCursor(KeyboardEventArgs keyboardEventArgs, TextEditorCursor textEditorCursor, TextEditorBase textEditorBase)
    {
        var localIndexCoordinates = textEditorCursor.IndexCoordinates;
        var localPreferredColumnIndex = textEditorCursor.PreferredColumnIndex;

        var localTextEditorSelection = textEditorCursor.TextEditorSelection; 

        void MutateIndexCoordinatesAndPreferredColumnIndex(int columnIndex)
        {
            localIndexCoordinates.columnIndex = columnIndex;
            localPreferredColumnIndex = columnIndex;
        }

        if (keyboardEventArgs.ShiftKey)
        {
            if (localTextEditorSelection.AnchorPositionIndex is null ||
                localTextEditorSelection.EndingPositionIndex == localTextEditorSelection.AnchorPositionIndex)
            {
                var positionIndex = textEditorBase.GetPositionIndex(
                    localIndexCoordinates.rowIndex,
                    localIndexCoordinates.columnIndex);

                localTextEditorSelection.AnchorPositionIndex = positionIndex;
            }    
        }
        else
        {
            localTextEditorSelection.AnchorPositionIndex = null;
        }
        
        switch (keyboardEventArgs.Key)
        {
            case KeyboardKeyFacts.MovementKeys.ARROW_LEFT:
            {
                if (localIndexCoordinates.columnIndex == 0)
                {
                    if (localIndexCoordinates.rowIndex != 0)
                    {
                        localIndexCoordinates.rowIndex--;

                        var lengthOfRow = textEditorBase.GetLengthOfRow(localIndexCoordinates.rowIndex);
                        
                        MutateIndexCoordinatesAndPreferredColumnIndex(lengthOfRow);
                    }
                }
                else
                {
                    if (keyboardEventArgs.CtrlKey)
                    {
                        var columnIndexOfCharacterWithDifferingKind = textEditorBase
                            .GetColumnIndexOfCharacterWithDifferingKind(
                                localIndexCoordinates.rowIndex,
                                localIndexCoordinates.columnIndex,
                                true);

                        if (columnIndexOfCharacterWithDifferingKind == -1)
                        {
                            MutateIndexCoordinatesAndPreferredColumnIndex(0);
                        }
                        else
                        {
                            MutateIndexCoordinatesAndPreferredColumnIndex(
                                columnIndexOfCharacterWithDifferingKind);
                        }
                    }
                    else
                    {
                        MutateIndexCoordinatesAndPreferredColumnIndex(localIndexCoordinates.columnIndex - 1);
                    }
                }
                
                break;
            }
            case KeyboardKeyFacts.MovementKeys.ARROW_DOWN:
            {
                if (localIndexCoordinates.rowIndex < textEditorBase.RowCount - 1)
                {
                    localIndexCoordinates.rowIndex++;
                    
                    var lengthOfRow = textEditorBase.GetLengthOfRow(localIndexCoordinates.rowIndex);

                    localIndexCoordinates.columnIndex = lengthOfRow < localPreferredColumnIndex
                        ? lengthOfRow
                        : localPreferredColumnIndex;
                }
                
                break;
            }
            case KeyboardKeyFacts.MovementKeys.ARROW_UP:
            {
                if (localIndexCoordinates.rowIndex > 0)
                {
                    localIndexCoordinates.rowIndex--;
                    
                    var lengthOfRow = textEditorBase.GetLengthOfRow(localIndexCoordinates.rowIndex);

                    localIndexCoordinates.columnIndex = lengthOfRow < localPreferredColumnIndex
                        ? lengthOfRow
                        : localPreferredColumnIndex;
                }
                
                break;
            }
            case KeyboardKeyFacts.MovementKeys.ARROW_RIGHT:
            {
                var lengthOfRow = textEditorBase.GetLengthOfRow(localIndexCoordinates.rowIndex);
                
                if (localIndexCoordinates.columnIndex == lengthOfRow &&
                    localIndexCoordinates.rowIndex < textEditorBase.RowCount - 1)
                {
                    MutateIndexCoordinatesAndPreferredColumnIndex(0);
                    localIndexCoordinates.rowIndex++;
                }
                else if (localIndexCoordinates.columnIndex != lengthOfRow)
                {
                    if (keyboardEventArgs.CtrlKey)
                    {
                        var columnIndexOfCharacterWithDifferingKind = textEditorBase
                            .GetColumnIndexOfCharacterWithDifferingKind(
                                localIndexCoordinates.rowIndex,
                                localIndexCoordinates.columnIndex,
                                false);

                        if (columnIndexOfCharacterWithDifferingKind == -1)
                        {
                            MutateIndexCoordinatesAndPreferredColumnIndex(lengthOfRow);
                        }
                        else
                        {
                            MutateIndexCoordinatesAndPreferredColumnIndex(
                                columnIndexOfCharacterWithDifferingKind);
                        }
                    }
                    else
                    {
                        MutateIndexCoordinatesAndPreferredColumnIndex(localIndexCoordinates.columnIndex + 1);
                    }    
                }
                
                break;
            }
            case KeyboardKeyFacts.MovementKeys.HOME:
            {
                if (keyboardEventArgs.CtrlKey)
                    localIndexCoordinates.rowIndex = 0;
                
                MutateIndexCoordinatesAndPreferredColumnIndex(0);
                
                break;
            }
            case KeyboardKeyFacts.MovementKeys.END:
            {
                if (keyboardEventArgs.CtrlKey)
                    localIndexCoordinates.rowIndex = textEditorBase.RowCount - 1;
                    
                var lengthOfRow = textEditorBase.GetLengthOfRow(localIndexCoordinates.rowIndex);
                
                MutateIndexCoordinatesAndPreferredColumnIndex(lengthOfRow);
                
                break;
            }
        }

        textEditorCursor.IndexCoordinates = localIndexCoordinates;
        textEditorCursor.PreferredColumnIndex = localPreferredColumnIndex;

        if (keyboardEventArgs.ShiftKey)
        {
            var positionIndex = textEditorBase.GetPositionIndex(
                localIndexCoordinates.rowIndex,
                localIndexCoordinates.columnIndex);

            localTextEditorSelection.EndingPositionIndex = positionIndex;
            
            textEditorCursor.TextEditorSelection.AnchorPositionIndex = 
                localTextEditorSelection.AnchorPositionIndex;
                
            textEditorCursor.TextEditorSelection.EndingPositionIndex = 
                localTextEditorSelection.EndingPositionIndex;
        }
    }

    public string? GetSelectedText(TextEditorBase textEditorBase)
    {
        if (TextEditorSelection.AnchorPositionIndex.HasValue &&
            TextEditorSelection.AnchorPositionIndex.Value !=
            TextEditorSelection.EndingPositionIndex)
        {
            var lowerBound = TextEditorSelection.AnchorPositionIndex.Value;
            var upperBound = TextEditorSelection.EndingPositionIndex;

            if (lowerBound > upperBound)
            {
                (lowerBound, upperBound) = (upperBound, lowerBound);
            }

            var result = textEditorBase.GetTextRange(lowerBound,
                upperBound - lowerBound);

            return result.Length != 0
                ? result
                : null;
        }

        return null;
    }

    public bool HasSelectedText()
    {
        if (TextEditorSelection.AnchorPositionIndex.HasValue &&
            TextEditorSelection.AnchorPositionIndex.Value !=
            TextEditorSelection.EndingPositionIndex)
        {
            return true;
        }

        return false;
    }
}
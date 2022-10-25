namespace BlazorTextEditor.RazorLib.TextEditor;

/// <summary>
/// To select the first character in a TextEditor one would
/// set <see cref="AnchorPositionIndex"/> = 0 and
/// set <see cref="EndingPositionIndex"/> = 1
/// <br/><br/>
/// The <see cref="AnchorPositionIndex"/> does not select any text by itself.
/// One must visualize that the user's cursor is in <see cref="TextCursorKind.Beam"/> mode.
/// <br/><br/>
/// The <see cref="EndingPositionIndex"/> can then be set.
/// If <see cref="EndingPositionIndex"/> is less than <see cref="AnchorPositionIndex"/>
/// then <see cref="EndingPositionIndex"/> will be INCLUSIVE in respect to
/// selecting the character at that PositionIndex and <see cref="AnchorPositionIndex"/> will be EXCLUSIVE.
/// <br/><br/>
/// If <see cref="EndingPositionIndex"/> is greater than <see cref="AnchorPositionIndex"/> then
/// <see cref="EndingPositionIndex"/> will be EXCLUSIVE in respect to
/// selecting the character at that PositionIndex and <see cref="AnchorPositionIndex"/> will be INCLUSIVE.
/// <br/><br/>
/// If <see cref="EndingPositionIndex"/> is equal to <see cref="AnchorPositionIndex"/> then
/// no selection is active.
/// <br/><br/>
/// If <see cref="AnchorPositionIndex"/> is null then
/// no selection is active.
/// </summary>
public class TextEditorSelection
{
    public int? AnchorPositionIndex { get; set; }
    public int EndingPositionIndex { get; set; }
    
    public bool HasSelectedText()
    {
        if (AnchorPositionIndex.HasValue &&
            AnchorPositionIndex.Value !=
            EndingPositionIndex)
        {
            return true;
        }

        return false;
    }
    
    public string? GetSelectedText(TextEditorBase textEditorBase)
    {
        if (AnchorPositionIndex.HasValue &&
            AnchorPositionIndex.Value !=
            EndingPositionIndex)
        {
            var lowerBound = AnchorPositionIndex.Value;
            var upperBound = EndingPositionIndex;

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
}
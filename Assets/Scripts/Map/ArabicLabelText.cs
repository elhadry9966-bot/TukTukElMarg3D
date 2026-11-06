using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ArabicLabelText : MonoBehaviour
{
    [SerializeField] private TextMesh label;
    [SerializeField, TextArea] private string sourceText;
    [SerializeField, Min(8)] private int fontSize = 42;

    private static Font sharedArabicFont;
    private static bool warnedAboutFont;

    public void Configure(TextMesh textMesh, string value, int requestedFontSize)
    {
        label = textMesh;
        sourceText = value;
        fontSize = requestedFontSize;
        ApplyFontAndText();
    }

    private void OnEnable()
    {
        ApplyFontAndText();
    }

    private void ApplyFontAndText()
    {
        if (label == null)
            label = GetComponent<TextMesh>();
        if (label == null)
            return;

        label.characterSize = Mathf.Min(label.characterSize, 0.14f);
        label.text = ArabicTextLayout.ShapeAndReverse(sourceText);
        Font font = GetArabicFont();
        if (font == null)
            return;

        label.font = font;
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = font.material;
    }

    private Font GetArabicFont()
    {
        if (sharedArabicFont != null)
            return sharedArabicFont;

        sharedArabicFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Arial", "Tahoma" },
            fontSize);
        if (sharedArabicFont == null)
            sharedArabicFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (sharedArabicFont != null)
        {
            if (string.IsNullOrEmpty(sourceText))
                return sharedArabicFont;

            bool missingGlyph = false;
            string shapedText = ArabicTextLayout.ShapeAndReverse(sourceText);
            for (int i = 0; i < shapedText.Length; i++)
            {
                char character = shapedText[i];
                if (character >= '\uFE70' && character <= '\uFEFF' &&
                    !sharedArabicFont.HasCharacter(character))
                {
                    missingGlyph = true;
                    break;
                }
            }

            if (missingGlyph && !warnedAboutFont)
            {
                Debug.LogWarning(
                    "The selected Windows font is missing one or more Arabic glyphs. " +
                    "Install Arial or Tahoma to display neighborhood signs correctly.",
                    this);
                warnedAboutFont = true;
            }
        }

        return sharedArabicFont;
    }
}

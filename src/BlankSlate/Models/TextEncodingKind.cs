using System.Text;

namespace BlankSlate.Models;

/// <summary>The encodings BlankSlate offers, mirroring Notepad++'s Encoding menu.</summary>
public enum TextEncodingKind
{
    Utf8,
    Utf8Bom,
    Utf16LeBom,
    Utf16BeBom,
    Ansi,
}

public static class TextEncodings
{
    public static string GetLabel(TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Utf8 => "UTF-8",
        TextEncodingKind.Utf8Bom => "UTF-8-BOM",
        TextEncodingKind.Utf16LeBom => "UTF-16 LE BOM",
        TextEncodingKind.Utf16BeBom => "UTF-16 BE BOM",
        TextEncodingKind.Ansi => "ANSI",
        _ => kind.ToString(),
    };

    public static Encoding GetEncoding(TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Utf8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        TextEncodingKind.Utf8Bom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        TextEncodingKind.Utf16LeBom => Encoding.Unicode,
        TextEncodingKind.Utf16BeBom => Encoding.BigEndianUnicode,
        TextEncodingKind.Ansi => Encoding.GetEncoding(1252),
        _ => Encoding.UTF8,
    };

    /// <summary>
    /// Detects encoding from a BOM when present; otherwise tries strict UTF-8
    /// and falls back to ANSI (Windows-1252), matching Notepad++'s heuristic.
    /// </summary>
    public static (TextEncodingKind Kind, Encoding Encoding, string Text) DetectAndDecode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            var enc = GetEncoding(TextEncodingKind.Utf8Bom);
            return (TextEncodingKind.Utf8Bom, enc, Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            var enc = GetEncoding(TextEncodingKind.Utf16LeBom);
            return (TextEncodingKind.Utf16LeBom, enc, enc.GetString(bytes, 2, bytes.Length - 2));
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            var enc = GetEncoding(TextEncodingKind.Utf16BeBom);
            return (TextEncodingKind.Utf16BeBom, enc, enc.GetString(bytes, 2, bytes.Length - 2));
        }

        try
        {
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var text = strictUtf8.GetString(bytes);
            return (TextEncodingKind.Utf8, GetEncoding(TextEncodingKind.Utf8), text);
        }
        catch (DecoderFallbackException)
        {
            var ansi = GetEncoding(TextEncodingKind.Ansi);
            return (TextEncodingKind.Ansi, ansi, ansi.GetString(bytes));
        }
    }
}

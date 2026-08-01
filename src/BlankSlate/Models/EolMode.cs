using System.Text;

namespace BlankSlate.Models;

public enum EolMode { Crlf, Lf, Cr }

public static class EolModes
{
    public static string GetLabel(EolMode mode) => mode switch
    {
        EolMode.Crlf => "Windows (CR LF)",
        EolMode.Lf => "Unix (LF)",
        EolMode.Cr => "Macintosh (CR)",
        _ => mode.ToString(),
    };

    public static string GetTerminator(EolMode mode) => mode switch
    {
        EolMode.Crlf => "\r\n",
        EolMode.Cr => "\r",
        _ => "\n",
    };

    /// <summary>Detects the first line terminator found; defaults to LF (this app's platform default) for single-line/empty text.</summary>
    public static EolMode Detect(string text)
    {
        var idx = text.IndexOfAny(['\r', '\n']);
        if (idx == -1)
            return EolMode.Lf;
        if (text[idx] == '\n')
            return EolMode.Lf;
        return idx + 1 < text.Length && text[idx + 1] == '\n' ? EolMode.Crlf : EolMode.Cr;
    }

    /// <summary>Rewrites every line terminator (CRLF, LF, or CR) in <paramref name="text"/> to <paramref name="mode"/>'s terminator.</summary>
    public static string Normalize(string text, EolMode mode)
    {
        var terminator = GetTerminator(mode);
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r')
            {
                sb.Append(terminator);
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
            }
            else if (c == '\n')
            {
                sb.Append(terminator);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}

using System.Collections.Generic;

namespace BlankSlate.Models;

/// <summary>Contents of settings.json.</summary>
public sealed class AppSettingsData
{
    public bool WordWrap { get; set; }
    public bool ShowWhitespace { get; set; }
    public bool ShowEndOfLine { get; set; }
    public bool HighlightCurrentLine { get; set; } = true;
    public double FontSize { get; set; } = 13;
    public List<string> RecentFiles { get; set; } = [];
    public bool SessionSnapshotEnabled { get; set; } = true;
    public int BackupIntervalSeconds { get; set; } = 7;
}

/// <summary>One open tab as stored in session.json.</summary>
public sealed class SessionDocumentData
{
    public string? FilePath { get; set; }

    /// <summary>Backup file name (in the backup dir) holding unsaved content, when the buffer was dirty or untitled.</summary>
    public string? BackupFile { get; set; }

    public string? Title { get; set; }
    public string? LanguageId { get; set; }
    public TextEncodingKind EncodingKind { get; set; }
    public EolMode EolMode { get; set; }
    public int CaretLine { get; set; } = 1;
}

/// <summary>Contents of session.json.</summary>
public sealed class SessionData
{
    public int SelectedIndex { get; set; }
    public List<SessionDocumentData> Documents { get; set; } = [];
}

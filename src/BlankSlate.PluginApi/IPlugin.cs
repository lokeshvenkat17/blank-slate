namespace BlankSlate.Plugins;

/// <summary>
/// Implement this in a plugin assembly to be discovered by BlankSlate. The type must
/// be public, non-abstract, and have a public parameterless constructor.
/// </summary>
/// <remarks>
/// Notepad++ plugins are Win32 DLLs and are not compatible; this is a fresh .NET API.
/// Plugins run in-process with full trust — only install plugins you trust.
/// </remarks>
public interface IPlugin
{
    /// <summary>Display name shown in the Plugins menu and Plugin Manager.</summary>
    string Name { get; }

    /// <summary>One-line description shown in the Plugin Manager.</summary>
    string Description { get; }

    /// <summary>
    /// Called once after loading. Register commands and subscribe to events here.
    /// Exceptions are caught and reported in the Plugin Manager rather than crashing the editor.
    /// </summary>
    void Initialize(IPluginHost host);
}

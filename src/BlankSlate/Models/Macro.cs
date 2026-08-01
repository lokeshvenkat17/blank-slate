using System.Collections.Generic;
using Avalonia.Input;

namespace BlankSlate.Models;

public abstract record MacroStep;

/// <summary>Typed text captured from a TextInput event.</summary>
public sealed record MacroTextStep(string Text) : MacroStep;

/// <summary>A non-text key press (navigation, deletion, shortcuts).</summary>
public sealed record MacroKeyStep(Key Key, KeyModifiers Modifiers) : MacroStep;

public sealed class Macro
{
    public required string Name { get; set; }
    public List<MacroStep> Steps { get; set; } = [];
}

/// <summary>JSON-friendly shape for macros.json.</summary>
public sealed class MacroStepData
{
    public string Type { get; set; } = "text"; // "text" | "key"
    public string? Text { get; set; }
    public Key Key { get; set; }
    public KeyModifiers Modifiers { get; set; }

    public static MacroStepData From(MacroStep step) => step switch
    {
        MacroTextStep t => new MacroStepData { Type = "text", Text = t.Text },
        MacroKeyStep k => new MacroStepData { Type = "key", Key = k.Key, Modifiers = k.Modifiers },
        _ => new MacroStepData(),
    };

    public MacroStep ToStep() => Type == "key"
        ? new MacroKeyStep(Key, Modifiers)
        : new MacroTextStep(Text ?? "");
}

public sealed class MacroData
{
    public string Name { get; set; } = "";
    public List<MacroStepData> Steps { get; set; } = [];
}

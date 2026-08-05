using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace BlankSlate.Tests;

/// <summary>Guards the app's macOS identity: menu-bar name and our own About item.</summary>
public class AppIdentityTests
{
    [AvaloniaFact]
    public void ApplicationName_IsBlankSlate()
    {
        // Drives the macOS menu-bar title and "Hide <app>" entries.
        Assert.Equal("BlankSlate", Application.Current!.Name);
    }

    [AvaloniaFact]
    public void NativeAppMenu_HasOurAboutItem()
    {
        var menu = NativeMenu.GetMenu(Application.Current!);
        Assert.NotNull(menu);
        var headers = menu!.Items.OfType<NativeMenuItem>().Select(i => i.Header).ToList();
        Assert.Contains("About BlankSlate", headers);
    }
}

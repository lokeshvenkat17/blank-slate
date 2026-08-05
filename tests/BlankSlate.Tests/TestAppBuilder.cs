using Avalonia;
using Avalonia.Headless;
using BlankSlate;

[assembly: AvaloniaTestApplication(typeof(BlankSlate.Tests.TestAppBuilder))]

namespace BlankSlate.Tests;

public static class TestAppBuilder
{
    // UseHeadlessDrawing=false enables real Skia rendering so tests can capture frames.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

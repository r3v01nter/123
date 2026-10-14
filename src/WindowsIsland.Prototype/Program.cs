using System.Text;
using WindowsIsland.Core.Media;
using WindowsIsland.Core.Positioning;
using WindowsIsland.Prototype;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("=== WIMC Phase 1: Prototype Harness ===");

var topBounds = TaskbarDetector.CalculateIslandBounds(440, 44, IslandDockMode.TopCenter);
Console.WriteLine($"[Position] X={topBounds.X}, Y={topBounds.Y}, W={topBounds.Width}, H={topBounds.Height}, DPI={topBounds.Dpi}");

using IMediaSessionService media = new MediaSessionService();
media.MediaStateChanged += (_, info) =>
{
    if (info is null)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No Active Media");
        return;
    }
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {(info.IsPlaying ? "Playing" : "Paused")} | {info.Title} - {info.Artist} ({info.ThumbnailBytes?.Length ?? 0} B)");
};

await media.InitializeAsync();
Console.WriteLine("Controls: [W] Open Non-Activating Overlay  [P] Play/Pause  [N] Next  [B] Prev  [Q] Quit");

while (true)
{
    var key = Console.ReadKey(intercept: true).Key;
    if (key == ConsoleKey.Q) break;
    if (key == ConsoleKey.W)
    {
        Console.WriteLine("Starting Non-Activating Overlay (Click [X] on overlay to close)...");
        OverlayTestHarness.Run(media, IslandDockMode.TopCenter);
        continue;
    }

    bool ok = key switch
    {
        ConsoleKey.P or ConsoleKey.Spacebar => await media.TogglePlayPauseAsync(),
        ConsoleKey.N => await media.SkipNextAsync(),
        ConsoleKey.B => await media.SkipPreviousAsync(),
        _ => false
    };
    Console.WriteLine($" -> Action [{key}]: {ok}");
}

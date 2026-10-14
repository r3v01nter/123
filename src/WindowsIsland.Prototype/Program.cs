using System.Text;
using WindowsIsland.Core.Media;
using WindowsIsland.Core.Positioning;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("=== WIMC Phase 1.5: Core & GSMTC 最小验证原型 ===");

var topBounds = TaskbarDetector.CalculateIslandBounds(220, 48, IslandDockMode.TopCenter);
var dockBounds = TaskbarDetector.CalculateIslandBounds(220, 48, IslandDockMode.TaskbarAdjacent);
Console.WriteLine($"[定位探测-顶部居中] X={topBounds.X}, Y={topBounds.Y}, W={topBounds.Width}, H={topBounds.Height}, DPI={topBounds.Dpi}");
Console.WriteLine($"[定位探测-任务栏旁] X={dockBounds.X}, Y={dockBounds.Y}, W={dockBounds.Width}, H={dockBounds.Height}, Fallback={dockBounds.IsFallback}");

using IMediaSessionService media = new MediaSessionService();
media.MediaStateChanged += (_, info) =>
{
    if (info is null)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 当前无活动媒体会话");
        return;
    }

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 播放状态: {(info.IsPlaying ? "播放中" : "已暂停")} | " +
                      $"曲目: {info.Title} - {info.Artist} | " +
                      $"封面字节: {info.ThumbnailBytes?.Length ?? 0} B | " +
                      $"来源: {info.SourceAppId}");
};

await media.InitializeAsync();
Console.WriteLine("操作指令: [P]播放/暂停  [N]下一首  [B]上一首  [R]刷新  [Q]退出");

while (true)
{
    var key = Console.ReadKey(intercept: true).Key;
    if (key == ConsoleKey.Q)
        break;

    bool ok = key switch
    {
        ConsoleKey.P or ConsoleKey.Spacebar => await media.TogglePlayPauseAsync(),
        ConsoleKey.N => await media.SkipNextAsync(),
        ConsoleKey.B => await media.SkipPreviousAsync(),
        ConsoleKey.R => await RefreshAsync(media),
        _ => false
    };

    Console.WriteLine($" -> 指令 [{key}] 执行结果: {ok}");
}

static async Task<bool> RefreshAsync(IMediaSessionService svc)
{
    await svc.RefreshCurrentSessionAsync();
    return true;
}

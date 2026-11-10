using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HalawaHub.App;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;
using HalawaHub.Tests.Ui;
using Moq;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(HeadlessApp))]
// Loc وملفات البيانات حالة مشتركة، فالاختبارات لا تعمل بالتوازي
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HalawaHub.Tests.Ui;

public class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App.App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .WithInterFont();
}

/// <summary>يشغّل النافذة الحقيقية ببيانات ثابتة، ويضغط عناصرها كما يفعل المستخدم.</summary>
internal sealed class UiHarness : IDisposable
{
    public MainWindow Window { get; }
    public MainViewModel Vm { get; }

    public static readonly GameInfo[] SampleGames =
    {
        Game("cyberpunk", "Cyberpunk 2077", "GOG", @"D:\Games\Cyberpunk 2077"),
        Game("hades", "Hades II", "Steam", @"D:\Games\Hades II"),
        Game("valorant", "Valorant", "Riot Games", @"C:\Riot Games\VALORANT"),
        Game("forza", "Forza Horizon 5", "Xbox / Microsoft Store", @"C:\XboxGames\Forza"),
        Game("alan", "Alan Wake 2", "Epic Games", @"D:\Epic\AlanWake2"),
        Game("elden", "Elden Ring", "Steam", @"D:\Games\Elden Ring"),
    };

    private static GameInfo Game(string id, string name, string platform, string path) => new()
    {
        Id = id, Name = name, Platform = platform, InstallPath = path, ExecutablePath = path + @"\game.exe"
    };

    public UiHarness(string language = Loc.Arabic, IUpdateService? updates = null, GameInfo[]? games = null, bool motion = false, ManualResetEventSlim? scanGate = null)
    {
        // قبل إنشاء النافذة: بعض العناصر تقرأها مرة واحدة عند إنشائها
        MotionSettings.Enabled = motion;

        var cfg = ConfigService.Load();
        cfg.Language = language;
        cfg.SteamGridDbApiKey = "";
        cfg.ShowHiddenGames = false;
        cfg.SortMode = "name";
        cfg.SortReverse = false;
        cfg.ShowPlayTimeOnCards = true;
        ConfigService.Save(cfg);

        var library = new Mock<ILibraryService>();
        library.Setup(l => l.ScanAllLibraries(It.IsAny<IEnumerable<IGameLibraryProvider>>(), It.IsAny<IEnumerable<IGameTool>>()))
            .Returns(() =>
            {
                scanGate?.Wait(TimeSpan.FromSeconds(30));
                var result = new LibraryScanResult();
                result.Games.AddRange((games ?? SampleGames).Select(Clone));
                return result;
            });

        if (updates == null)
        {
            var mock = new Mock<IUpdateService>();
            mock.Setup(u => u.CheckForUpdateAsync())
                .ReturnsAsync(new UpdateCheckResult(false, null, null, null, "ok"));
            updates = mock.Object;
        }

        Vm = new MainViewModel(library.Object, new FilterService(), updates, startBackgroundTasks: false);
        Window = new MainWindow { DataContext = Vm, Width = 1280, Height = 860 };
        Window.Show();
        Settle();
        WaitFor(() => !Vm.IsRefreshing);

        // في التطبيق يبدأ هذا الفحص وحده عند الفتح، والاختبارات توقف المهام الخلفية
        Vm.CheckForUpdatesNowCommand.Execute(null);
        WaitFor(() => !Vm.IsCheckingForUpdates);
        Settle();
    }

    private static GameInfo Clone(GameInfo g) => new()
    {
        Id = g.Id, Name = g.Name, Platform = g.Platform, InstallPath = g.InstallPath,
        ExecutablePath = g.ExecutablePath, IsInstalled = g.IsInstalled, CoverImageUrl = g.CoverImageUrl
    };

    public void Settle()
    {
        for (var i = 0; i < 6; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    public void WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("UI condition not met");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
    }

    public IEnumerable<T> All<T>() where T : Visual =>
        Window.GetVisualDescendants().OfType<T>().Where(v => v.IsEffectivelyVisible);

    /// زر يعرض هذا النص، سواء كان نصًا مباشرًا أو داخل عناصره
    public Button ButtonWithText(string text)
    {
        foreach (var button in All<Button>())
        {
            if (button.Content is string s && s == text) return button;
            if (button.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text)) return button;
        }
        throw new InvalidOperationException($"no visible button with text: {text}");
    }

    /// ضغطة ماوس حقيقية عند موضع الزر: تفشل إذا غطاه عنصر آخر أو تحرك عن مكانه
    public void Click(Button button)
    {
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), Window)
            ?? throw new InvalidOperationException("button is not in the window");

        Window.MouseMove(center);
        Window.MouseDown(center, MouseButton.Left);
        Window.MouseUp(center, MouseButton.Left);
        Settle();
    }

    public void Click(string text) => Click(ButtonWithText(text));

    public void Dispose()
    {
        Window.Close();
        Settle();

        // لا نترك اللغة لبقية الاختبارات، لا في الذاكرة ولا في config.json المؤقت
        MotionSettings.Enabled = false;
        Loc.Instance.Language = Loc.Arabic;
        var cfg = ConfigService.Load();
        cfg.Language = Loc.Arabic;
        ConfigService.Save(cfg);
    }
}

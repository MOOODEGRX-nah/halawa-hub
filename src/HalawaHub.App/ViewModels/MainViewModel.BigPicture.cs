using HalawaHub.App.BigPicture;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Sessions;

namespace HalawaHub.App.ViewModels;

/// <summary>ما تستعمله الشاشة الكبيرة من النافذة العادية: نفس التشغيل وتتبع وقت اللعب.</summary>
public partial class MainViewModel
{
    internal AppConfig Config => _config;

    internal void SaveBigPictureSettings()
    {
        SaveConfig();
        OnPropertyChanged(nameof(BigPictureOnController));
        BigPictureSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// زر الشاشة الكبيرة في الشريط العلوي
    public event EventHandler? BigPictureRequested;
    /// تغيّر إعداد يخص يد التحكم (فتح عند التوصيل...)
    public event EventHandler? BigPictureSettingsChanged;

    private RelayCommand? _openBigPictureCommand;
    public RelayCommand OpenBigPictureCommand => _openBigPictureCommand ??= new RelayCommand(_ => BigPictureRequested?.Invoke(this, EventArgs.Empty));

    /// الإعدادات › عام: "الشاشة الكبيرة عند توصيل يد تحكم" (نفس إعداد الشاشة الكبيرة)
    public bool BigPictureOnController
    {
        get => _config.BigPicture?.OpenOnController ?? true;
        set
        {
            _config.BigPicture ??= new BpSettings();
            if (_config.BigPicture.OpenOnController == value) return;
            _config.BigPicture.OpenOnController = value;
            SaveBigPictureSettings();
        }
    }

    /// <summary>
    /// نفس فحص ما قبل التشغيل؛ ما يمنع التشغيل يعود للشاشة الكبيرة بسببه الحقيقي بدل
    /// شريط الحالة. Steam بلا حساب يُعرض فشلًا هنا (كما في التصميم) ولا تُفتح جلسة.
    /// </summary>
    internal BpLaunchResult LaunchFromBigPicture(GameCardViewModel card)
    {
        var (problem, detail) = Preflight.Check(card.Game);
        if (problem != LaunchProblem.None && (LaunchPreflight.BlocksLaunch(problem) || problem == LaunchProblem.SteamNotSignedIn))
        {
            var message = LaunchProblemMessage(problem, card.Name, detail) ?? "";
            Log.Info($"big picture launch blocked for {card.Game.Name}: {problem} ({detail})");
            if (LaunchPreflight.BlocksLaunch(problem)) Notify("launch", message, card);
            var reason = Loc.Instance[problem switch
            {
                LaunchProblem.DriveNotConnected => "bpWhyDrive",
                LaunchProblem.GameFilesMissing => "bpWhyFiles",
                LaunchProblem.LauncherNotFound => "bpWhyLauncher",
                _ => "bpWhySteam",
            }];
            return new BpLaunchResult(false, reason, message, CanOpenLauncher: problem != LaunchProblem.LauncherNotFound && card.CanOpenInLauncher);
        }

        LaunchGameNow(card);
        return BpLaunchResult.Success;
    }

    internal void MarkNotificationsReadFromBigPicture() => UnreadNotifications = 0;

    internal void OpenInLauncherFromBigPicture(GameCardViewModel card) => OpenInLauncher(card);

    // ===== إعدادات اللعبة من الشاشة الكبيرة =====

    /// اسم جديد للعبة، ثم إعادة الترتيب كما عند إغلاق نافذة اللعبة
    internal void RenameFromBigPicture(GameCardViewModel card, string name)
    {
        card.EditableName = name;
        ApplyFilter();
    }

    /// «تحقق واحفظ» من الشاشة الكبيرة: true إن قبله SteamGridDB
    internal async System.Threading.Tasks.Task<bool> SaveSgdbKeyFromBigPictureAsync(string key)
    {
        SteamGridDbApiKey = key;
        await VerifyApiKeyAsync();
        return ApiKeyStatus == ApiKeyCheckState.Valid;
    }
}

using HalawaHub.App.BigPicture;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Sessions;

namespace HalawaHub.App.ViewModels;

/// <summary>ما تستعمله الشاشة الكبيرة من النافذة العادية: نفس التشغيل وتتبع وقت اللعب.</summary>
public partial class MainViewModel
{
    internal AppConfig Config => _config;

    internal void SaveBigPictureSettings() => SaveConfig();

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
            if (LaunchPreflight.BlocksLaunch(problem)) Notify("launch", message);
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
}

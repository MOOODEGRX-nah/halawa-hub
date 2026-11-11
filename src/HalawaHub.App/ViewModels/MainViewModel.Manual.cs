using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Library;

namespace HalawaHub.App.ViewModels;

/// إضافة لعبة يدويًا، وتغيير ملفها، وإزالتها من Halawa (لا يُحذف أي ملف من القرص)
public partial class MainViewModel
{
    internal string ManualGamesPath { get; set; } = ManualGames.DefaultPath;

    /// يضيف الملف الذي اختاره المستخدم ثم يفتح نافذة اللعبة لتعديل اسمها وغلافها
    public async Task AddManualGameAsync(string file)
    {
        var record = ManualGames.FromFile(file);
        if (record == null)
        {
            ShowStatus(Loc.Instance.Format("addGameFailed", System.IO.Path.GetFileName(file)));
            return;
        }

        try
        {
            var all = ManualGames.Load(ManualGamesPath);
            all.Add(record);
            ManualGames.Save(ManualGamesPath, all);
        }
        catch (Exception ex)
        {
            Log.Error("manual-games.json: save failed", ex);
            ShowStatus(Loc.Instance.Format("addGameFailed", record.Name));
            return;
        }

        Log.Info($"manual game added: {record.Name} -> {record.Target}");
        await RefreshLibraryAndWaitAsync();
        var card = Games.FirstOrDefault(c => c.Game.Platform == ManualGames.Platform && c.Game.Id == record.Id);
        if (card != null) OpenDetailsCommand.Execute(card);
    }

    public async Task ChangeManualGameFileAsync(GameCardViewModel card, string file)
    {
        var fresh = ManualGames.FromFile(file);
        if (fresh == null)
        {
            ShowStatus(Loc.Instance.Format("addGameFailed", System.IO.Path.GetFileName(file)));
            return;
        }

        try
        {
            var all = ManualGames.Load(ManualGamesPath);
            var record = all.FirstOrDefault(r => r.Id == card.Game.Id);
            if (record == null) return;
            record.Target = fresh.Target;
            record.Arguments = fresh.Arguments;
            record.InstallDir = fresh.InstallDir;
            ManualGames.Save(ManualGamesPath, all);
        }
        catch (Exception ex)
        {
            Log.Error("manual-games.json: save failed", ex);
            ShowStatus(Loc.Instance.Format("addGameFailed", card.Name));
            return;
        }

        CloseDetails();
        await RefreshLibraryAndWaitAsync();
    }

    public async Task RemoveManualGameAsync(GameCardViewModel card)
    {
        try
        {
            var all = ManualGames.Load(ManualGamesPath);
            if (all.RemoveAll(r => r.Id == card.Game.Id) == 0) return;
            ManualGames.Save(ManualGamesPath, all);
        }
        catch (Exception ex)
        {
            Log.Error("manual-games.json: save failed", ex);
            ShowStatus(Loc.Instance.Format("addGameFailed", card.Name));
            return;
        }
        CloseDetails();
        await RefreshLibraryAndWaitAsync();
    }

    /// فحص يُنتظر حتى نهايته (طلب أثناء فحص جارٍ ينتظر الجاري ثم يعيد)
    private async Task RefreshLibraryAndWaitAsync()
    {
        while (_scanRunner.IsRunning) await Task.Delay(100);
        await RefreshLibraryAsync();
    }
}

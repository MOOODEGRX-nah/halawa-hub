using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Store;

namespace HalawaHub.App.ViewModels;

/// <summary>
/// قسم "التفاصيل" في نافذة لعبة Steam: الوصف، المطوّر والناشر، تاريخ الإصدار، شارة
/// Steam Deck، متطلبات التشغيل (ويندوز ولينكس)، ولقطات الشاشة مع عارض. البيانات من
/// متجر Steam وتُحفظ على القرص؛ بلا إنترنت تُعرض آخر نسخة محفوظة أو رسالة واضحة.
/// </summary>
public partial class MainViewModel
{
    public const int DetailsSection = 6;

    /// ترتيب أقسام نافذة اللعبة كما تظهر (أرقام الأقسام ثابتة)
    private List<int> GameDialogOrder()
    {
        var order = new List<int> { 0 };
        if (DetailsGame?.HasNews == true) order.Add(DetailsSection);
        order.AddRange(new[] { 1, 2, 3 });
        if (DetailsGame?.HasNews == true) order.Add(NewsSection);
        if (DetailsGame?.HasTools == true) order.Add(ToolsSection);
        return order;
    }

    public int GameDialogSectionCount => GameDialogOrder().Count;
    public int GameDialogPillIndex => Math.Max(0, GameDialogOrder().IndexOf(GameDialogSection));

    private SteamAppDetails? _store;
    private DeckCompatibility _deck;
    private string? _storeLoadedFor;

    public ObservableCollection<SteamScreenshot> StoreScreenshots { get; } = new();

    private bool _isStoreLoading;
    public bool IsStoreLoading
    {
        get => _isStoreLoading;
        private set { if (_isStoreLoading == value) return; _isStoreLoading = value; RaiseStore(); }
    }

    public bool HasStoreInfo => _store != null;
    public bool HasNoStoreInfo => !_isStoreLoading && _store == null;
    public string StoreDescription => _store?.Description ?? "";
    public string StoreDeveloper => _store?.Developers ?? "";
    public string StorePublisher => _store?.Publishers ?? "";
    public string StoreRelease => _store?.ReleaseDate ?? "";
    public string? StoreHeader => _store?.HeaderImage;
    public bool HasStoreHeader => !string.IsNullOrEmpty(_store?.HeaderImage);
    public string StoreWindowsReq => _store?.WindowsRequirements ?? "";
    public string StoreLinuxReq => _store?.LinuxRequirements ?? "";
    public bool HasWindowsReq => StoreWindowsReq.Length > 0;
    public bool HasLinuxReq => StoreLinuxReq.Length > 0;
    public bool HasScreenshots => StoreScreenshots.Count > 0;

    /// الشارة تظهر فقط حين يعرف Steam النتيجة
    public bool HasDeckBadge => _store != null && _deck != DeckCompatibility.Unknown;
    public string DeckText => Loc.Instance[_deck switch
    {
        DeckCompatibility.Verified => "deckVerified",
        DeckCompatibility.Playable => "deckPlayable",
        DeckCompatibility.Unsupported => "deckUnsupported",
        _ => "deckUnknown"
    }];

    /// 1 أخضر (موثَّقة)، 3 كهرماني (قابلة للعب)، 0 رمادي (غير مدعومة)
    public int DeckTone => _deck switch { DeckCompatibility.Verified => 1, DeckCompatibility.Playable => 3, _ => 0 };

    /// للاختبارات: بدل الشبكة
    internal Func<string, Task<(SteamAppDetails? Details, DeckCompatibility Deck)>>? FetchStoreOverride { get; set; }

    internal async Task LoadStoreDetailsAsync(GameCardViewModel card)
    {
        if (!card.HasNews) return;
        var appId = card.Game.Id;
        var key = appId + "|" + Loc.Instance.Language;
        if (_storeLoadedFor == key) return;
        _storeLoadedFor = key;
        _store = null;
        _deck = DeckCompatibility.Unknown;
        StoreScreenshots.Clear();
        IsStoreLoading = true;

        try
        {
            var language = Loc.Instance.Language;
            var (details, deck) = FetchStoreOverride != null
                ? await FetchStoreOverride(appId)
                : (await SteamStoreClient.GetDetailsAsync(appId, language), await SteamStoreClient.GetDeckAsync(appId));
            if (_storeLoadedFor != key) return;
            _store = details;
            _deck = deck;
            foreach (var s in details?.Screenshots ?? Array.Empty<SteamScreenshot>()) StoreScreenshots.Add(s);
        }
        catch (Exception ex)
        {
            Log.Error($"store details for {appId} failed", ex);
        }
        finally
        {
            if (_storeLoadedFor == key) IsStoreLoading = false;
            RaiseStore();
        }
    }

    private void RaiseStore()
    {
        foreach (var n in new[] { nameof(IsStoreLoading), nameof(HasStoreInfo), nameof(HasNoStoreInfo), nameof(StoreDescription), nameof(StoreDeveloper),
                     nameof(StorePublisher), nameof(StoreRelease), nameof(StoreHeader), nameof(HasStoreHeader), nameof(StoreWindowsReq), nameof(StoreLinuxReq),
                     nameof(HasWindowsReq), nameof(HasLinuxReq), nameof(HasScreenshots), nameof(HasDeckBadge), nameof(DeckText), nameof(DeckTone) })
            OnPropertyChanged(n);
    }

    // ===== عارض اللقطات =====

    private int _mediaIndex = -1;
    public bool IsMediaOpen => _mediaIndex >= 0 && _mediaIndex < StoreScreenshots.Count;
    public string? MediaUrl => IsMediaOpen ? StoreScreenshots[_mediaIndex].FullUrl : null;
    public string MediaCounter => IsMediaOpen ? $"{_mediaIndex + 1} / {StoreScreenshots.Count}" : "";

    private RelayCommand? _openMediaCommand, _mediaNextCommand, _mediaPrevCommand, _closeMediaCommand;
    public RelayCommand OpenMediaCommand => _openMediaCommand ??= new RelayCommand(p => { if (p is SteamScreenshot s) SetMedia(StoreScreenshots.IndexOf(s)); });
    public RelayCommand MediaNextCommand => _mediaNextCommand ??= new RelayCommand(_ => StepMedia(1));
    public RelayCommand MediaPrevCommand => _mediaPrevCommand ??= new RelayCommand(_ => StepMedia(-1));
    public RelayCommand CloseMediaCommand => _closeMediaCommand ??= new RelayCommand(_ => SetMedia(-1));

    public void StepMedia(int delta)
    {
        if (!IsMediaOpen) return;
        SetMedia((_mediaIndex + delta + StoreScreenshots.Count) % StoreScreenshots.Count);
    }

    private void SetMedia(int index)
    {
        _mediaIndex = index;
        OnPropertyChanged(nameof(IsMediaOpen));
        OnPropertyChanged(nameof(MediaUrl));
        OnPropertyChanged(nameof(MediaCounter));
    }
}

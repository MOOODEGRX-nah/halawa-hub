using System;
using System.Collections.Generic;
using System.Linq;
using HalawaHub.App.BigPicture.Input;

namespace HalawaHub.App.BigPicture;

/// <summary>تلميح في الشريط السفلي: رمز الزر (a, b, x, y, menu) ونصه.</summary>
public sealed record BpHint(string Button, string Label);

public sealed partial class BigPictureViewModel
{
    private PadInfo? _pad;

    public PadInfo? Pad => _pad;
    public bool HasPad => _pad != null;
    public bool HasBattery => _pad is { Battery: >= 0 };
    /// عدد الأشرطة المضيئة من أربعة
    public int BatteryBars => _pad is { Battery: >= 0 } p ? System.Math.Clamp((p.Battery + 24) / 25, 1, 4) : 0;

    /// رموز PlayStation (✕ ○ □ △): حسب الإعداد، أو حسب اليد الموصولة في "تلقائي"
    public bool UsePsGlyphs => Settings.Glyphs == "ps" || (Settings.Glyphs == "auto" && _pad?.Kind == PadKind.PlayStation);

    public void SetPad(PadInfo? pad)
    {
        _pad = pad;
        Changed();
    }

    public string ContextPadText => T(_pad != null ? "bpController" : "bpNoController");

    /// "حجم النص: كبير": اللوحة نفسها أصغر (1745 بدل 1920) فيكبر كل شيء بنحو 10% عند التحجيم
    public double CanvasWidth => Settings.TextSize == "large" ? 1745 : 1920;
    public double CanvasHeight => Math.Round(CanvasWidth * 1080 / 1920);
    public double ResultsWidth => Settings.TextSize == "large" ? 500 : 600;

    public bool IsHomeTab => TabScreen == BpScreen.Home;
    public bool IsLibraryTab => TabScreen == BpScreen.Library;
    public bool IsSearchTab => TabScreen == BpScreen.Search;
    public BpUpper Upper => BpUpper.Instance;

    public string ContextText => _overlay == BpOverlay.Guide ? ""
        : _screen == BpScreen.Library && _overlay == BpOverlay.None ? LibraryCountText
        : T(_pad != null ? "bpController" : "bpNoController");

    private IReadOnlyList<BpHint> _hints = Array.Empty<BpHint>();

    /// نفس القائمة تُعاد ما لم تتغير، حتى لا يُعاد بناء الشريط السفلي مع كل ضغطة
    public IReadOnlyList<BpHint> Hints
    {
        get
        {
            var next = BuildHints();
            if (!next.SequenceEqual(_hints)) _hints = next;
            return _hints;
        }
    }

    private IReadOnlyList<BpHint> BuildHints()
    {
        {
            // مع تبديل التأكيد والرجوع يتبدل الرمزان في التلميحات أيضًا
            var a = Settings.SwapConfirmBack ? "b" : "a";
            var b = Settings.SwapConfirmBack ? "a" : "b";
            BpHint H(string k, string label) => new(k == "a" ? a : k == "b" ? b : k, T(label));

            return _overlay switch
            {
                BpOverlay.Guide => new[] { H("a", "bpSelect"), H("b", "bpClose") },
                BpOverlay.Notifications => NotifItems.Count > 0
                    ? new[] { H("a", "bpSelect"), H("x", "bpNtDismiss"), H("y", "bpNtClear"), H("b", "bpClose") }
                    : new[] { H("b", "bpClose") },
                BpOverlay.Update => new[] { H("a", "bpSelect"), H("b", "bpCancel") },
                BpOverlay.Launch => LaunchState == BpLaunchState.Failed
                    ? new[] { H("a", "bpSelect"), H("b", "bpBack") }
                    : new[] { H("b", "bpCancel") },
                _ => _screen switch
                {
                    BpScreen.Home => new[] { H("a", _homeRow == 2 ? "bpOpen" : "bpSelect"), H("y", "bpSearch"), H("menu", "bpGuide") },
                    BpScreen.Library => new[] { H("a", "bpOpen"), H("x", "bpFilter"), H("b", "bpBack"), H("menu", "bpGuide") },
                    BpScreen.Game => new[] { H("a", "bpSelect"), H("b", "bpBack"), H("menu", "bpGuide") },
                    BpScreen.Search => new[] { H("a", "bpType"), H("b", "bpDel"), H("menu", "bpGuide") },
                    _ => new[] { H("a", "bpSelect"), H("b", "bpBack") },
                },
            };
        }
    }
}

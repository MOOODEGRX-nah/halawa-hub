using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace HalawaHub.App.Services;

/// <summary>
/// الاستخدام في XAML: Text="{l:Loc play}". يربط بفهرس Loc.Instance فيتحدث النص
/// فور تغيير اللغة، وفي أي DataContext (بطاقات اللعب، النوافذ...).
/// </summary>
public sealed class LocExtension
{
    public LocExtension(string key) => Key = key;

    public string Key { get; }

    public IBinding ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBindingExtension($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay
        }.ProvideValue(serviceProvider);
}

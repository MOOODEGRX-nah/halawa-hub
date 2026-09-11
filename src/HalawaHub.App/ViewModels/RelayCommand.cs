using System.Windows.Input;

namespace HalawaHub.App.ViewModels;

/// تطبيق بسيط لـ ICommand بدون الحاجة لمكتبة MVVM خارجية
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged;

    /// يستدعى يدويًا كل ما يتغيّر شيء يأثر على CanExecute
    /// (Avalonia ما عنده تحديث تلقائي زي CommandManager بـ WPF)
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

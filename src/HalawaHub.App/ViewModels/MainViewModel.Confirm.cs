using System.Collections.Generic;
using System.Linq;
using HalawaHub.Core;

namespace HalawaHub.App.ViewModels;

/// نافذة التأكيد العامة: طابور طلبات يُعرض واحدًا تلو الآخر
public partial class MainViewModel
{
    private readonly Queue<ConfirmRequest> _confirmQueue = new();
    private ConfirmRequest? _confirm;
    private RelayCommand? _chooseConfirmCommand;
    private RelayCommand? _cancelConfirmCommand;

    public ConfirmRequest? Confirm
    {
        get => _confirm;
        private set
        {
            if (ReferenceEquals(_confirm, value)) return;
            _confirm = value;
            OnPropertyChanged(nameof(Confirm));
            OnPropertyChanged(nameof(IsConfirmOpen));
        }
    }

    public bool IsConfirmOpen => _confirm != null;

    public RelayCommand ChooseConfirmCommand =>
        _chooseConfirmCommand ??= new RelayCommand(param => ChooseConfirm(param as ConfirmChoice));

    /// ✕ و Esc = آخر خيار (دائمًا "إلغاء" أو "ليس الآن")
    public RelayCommand CancelConfirmCommand =>
        _cancelConfirmCommand ??= new RelayCommand(_ => ChooseConfirm(_confirm?.Choices.LastOrDefault()));

    internal void RequestConfirm(ConfirmRequest request)
    {
        if (_confirm == null) Confirm = request;
        else _confirmQueue.Enqueue(request);
    }

    internal void ChooseConfirm(ConfirmChoice? choice)
    {
        Confirm = _confirmQueue.Count > 0 ? _confirmQueue.Dequeue() : null;
        if (choice?.Run == null) return;
        try
        {
            choice.Run();
        }
        catch (System.Exception ex)
        {
            Log.Error($"confirm action '{choice.Label}' failed", ex);
        }
    }
}

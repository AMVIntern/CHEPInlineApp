using ChepInlineApp.Base;
using ChepInlineApp.Stores;
using ChepInlineApp.UserControls.SubControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static ChepInlineApp.Stores.ModalStore;

namespace ChepInlineApp.ViewModels
{
    public partial class TextInputModalViewModel : ViewModelBase
    {
        [ObservableProperty]
        public string title;

        [ObservableProperty]
        public string message;

        [ObservableProperty]
        public string inputText;

        private readonly ModalStore _modalStore;
        private readonly TaskCompletionSource<TextInputResult> _tcs;

        [RelayCommand]
        public void OkButton()
        {
            _modalStore.CloseModal();
            _tcs.TrySetResult(new TextInputResult(InputText?.Trim(), true));
        }

        [RelayCommand]
        public void CancelButton()
        {
            _modalStore.CloseModal();
            _tcs.TrySetResult(new TextInputResult(null, false));
        }

        public TextInputModalViewModel(string title, string message, ModalStore modalStore, TaskCompletionSource<TextInputResult> tcs)
        {
            Title = title;
            Message = message;
            _modalStore = modalStore;
            _tcs = tcs;
        }

    }
}

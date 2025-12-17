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

namespace ChepInlineApp.ViewModels
{
    public partial class ConfirmModalViewModel : ViewModelBase
    {
        [ObservableProperty]
        public string title;

        [ObservableProperty]
        public string message;

        private readonly ModalStore _modalStore;
        private readonly TaskCompletionSource<bool> _tcs;

        [RelayCommand]
        public void YesButton()
        {
            _modalStore.CloseModal();
            _tcs.TrySetResult(true);
        }

        [RelayCommand]
        public void NoButton()
        {
            _modalStore.CloseModal();
            _tcs.TrySetResult(false);
        }

        public ConfirmModalViewModel(string title, string message, ModalStore modalStore, TaskCompletionSource<bool> tcs)
        {
            Title = title;
            Message = message;
            _modalStore = modalStore;
            _tcs = tcs;
        }
    }
}

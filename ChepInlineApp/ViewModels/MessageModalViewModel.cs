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
    public partial class MessageModalViewModel : ViewModelBase
    {
        [ObservableProperty]
        public string title;

        [ObservableProperty]
        public string message;

        private readonly ModalStore _modalStore;

        [RelayCommand]
        public void OkButton()
        {
            _modalStore.CloseModal();
        }

        public MessageModalViewModel(string title, string message, ModalStore modalStore)
        {
            Title = title;
            Message = message;
            _modalStore = modalStore;
        }

    }
}

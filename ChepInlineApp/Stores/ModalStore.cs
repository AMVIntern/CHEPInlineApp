using ChepInlineApp.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using ChepInlineApp.ViewModels;

namespace ChepInlineApp.Stores
{
    public class ModalStore : ObservableObject
    {
        private ViewModelBase? _modalViewModel;

        public ViewModelBase? ModalViewModel
        {
            get { return _modalViewModel; }
            set
            {
                _modalViewModel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsModalOpen));
            }
        }

        public bool IsModalOpen => ModalViewModel != null;

        public void ShowModal(ViewModelBase viewModel)
        {
            ModalViewModel = viewModel;
        }

        public void CloseModal()
        {
            ModalViewModel = null;
        }
        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            var tcs = new TaskCompletionSource<bool>();
            ShowModal(new ConfirmModalViewModel(title, message, this, tcs));
            return await tcs.Task;
        }

        public record TextInputResult(string? Value, bool WasConfirmed);
        public async Task<TextInputResult> ShowTextInputAsync(string title, string message)
        {
            var tcs = new TaskCompletionSource<TextInputResult>();
            ShowModal(new TextInputModalViewModel(title, message, this, tcs));
            return await tcs.Task;
        }
    }
}

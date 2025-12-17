using ChepInlineApp.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChepInlineApp.Navigation.Stores
{
    public class NavigationStore
    {
        public event Action? CurrentViewModelChanged;

        public void OnCurrentViewModelChanged()
        {
            CurrentViewModelChanged?.Invoke();
        }

        private ViewModelBase _currentViewModel;

        public ViewModelBase CurrentViewModel
        {
            get
            {
                return _currentViewModel;
            }
            set
            {
                if (_currentViewModel != value && _currentViewModel is IDisposable disposable && !_retainViewModels.Contains(_currentViewModel))
                    disposable.Dispose();

                _currentViewModel = value;
                OnCurrentViewModelChanged();
            }
        }

        private readonly HashSet<ViewModelBase> _retainViewModels = new();

        public void RetainViewModel(ViewModelBase viewModel)
        {
            _retainViewModels.Add(viewModel);
        }

        public void ForceNavigateTo(ViewModelBase viewModel)
        {
            // If it's already the current VM, manually trigger change
            if (_currentViewModel == viewModel)
            {
                OnCurrentViewModelChanged(); // Force UI to update
            }
            else
            {
                CurrentViewModel = viewModel;
            }
        }
    }
}

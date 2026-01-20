using AppWatchdog.UI.WPF.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppWatchdog.UI.WPF.Services
{
    public partial class BackendStateService : ObservableObject
    {
        [ObservableProperty]
        private BackendState _state = BackendState.Unknown;

        [ObservableProperty]
        private string _statusMessage = "Backend unbekannt";

        public bool IsReady => State == BackendState.Ready;

        public void SetReady(string message = "Backend verfügbar")
        {
            State = BackendState.Ready;
            StatusMessage = message;
        }

        public void SetOffline(string message)
        {
            State = BackendState.Offline;
            StatusMessage = message;
        }

        public void SetError(string message)
        {
            State = BackendState.Error;
            StatusMessage = message;
        }

        partial void OnStateChanged(BackendState value)
        {
            OnPropertyChanged(nameof(IsReady));
        }

    }

}

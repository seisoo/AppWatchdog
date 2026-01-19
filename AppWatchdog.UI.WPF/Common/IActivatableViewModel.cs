using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppWatchdog.UI.WPF.Common
{
    public interface IActivatableViewModel
    {
        Task ActivateAsync();
        void Deactivate();
    }

}

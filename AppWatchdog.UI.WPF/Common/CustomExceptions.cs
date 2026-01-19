using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppWatchdog.UI.WPF.Common
{
    public sealed class PipeTimeoutException : Exception
    {
        public PipeTimeoutException(string message, Exception inner)
            : base(message, inner) { }
    }

    public sealed class PipeUnavailableException : Exception
    {
        public PipeUnavailableException(string message, Exception inner)
            : base(message, inner) { }
    }

}

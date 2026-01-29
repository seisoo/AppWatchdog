using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppWatchdog.Service.Helpers
{
    /// <summary>
    /// Provides file system helper methods.
    /// </summary>
    public static class FileHelper
    {
        /// <summary>
        /// Attempts to delete a file and ignores any errors.
        /// </summary>
        /// <param name="path">Path to the file.</param>
        public static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace ElStar_Macro
{
    class Program
    {
        static void Main(string[] args) => new App().StartAsync().GetAwaiter().GetResult();
    }
    public static class ProgramUtils
    {
        public static async Task WithAggregateException(this Task source)
        {
            try
            {
                await source.ConfigureAwait(false);
            }
            catch
            {
                // source.Exception may be null if the task was canceled.
                if (source.Exception == null)
                    throw;

                // EDI preserves the original exception's stack trace, if any.
                ExceptionDispatchInfo.Capture(source.Exception).Throw();
            }
        }
    }
}

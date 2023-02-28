using System;
using System.Runtime.InteropServices;

namespace Fleck.Helpers
{
    internal static class FleckRuntime
    {
        public static bool IsRunningOnMono()
        {
            return Type.GetType("Mono.Runtime") != null;
        }

        public static bool IsRunningOnWindows()
        {
            return true;
            /*
#if  NET40 || NET45 || NET451 || NET452 || NET46 || NET461
            return true;
#else
            return (RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
#endif
            */
        }
    }
}
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        private const int CpuUtilizationHigh = 95;
        private const int CpuUtilizationLow = 80;
        private static int s_cpuUtilization = 85; // TODO: Add calculation for CPU utilization
        
        private static int s_minThreads; // TODO: Initialize
        private static int s_maxThreads; // TODO: Initialize

        // TODO: SetMinThreads and SetMaxThreads need to be synchronized with a lock
        // TODO: Compare with CoreCLR implementation and ensure this has the same guarantees.
        public static bool SetMinThreads(int threads)
        {
            if (threads < 0 || threads > s_maxThreads)
            {
                return false;
            }
            else
            {
                s_minThreads = threads;
                return true;
            }
        }

        public static int GetMinThreads() => s_minThreads;

        public static bool SetMaxThreads(int threads)
        {
            if (threads < s_minThreads || threads == 0)
            {
                return false;
            }
            else
            {
                s_maxThreads = threads;
                return true;
            }
        }

        public static int GetMaxThreads() => s_maxThreads;
    }
}

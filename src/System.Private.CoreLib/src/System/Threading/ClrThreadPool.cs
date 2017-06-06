using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        private const int CpuUtilizationHigh = 95;
        private const int CpuUtilizationLow = 80;
        private static int s_cpuUtilization = 85; // TODO: Add calculation for CPU utilization
        
        private static int s_minThreads;
        private static int s_maxThreads;

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
            if (threads < ThreadPoolGlobals.processorCount || threads < s_minThreads)
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

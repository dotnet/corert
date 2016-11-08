// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace System
{
    public static partial class Environment
    {
        internal static long TickCount64
        {
            get
            {
                return (long)Interop.Sys.GetTickCount64();
            }
        }

        public unsafe static String ExpandEnvironmentVariables(String name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (name.Length == 0)
            {
                return name;
            }

            int currentSize = 100;
            StringBuilder blob = new StringBuilder(currentSize); // A somewhat reasonable default size

            int lastPos = 0, pos;
            while (lastPos < name.Length && (pos = name.IndexOf('%', lastPos + 1)) >= 0)
            {
                if (name[lastPos] == '%')
                {
                    string key = name.Substring(lastPos + 1, pos - lastPos - 1);
                    string value = Environment.GetEnvironmentVariable(key);
                    if (value != null)
                    {
                        blob.Append(value);
                        lastPos = pos + 1;
                        continue;
                    }
                }
                blob.Append(name.Substring(lastPos, pos - lastPos));
                lastPos = pos;
            }
            blob.Append(name.Substring(lastPos));

            return blob.ToString();
        }

        public static int ProcessorCount => (int)Interop.Sys.SysConf(Interop.Sys.SysConfName._SC_NPROCESSORS_ONLN);

        public unsafe static String GetEnvironmentVariable(String variable)
        {
            if (variable == null)
                throw new ArgumentNullException(nameof(variable));

            IntPtr result;
            int size = Interop.Sys.GetEnvironmentVariable(variable, out result);

            // The size can be -1 if the environment variable's size overflows an integer
            if (size == -1)
                throw new OverflowException();

            if (result == IntPtr.Zero)
                return null;

            return Encoding.UTF8.GetString((byte*)result, size);
        }

        public static void Exit(int exitCode)
        {
            // CORERT-TODO: Shut down the runtime
            Interop.Sys.ExitProcess(exitCode);
        }
    }
}

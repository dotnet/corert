// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace System
{
    public static partial class Environment
    {
        public unsafe static String GetEnvironmentVariable(String variable)
        {
            if (variable == null)
                throw new ArgumentNullException("variable");

            // The convention of the API is as follows:
            // You call the API with a buffer of a given size. 
            // If the size of the buffer is insufficient to hold the value, 
            //   the API will return the required size for the buffer. 
            // In that case we resize the buffer and try again.

            int currentSize = 128;
            char* blob = stackalloc char[currentSize];   // A somewhat reasonable default size

            int requiredSize;
            fixed (char* pText = variable)
            {
                requiredSize = Interop.mincore.GetEnvironmentVariable(pText, blob, currentSize);
            }

            if (requiredSize == 0)
            {
                return null;
            }

            if (requiredSize <= currentSize)
            {
                return new string(blob);
            }

            // Fallback to using heap allocated buffers.
            char[] newblob = null;
            while (requiredSize > currentSize)
            {
                currentSize = requiredSize;
                // need to retry since the environment variable might be changed 
                newblob = new char[currentSize];
                fixed (char* pText = variable, pBlob = newblob)
                {
                    requiredSize = Interop.mincore.GetEnvironmentVariable(pText, pBlob, currentSize);
                }

                if (requiredSize == 0)
                {
                    return null;
                }
            }
            // We should never end up with a null blob
            Diagnostics.Debug.Assert(newblob != null);

            return new string(newblob);
        }

        public static void Exit(int exitCode)
        {
            // CORERT-TODO: Shut down the runtime
            Interop.mincore.ExitProcess(exitCode);
        }

        public static string MachineName
        {
            get
            {
                const int MaxMachineNameLength = 256;

                // In future release of operating systems, you might be able to rename a machine without
                // rebooting.  Therefore, don't cache this machine name.
                StringBuilder buf = new StringBuilder(MaxMachineNameLength);
                int len = MaxMachineNameLength;
                if (!Interop.mincore.GetComputerName(buf, ref len))
                    throw new InvalidOperationException(SR.InvalidOperation_ComputerName);

                return buf.ToString();
            }
        }
    }
}

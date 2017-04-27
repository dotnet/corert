// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;

namespace System
{
    internal static partial class Environment
    {
        public static String GetEnvironmentVariable(String variable)
        {
            if (variable == null)
                throw new ArgumentNullException(nameof(variable));

            // The convention of the API is as follows:
            // You call the API with a buffer of a given size. 
            // If the size of the buffer is insufficient to hold the value, 
            //   the API will return the required size for the buffer. 
            // In that case we resize the buffer and try again.

            int currentSize = 128;
            for (;;)
            {
                char[] buffer = ArrayPool<char>.Shared.Rent(currentSize);

                int actualSize = Interop.mincore.GetEnvironmentVariable(variable, buffer, buffer.Length);
                if (actualSize <= buffer.Length)
                {
                    string result = (actualSize != 0) ? new string(buffer, 0, actualSize) : null;
                    ArrayPool<char>.Shared.Return(buffer);
                    return result;
                }

                ArrayPool<char>.Shared.Return(buffer);
                currentSize = actualSize;
            }
        }
    }
}

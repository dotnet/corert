// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    /// <summary>For internal use only.  Exposes runtime functionality to the Environments implementation in corefx.</summary>
    public static partial class EnvironmentAugments
    {
        public static string GetEnvironmentVariable(string variable)
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

        public static IDictionary GetEnvironmentVariables()
        {
            if ("".Length != 0)
                throw new NotImplementedException(); // TODO: https://github.com/dotnet/corert/issues/3688 Need to implement GetEnvironmentVariables() properly.
            return new LowLevelListDictionary();
        }

        public static IDictionary GetEnvironmentVariables(EnvironmentVariableTarget target) { throw new NotImplementedException(); }
        public static string GetEnvironmentVariable(string variable, EnvironmentVariableTarget target) { throw new NotImplementedException(); }
        public static void SetEnvironmentVariable(string variable, string value) { throw new NotImplementedException(); }
        public static void SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target) { throw new NotImplementedException(); }
    }
}

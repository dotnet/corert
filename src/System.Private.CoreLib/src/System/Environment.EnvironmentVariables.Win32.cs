// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
**
** Purpose: Provides some basic access to some environment 
** functionality.
**
**
============================================================*/

using System.Text;
using System.Collections;

namespace System
{
    public static partial class Environment
    {
        public unsafe static String ExpandEnvironmentVariables(String name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (name.Length == 0)
            {
                return name;
            }

            int currentSize = 128;
            char* blob = stackalloc char[currentSize]; // A somewhat reasonable default size
            int requiredSize;
            fixed (char* pName = name)
            {
                requiredSize = Interop.mincore.ExpandEnvironmentStrings(pName, blob, currentSize);
            }

            if (requiredSize == 0)
            {
                // TODO: This used to throw an exception:
                // Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                throw new ArgumentException();
            }

            if (requiredSize <= currentSize)
            {
                return new string(blob);
            }

            // Fallback to using heap allocated buffers.
            char[] newBlob = null;
            while (requiredSize > currentSize)
            {
                currentSize = requiredSize;
                newBlob = new char[currentSize];

                fixed (char* pName = name, pBlob = newBlob)
                {
                    requiredSize = Interop.mincore.ExpandEnvironmentStrings(pName, pBlob, currentSize);
                }

                if (requiredSize == 0)
                {
                    // TODO: This used to throw an exception:
                    // Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    throw new ArgumentException();
                }
            }

            return new string(newBlob);
        }

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
    }
}

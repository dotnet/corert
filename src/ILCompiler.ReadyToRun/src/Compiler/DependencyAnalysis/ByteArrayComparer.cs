// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

using Internal.NativeFormat;
using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static ByteArrayComparer Instance = new ByteArrayComparer();
        
        public bool Equals(byte[] a, byte[] b)
        {
            if (a == b)
            {
                return true;
            }
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int index = 0; index < a.Length; index++)
            {
                if (a[index] != b[index])
                {
                    return false;
                }
            }
            return true;
        }
        
        public int GetHashCode(byte[] a)
        {
            int hash = unchecked(5381 + (a.Length << 7));
            for (int index = 0; index < a.Length; index++)
            {
                hash = unchecked(((hash << 5) + hash) ^ a[index]);
            }
            return hash;
        }
    }
}

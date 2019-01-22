// Licensed to the.NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Reflection.Emit
{
    public class DynamicILInfo
    {
        public DynamicMethod DynamicMethod { get { return default; } }

        public void SetCode(byte[] code, int maxStackSize)
        {
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
        }

        [CLSCompliant(false)]
        public unsafe void SetCode(byte* code, int codeSize, int maxStackSize)
        {
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
        }

        public void SetExceptions(byte[] exceptions)
        {
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
        }

        [CLSCompliant(false)]
        public unsafe void SetExceptions(byte* exceptions, int exceptionsSize)
        {
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
        }

        public void SetLocalSignature(byte[] localSignature)
        {
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
        }

        [CLSCompliant(false)]
        public unsafe void SetLocalSignature(byte* localSignature, int signatureSize)
        {
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
        }

        public int GetTokenFor(RuntimeMethodHandle method)
        {
            return default;
        }
        public int GetTokenFor(DynamicMethod method)
        {
            return default;
        }
        public int GetTokenFor(RuntimeMethodHandle method, RuntimeTypeHandle contextType)
        {
            return default;
        }
        public int GetTokenFor(RuntimeFieldHandle field)
        {
            return default;
        }
        public int GetTokenFor(RuntimeFieldHandle field, RuntimeTypeHandle contextType)
        {
            return default;
        }
        public int GetTokenFor(RuntimeTypeHandle type)
        {
            return default;
        }
        public int GetTokenFor(string literal)
        {
            return default;
        }
        public int GetTokenFor(byte[] signature)
        {
            return default;
        }
    }
}
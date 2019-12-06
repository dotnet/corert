// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;
using System.Text;

namespace LLVMSharp
{
    internal static unsafe class SpanExtensions
    {
        public static string AsString(this Span<byte> self) => AsString((ReadOnlySpan<byte>)self);

        public static string AsString(this ReadOnlySpan<byte> self)
        {
            if (self.IsEmpty)
            {
                return string.Empty;
            }

            fixed (byte* pSelf = self)
            {
                return Encoding.UTF8.GetString(pSelf, self.Length);
            }
        }
    }
}

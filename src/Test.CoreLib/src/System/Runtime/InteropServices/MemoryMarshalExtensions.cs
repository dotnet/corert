// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.InteropServices
{
    public static class MemoryMarshal
    {
        public static Span<T> CreateSpan<T>(ref T reference, int length) => new Span<T>(ref reference, length);

        public static ReadOnlySpan<T> CreateReadOnlySpan<T>(ref T reference, int length) => new ReadOnlySpan<T>(ref reference, length);

        public static ref T GetReference<T>(Span<T> span) => ref span._pointer.Value;

        public static ref T GetReference<T>(ReadOnlySpan<T> span) => ref span._pointer.Value;
    }
}

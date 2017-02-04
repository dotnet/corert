// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Security
{
    [System.CLSCompliant(false)]
    public sealed class SecureString : IDisposable
    {
        public SecureString() { throw null; }
        public unsafe SecureString(char* value, int length) { throw null; }
        public int Length { get { throw null; } }
        public void AppendChar(char c) { throw null; }
        public void Clear() { throw null; }
        public SecureString Copy() { throw null; }
        public void Dispose() { throw null; }
        public void InsertAt(int index, char c) { throw null; }
        public bool IsReadOnly() { throw null; }
        public void MakeReadOnly() { throw null; }
        public void RemoveAt(int index) { throw null; }
        public void SetAt(int index, char c) { throw null; }
    }
}

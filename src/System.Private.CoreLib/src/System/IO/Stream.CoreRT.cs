// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO
{
    public abstract partial class Stream
    {
        private bool HasOverriddenBeginEndRead() => true;

        private bool HasOverriddenBeginEndWrite() => true;
    }
}

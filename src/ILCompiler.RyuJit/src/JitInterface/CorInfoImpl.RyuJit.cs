// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;

using ILCompiler;

namespace Internal.JitInterface
{
    partial class CorInfoImpl
    {
        private Compilation _compilation;

        public CorInfoImpl(Compilation compilation, JitConfigProvider jitConfig)
            : this(jitConfig)
        {
            _compilation = compilation;
        }
    }
}

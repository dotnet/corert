// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
  Type:  MissingRuntimeArtifactException
**
==============================================================*/

using global::System;

namespace System.Reflection
{
    internal sealed class MissingRuntimeArtifactException : MemberAccessException
    {
        public MissingRuntimeArtifactException()
        {
        }

        public MissingRuntimeArtifactException(String message)
            : base(message)
        {
        }
    }
}

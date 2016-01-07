// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


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

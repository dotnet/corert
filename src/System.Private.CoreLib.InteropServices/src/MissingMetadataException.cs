// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;

namespace System.Reflection
{
    public sealed class MissingMetadataException : TypeAccessException
    {
        public MissingMetadataException()
        {
        }

        public MissingMetadataException(String message)
            : base(message)
        {
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//

//

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Module, Inherited = false)]
    public sealed class DefaultCharSetAttribute : Attribute
    {
        internal CharSet _CharSet;

        public DefaultCharSetAttribute(CharSet charSet)
        {
            _CharSet = charSet;
        }

        public CharSet CharSet { get { return _CharSet; } }
    }
}

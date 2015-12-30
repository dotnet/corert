// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

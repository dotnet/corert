// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ComDefaultInterfaceAttribute : Attribute
    {
        internal Type _val;

        public ComDefaultInterfaceAttribute(Type defaultInterface)
        {
            _val = defaultInterface;
        }

        public Type Value { get { return _val; } }
    }
}

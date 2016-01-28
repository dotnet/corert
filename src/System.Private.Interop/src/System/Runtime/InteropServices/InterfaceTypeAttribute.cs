// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    public sealed class InterfaceTypeAttribute : Attribute
    {
        internal ComInterfaceType _val;
        public InterfaceTypeAttribute(ComInterfaceType interfaceType)
        {
            _val = interfaceType;
        }
        public InterfaceTypeAttribute(short interfaceType)
        {
            _val = (ComInterfaceType)interfaceType;
        }
        public ComInterfaceType Value { get { return _val; } }
    }
}

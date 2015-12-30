// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

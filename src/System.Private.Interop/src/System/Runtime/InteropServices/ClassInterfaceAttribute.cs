// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, Inherited = false)]
    public sealed class ClassInterfaceAttribute : Attribute
    {
        internal ClassInterfaceType _val;
        public ClassInterfaceAttribute(ClassInterfaceType classInterfaceType)
        {
            _val = classInterfaceType;
        }
        public ClassInterfaceAttribute(short classInterfaceType)
        {
            _val = (ClassInterfaceType)classInterfaceType;
        }
        public ClassInterfaceType Value { get { return _val; } }
    }
}

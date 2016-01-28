// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//

using System;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    // DefaultInterfaceAttribute marks a WinRT class (or interface group) that has its default interface specified.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class DefaultInterfaceAttribute : Attribute
    {
        private Type m_defaultInterface;

        public DefaultInterfaceAttribute(Type defaultInterface)
        {
            m_defaultInterface = defaultInterface;
        }

        public Type DefaultInterface
        {
            get { return m_defaultInterface; }
        }
    }
}

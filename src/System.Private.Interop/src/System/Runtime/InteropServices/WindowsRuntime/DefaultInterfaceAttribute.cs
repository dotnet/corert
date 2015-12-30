// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

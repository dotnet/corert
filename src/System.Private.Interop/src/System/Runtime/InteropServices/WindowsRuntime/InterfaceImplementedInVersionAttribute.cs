// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//

using System;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    // This attribute is applied to class interfaces in a generated projection assembly.  It is used by Visual Studio
    // and other tools to find out what version of a component (eg. Windows) a WinRT class began to implement
    // a particular interfaces.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = true)]
    public sealed class InterfaceImplementedInVersionAttribute : Attribute
    {
        public InterfaceImplementedInVersionAttribute(Type interfaceType, byte majorVersion, byte minorVersion, byte buildVersion, byte revisionVersion)
        {
            m_interfaceType = interfaceType;
            m_majorVersion = majorVersion;
            m_minorVersion = minorVersion;
            m_buildVersion = buildVersion;
            m_revisionVersion = revisionVersion;
        }

        public Type InterfaceType
        {
            get { return m_interfaceType; }
        }

        public byte MajorVersion
        {
            get { return m_majorVersion; }
        }

        public byte MinorVersion
        {
            get { return m_minorVersion; }
        }

        public byte BuildVersion
        {
            get { return m_buildVersion; }
        }

        public byte RevisionVersion
        {
            get { return m_revisionVersion; }
        }

        private Type m_interfaceType;
        private byte m_majorVersion;
        private byte m_minorVersion;
        private byte m_buildVersion;
        private byte m_revisionVersion;
    }
}

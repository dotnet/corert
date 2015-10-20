// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class InternalsVisibleToAttribute : Attribute
    {
        private string _assemblyName;

        public InternalsVisibleToAttribute(string assemblyName)
        {
            _assemblyName = assemblyName;
        }

        public string AssemblyName
        {
            get
            {
                return _assemblyName;
            }
        }
    }

    /// <summary>
    ///     If AllInternalsVisible is not true for a friend assembly, the FriendAccessAllowed attribute
    ///     indicates which internals are shared with that friend assembly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Enum |
                    AttributeTargets.Event |
                    AttributeTargets.Field |
                    AttributeTargets.Interface |
                    AttributeTargets.Method |
                    AttributeTargets.Property |
                    AttributeTargets.Struct,
        AllowMultiple = false,
        Inherited = false)]
    [FriendAccessAllowed]
    internal sealed class FriendAccessAllowedAttribute : Attribute
    {
    }
}


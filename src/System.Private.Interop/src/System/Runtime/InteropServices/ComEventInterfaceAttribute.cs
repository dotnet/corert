// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    public sealed class ComEventInterfaceAttribute : Attribute
    {
        internal Type _SourceInterface;
        internal Type _EventProvider;

        public ComEventInterfaceAttribute(Type SourceInterface, Type EventProvider)
        {
            _SourceInterface = SourceInterface;
            _EventProvider = EventProvider;
        }

        public Type SourceInterface { get { return _SourceInterface; } }
        public Type EventProvider { get { return _EventProvider; } }
    }
}

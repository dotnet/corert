// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Reflection;
using Internal.Metadata.NativeFormat;
using System.Threading;
using Debug = System.Diagnostics.Debug;

using Internal.TypeSystem;

namespace Internal.TypeSystem.NativeFormat
{
    // This file has implementations of the .Interfaces.cs logic from its base type.

    public sealed partial class NativeFormatType : MetadataType
    {
        private DefType[] _implementedInterfaces;

        public override DefType[] ExplicitlyImplementedInterfaces
        {
            get
            {
                if (_implementedInterfaces == null)
                    return InitializeImplementedInterfaces();
                return _implementedInterfaces;
            }
        }

        private DefType[] InitializeImplementedInterfaces()
        {
            var interfaceHandles = _typeDefinition.Interfaces;

            int count = interfaceHandles.Count;
            if (count == 0)
                return (_implementedInterfaces = Array.Empty<DefType>());

            DefType[] implementedInterfaces = new DefType[count];
            int i = 0;
            foreach (var interfaceHandle in interfaceHandles)
            {
                implementedInterfaces[i++] = (DefType)_metadataUnit.GetType(interfaceHandle);
            }

            // TODO Add duplicate detection

            return (_implementedInterfaces = implementedInterfaces);
        }
    }
}

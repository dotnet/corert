// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.TypeInfos;

using Internal.Reflection.Tracing;

using System.Reflection.Metadata;

namespace System.Reflection.Runtime.TypeInfos.EcmaFormat
{
    internal sealed partial class EcmaFormatRuntimeGenericParameterTypeInfoForMethods : EcmaFormatRuntimeGenericParameterTypeInfo, IKeyedItem<EcmaFormatRuntimeGenericParameterTypeInfoForMethods.UnificationKey>
    {
        //
        // Key for unification.
        //
        internal struct UnificationKey : IEquatable<UnificationKey>
        {
            public UnificationKey(RuntimeNamedMethodInfo methodOwner, MetadataReader reader, GenericParameterHandle genericParameterHandle)
            {
                MethodOwner = methodOwner;
                GenericParameterHandle = genericParameterHandle;
                Reader = reader;
            }

            public RuntimeNamedMethodInfo MethodOwner { get; }
            public MetadataReader Reader { get; }
            public GenericParameterHandle GenericParameterHandle { get; }

            public override bool Equals(object obj)
            {
                if (!(obj is UnificationKey other))
                    return false;
                return Equals(other);
            }

            public bool Equals(UnificationKey other)
            {
                if (!(GenericParameterHandle.Equals(other.GenericParameterHandle)))
                    return false;
                if (!(Reader == other.Reader))
                    return false;
                if (!MethodOwner.Equals(other.MethodOwner))
                    return false;
                return true;
            }

            public override int GetHashCode()
            {
                return GenericParameterHandle.GetHashCode();
            }
        }
    }
}


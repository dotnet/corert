// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.Dispensers;
using System.Reflection.Runtime.FieldInfos;
using System.Reflection.Runtime.EventInfos;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.PropertyInfos;

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.TypeInfos
{
    //================================================================================================================
    // TypeInfoCachedData objects are allocated on-demand on a per-TypeInfo basis to cache hot data for key scenarios.
    // To maximize throughput once the cache is created, the object creates all of its internal caches up front
    // and holds entries strongly (and relying on the fact that TypeInfos themselves are held weakly to avoid immortality.)
    //
    // Note that it is possible that two threads racing to query the same TypeInfo may allocate and query two different
    // CachedData objecs. Thus, this object must not be relied upon to preserve object identity.
    //================================================================================================================
    internal sealed class TypeInfoCachedData
    {
        public TypeInfoCachedData(RuntimeTypeInfo runtimeTypeInfo)
        {
            _runtimeTypeInfo = runtimeTypeInfo;
            _methodLookupDispenser = new DispenserThatAlwaysReuses<String, RuntimeMethodInfo>(LookupDeclaredMethodByName);
            _fieldLookupDispenser = new DispenserThatAlwaysReuses<String, RuntimeFieldInfo>(LookupDeclaredFieldByName);
            _propertyLookupDispenser = new DispenserThatAlwaysReuses<String, RuntimePropertyInfo>(LookupDeclaredPropertyByName);
            _eventLookupDispenser = new DispenserThatAlwaysReuses<String, RuntimeEventInfo>(LookupDeclaredEventByName);
        }

        public RuntimeMethodInfo GetDeclaredMethod(String name)
        {
            return _methodLookupDispenser.GetOrAdd(name);
        }

        public RuntimeFieldInfo GetDeclaredField(String name)
        {
            return _fieldLookupDispenser.GetOrAdd(name);
        }

        public RuntimePropertyInfo GetDeclaredProperty(String name)
        {
            return _propertyLookupDispenser.GetOrAdd(name);
        }

        public RuntimeEventInfo GetDeclaredEvent(String name)
        {
            return _eventLookupDispenser.GetOrAdd(name);
        }


        private readonly Dispenser<String, RuntimeMethodInfo> _methodLookupDispenser;

        private RuntimeMethodInfo LookupDeclaredMethodByName(String name)
        {
            RuntimeNamedTypeInfo definingType = _runtimeTypeInfo.AnchoringTypeDefinitionForDeclaredMembers;
            IEnumerator<RuntimeMethodInfo> matches = _runtimeTypeInfo.GetDeclaredMethodsInternal(definingType, name).GetEnumerator();
            if (!matches.MoveNext())
                return null;
            RuntimeMethodInfo result = matches.Current;
            if (matches.MoveNext())
                throw new AmbiguousMatchException();
            return result;
        }

        private readonly Dispenser<String, RuntimeFieldInfo> _fieldLookupDispenser;

        private RuntimeFieldInfo LookupDeclaredFieldByName(String name)
        {
            RuntimeNamedTypeInfo definingType = _runtimeTypeInfo.AnchoringTypeDefinitionForDeclaredMembers;
            IEnumerator<RuntimeFieldInfo> matches = _runtimeTypeInfo.GetDeclaredFieldsInternal(definingType, name).GetEnumerator();
            if (!matches.MoveNext())
                return null;
            RuntimeFieldInfo result = matches.Current;
            if (matches.MoveNext())
                throw new AmbiguousMatchException();
            return result;
        }

        private readonly Dispenser<String, RuntimePropertyInfo> _propertyLookupDispenser;

        private RuntimePropertyInfo LookupDeclaredPropertyByName(String name)
        {
            RuntimeNamedTypeInfo definingType = _runtimeTypeInfo.AnchoringTypeDefinitionForDeclaredMembers;
            IEnumerator<RuntimePropertyInfo> matches = _runtimeTypeInfo.GetDeclaredPropertiesInternal(definingType, name).GetEnumerator();
            if (!matches.MoveNext())
                return null;
            RuntimePropertyInfo result = matches.Current;
            if (matches.MoveNext())
                throw new AmbiguousMatchException();
            return result;
        }


        private readonly Dispenser<String, RuntimeEventInfo> _eventLookupDispenser;

        private RuntimeEventInfo LookupDeclaredEventByName(String name)
        {
            RuntimeNamedTypeInfo definingType = _runtimeTypeInfo.AnchoringTypeDefinitionForDeclaredMembers;
            IEnumerator<RuntimeEventInfo> matches = _runtimeTypeInfo.GetDeclaredEventsInternal(definingType, name).GetEnumerator();
            if (!matches.MoveNext())
                return null;
            RuntimeEventInfo result = matches.Current;
            if (matches.MoveNext())
                throw new AmbiguousMatchException();
            return result;
        }

        private readonly RuntimeTypeInfo _runtimeTypeInfo;
    }
}

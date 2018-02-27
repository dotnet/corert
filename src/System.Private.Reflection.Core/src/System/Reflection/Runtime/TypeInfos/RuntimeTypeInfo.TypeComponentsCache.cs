// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.BindingFlagSupport;

using Unsafe = Internal.Runtime.CompilerServices.Unsafe;

using EnumInfo = Internal.Runtime.Augments.EnumInfo;

namespace System.Reflection.Runtime.TypeInfos
{
    internal abstract partial class RuntimeTypeInfo
    {
        //================================================================================================================
        // TypeComponentsCache objects are allocated on-demand on a per-Type basis to cache hot data for key scenarios.
        // To maximize throughput once the cache is created, the object creates all of its internal caches up front
        // and holds entries strongly (and relying on the fact that Types themselves are held weakly to avoid immortality.)
        //
        // Note that it is possible that two threads racing to query the same TypeInfo may allocate and query two different
        // cache objects. Thus, this object must not be relied upon to preserve object identity.
        //================================================================================================================

        private sealed class TypeComponentsCache
        {
            public TypeComponentsCache(RuntimeTypeInfo type)
            {
                _type = type;

                _perNameQueryCaches_CaseSensitive = CreatePerNameQueryCaches(type, ignoreCase: false);
                _perNameQueryCaches_CaseInsensitive = CreatePerNameQueryCaches(type, ignoreCase: true);

                _nameAgnosticQueryCaches = new object[MemberTypeIndex.Count];
            }

            //
            // Returns the cached result of a name-specific query on the Type's members, as if you'd passed in
            //
            //  BindingFlags == Public | NonPublic | Instance | Static | FlattenHierarchy
            //
            public QueriedMemberList<M> GetQueriedMembers<M>(string name, bool ignoreCase) where M : MemberInfo
            {
                int index = MemberPolicies<M>.MemberTypeIndex;
                object obj = ignoreCase ? _perNameQueryCaches_CaseInsensitive[index] : _perNameQueryCaches_CaseSensitive[index];
                Debug.Assert(obj is PerNameQueryCache<M>);
                PerNameQueryCache<M> unifier = Unsafe.As<PerNameQueryCache<M>>(obj);
                QueriedMemberList<M> result = unifier.GetOrAdd(name);
                return result;
            }

            //
            // Returns the cached result of a name-agnostic query on the Type's members, as if you'd passed in
            //
            //  BindingFlags == Public | NonPublic | Instance | Static | FlattenHierarchy
            //
            public QueriedMemberList<M> GetQueriedMembers<M>() where M : MemberInfo
            {
                int index = MemberPolicies<M>.MemberTypeIndex;
                object result = Volatile.Read(ref _nameAgnosticQueryCaches[index]);
                if (result == null)
                {
                    QueriedMemberList<M> newResult = QueriedMemberList<M>.Create(_type, optionalNameFilter: null, ignoreCase: false);
                    newResult.Compact();
                    result = newResult;
                    Volatile.Write(ref _nameAgnosticQueryCaches[index], result);
                }

                Debug.Assert(result is QueriedMemberList<M>);
                return Unsafe.As<QueriedMemberList<M>>(result);
            }

            public EnumInfo EnumInfo => _lazyEnumInfo ?? (_lazyEnumInfo = new EnumInfo(_type));

            public EnumInfo LowLevelEnumInfo => _lazyEnumInfo ?? (_lazyEnumInfo = AllocatedEnumInfo.Create(_type));

            private static object[] CreatePerNameQueryCaches(RuntimeTypeInfo type, bool ignoreCase)
            {
                object[] perNameCaches = new object[MemberTypeIndex.Count];
                perNameCaches[MemberTypeIndex.Constructor] = new PerNameQueryCache<ConstructorInfo>(type, ignoreCase: ignoreCase);
                perNameCaches[MemberTypeIndex.Event] = new PerNameQueryCache<EventInfo>(type, ignoreCase: ignoreCase);
                perNameCaches[MemberTypeIndex.Field] = new PerNameQueryCache<FieldInfo>(type, ignoreCase: ignoreCase);
                perNameCaches[MemberTypeIndex.Method] = new PerNameQueryCache<MethodInfo>(type, ignoreCase: ignoreCase);
                perNameCaches[MemberTypeIndex.Property] = new PerNameQueryCache<PropertyInfo>(type, ignoreCase: ignoreCase);
                perNameCaches[MemberTypeIndex.NestedType] = new PerNameQueryCache<Type>(type, ignoreCase: ignoreCase);
                return perNameCaches;
            }

            // This array holds six PerNameQueryCache<M> objects, one for each of the possible M types (ConstructorInfo, EventInfo, etc.)
            // The caches are configured to do a case-sensitive query.
            private readonly object[] _perNameQueryCaches_CaseSensitive;

            // This array holds six PerNameQueryCache<M> objects, one for each of the possible M types (ConstructorInfo, EventInfo, etc.)
            // The caches are configured to do a case-insensitive query.
            private readonly object[] _perNameQueryCaches_CaseInsensitive;

            // This array holds six lazily created QueriedMemberList<M> objects, one for each of the possible M types (ConstructorInfo, EventInfo, etc.).
            // The objects are the results of a name-agnostic query.
            private readonly object[] _nameAgnosticQueryCaches;

            private readonly RuntimeTypeInfo _type;

            private volatile EnumInfo _lazyEnumInfo;

            //
            // Each PerName cache persists the results of a Type.Get(name, bindingFlags) for a particular MemberInfoType "M".
            //
            // where "bindingFlags" == Public | NonPublic | Instance | Static | FlattenHierarchy
            //
            // In addition, if "ignoreCase" was passed to the constructor, BindingFlags.IgnoreCase is also in effect.
            //
            private sealed class PerNameQueryCache<M> : ConcurrentUnifier<string, QueriedMemberList<M>> where M : MemberInfo
            {
                public PerNameQueryCache(RuntimeTypeInfo type, bool ignoreCase)
                {
                    _type = type;
                    _ignoreCase = ignoreCase;
                }

                protected sealed override QueriedMemberList<M> Factory(string key)
                {
                    QueriedMemberList<M> result = QueriedMemberList<M>.Create(_type, key, ignoreCase: _ignoreCase);
                    result.Compact();
                    return result;
                }

                private readonly RuntimeTypeInfo _type;
                private readonly bool _ignoreCase;
            }
        }
    }

    /// <summary>
    /// Represents an EnumInfo for a type that is guaranteed to be allocated by the runtime
    /// (i.e. there's a valid full type handle for it).
    /// This implementation is low level and takes advantage of that. One reason is runtime perf,
    /// but the most important reason is reduced size on disk footprint. This code path
    /// is used in Enum.ToString and is therefore present in any .NET app. It should better
    /// not drag the entire reflection stack into the executable image (custom attribute
    /// resolution, field resolution, etc.).
    /// </summary>
    class AllocatedEnumInfo : EnumInfo
    {
        private AllocatedEnumInfo(Type underlyingType, KeyValuePair<string, ulong>[] namesAndValues, Array rawValues, bool hasFlagsAttribute)
            : base(underlyingType, namesAndValues, rawValues, hasFlagsAttribute)
        {
        }

        public static EnumInfo Create(RuntimeTypeInfo typeInfo)
        {
#if ECMA_METADATA_SUPPORT
            return new EnumInfo(typeInfo);
#else
            Type underlyingType = ComputeLowLevelUnderlyingType(typeInfo);

            RuntimeTypeHandle typeHandle = typeInfo.TypeHandle;

            bool success = Internal.Reflection.Core.Execution.ReflectionCoreExecution.ExecutionEnvironment.TryGetMetadataForNamedType(typeHandle, out QTypeDefinition qTypeDef);
            if (!success)
                return null;

            var reader = qTypeDef.NativeFormatReader;
            var typeDef = reader.GetTypeDefinition(qTypeDef.NativeFormatHandle);

            var rawValuesList = new List<object>();
            var namesAndValuesList = new List<KeyValuePair<string, ulong>>();

            foreach (var fieldHandle in typeDef.Fields)
            {
                var field = fieldHandle.GetField(reader);
                if ((field.Flags & FieldAttributes.Public) == FieldAttributes.Public &&
                    (field.Flags & FieldAttributes.Static) == FieldAttributes.Static)
                {
                    object rawValue = field.DefaultValue.ParseConstantNumericValue(reader);
                    rawValuesList.Add(rawValue);

                    ulong rawUnboxedValue;
                    if (rawValue is ulong)
                    {
                        rawUnboxedValue = (ulong)rawValue;
                    }
                    else
                    {
                        // This conversion is this way for compatibility: do a value-preseving cast to long - then store (and compare) as ulong. This affects
                        // the order in which the Enum apis return names and values.
                        rawUnboxedValue = (ulong)(((IConvertible)rawValue).ToInt64(null));
                    }

                    namesAndValuesList.Add(new KeyValuePair<string, ulong>(field.Name.GetString(reader), rawUnboxedValue));
                }
            }

            var rawValues = rawValuesList.ToArray();
            var namesAndValues = namesAndValuesList.ToArray();

            Array.Sort(keys: namesAndValues, items: rawValues, comparer: NamesAndValueComparer.Default);

            // The array element type is the underlying type, not the enum type. (The enum type could be an open generic.)
            var Values = Array.CreateInstance(underlyingType, rawValues.Length);
            Array.Copy(rawValues, Values, rawValues.Length);

            // Compat note: we should check the identity of the FlagsAttribute type, but the runtime is pretty lax
            // about custom attribute type identity otherwise, so it's unlikely to matter here.
            bool hasFlagsAttribute = false;
            foreach (var attribute in typeDef.CustomAttributes)
            {
                if (attribute.IsCustomAttributeOfType(reader, "System", "FlagsAttribute"))
                {
                    hasFlagsAttribute = true;
                    break;
                }
            }

            return new AllocatedEnumInfo(underlyingType, namesAndValues, rawValues, hasFlagsAttribute);
#endif
        }
    }
}


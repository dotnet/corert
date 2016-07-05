// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// ----------------------------------------------------------------------------------
// Interop library code
//
// Type marshalling helpers used by MCG
//
// NOTE:
//   These source code are being published to InternalAPIs and consumed by RH builds
//   Use PublishInteropAPI.bat to keep the InternalAPI copies in sync
// ----------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.Runtime;
using System.Diagnostics.Contracts;
using Internal.NativeFormat;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    internal static class McgTypeHelpers
    {
        static readonly Type[] s_wellKnownTypes = new Type[]
        {
            typeof(Boolean),
            typeof(Char),
            typeof(Byte),
            typeof(Int16),
            typeof(UInt16),
            typeof(Int32),
            typeof(UInt32),
            typeof(Int64),
            typeof(UInt64),
            typeof(Single),
            typeof(Double),
            typeof(String),
            typeof(Object),
            typeof(Guid)
        };

        static readonly string[] s_wellKnownTypeNames = new string[]
        {
            "Boolean",
            "Char16",
            "UInt8",
            "Int16",
            "UInt16",
            "Int32",
            "UInt32",
            "Int64",
            "UInt64",
            "Single",
            "Double",
            "String",
            "Object",
            "Guid"
        };

        private const string PseudonymPrefix = "System.Runtime.InteropServices.RuntimePseudonyms.";
#if ENABLE_WINRT
        /// <summary>
        /// A 'fake' System.Type instance for native WinMD types (metadata types) that are not needed in
        /// managed code, which means it is:
        /// 1. Imported in MCG, but reduced away by reducer
        /// 2. Not imported by MCG at all
        /// In either case, it is possible that it is needed only in native code, and native code can return
        /// a IXamlType instance to C# xaml compiler generated code that attempts to call get_UnderlyingType
        /// which tries to convert a TypeName to System.Type and then stick it into a cache. In order to make
        /// such scenario work, we need to create a fake System.Type instance that is unique to the name
        /// and is roundtrippable.
        /// As long as it is only used in the cache scenarios in xaml compiler generated code, we should be
        /// fine. Any other attempt to use such types will surely result an exception
        /// NOTE: in order to avoid returning fake types for random non-existent metadata types, we look
        /// in McgAdditionalClassData (which encodes all interesting class data) before we create such fake
        /// types
        /// </summary>
        class McgFakeMetadataType
#if RHTESTCL
            : Type
#else
            : Internal.Reflection.Extensibility.ExtensibleType
#endif
        {
            /// <summary>
            /// Full type name of the WinMD type
            /// </summary>
            string _fullTypeName;

            public McgFakeMetadataType(string fullTypeName, TypeKind typeKind)
#if RHTESTCL
                : base(default(RuntimeTypeHandle))
#else
                : base()
#endif
            {
                _fullTypeName = fullTypeName;
                TypeKind = typeKind;
            }

            public TypeKind TypeKind { get; private set; }

#if RHTESTCL
            public string FullName { get { return _fullTypeName; } }
#else
            public override String AssemblyQualifiedName { get { throw new System.Reflection.MissingMetadataException(_fullTypeName); } }

            public override Type DeclaringType { get { throw new System.Reflection.MissingMetadataException(_fullTypeName); } }
            public override String FullName { get { return _fullTypeName; } }
            public override int GenericParameterPosition { get { throw new System.Reflection.MissingMetadataException(_fullTypeName); } }
            public override Type[] GenericTypeArguments { get { throw new System.Reflection.MissingMetadataException(_fullTypeName); } }

            public override bool IsConstructedGenericType { get { throw new System.Reflection.MissingMetadataException(_fullTypeName); } }
            public override bool IsGenericParameter { get { throw new System.Reflection.MissingMetadataException(_fullTypeName); } }

            public override String Name { get { throw new System.Reflection.MissingMetadataException(_fullTypeName); } }
            public override String Namespace { get { throw new System.Reflection.MissingMetadataException(_fullTypeName); } }

            public override int GetArrayRank() { throw new System.Reflection.MissingMetadataException(_fullTypeName); }
            public override Type GetElementType() { throw new System.Reflection.MissingMetadataException(_fullTypeName); }
            public override Type GetGenericTypeDefinition() { throw new System.Reflection.MissingMetadataException(_fullTypeName); }

            public override Type MakeArrayType() { throw new System.Reflection.MissingMetadataException(_fullTypeName); }
            public override Type MakeArrayType(int rank) { throw new System.Reflection.MissingMetadataException(_fullTypeName); }
            public override Type MakeByRefType() { throw new System.Reflection.MissingMetadataException(_fullTypeName); }
            public override Type MakeGenericType(params Type[] typeArguments) { throw new System.Reflection.MissingMetadataException(_fullTypeName); }
            public override Type MakePointerType() { throw new System.Reflection.MissingMetadataException(_fullTypeName); }

            public override String ToString()
            {
                return "Type: " + _fullTypeName;
            }

            public override bool Equals(Object o)
            {
                if (o == null)
                    return false;

                //
                // We guarantee uniqueness in Mcg marshalling code
                //
                if (o == this)
                    return true;

                return false;
            }

            public override int GetHashCode()
            {
                return _fullTypeName.GetHashCode();
            }
#endif //RHTESTCL
        }


        internal unsafe static void TypeToTypeName(
            Type type,
            out HSTRING nativeTypeName,
            out int nativeTypeKind)
        {
            if (type == null)
            {
                nativeTypeName.handle = default(IntPtr);
                nativeTypeKind = (int)TypeKind.Custom;
            }
            else
            {
                McgFakeMetadataType fakeType = type as McgFakeMetadataType;
                if (fakeType != null)
                {
                    //
                    // Handle round tripping fake types
                    // See McgFakeMetadataType for details
                    //
                    nativeTypeKind = (int)fakeType.TypeKind;
                    nativeTypeName = McgMarshal.StringToHString(fakeType.FullName);
                }
                else
                {
                    string typeName;
                    TypeKind typeKind;
                    TypeToTypeName(type.TypeHandle, out typeName, out typeKind);

                    nativeTypeName = McgMarshal.StringToHString(typeName);
                    nativeTypeKind = (int)typeKind;
                }
            }
        }
#endif //!CORECLR
        private unsafe static void TypeToTypeName(
            RuntimeTypeHandle typeHandle,
            out string typeName,
            out TypeKind typeKind)
        {
            //
            // Primitive types
            //
            for (int i = 0; i < s_wellKnownTypes.Length; i++)
            {
                if (s_wellKnownTypes[i].TypeHandle.Equals(typeHandle))
                {
                    typeName = s_wellKnownTypeNames[i];
                    typeKind = TypeKind.Primitive;

                    return;
                }
            }

            //
            // User-imported types
            //
            bool isWinRT;
            string name = McgModuleManager.GetTypeName(typeHandle, out isWinRT);

            if (name != null)
            {
                typeName = name;
                typeKind =
                    (isWinRT ?
                     TypeKind.Metadata :
                     TypeKind.Custom);

                return;
            }

            //
            // Handle managed types
            //
            typeName = GetCustomTypeName(typeHandle);
            typeKind = TypeKind.Custom;
        }

        static System.Collections.Generic.Internal.Dictionary<string, Type> s_fakeTypeMap
            = new Collections.Generic.Internal.Dictionary<string, Type>();
        static Lock s_fakeTypeMapLock = new Lock();
        static System.Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, Type> s_realToFakeTypeMap
            = new System.Collections.Generic.Internal.Dictionary<RuntimeTypeHandle, Type>();

#if ENABLE_WINRT
        /// <summary>
        /// Returns a type usable in XAML roundtripping whether it's reflectable or not
        /// </summary>
        /// <param name="realType">Type for the real object</param>
        /// <returns>realType if realType is reflectable, otherwise a fake type that can be roundtripped
        /// and won't throw for XAML usage.</returns>
        internal static Type GetReflectableOrFakeType(Type realType)
        {
#if !RHTESTCL
            if(realType.SupportsReflection())
            {
                return realType;
            }
#endif

            s_fakeTypeMapLock.Acquire();
            try
            {
                Type fakeType;
                RuntimeTypeHandle realTypeHandle = realType.TypeHandle;
                if (s_realToFakeTypeMap.TryGetValue(realTypeHandle, out fakeType))
                {
                    return fakeType;
                }

                string pseudonym = GetPseudonymForType(realTypeHandle, /* useFake: */ true);
                fakeType = new McgFakeMetadataType(pseudonym, TypeKind.Custom);
                s_realToFakeTypeMap.Add(realTypeHandle, fakeType);
                s_fakeTypeMap.Add(pseudonym, fakeType);

                return fakeType;
            }
            finally
            {
                s_fakeTypeMapLock.Release();
            }
        }

        internal static unsafe Type TypeNameToType(HSTRING nativeTypeName, int nativeTypeKind)
        {
            string name = McgMarshal.HStringToString(nativeTypeName);

            if (!string.IsNullOrEmpty(name))
            {
                //
                // Well-known types
                //
                for (int i = 0; i < s_wellKnownTypeNames.Length; i++)
                {
                    if (s_wellKnownTypeNames[i] == name)
                    {
                        if (nativeTypeKind != (int)TypeKind.Primitive)
                            throw new ArgumentException(SR.Arg_UnexpectedTypeKind);

                        return s_wellKnownTypes[i];
                    }
                }

                if (nativeTypeKind == (int)TypeKind.Primitive)
                {
                    //
                    // We've scanned all primitive types that we know of and came back nothing
                    //
                    throw new ArgumentException("Unrecognized primitive type name");
                }

                //
                // User-imported types
                // Try to get a type if MCG knows what this is
                // If the returned type does not have metadata, the type is no good as Jupiter needs to pass
                // it to XAML type provider code which needs to call FullName on it
                //
                bool isWinRT;
                Type type = McgModuleManager.GetTypeFromName(name, out isWinRT);

#if !RHTESTCL
                if (type != null && !type.SupportsReflection())
                    type = null;
#endif

                //
                // If we got back a type that is valid (not reduced)
                //
                if (type != null && !type.TypeHandle.Equals(McgModule.s_DependencyReductionTypeRemovedTypeHandle))
                {
                    if (nativeTypeKind !=
                            (int)
                            (isWinRT ? TypeKind.Metadata : TypeKind.Custom))
                        throw new ArgumentException(SR.Arg_UnexpectedTypeKind);
                    return type;
                }

                if (nativeTypeKind == (int)TypeKind.Metadata)
                {
                    //
                    // Handle converting native WinMD type names to fake McgFakeMetadataType to make C# xaml
                    // compiler happy
                    // See McgFakeMetadataType for more details
                    //
                    s_fakeTypeMapLock.Acquire();
                    try
                    {
                        if (s_fakeTypeMap.TryGetValue(name, out type))
                        {
                            return type;
                        }
                        else
                        {
                            type = new McgFakeMetadataType(name, TypeKind.Metadata);
                            s_fakeTypeMap.Add(name, type);
                            return type;
                        }
                    }
                    finally
                    {
                        s_fakeTypeMapLock.Release();
                    }
                }


                if (nativeTypeKind != (int)TypeKind.Custom)
                    throw new ArgumentException(SR.Arg_UnrecognizedTypeName);

                //
                // Arbitrary managed types.  See comment in TypeToTypeName.
                //
                return StringToCustomType(name);
            }

            return null;
        }
#endif
        private static string GetCustomTypeName(RuntimeTypeHandle type)
        {
            //
            // For types loaded by the runtime, we may not have metadata from which to get the name.
            // So we use the RuntimeTypeHandle instead.  For types loaded via reflection, we may not
            // have a RuntimeTypeHandle, in which case we will try to use the name.
            //

#if !RHTESTCL
            Type realType = InteropExtensions.GetTypeFromHandle(type);
            if (realType.SupportsReflection())
            {
                //
                // Managed types that has reflection metadata
                //
                // Use the fully assembly qualified name to make Jupiter happy as Jupiter might parse the
                // name (!!) to extract the assembly name to look up files from directory with the same
                // name. A bug has filed to them to fix this for the next release, because the format
                // of Custom TypeKind is up to the interpretation of the projection layer and is supposed
                // to be an implementation detail
                // NOTE: The try/catch is added as a fail-safe
                //
                try
                {
                    return realType.AssemblyQualifiedName;
                }
                catch (MissingMetadataException ex)
                {
                    ExternalInterop.OutputDebugString(
                        SR.Format(SR.TypeNameMarshalling_MissingMetadata, ex.Message)
                    );
                }
            }
#endif

            return GetPseudonymForType(type, /* useFake: */ false);
        }

        private static string GetPseudonymForType(RuntimeTypeHandle type, bool useFake)
        {
            // I'd really like to use the standard .net string formatting stuff here,
            // but not enough of it is supported by rhtestcl.
            ulong value = (ulong)type.GetRawValue();

            StringBuilder sb = new StringBuilder(PseudonymPrefix, PseudonymPrefix.Length + 17);
            if(useFake)
            {
                sb.Append('f');
            }
            else
            {
                sb.Append('r');
            }

            // append 64 bits, high to low, one nibble at a time
            for (int shift = 60; shift >= 0; shift -= 4)
            {
                ulong nibble = (value >> shift) & 0xf;
                if (nibble < 10)
                    sb.Append((char)(nibble + '0'));
                else
                    sb.Append((char)((nibble - 10) + 'A'));
            }

            string result = sb.ToString();
            return result;
        }

        private static Type StringToCustomType(string s)
        {
            ulong value = 0;

            if (s.StartsWith(PseudonymPrefix))
            {
                //
                // This is a name created from a RuntimeTypeHandle that does not have reflection metadata
                //
                if (s.Length != PseudonymPrefix.Length + 17)
                    throw new ArgumentException(SR.Arg_InvalidCustomTypeNameValue);

                bool useFake = s[PseudonymPrefix.Length] == 'f';
                if (useFake)
                {
                    s_fakeTypeMapLock.Acquire();
                    try
                    {
                        return s_fakeTypeMap[s];
                    }
                    finally
                    {
                        s_fakeTypeMapLock.Release();
                    }
                }

                for (int i = PseudonymPrefix.Length + 1; i < s.Length; i++)
                {
                    char c = s[i];
                    ulong nibble;

                    if (c >= '0' && c <= '9')
                        nibble = (ulong)(c - '0');
                    else if (c >= 'A' && c <= 'F')
                        nibble = (ulong)(c - 'A') + 10;
                    else
                        throw new ArgumentException(SR.Arg_InvalidCustomTypeNameValue);

                    value = (value << 4) | nibble;
                }

                return InteropExtensions.GetTypeFromHandle((IntPtr)value);
            }
#if !RHTESTCL
            else
            {
                //
                // Try reflection
                // If reflection failed, this is a type name that we don't know about
                // In theory we could support round tripping of such types.
                //
                Type reflectType = Type.GetType(s);
                if (reflectType == null)
                    throw new ArgumentException("Unrecognized custom TypeName");

                return reflectType;
            }
#else
            else
            {
                return null;
            }
#endif
        }

        /// <summary>
        /// Try to get Diagnostic String for given RuntimeTypeHandle
        /// Diagnostic usually means MissingMetadata Message
        /// </summary>
        /// <param name="interfaceType"></param>
        /// <returns></returns>
        public static string GetDiagnosticMessageForMissingType(RuntimeTypeHandle interfaceType)
        {
#if ENABLE_WINRT
            string msg = string.Empty;
            try
            {
                // case 1: missing reflection metadata for interfaceType
                // if this throws, we just return MissMetadataException Message
                string typeName = interfaceType.GetDisplayName();

                // case 2:  if intefaceType is ICollection<T>/IReadOnlyCollection<T>,
                // we need to find out its corresponding WinRT Interface and ask users to root them.
                // Current there is an issue for projected type in rd.xml file--if user specify IList<T> in rd.xml,
                // DR will only root IList<T> instead of both IList<T> and IVector<T>

                Type type = interfaceType.GetType();
                if (InteropExtensions.IsGenericType(interfaceType)
                    && type.GenericTypeArguments != null
                    && type.GenericTypeArguments.Length == 1)
                {
                    List<string> missTypeNames = new List<string>();
                    Type genericType = type.GetGenericTypeDefinition();
                    bool isICollectOfT = false;
                    bool isIReadOnlyCollectOfT = false;
                    if (genericType.TypeHandle.Equals(typeof(ICollection<>).TypeHandle))
                    {
                        isICollectOfT = true;
                        Type argType = type.GenericTypeArguments[0];
                        if (argType.IsConstructedGenericType && argType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                        {
                            // the missing type could be either IMap<K,V> or IVector<IKeyValuePair<K,v>>
                            missTypeNames.Add(
                                McgTypeHelpers.ConstructGenericTypeFullName(
                                    "Windows.Foundation.Collections.IMap",
                                    new string[]
                                    {
                                        argType.GenericTypeArguments[0].ToString(),
                                        argType.GenericTypeArguments[1].ToString()
                                    }
                                )
                            );
                            missTypeNames.Add(
                                McgTypeHelpers.ConstructGenericTypeFullName(
                                    "Windows.Foundation.Collections.IVector",
                                    new String[]
                                    {
                                        McgTypeHelpers.ConstructGenericTypeFullName(
                                            "Windows.Foundation.Collections.IKeyValuePair",
                                            new string[]
                                            {
                                                argType.GenericTypeArguments[0].ToString(),
                                                argType.GenericTypeArguments[1].ToString()
                                            }
                                        )
                                    }
                                )
                            );
                        }
                        else
                        {
                            // the missing type is IVector<T>
                            missTypeNames.Add(
                                McgTypeHelpers.ConstructGenericTypeFullName(
                                    "Windows.Foundation.Collections.IVector",
                                    new string[]
                                    {
                                        argType.ToString()
                                    }
                                )
                            );
                        }
                    } // genericType == typeof(ICollection<>)
                    else if (genericType == typeof(IReadOnlyCollection<>))
                    {
                        isIReadOnlyCollectOfT = true;
                        Type argType = type.GenericTypeArguments[0];
                        if (argType.IsConstructedGenericType && argType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                        {
                            // the missing type could be either IVectorView<IKeyValuePair<K,v>> or IMapView<K,V>
                            missTypeNames.Add(
                                McgTypeHelpers.ConstructGenericTypeFullName(
                                    "Windows.Foundation.Collections.IVectorView",
                                    new String[]
                                    {
                                        McgTypeHelpers.ConstructGenericTypeFullName(
                                            "Windows.Foundation.Collections.IKeyValuePair",
                                            new string[]
                                            {
                                                argType.GenericTypeArguments[0].ToString(),
                                                argType.GenericTypeArguments[1].ToString()
                                            }
                                        )
                                    }
                                )
                            );
                            missTypeNames.Add(
                                McgTypeHelpers.ConstructGenericTypeFullName(
                                    "Windows.Foundation.Collections.IMapView",
                                    new string[]
                                    {
                                        argType.GenericTypeArguments[0].ToString(),
                                        argType.GenericTypeArguments[1].ToString()
                                    }
                                )
                           );
                        }
                        else
                        {
                            //the missing type is IVectorView<T>
                            missTypeNames.Add(
                                McgTypeHelpers.ConstructGenericTypeFullName(
                                    "Windows.Foundation.Collections.IVectorView",
                                    new string[]
                                    {
                                        argType.ToString()
                                    }
                                )
                           );
                        }
                    }

                    if (isICollectOfT || isIReadOnlyCollectOfT)
                    {
                        // Concat all missing Type Names into one message
                        for (int i = 0; i < missTypeNames.Count; i++)
                        {
                            msg += String.Format(SR.ComTypeMarshalling_MissingInteropData, missTypeNames[i]);
                            if (i != missTypeNames.Count - 1)
                                msg += Environment.NewLine;
                        }
                        return msg;
                    }
                }

                // case 3: We can get type name but not McgTypeInfo, maybe another case similar to case 2
                // definitely is a bug.
                msg = String.Format(SR.ComTypeMarshalling_MissingInteropData, Type.GetTypeFromHandle(interfaceType));
            }
            catch (MissingMetadataException ex)
            {
                msg = ex.Message;
            }
            return msg;
#else
            return interfaceType.ToString();
#endif //ENABLE_WINRT
        }

        // Construct Generic Type Full Name
        private static string ConstructGenericTypeFullName(string genericTypeDefinitionFullName, string[] genericTypeArguments)
        {
            string fullName = genericTypeDefinitionFullName;
            fullName += "<";
            for (int i = 0; i < genericTypeArguments.Length; i++)
            {
                if (i != 0)
                    fullName += ",";
                fullName += genericTypeArguments[i];
            }
            fullName += ">";
            return fullName;
        }
    }
    internal static class TypeHandleExtensions
    {
        internal static string GetDisplayName(this RuntimeTypeHandle handle)
        {
#if ENABLE_WINRT
            return Internal.Reflection.Execution.PayForPlayExperience.MissingMetadataExceptionCreator.ComputeUsefulPertainantIfPossible(Type.GetTypeFromHandle(handle));
#else
            return handle.ToString();
#endif
        }

        internal static bool IsClass(this RuntimeTypeHandle handle)
        {
#if ENABLE_WINRT
            return  !InteropExtensions.IsInterface(handle) && 
                    !handle.IsValueType() && 
                    !InteropExtensions.AreTypesAssignable(handle, typeof(Delegate).TypeHandle);
#else
            return false;
#endif
        }

        internal static bool IsIJupiterObject(this RuntimeTypeHandle interfaceType)
        {
#if ENABLE_WINRT
            return interfaceType.Equals(InternalTypes.IJupiterObject);
#else
            return false;
#endif
        }

        internal static bool IsIInspectable(this RuntimeTypeHandle interfaceType)
        {
#if ENABLE_WINRT
            return interfaceType.Equals(InternalTypes.IInspectable);
#else
            return false;
#endif
        }

        #region "Interface Data"
        internal static bool HasInterfaceData(this RuntimeTypeHandle interfaceType)
        {
            McgInterfaceInfo interfaceInfo = McgModuleManager.GetInterfaceInfoByHandle(interfaceType);
            if (interfaceInfo != null)
                return true;
            return false;
        }

        internal static bool IsWinRTInterface(this RuntimeTypeHandle interfaceType)
        {
            McgInterfaceInfo interfaceInfo = McgModuleManager.GetInterfaceInfoByHandle(interfaceType);
            if (interfaceInfo != null)
                return interfaceInfo.InterfaceData.IsIInspectable;

#if ENABLE_WINRT
           throw new MissingInteropDataException(SR.DelegateMarshalling_MissingInteropData, Type.GetTypeFromHandle(interfaceType));
#else
           return false;
#endif
        }

        internal static bool HasDynamicAdapterClass(this RuntimeTypeHandle interfaceType)
        {
            McgInterfaceInfo interfaceInfo = McgModuleManager.GetInterfaceInfoByHandle(interfaceType);
            if (interfaceInfo != null)
            {
                return interfaceInfo.HasDynamicAdapterClass;
            }

#if ENABLE_WINRT
           throw new MissingInteropDataException(SR.DelegateMarshalling_MissingInteropData, Type.GetTypeFromHandle(interfaceType));
#else
            Environment.FailFast("HasDynamicAdapterClass.");
            return false;
#endif
        }

        internal static RuntimeTypeHandle GetDynamicAdapterClassType(this RuntimeTypeHandle interfaceType)
        {
            McgInterfaceInfo interfaceInfo = McgModuleManager.GetInterfaceInfoByHandle(interfaceType);
            if(interfaceInfo != null)
            {
                return interfaceInfo.DynamicAdapterClassType;
            }

            return default(RuntimeTypeHandle);
        }

        internal static Guid GetInterfaceGuid(this RuntimeTypeHandle interfaceType)
        {
            McgInterfaceInfo interfaceInfo = McgModuleManager.GetInterfaceInfoByHandle(interfaceType);
            if(interfaceInfo != null)
            {
                return interfaceInfo.ItfGuid;
            }

            return default(Guid);
        }

        internal static IntPtr GetCcwVtableThunk(this RuntimeTypeHandle interfaceType)
        {
            McgInterfaceInfo interfaceInfo = McgModuleManager.GetInterfaceInfoByHandle(interfaceType);
            if (interfaceInfo != null)
            {
                return interfaceInfo.InterfaceData.CcwVtable;
            }

            return default(IntPtr);
        }

        internal static IntPtr GetCcwVtable(this RuntimeTypeHandle interfaceType)
        {
            McgInterfaceInfo interfaceInfo = McgModuleManager.GetInterfaceInfoByHandle(interfaceType);
            if (interfaceInfo != null)
            {
                return interfaceInfo.CcwVtable;
            }

            return default(IntPtr);
        }

        internal static int GetMarshalIndex(this RuntimeTypeHandle interfaceType)
        {
            McgInterfaceInfo interfaceInfo = McgModuleManager.GetInterfaceInfoByHandle(interfaceType);
            if (interfaceInfo != null)
                return interfaceInfo.MarshalIndex;

            return -1;
        }

        internal static McgInterfaceFlags GetInterfaceFlags(this RuntimeTypeHandle interfaceType)
        {
            McgInterfaceInfo interfaceInfo = McgModuleManager.GetInterfaceInfoByHandle(interfaceType);
            if(interfaceInfo != null)
                return interfaceInfo.Flags;

            return default(McgInterfaceFlags);
        }

        internal static RuntimeTypeHandle GetDispatchClassType(this RuntimeTypeHandle interfaceType)
        {
            McgInterfaceInfo interfaceInfo = McgModuleManager.GetInterfaceInfoByHandle(interfaceType);
            if (interfaceInfo != null)
               return interfaceInfo.DispatchClassType;

            return default(RuntimeTypeHandle);
        }

        internal static IntPtr GetDelegateInvokeStub(this RuntimeTypeHandle winrtDelegateType)
        {
            McgInterfaceInfo interfaceInfo = McgModuleManager.GetInterfaceInfoByHandle(winrtDelegateType);
            if (interfaceInfo != null)
                return interfaceInfo.DelegateInvokeStub;

            return default(IntPtr);
        }
        #endregion

        #region "Class Data"
        internal static GCPressureRange GetGCPressureRange(this RuntimeTypeHandle classType)
        {
            McgClassInfo classInfo = McgModuleManager.GetClassInfoFromTypeHandle(classType);
            if (classInfo != null)
                return classInfo.GCPressureRange;

            return GCPressureRange.None;
        }

        internal static bool IsSealed(this RuntimeTypeHandle classType)
        {
            McgClassInfo classInfo = McgModuleManager.GetClassInfoFromTypeHandle(classType);
            if (classInfo != null)
                return classInfo.IsSealed;

#if ENABLE_WINRT
           throw new MissingInteropDataException(SR.DelegateMarshalling_MissingInteropData, Type.GetTypeFromHandle(classType));
#else
            Environment.FailFast("IsSealed");
            return false;
#endif
        }

        internal static ComMarshalingType GetMarshalingType(this RuntimeTypeHandle classType)
        {
            McgClassInfo classInfo = McgModuleManager.GetClassInfoFromTypeHandle(classType);
            if (classInfo != null)
                return classInfo.MarshalingType;

            return ComMarshalingType.Unknown;
        }

        internal static RuntimeTypeHandle GetDefaultInterface(this RuntimeTypeHandle classType)
        {
            McgClassInfo classInfo = McgModuleManager.GetClassInfoFromTypeHandle(classType);
            if (classInfo != null)
                return classInfo.DefaultInterface;

            return default(RuntimeTypeHandle);
        }
        #endregion

        #region "Generic Argument Data"
        internal static RuntimeTypeHandle GetIteratorType(this RuntimeTypeHandle interfaceType)
        {
            McgGenericArgumentMarshalInfo mcgGenericArgumentMarshalInfo;
            if (McgModuleManager.TryGetGenericArgumentMarshalInfo(interfaceType, out mcgGenericArgumentMarshalInfo))
            {
                return mcgGenericArgumentMarshalInfo.IteratorType;
            }

            return default(RuntimeTypeHandle);
        }

        internal static RuntimeTypeHandle GetElementClassType(this RuntimeTypeHandle interfaceType)
        {
            McgGenericArgumentMarshalInfo mcgGenericArgumentMarshalInfo;
            if (McgModuleManager.TryGetGenericArgumentMarshalInfo(interfaceType, out mcgGenericArgumentMarshalInfo))
            {
                return mcgGenericArgumentMarshalInfo.ElementClassType;
            }

            return default(RuntimeTypeHandle);
        }

        internal static RuntimeTypeHandle GetElementInterfaceType(this RuntimeTypeHandle interfaceType)
        {
            McgGenericArgumentMarshalInfo mcgGenericArgumentMarshalInfo;
            if (McgModuleManager.TryGetGenericArgumentMarshalInfo(interfaceType, out mcgGenericArgumentMarshalInfo))
            {
                return mcgGenericArgumentMarshalInfo.ElementInterfaceType;
            }

            return default(RuntimeTypeHandle);
        }

        internal static RuntimeTypeHandle GetVectorViewType(this RuntimeTypeHandle interfaceType)
        {
            McgGenericArgumentMarshalInfo mcgGenericArgumentMarshalInfo;
            if (McgModuleManager.TryGetGenericArgumentMarshalInfo(interfaceType, out mcgGenericArgumentMarshalInfo))
            {
                return mcgGenericArgumentMarshalInfo.VectorViewType;
            }

            return default(RuntimeTypeHandle);
        }

        internal static RuntimeTypeHandle GetAsyncOperationType(this RuntimeTypeHandle interfaceType)
        {
            McgGenericArgumentMarshalInfo mcgGenericArgumentMarshalInfo;
            if (McgModuleManager.TryGetGenericArgumentMarshalInfo(interfaceType, out mcgGenericArgumentMarshalInfo))
            {
                return mcgGenericArgumentMarshalInfo.AsyncOperationType;
            }

            return default(RuntimeTypeHandle);
        }

        internal static int GetByteSize(this RuntimeTypeHandle interfaceType)
        {
            McgGenericArgumentMarshalInfo mcgGenericArgumentMarshalInfo;
            if (McgModuleManager.TryGetGenericArgumentMarshalInfo(interfaceType, out mcgGenericArgumentMarshalInfo))
            {
                return (int)mcgGenericArgumentMarshalInfo.ElementSize;
            }

            return -1;
        }
        #endregion

        #region "CCWTemplate Data"
        internal static string GetCCWRuntimeClassName(this RuntimeTypeHandle ccwType)
        {
            string ccwRuntimeClassName;
            if (McgModuleManager.TryGetCCWRuntimeClassName(ccwType, out ccwRuntimeClassName))
                return ccwRuntimeClassName;
            return default(string);
        }

        internal static bool IsSupportCCWTemplate(this RuntimeTypeHandle ccwType)
        {
            CCWTemplateInfo ccwTemplateInfo = McgModuleManager.GetCCWTemplateDataInfoFromTypeHandle(ccwType);
            if (ccwTemplateInfo != null)
            {
                return true;
            }

            return false;
        }

        internal static bool IsCCWWinRTType(this RuntimeTypeHandle ccwType)
        {
            CCWTemplateInfo ccwTemplateInfo = McgModuleManager.GetCCWTemplateDataInfoFromTypeHandle(ccwType);
            if (ccwTemplateInfo != null)
                return ccwTemplateInfo.IsWinRTType;

#if ENABLE_WINRT
           throw new MissingInteropDataException(SR.DelegateMarshalling_MissingInteropData, Type.GetTypeFromHandle(ccwType));
#else
            Environment.FailFast("IsCCWWinRTType");
            return false;
#endif
        }

        internal static IEnumerable<RuntimeTypeHandle> GetImplementedInterfaces(this RuntimeTypeHandle ccwType)
        {
            CCWTemplateInfo ccwTemplateInfo = McgModuleManager.GetCCWTemplateDataInfoFromTypeHandle(ccwType);
            if (ccwTemplateInfo != null)
                return ccwTemplateInfo.ImplementedInterfaces;

#if ENABLE_WINRT
           throw new MissingInteropDataException(SR.DelegateMarshalling_MissingInteropData, Type.GetTypeFromHandle(ccwType));
#else
            Environment.FailFast("GetImplementedInterfaces");
            return null;
#endif
        }

        internal static RuntimeTypeHandle GetBaseClass(this RuntimeTypeHandle ccwType)
        {
            CCWTemplateInfo ccwTemplateInfo = McgModuleManager.GetCCWTemplateDataInfoFromTypeHandle(ccwType);
            if (ccwTemplateInfo != null)
                return ccwTemplateInfo.BaseClass;

#if ENABLE_WINRT
           throw new MissingInteropDataException(SR.DelegateMarshalling_MissingInteropData, Type.GetTypeFromHandle(ccwType));
#else
            Environment.FailFast("GetBaseClass");
            return default(RuntimeTypeHandle);
#endif
        }

        private static void GetIIDsImpl(RuntimeTypeHandle typeHandle, System.Collections.Generic.Internal.List<Guid> iids)
        {
            RuntimeTypeHandle baseClass = typeHandle.GetBaseClass();
            if (!baseClass.IsNull())
            {
                GetIIDsImpl(baseClass, iids);
            } 

            foreach(RuntimeTypeHandle t in typeHandle.GetImplementedInterfaces())
            {
                if (t.IsInvalid())
                    continue;

                Guid guid = t.GetInterfaceGuid();
                //
                // Retrieve the GUID and add it to the list
                // Skip ICustomPropertyProvider - we've already added it as the first item
                //
                if (!InteropExtensions.GuidEquals(ref guid, ref Interop.COM.IID_ICustomPropertyProvider))
                {
                    //
                    // Avoid duplicated ones
                    //
                    // The duplicates comes from duplicated interface declarations in the metadata across
                    // parent/child classes, as well as the "injected" override interfaces for protected
                    // virtual methods (for example, if a derived class implements a IShapeInternal protected
                    // method, it only implements a protected method and doesn't implement IShapeInternal
                    // directly, and we have to "inject" it in MCG
                    //
                    // Doing a linear lookup is slow, but people typically never call GetIIDs perhaps except
                    // for debugging purposes (because the GUIDs returned back aren't exactly useful and you
                    // can't map it back to type), so I don't care about perf here that much
                    //
                    if (!iids.Contains(guid))
                        iids.Add(guid);
                }
            }
        }

        /// <summary>
        /// Return the list of IIDs
        /// Used by IInspectable.GetIIDs implementation for every CCW
        /// </summary>
        internal static System.Collections.Generic.Internal.List<Guid> GetIIDs(this RuntimeTypeHandle ccwType)
        {
            System.Collections.Generic.Internal.List<Guid> iids = new System.Collections.Generic.Internal.List<Guid>();

            // Every CCW implements ICPP
            iids.Add(Interop.COM.IID_ICustomPropertyProvider);

            // if there isn't any data about this type, just return empty list
            if (!ccwType.IsSupportCCWTemplate())
                return iids;

            GetIIDsImpl(ccwType, iids);
            return iids;
        }
        #endregion

        internal static bool IsInvalid(this RuntimeTypeHandle typeHandle)
        {
            if (typeHandle.IsNull())
                return true;

            if (typeHandle.Equals(typeof(DependencyReductionTypeRemoved).TypeHandle))
                return true;

            return false;
        }
    }
    public static class TypeOfHelper
    {
        static void RuntimeTypeHandleOf_DidntGetTransformedAway()
        {
#if !RHTESTCL
            Debug.Assert(false);
#endif // RHTESTCL
        }

        public static RuntimeTypeHandle RuntimeTypeHandleOf(string typeName)
        {
            RuntimeTypeHandleOf_DidntGetTransformedAway();
            return default(RuntimeTypeHandle);
        }

        public static RuntimeTypeHandle RuntimeTypeHandleOf(string typeName, string arg)
        {
            RuntimeTypeHandleOf_DidntGetTransformedAway();
            return default(RuntimeTypeHandle);
        }

        public static RuntimeTypeHandle RuntimeTypeHandleOf(string typeName, string arg1, string arg2)
        {
            RuntimeTypeHandleOf_DidntGetTransformedAway();
            return default(RuntimeTypeHandle);
        }

        public static RuntimeTypeHandle RuntimeTypeHandleOf(string typeName, string arg1, string arg2, string arg3)
        {
            RuntimeTypeHandleOf_DidntGetTransformedAway();
            return default(RuntimeTypeHandle);
        }

        public static RuntimeTypeHandle RuntimeTypeHandleOf(string typeName, string arg1, string arg2, string arg3, string arg4)
        {
            RuntimeTypeHandleOf_DidntGetTransformedAway();
            return default(RuntimeTypeHandle);
        }

        public static RuntimeTypeHandle RuntimeTypeHandleOf(string typeName, string arg1, string arg2, string arg3, string arg4, string arg5)
        {
            RuntimeTypeHandleOf_DidntGetTransformedAway();
            return default(RuntimeTypeHandle);
        }

        public static RuntimeTypeHandle RuntimeTypeHandleOf(string typeName, string arg1, string arg2, string arg3, string arg4, string arg5, string arg6)
        {
            RuntimeTypeHandleOf_DidntGetTransformedAway();
            return default(RuntimeTypeHandle);
        }

        public static Type TypeOf(string typeName)
        {
            RuntimeTypeHandleOf_DidntGetTransformedAway();
            return default(Type);
        }
    }
}

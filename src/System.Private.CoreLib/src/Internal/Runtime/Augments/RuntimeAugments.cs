// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//Internal.Runtime.Augments
//-------------------------------------------------
//  Why does this exist?:
//    Reflection.Execution cannot physically live in System.Private.CoreLib.dll
//    as it has a dependency on System.Reflection.Metadata. Its inherently
//    low-level nature means, however, it is closely tied to System.Private.CoreLib.dll.
//    This contract provides the two-communication between those two .dll's.
//
//
//  Implemented by:
//    System.Private.CoreLib.dll
//
//  Consumed by:
//    Reflection.Execution.dll

using System;
using System.Runtime;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Internal.Reflection.Core.NonPortable;
using Internal.Runtime.CompilerServices;

using Interlocked = System.Threading.Interlocked;

namespace Internal.Runtime.Augments
{
    public enum CanonTypeKind
    {
        NormalCanon,
        UniversalCanon
    }

    public static class RuntimeAugments
    {
        /// <summary>
        /// Callbacks used for desktop emulation in console lab tests. Initialized by
        /// InitializeDesktopSupport; currently the only provided method is OpenFileIfExists.
        /// </summary>
        private static DesktopSupportCallbacks s_desktopSupportCallbacks;

        //==============================================================================================
        // One-time initialization.
        //==============================================================================================
        [CLSCompliant(false)]
        public static void Initialize(ReflectionExecutionDomainCallbacks callbacks)
        {
            s_reflectionExecutionDomainCallbacks = callbacks;
        }

        [CLSCompliant(false)]
        public static void InitializeLookups(TypeLoaderCallbacks callbacks)
        {
            s_typeLoaderCallbacks = callbacks;
        }

        [CLSCompliant(false)]
        public static void InitializeDesktopSupport(DesktopSupportCallbacks callbacks)
        {
            s_desktopSupportCallbacks = callbacks;
        }

        //==============================================================================================
        // Access to the underlying execution engine's object allocation routines.
        //==============================================================================================

        //
        // Perform the equivalent of a "newobj", but without invoking any constructors. Other than the EEType, the result object is zero-initialized.
        //
        // Special cases:
        //
        //    Strings: The .ctor performs both the construction and initialization
        //      and compiler special cases these.
        //
        //    IntPtr/UIntPtr: These have intrinsic constructors and it happens, special-casing these in the class library
        //      is the lesser evil compared to special-casing them in the toolchain.
        //
        //    Nullable<T>: the boxed result is the underlying type rather than Nullable so the constructor
        //      cannot truly initialize it.
        //
        //    In these cases, this helper returns "null" and ConstructorInfo.Invoke() must deal with these specially.
        //
        public static Object NewObject(RuntimeTypeHandle typeHandle)
        {
            EETypePtr eeType = typeHandle.ToEETypePtr();
            if (RuntimeImports.RhIsNullable(eeType)
                || eeType == typeof(String).TypeHandle.ToEETypePtr()
                || eeType == typeof(IntPtr).TypeHandle.ToEETypePtr()
                || eeType == typeof(UIntPtr).TypeHandle.ToEETypePtr()
               )
                return null;
            return RuntimeImports.RhNewObject(eeType);
        }

        //
        // Perform the equivalent of a "newarr" The resulting array is zero-initialized.
        //
        public static Array NewArray(RuntimeTypeHandle typeHandleForArrayType, int count)
        {
            // Don't make the easy mistake of passing in the element EEType rather than the "array of element" EEType.
            Debug.Assert(typeHandleForArrayType.ToEETypePtr().IsArray);
            return RuntimeImports.RhNewArray(typeHandleForArrayType.ToEETypePtr(), count);
        }

        //
        // Perform the equivalent of a "newarr" The resulting array is zero-initialized.
        //
        // Note that invoking NewMultiDimArray on a rank-1 array type is not the same thing as invoking NewArray().
        //
        // As a concession to the fact that we don't actually support non-zero lower bounds, "lowerBounds" accepts "null"
        // to avoid unnecessary array allocations by the caller.
        //
        public static Array NewMultiDimArray(RuntimeTypeHandle typeHandleForArrayType, int[] lengths, int[] lowerBounds)
        {
            Debug.Assert(lengths != null);
            Debug.Assert(lowerBounds == null || lowerBounds.Length == lengths.Length);

            if (lowerBounds != null)
            {
                foreach (int lowerBound in lowerBounds)
                {
                    if (lowerBound != 0)
                        throw new PlatformNotSupportedException(SR.Arg_NotSupportedNonZeroLowerBound);
                }
            }

            MDArray mdArray = (MDArray)(NewObject(typeHandleForArrayType));
            mdArray.MDInitialize(lengths);
            return mdArray;
        }

        public static IntPtr GetAllocateObjectHelperForType(RuntimeTypeHandle type)
        {
            return RuntimeImports.RhGetRuntimeHelperForType(CreateEETypePtr(type), RuntimeImports.RuntimeHelperKind.AllocateObject);
        }

        public static IntPtr GetAllocateArrayHelperForType(RuntimeTypeHandle type)
        {
            return RuntimeImports.RhGetRuntimeHelperForType(CreateEETypePtr(type), RuntimeImports.RuntimeHelperKind.AllocateArray);
        }

        public static IntPtr GetCastingHelperForType(RuntimeTypeHandle type, bool throwing)
        {
            return RuntimeImports.RhGetRuntimeHelperForType(CreateEETypePtr(type),
                throwing ? RuntimeImports.RuntimeHelperKind.CastClass : RuntimeImports.RuntimeHelperKind.IsInst);
        }

        public static IntPtr GetCheckArrayElementTypeHelperForType(RuntimeTypeHandle type)
        {
            return RuntimeImports.RhGetRuntimeHelperForType(CreateEETypePtr(type), RuntimeImports.RuntimeHelperKind.CheckArrayElementType);
        }

        public static IntPtr GetDispatchMapForType(RuntimeTypeHandle typeHandle)
        {
            return RuntimeImports.RhGetDispatchMapForType(CreateEETypePtr(typeHandle));
        }

        public static IntPtr GetFallbackDefaultConstructor()
        {
            return System.Runtime.InteropServices.AddrofIntrinsics.AddrOf<Action>(System.Activator.ClassWithMissingConstructor.MissingDefaultConstructorStaticEntryPoint);
        }

        //
        // Helper to create a delegate on a runtime-supplied type.
        //
        public static Delegate CreateDelegate(RuntimeTypeHandle typeHandleForDelegate, IntPtr ldftnResult, Object thisObject, bool isStatic, bool isOpen)
        {
            return Delegate.CreateDelegate(typeHandleForDelegate.ToEETypePtr(), ldftnResult, thisObject, isStatic: isStatic, isOpen: isOpen);
        }

        //
        // Helper to extract the artifact that uniquely identifies a method in the runtime mapping tables.
        //
        public static IntPtr GetDelegateLdFtnResult(Delegate d, out RuntimeTypeHandle typeOfFirstParameterIfInstanceDelegate, out bool isOpenResolver)
        {
            return d.GetFunctionPointer(out typeOfFirstParameterIfInstanceDelegate, out isOpenResolver);
        }

        public static int GetLoadedModules(IntPtr[] resultArray)
        {
            return (int)RuntimeImports.RhGetLoadedModules(resultArray);
        }

        public static IntPtr GetModuleFromPointer(IntPtr pointerVal)
        {
            return RuntimeImports.RhGetModuleFromPointer(pointerVal);
        }

        public static unsafe bool FindBlob(IntPtr hOsModule, int blobId, IntPtr ppbBlob, IntPtr pcbBlob)
        {
            return RuntimeImports.RhFindBlob(hOsModule, (uint)blobId, (byte**)ppbBlob, (uint*)pcbBlob);
        }

        public static IntPtr GetPointerFromTypeHandle(RuntimeTypeHandle typeHandle)
        {
            return typeHandle.ToEETypePtr().RawValue;
        }

        public static IntPtr GetModuleFromTypeHandle(RuntimeTypeHandle typeHandle)
        {
            return RuntimeImports.RhGetModuleFromEEType(GetPointerFromTypeHandle(typeHandle));
        }

        public static RuntimeTypeHandle CreateRuntimeTypeHandle(IntPtr ldTokenResult)
        {
            return new RuntimeTypeHandle(new EETypePtr(ldTokenResult));
        }

        public unsafe static IntPtr GetThreadStaticFieldAddress(RuntimeTypeHandle typeHandle, IntPtr fieldCookie)
        {
            return new IntPtr(RuntimeImports.RhGetThreadStaticFieldAddress(typeHandle.ToEETypePtr(), fieldCookie));
        }

        public unsafe static void StoreValueTypeField(IntPtr address, Object fieldValue, RuntimeTypeHandle fieldType)
        {
            RuntimeImports.RhUnbox(fieldValue, *(void**)&address, fieldType.ToEETypePtr());
        }

        public unsafe static Object LoadValueTypeField(IntPtr address, RuntimeTypeHandle fieldType)
        {
            return RuntimeImports.RhBox(fieldType.ToEETypePtr(), *(void**)&address);
        }

        public unsafe static void StoreValueTypeField(Object obj, int fieldOffset, Object fieldValue, RuntimeTypeHandle fieldType)
        {
            fixed (IntPtr* pObj = &obj.m_pEEType)
            {
                IntPtr pData = (IntPtr)pObj;
                IntPtr pField = pData + fieldOffset;
                StoreValueTypeField(pField, fieldValue, fieldType);
            }
        }

        public unsafe static Object LoadValueTypeField(Object obj, int fieldOffset, RuntimeTypeHandle fieldType)
        {
            fixed (IntPtr* pObj = &obj.m_pEEType)
            {
                IntPtr pData = (IntPtr)pObj;
                IntPtr pField = pData + fieldOffset;
                return LoadValueTypeField(pField, fieldType);
            }
        }

        public static void StoreReferenceTypeField(IntPtr address, Object fieldValue)
        {
            // Doing an interlocked exchange makes sure there is a proper memory barrier
            Interlocked.Exchange<Object>(address, fieldValue);
        }

        public static Object LoadReferenceTypeField(IntPtr address)
        {
            return Interlocked.CompareExchange<Object>(address, null, null);
        }

        public unsafe static void StoreReferenceTypeField(Object obj, int fieldOffset, Object fieldValue)
        {
            fixed (IntPtr* pObj = &obj.m_pEEType)
            {
                IntPtr pData = (IntPtr)pObj;
                IntPtr pField = pData + fieldOffset;
                StoreReferenceTypeField(pField, fieldValue);
            }
        }

        public unsafe static Object LoadReferenceTypeField(Object obj, int fieldOffset)
        {
            fixed (IntPtr* pObj = &obj.m_pEEType)
            {
                IntPtr pData = (IntPtr)pObj;
                IntPtr pField = pData + fieldOffset;
                return LoadReferenceTypeField(pField);
            }
        }

        public static object CallDynamicInvokeMethod(object thisPtr, IntPtr methodToCall, object thisPtrDynamicInvokeMethod, IntPtr dynamicInvokeHelperMethod, IntPtr dynamicInvokeHelperGenericDictionary, string defaultValueString, object[] parameters, bool invokeMethodHelperIsThisCall, bool methodToCallIsThisCall)
        {
            return InvokeUtils.CallDynamicInvokeMethod(thisPtr, methodToCall, thisPtrDynamicInvokeMethod, dynamicInvokeHelperMethod, dynamicInvokeHelperGenericDictionary, defaultValueString, parameters, invokeMethodHelperIsThisCall, methodToCallIsThisCall);
        }

        public unsafe static void EnsureClassConstructorRun(IntPtr staticClassConstructionContext)
        {
            StaticClassConstructionContext* context = (StaticClassConstructionContext*)staticClassConstructionContext;
            ClassConstructorRunner.EnsureClassConstructorRun(null, context);
        }

        public static bool GetMdArrayRankTypeHandleIfSupported(int rank, out RuntimeTypeHandle mdArrayTypeHandle)
        {
            switch (rank)
            {
                case 2:
                    mdArrayTypeHandle = typeof(MDArrayRank2<>).TypeHandle;
                    return true;
                case 3:
                    mdArrayTypeHandle = typeof(MDArrayRank3<>).TypeHandle;
                    return true;
                case 4:
                    mdArrayTypeHandle = typeof(MDArrayRank4<>).TypeHandle;
                    return true;
                default:
                    mdArrayTypeHandle = default(RuntimeTypeHandle);
                    return false;
            }
        }

        public static RuntimeTypeHandle GetTypeHandleIfAvailable(Type type)
        {
            RuntimeType runtimeType = type as RuntimeType;
            if (runtimeType == null)
                return default(RuntimeTypeHandle);
            RuntimeTypeHandle runtimeTypeHandle;
            if (!runtimeType.InternalTryGetTypeHandle(out runtimeTypeHandle))
                return default(RuntimeTypeHandle);
            return runtimeTypeHandle;
        }


        public static RuntimeTypeHandle GetRelatedParameterTypeHandle(RuntimeTypeHandle parameterTypeHandle)
        {
            EETypePtr elementType = RuntimeImports.RhGetRelatedParameterType(parameterTypeHandle.ToEETypePtr());
            return new RuntimeTypeHandle(elementType);
        }

        public static bool IsValueType(RuntimeTypeHandle type)
        {
            return RuntimeImports.RhIsValueType(CreateEETypePtr(type));
        }

        public static bool IsInterface(RuntimeTypeHandle type)
        {
            return RuntimeImports.RhIsInterface(CreateEETypePtr(type));
        }

        public static unsafe object Box(RuntimeTypeHandle type, IntPtr address)
        {
            return RuntimeImports.RhBox(type.ToEETypePtr(), address.ToPointer());
        }

        // Used to mutate the first parameter in a closed static delegate.  Note that this does no synchronization of any kind;
        // use only on delegate instances you're sure nobody else is using.
        public static void SetClosedStaticDelegateFirstParameter(Delegate del, object firstParameter)
        {
            del.SetClosedStaticFirstParameter(firstParameter);
        }

        //==============================================================================================
        // Execution engine policies.
        //==============================================================================================
        //
        // This returns a generic type with one generic parameter (representing the array element type)
        // whose base type and interface list determines what TypeInfo.BaseType and TypeInfo.ImplementedInterfaces
        // return for types that return true for IsArray.
        //
        public static RuntimeTypeHandle ProjectionTypeForArrays
        {
            get
            {
                return typeof(Array<>).TypeHandle;
            }
        }

        //
        // Returns the name of a virtual assembly we dump types private class library-Reflectable ty[es for internal class library use.
        // The assembly binder visible to apps will never reveal this assembly.
        //
        // Note that this is not versionable as it is exposed as a const (and needs to be a const so we can used as a custom attribute argument - which
        // is the other reason this string is not versionable.)
        //
        public const String HiddenScopeAssemblyName = "HiddenScope, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";

        //
        // This implements the "IsAssignableFrom()" api for runtime-created types. By policy, we let the underlying runtime decide assignability.
        //
        public static bool IsAssignableFrom(RuntimeTypeHandle dstType, RuntimeTypeHandle srcType)
        {
            EETypePtr dstEEType = dstType.ToEETypePtr();
            EETypePtr srcEEType = srcType.ToEETypePtr();

            return RuntimeImports.AreTypesAssignable(srcEEType, dstEEType);
        }

        //
        // Return a type's base type using the runtime type system. If the underlying runtime type system does not support
        // this operation, return false and TypeInfo.BaseType will fall back to metadata.
        //
        // Note that "default(RuntimeTypeHandle)" is a valid result that will map to a null result. (For example, System.Object has a "null" base type.)
        //
        public static bool TryGetBaseType(RuntimeTypeHandle typeHandle, out RuntimeTypeHandle baseTypeHandle)
        {
            EETypePtr eeType = typeHandle.ToEETypePtr();
            RuntimeImports.RhEETypeClassification eeTypeClassification = RuntimeImports.RhGetEETypeClassification(eeType);
            if (eeTypeClassification == RuntimeImports.RhEETypeClassification.GenericTypeDefinition ||
                eeTypeClassification == RuntimeImports.RhEETypeClassification.UnmanagedPointer)
            {
                baseTypeHandle = default(RuntimeTypeHandle);
                return false;
            }
            baseTypeHandle = new RuntimeTypeHandle(eeType.BaseType);
            return true;
        }

        //
        // Return a type's transitive implemeted interface list using the runtime type system. If the underlying runtime type system does not support
        // this operation, return null and TypeInfo.ImplementedInterfaces will fall back to metadata. Note that returning null is not the same thing
        // as returning a 0-length enumerable.
        //
        public static IEnumerable<RuntimeTypeHandle> TryGetImplementedInterfaces(RuntimeTypeHandle typeHandle)
        {
            EETypePtr eeType = typeHandle.ToEETypePtr();
            RuntimeImports.RhEETypeClassification eeTypeClassification = RuntimeImports.RhGetEETypeClassification(eeType);
            if (eeTypeClassification == RuntimeImports.RhEETypeClassification.GenericTypeDefinition ||
                eeTypeClassification == RuntimeImports.RhEETypeClassification.UnmanagedPointer)
                return null;

            uint numInterfaces = RuntimeImports.RhGetNumInterfaces(eeType);
            LowLevelList<RuntimeTypeHandle> implementedInterfaces = new LowLevelList<RuntimeTypeHandle>();
            for (uint i = 0; i < numInterfaces; i++)
            {
                EETypePtr ifcEEType = RuntimeImports.RhGetInterface(eeType, i);
                RuntimeTypeHandle ifcrth = new RuntimeTypeHandle(ifcEEType);
                if (Callbacks.IsReflectionBlocked(ifcrth))
                    continue;

                implementedInterfaces.Add(ifcrth);
            }
            return implementedInterfaces.ToArray();
        }

        private static RuntimeTypeHandle CreateRuntimeTypeHandle(EETypePtr eeType)
        {
            return new RuntimeTypeHandle(eeType);
        }

        private static EETypePtr CreateEETypePtr(RuntimeTypeHandle runtimeTypeHandle)
        {
            return runtimeTypeHandle.ToEETypePtr();
        }

        public static int GetGCDescSize(RuntimeTypeHandle typeHandle)
        {
            EETypePtr eeType = CreateEETypePtr(typeHandle);
            return RuntimeImports.RhGetGCDescSize(eeType);
        }

        public unsafe static bool CreateGenericInstanceDescForType(RuntimeTypeHandle typeHandle, int arity, int nonGcStaticDataSize,
            int nonGCStaticDataOffset, int gcStaticDataSize, int threadStaticsOffset, IntPtr gcStaticsDesc, IntPtr threadStaticsDesc, int[] genericVarianceFlags)
        {
            EETypePtr eeType = CreateEETypePtr(typeHandle);
            fixed (int* pGenericVarianceFlags = genericVarianceFlags)
            {
                return RuntimeImports.RhCreateGenericInstanceDescForType2(eeType, arity, nonGcStaticDataSize, nonGCStaticDataOffset, gcStaticDataSize,
                    threadStaticsOffset, gcStaticsDesc.ToPointer(), threadStaticsDesc.ToPointer(), pGenericVarianceFlags);
            }
        }

        public static int GetInterfaceCount(RuntimeTypeHandle typeHandle)
        {
            EETypePtr eeType = CreateEETypePtr(typeHandle);
            return (int)RuntimeImports.RhGetNumInterfaces(eeType);
        }

        public static RuntimeTypeHandle GetInterface(RuntimeTypeHandle typeHandle, int index)
        {
            EETypePtr eeType = CreateEETypePtr(typeHandle);
            EETypePtr eeInterface = RuntimeImports.RhGetInterface(eeType, (uint)index);
            return CreateRuntimeTypeHandle(eeInterface);
        }

        public static void SetInterface(RuntimeTypeHandle typeHandle, int index, RuntimeTypeHandle interfaceTypeHandle)
        {
            EETypePtr eeType = CreateEETypePtr(typeHandle);
            EETypePtr eeInterface = CreateEETypePtr(interfaceTypeHandle);
            RuntimeImports.RhSetInterface(eeType, index, eeInterface);
        }

        public static IntPtr NewInterfaceDispatchCell(RuntimeTypeHandle interfaceTypeHandle, int slotNumber)
        {
            EETypePtr eeInterfaceType = CreateEETypePtr(interfaceTypeHandle);
            IntPtr cell = RuntimeImports.RhNewInterfaceDispatchCell(eeInterfaceType, slotNumber);
            if (cell == IntPtr.Zero)
                throw new OutOfMemoryException();
            return cell;
        }

        public static IntPtr GetNonGcStaticFieldData(RuntimeTypeHandle typeHandle)
        {
            EETypePtr eeType = CreateEETypePtr(typeHandle);
            return RuntimeImports.RhGetNonGcStaticFieldData(eeType);
        }

        public static IntPtr GetGcStaticFieldData(RuntimeTypeHandle typeHandle)
        {
            EETypePtr eeType = CreateEETypePtr(typeHandle);
            return RuntimeImports.RhGetGcStaticFieldData(eeType);
        }

        public static int GetValueTypeSize(RuntimeTypeHandle typeHandle)
        {
            EETypePtr eeType = CreateEETypePtr(typeHandle);
            Debug.Assert(eeType.IsValueType);
            return (int)RuntimeImports.RhGetValueTypeSize(eeType);
        }

        public static RuntimeTypeHandle GetCanonType(CanonTypeKind kind)
        {
            switch (kind)
            {
                case CanonTypeKind.NormalCanon:
                    return typeof(System.__Canon).TypeHandle;
                case CanonTypeKind.UniversalCanon:
                    return typeof(System.__UniversalCanon).TypeHandle;
                default:
                    Debug.Assert(false);
                    return default(RuntimeTypeHandle);
            }
        }

        public unsafe static void SetInstantiation(RuntimeTypeHandle typeHandle, RuntimeTypeHandle genericTypeDefinitionHandle, RuntimeTypeHandle[] genericTypeArgumentHandles)
        {
            EETypePtr eeTypeDefinition = CreateEETypePtr(genericTypeDefinitionHandle);
            EETypePtr eeType = CreateEETypePtr(typeHandle);

            int arity = genericTypeArgumentHandles.Length;
            EETypePtr* eeTypeArguments = stackalloc EETypePtr[genericTypeArgumentHandles.Length];
            for (int i = 0; i < arity; i++)
            {
                eeTypeArguments[i] = CreateEETypePtr(genericTypeArgumentHandles[i]);
            }

            if (!RuntimeImports.RhSetGenericInstantiation(eeType, eeTypeDefinition, arity, eeTypeArguments))
            {
                throw new OutOfMemoryException();
            }
        }

        public static bool IsGenericType(RuntimeTypeHandle typeHandle)
        {
            return RuntimeImports.RhGetEETypeClassification(CreateEETypePtr(typeHandle)) == RuntimeImports.RhEETypeClassification.Generic;
        }

        public static bool IsArrayType(RuntimeTypeHandle typeHandle)
        {
            return RuntimeImports.RhGetEETypeClassification(CreateEETypePtr(typeHandle)) == RuntimeImports.RhEETypeClassification.Array;
        }

        public static bool IsDynamicType(RuntimeTypeHandle typeHandle)
        {
            return RuntimeImports.RhIsDynamicType(CreateEETypePtr(typeHandle));
        }

        public static bool HasCctor(RuntimeTypeHandle typeHandle)
        {
            return RuntimeImports.RhHasCctor(CreateEETypePtr(typeHandle));
        }

        public static IntPtr ResolveDispatchOnType(RuntimeTypeHandle instanceType, RuntimeTypeHandle interfaceType, int slot)
        {
            return RuntimeImports.RhResolveDispatchOnType(CreateEETypePtr(instanceType), CreateEETypePtr(interfaceType), checked((ushort)slot));
        }

        public static IntPtr ResolveDispatch(object instance, RuntimeTypeHandle interfaceType, int slot)
        {
            return RuntimeImports.RhResolveDispatch(instance, CreateEETypePtr(interfaceType), checked((ushort)slot));
        }

        public static IntPtr GVMLookupForSlot(RuntimeTypeHandle type, RuntimeMethodHandle slot)
        {
            return GenericVirtualMethodSupport.GVMLookupForSlot(type, slot);
        }

        public static bool IsUnmanagedPointerType(RuntimeTypeHandle typeHandle)
        {
            return RuntimeImports.RhGetEETypeClassification(CreateEETypePtr(typeHandle)) == RuntimeImports.RhEETypeClassification.UnmanagedPointer;
        }

        public static bool IsGenericTypeDefinition(RuntimeTypeHandle typeHandle)
        {
            return RuntimeImports.RhGetEETypeClassification(CreateEETypePtr(typeHandle)) == RuntimeImports.RhEETypeClassification.GenericTypeDefinition;
        }

        //
        // This implements the equivalent of the desktop's InvokeUtil::CanPrimitiveWiden() routine.
        //
        public static bool CanPrimitiveWiden(RuntimeTypeHandle srcType, RuntimeTypeHandle dstType)
        {
            RuntimeImports.RhEETypeClassification srcEETypeClassification = srcType.Classification;
            RuntimeImports.RhEETypeClassification dstEETypeClassification = dstType.Classification;
            if (srcEETypeClassification == RuntimeImports.RhEETypeClassification.GenericTypeDefinition ||
                dstEETypeClassification == RuntimeImports.RhEETypeClassification.GenericTypeDefinition)
                return false;
            if (srcEETypeClassification == RuntimeImports.RhEETypeClassification.UnmanagedPointer ||
                dstEETypeClassification == RuntimeImports.RhEETypeClassification.UnmanagedPointer)
                return false;


            EETypePtr srcEEType = srcType.ToEETypePtr();
            EETypePtr dstEEType = dstType.ToEETypePtr();
            if (!srcEEType.IsPrimitive)
                return false;
            if (!dstEEType.IsPrimitive)
                return false;
            if (!srcEEType.CorElementTypeInfo.CanWidenTo(dstEEType.CorElementType))
                return false;
            return true;
        }

        public static Object CheckArgument(Object srcObject, RuntimeTypeHandle dstType)
        {
            return InvokeUtils.CheckArgument(srcObject, dstType);
        }

        public static bool IsAssignable(Object srcObject, RuntimeTypeHandle dstType)
        {
            EETypePtr srcEEType = srcObject.EETypePtr;
            return RuntimeImports.AreTypesAssignable(srcEEType, dstType.ToEETypePtr());
        }

        //==============================================================================================
        // Nullable<> support
        //==============================================================================================
        public static bool IsNullable(RuntimeTypeHandle declaringTypeHandle)
        {
            return RuntimeImports.RhIsNullable(declaringTypeHandle.ToEETypePtr());
        }

        public static RuntimeTypeHandle GetNullableType(RuntimeTypeHandle nullableType)
        {
            EETypePtr theT = RuntimeImports.RhGetNullableType(nullableType.ToEETypePtr());
            return new RuntimeTypeHandle(theT);
        }

        //
        // Useful helper for finding .pdb's. (This design is admittedly tied to the single-module design of Project N.)
        //
        public static String TryGetFullPathToMainApplication()
        {
            Func<String> delegateToAnythingInsideMergedApp = TryGetFullPathToMainApplication;
            RuntimeTypeHandle thDummy;
            bool boolDummy;
            IntPtr ipToAnywhereInsideMergedApp = delegateToAnythingInsideMergedApp.GetFunctionPointer(out thDummy, out boolDummy);
            IntPtr moduleBase = RuntimeImports.RhGetModuleFromPointer(ipToAnywhereInsideMergedApp);
            return TryGetFullPathToApplicationModule(moduleBase);
        }

        /// <summary>
        /// Locate the file path for a given native application module.
        /// </summary>
        /// <param name="moduleBase">Module base address</param>
        public static unsafe String TryGetFullPathToApplicationModule(IntPtr moduleBase)
        {
            char* pModuleName;
            int numChars = RuntimeImports.RhGetModuleFileName(moduleBase, out pModuleName);
            String modulePath = new String(pModuleName, 0, numChars);
            return modulePath;
        }

        //
        // Useful helper for getting RVA's to pass to DiaSymReader.
        //
        public static int ConvertIpToRva(IntPtr ip)
        {
            unsafe
            {
                IntPtr moduleBase = RuntimeImports.RhGetModuleFromPointer(ip);
                return (int)(ip.ToInt64() - moduleBase.ToInt64());
            }
        }


        public static IntPtr GetRuntimeTypeHandleRawValue(RuntimeTypeHandle runtimeTypeHandle)
        {
            return runtimeTypeHandle.RawValue;
        }

        // if functionPointer points at an import or unboxing stub, find the target of the stub
        public static IntPtr GetCodeTarget(IntPtr functionPointer)
        {
            return RuntimeImports.RhGetCodeTarget(functionPointer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr RuntimeCacheLookup(IntPtr context, IntPtr signature, int registeredResolutionFunction, object contextObject, out IntPtr auxResult)
        {
            return TypeLoaderExports.RuntimeCacheLookupInCache(context, signature, registeredResolutionFunction, contextObject, out auxResult);
        }

        public static int RegisterResolutionFunctionWithRuntimeCache(IntPtr functionPointer)
        {
            return TypeLoaderExports.RegisterResolutionFunction(functionPointer);
        }

        //==============================================================================================
        // Internals
        //==============================================================================================
        [CLSCompliant(false)]
        public static ReflectionExecutionDomainCallbacks CallbacksIfAvailable
        {
            get
            {
                return s_reflectionExecutionDomainCallbacks;
            }
        }

        [CLSCompliant(false)]
        public static ReflectionExecutionDomainCallbacks Callbacks
        {
            get
            {
                ReflectionExecutionDomainCallbacks callbacks = s_reflectionExecutionDomainCallbacks;
                if (callbacks != null)
                    return callbacks;
                throw new InvalidOperationException(SR.InvalidOperation_TooEarly);
            }
        }

        internal static TypeLoaderCallbacks TypeLoaderCallbacksIfAvailable
        {
            get
            {
                return s_typeLoaderCallbacks;
            }
        }

        internal static TypeLoaderCallbacks TypeLoaderCallbacks
        {
            get
            {
                TypeLoaderCallbacks callbacks = s_typeLoaderCallbacks;
                if (callbacks != null)
                    return callbacks;
                throw new InvalidOperationException(SR.InvalidOperation_TooEarly);
            }
        }

        internal static RuntimeTypeHandle[] ToTypeHandleArray(this RuntimeType[] runtimeTypes)
        {
            RuntimeTypeHandle[] runtimeTypeHandles = new RuntimeTypeHandle[runtimeTypes.Length];
            for (int i = 0; i < runtimeTypes.Length; i++)
                runtimeTypeHandles[i] = runtimeTypes[i].TypeHandle;
            return runtimeTypeHandles;
        }

        internal static RuntimeType ToRuntimeType(this RuntimeTypeHandle runtimeTypeHandle)
        {
            return ReflectionCoreNonPortable.GetTypeForRuntimeTypeHandle(runtimeTypeHandle);
        }

        internal static RuntimeType[] ToRuntimeTypeArray(this RuntimeTypeHandle[] runtimeTypeHandles)
        {
            RuntimeType[] runtimeTypes = new RuntimeType[runtimeTypeHandles.Length];
            for (int i = 0; i < runtimeTypes.Length; i++)
                runtimeTypes[i] = runtimeTypeHandles[i].ToRuntimeType();
            return runtimeTypes;
        }

        private static volatile ReflectionExecutionDomainCallbacks s_reflectionExecutionDomainCallbacks;
        private static TypeLoaderCallbacks s_typeLoaderCallbacks;

        public static void ReportUnhandledException(Exception exception)
        {
            RuntimeExceptionHelpers.ReportUnhandledException(exception);
        }

        public static void GenerateExceptionInformationForDump(Exception currentException, IntPtr exceptionCCWPtr)
        {
            RuntimeExceptionHelpers.GenerateExceptionInformationForDump(currentException, exceptionCCWPtr);
        }

        [Intrinsic]
        public static object ConvertIntPtrToObjectReference(IntPtr pointerToObject)
        {
            return ConvertIntPtrToObjectReference(pointerToObject);
        }

        public static int GetCorElementType(RuntimeTypeHandle type)
        {
            return (int)RuntimeImports.RhGetCorElementType(type.ToEETypePtr());
        }

        // Move memory which may be on the heap which may have object references in it.
        // In general, a memcpy on the heap is unsafe, but this is able to perform the
        // correct write barrier such that the GC is not incorrectly impacted.
        public unsafe static void BulkMoveWithWriteBarrier(IntPtr dmem, IntPtr smem, int size)
        {
            RuntimeImports.RhBulkMoveWithWriteBarrier((byte*)dmem.ToPointer(), (byte*)smem.ToPointer(), size);
        }

        public static IntPtr GetUniversalTransitionThunk()
        {
            return RuntimeImports.RhGetUniversalTransitionThunk();
        }

        [System.Diagnostics.DebuggerStepThrough]
        /* TEMP workaround due to bug 149078 */
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CallDescrWorker(IntPtr callDescr)
        {
            RuntimeImports.RhCallDescrWorker(callDescr);
        }

        /// <summary>
        /// This method opens a file if it exists. For console apps, ILC will inject a call to
        /// InitializeDesktopSupport in StartupCodeTrigger. This will set up the
        /// _desktopSupportCallbacks that can be then used to open files.
        /// The return type is actually a Stream (which we cannot use here due to layering).
        /// This mechanism shields AppX builds from the cost of merging in System.IO.FileSystem.
        /// </summary>
        /// <param name="path">File path / name</param>
        /// <returns>An initialized Stream instance or null if the file doesn't exist;
        /// throws when the desktop compat quirks are not enabled</returns>
        public static object OpenFileIfExists(string path)
        {
            if (s_desktopSupportCallbacks == null)
            {
                throw new NotSupportedException();
            }

            return s_desktopSupportCallbacks.OpenFileIfExists(path);
        }

        [System.Runtime.InteropServices.McgIntrinsicsAttribute]
        internal class RawCalliHelper
        {
            public static unsafe void Call<T>(System.IntPtr pfn, void* arg1, ref T arg2)
            {
                // This will be filled in by an IL transform
            }
        }

        /// <summary>
        /// This method creates a conservatively reported region and calls a function 
        /// while that region is conservatively reported. 
        /// </summary>
        /// <param name="cbBuffer">size of buffer to allocated (buffer size described in bytes)</param>
        /// <param name="pfnTargetToInvoke">function pointer to execute. Must have the calling convention void(void* pBuffer, ref T context)</param>
        /// <param name="context">context to pass to inner function. Passed by-ref to allow for efficient use of a struct as a context.</param>
        public static void RunFunctionWithConservativelyReportedBuffer<T>(int cbBuffer, IntPtr pfnTargetToInvoke, ref T context)
        {
            RuntimeImports.ConservativelyReportedRegionDesc regionDesc = new RuntimeImports.ConservativelyReportedRegionDesc();
            RunFunctionWithConservativelyReportedBufferInternal(cbBuffer, pfnTargetToInvoke, ref context, ref regionDesc);
        }

        // Marked as no-inlining so optimizer won't decide to optimize away the fact that pRegionDesc is a pinned interior pointer.
        // This function must also not make a p/invoke transition, or the fixed statement reporting of the ConservativelyReportedRegionDesc
        // will be ignored.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static unsafe void RunFunctionWithConservativelyReportedBufferInternal<T>(int cbBuffer, IntPtr pfnTargetToInvoke, ref T context, ref RuntimeImports.ConservativelyReportedRegionDesc regionDesc)
        {
            fixed (RuntimeImports.ConservativelyReportedRegionDesc* pRegionDesc = &regionDesc)
            {
                int cbBufferAligned = (cbBuffer + (sizeof(IntPtr) - 1)) & ~(sizeof(IntPtr) - 1);
                // The conservative region must be IntPtr aligned, and a multiple of IntPtr in size
                void* region = stackalloc IntPtr[cbBufferAligned / sizeof(IntPtr)];
                RuntimeImports.RhInitializeConservativeReportingRegion(pRegionDesc, region, cbBufferAligned);

                RawCalliHelper.Call<T>(pfnTargetToInvoke, region, ref context);

                RuntimeImports.RhDisableConservativeReportingRegion(pRegionDesc);
            }
        }
    }
}

namespace System.Runtime.InteropServices
{
    [McgIntrinsics]
    internal static class AddrofIntrinsics
    {
        // This method is implemented elsewhere in the toolchain
        internal static IntPtr AddrOf<T>(T ftn) { throw new PlatformNotSupportedException(); }
    }
}

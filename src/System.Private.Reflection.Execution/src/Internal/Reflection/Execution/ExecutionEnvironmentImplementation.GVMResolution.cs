// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.Reflection.Execution
{
    using global::System;
    using global::System.Runtime.InteropServices;

    using global::Internal.Runtime;
    using global::Internal.Runtime.Augments;
    using global::Internal.Runtime.CompilerServices;
    using global::Internal.Runtime.TypeLoader;
    using global::Internal.NativeFormat;
    
    using global::Internal.Reflection.Core.Execution;

    using Debug = System.Diagnostics.Debug;


    //==========================================================================================================
    // This file has all the GVM resolution related logic
    //==========================================================================================================
    internal sealed partial class ExecutionEnvironmentImplementation : ExecutionEnvironment
    {
        public MethodNameAndSignature GetMethodNameAndSignatureFromNativeReader(NativeReader nativeLayoutReader, uint nativeLayoutOffset)
        {
            NativeParser parser = new NativeParser(nativeLayoutReader, nativeLayoutOffset);

            string methodName = parser.GetString();

            // Signatures are indirected to through a relative offset so that we don't have to parse them
            // when not comparing signatures (parsing them requires resolving types and is tremendously 
            // expensive).
            NativeParser sigParser = parser.GetParserFromRelativeOffset();
            IntPtr methodSigPtr = sigParser.Reader.OffsetToAddress(sigParser.Offset);

            return new MethodNameAndSignature(methodName, methodSigPtr);
        }

        private bool FindMatchingInterfaceSlot(IntPtr moduleHandle, NativeReader nativeLayoutReader, ref NativeParser entryParser, ref ExternalReferencesTable extRefs, ref RuntimeTypeHandle declaringType, ref MethodNameAndSignature methodNameAndSignature, RuntimeTypeHandle openTargetTypeHandle, RuntimeTypeHandle[] targetTypeInstantiation, bool variantDispatch)
        {
            uint numTargetImplementations = entryParser.GetUnsigned();

            for (uint j = 0; j < numTargetImplementations; j++)
            {
                uint nameAndSigToken = extRefs.GetRvaFromIndex(entryParser.GetUnsigned());
                MethodNameAndSignature targetMethodNameAndSignature = GetMethodNameAndSignatureFromNativeReader(nativeLayoutReader, nameAndSigToken);
                RuntimeTypeHandle targetTypeHandle = extRefs.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());

#if REFLECTION_EXECUTION_TRACE
                ReflectionExecutionLogger.WriteLine("    Searching for GVM implementation on targe type = " + GetTypeNameDebug(targetTypeHandle));
#endif

                uint numIfaceImpls = entryParser.GetUnsigned();

                for (uint k = 0; k < numIfaceImpls; k++)
                {
                    RuntimeTypeHandle implementingTypeHandle = extRefs.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());

                    uint numIfaceSigs = entryParser.GetUnsigned();

                    if (!openTargetTypeHandle.Equals(implementingTypeHandle))
                    {
                        // Skip over signatures data
                        for (uint l = 0; l < numIfaceSigs; l++)
                            entryParser.GetUnsigned();

                        continue;
                    }

                    for (uint l = 0; l < numIfaceSigs; l++)
                    {
                        RuntimeTypeHandle currentIfaceTypeHandle = default(RuntimeTypeHandle);

                        NativeParser ifaceSigParser = new NativeParser(nativeLayoutReader, extRefs.GetRvaFromIndex(entryParser.GetUnsigned()));
                        IntPtr currentIfaceSigPtr = ifaceSigParser.Reader.OffsetToAddress(ifaceSigParser.Offset);

                        if (TypeLoaderEnvironment.Instance.GetTypeFromSignatureAndContext(currentIfaceSigPtr, targetTypeInstantiation, null, out currentIfaceTypeHandle, out currentIfaceSigPtr))
                        {
                            Debug.Assert(!currentIfaceTypeHandle.IsNull());

                            if ((!variantDispatch && declaringType.Equals(currentIfaceTypeHandle)) ||
                                (variantDispatch && RuntimeAugments.IsAssignableFrom(declaringType, currentIfaceTypeHandle)))
                            {
#if REFLECTION_EXECUTION_TRACE
                                ReflectionExecutionLogger.WriteLine("    " + (declaringType.Equals(currentIfaceTypeHandle) ? "Exact" : "Variant-compatible") + " match found on this target type!");
#endif
                                // We found the GVM slot target for the input interface GVM call, so let's update the interface GVM slot and return success to the caller
                                declaringType = targetTypeHandle;
                                methodNameAndSignature = targetMethodNameAndSignature;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private bool ResolveInterfaceGenericVirtualMethodSlot(RuntimeTypeHandle targetTypeHandle, ref RuntimeTypeHandle declaringType, ref MethodNameAndSignature methodNameAndSignature)
        {
            // Get the open type definition of the containing type of the generic virtual method being resolved
            RuntimeTypeHandle openCallingTypeHandle;
            RuntimeTypeHandle[] callingTypeInstantiation;
            if (!TryGetOpenTypeDefinition(declaringType, out openCallingTypeHandle, out callingTypeInstantiation))
                return false;

            // Get the open type definition of the current type of the object instance on which the GVM is being resolved
            RuntimeTypeHandle openTargetTypeHandle;
            RuntimeTypeHandle[] targetTypeInstantiation;
            if (!TryGetOpenTypeDefinition(targetTypeHandle, out openTargetTypeHandle, out targetTypeInstantiation))
                return false;

#if REFLECTION_EXECUTION_TRACE
            ReflectionExecutionLogger.WriteLine("INTERFACE GVM call = " + GetTypeNameDebug(declaringType) + "." + methodNameAndSignature.Name);
#endif

            foreach (IntPtr moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(openTargetTypeHandle)))
            {
                NativeReader gvmTableReader;
                if (!TryGetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.InterfaceGenericVirtualMethodTable, out gvmTableReader))
                    continue;

                NativeReader nativeLayoutReader;
                if (!TryGetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.NativeLayoutInfo, out nativeLayoutReader))
                    continue;

                NativeParser gvmTableParser = new NativeParser(gvmTableReader, 0);
                NativeHashtable gvmHashtable = new NativeHashtable(gvmTableParser);

                ExternalReferencesTable extRefs = default(ExternalReferencesTable);
                extRefs.InitializeCommonFixupsTable(moduleHandle);

                var lookup = gvmHashtable.Lookup(openCallingTypeHandle.GetHashCode());

                NativeParser entryParser;
                while (!(entryParser = lookup.GetNext()).IsNull)
                {
                    RuntimeTypeHandle interfaceTypeHandle = extRefs.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                    if (!openCallingTypeHandle.Equals(interfaceTypeHandle))
                        continue;

                    uint nameAndSigToken = extRefs.GetRvaFromIndex(entryParser.GetUnsigned());
                    MethodNameAndSignature interfaceMethodNameAndSignature = GetMethodNameAndSignatureFromNativeReader(nativeLayoutReader, nameAndSigToken);

                    if (!interfaceMethodNameAndSignature.Equals(methodNameAndSignature))
                        continue;

                    // For each of the possible GVM slot targets for the current interface call, we will do the following:
                    //
                    //  Step 1: Scan the types that currently provide implementations for the current GVM slot target, and look
                    //          for ones that match the target object's type.
                    //
                    //  Step 2: For each type that we find in step #1, get a list of all the interfaces that the current GVM target
                    //          provides an implementation for
                    //
                    //  Step 3: For each interface in the list in step #2, parse the signature of that interface, do the generic argument
                    //          substitution (in case of a generic interface), and check if this interface signature is assignable from the
                    //          calling interface signature (from the name and sig input). if there is an exact match based on 
                    //          interface type, then we've found the right slot. Otherwise, re-scan the entry again and see if some interface 
                    //          type is compatible with the initial slots interface by means of variance. 
                    //          This is done by calling the TypeLoaderEnvironment helper function.
                    //
                    // Example:
                    //      public interface IFoo<out T, out U>
                    //      {
                    //          string M1<V>();
                    //      }
                    //      public class Foo1<T, U> : IFoo<T, U>, IFoo<Kvp<T, string>, U>
                    //      {
                    //          string IFoo<T, U>.M1<V>() { ... }
                    //          public virtual string M1<V>() { ... }
                    //      }
                    //      public class Foo2<T, U> : Foo1<object, U>, IFoo<U, T>
                    //      {
                    //          string IFoo<U, T>.M1<V>() { ... }
                    //      }        
                    //
                    //  GVM Table layout for IFoo<T, U>.M1<V>:
                    //  {
                    //      InterfaceTypeHandle = IFoo<T, U>
                    //      InterfaceMethodNameAndSignature = { "M1", SigOf(string M1) }
                    //      GVMTargetSlots[] = 
                    //      {
                    //          {
                    //              TargetMethodNameAndSignature = { "M1", SigOf(M1) }
                    //              TargetTypeHandle = Foo1<T, U>
                    //              ImplementingTypes[] = {
                    //                  ImplementingTypeHandle = Foo1<T, U>
                    //                  ImplementedInterfacesSignatures[] = { SigOf(IFoo<!0, !1>) }
                    //              }
                    //          },
                    //
                    //          {
                    //              TargetMethodNameAndSignature = { "M1", SigOf(M1) }
                    //              TargetTypeHandle = Foo1<T, U>
                    //              ImplementingTypes[] = {
                    //                  ImplementingTypeHandle = Foo1<T, U>
                    //                  ImplementedInterfacesSignatures[] = { SigOf(IFoo<Kvp<!0, string>, !1>) }
                    //              }
                    //          },
                    //
                    //          {
                    //              TargetMethodNameAndSignature = { "M1", SigOf(M1) }
                    //              TargetTypeHandle = Foo2<T, U>
                    //              ImplementingTypes = {
                    //                  ImplementingTypeHandle = Foo2<T, U>
                    //                  ImplementedInterfacesSignatures[] = { SigOf(IFoo<!1, !0>) }
                    //              }
                    //          }, 
                    //      }
                    //  }
                    //

                    uint currentOffset = entryParser.Offset;

                    // Non-variant dispatch of a variant generic interface generic virtual method.
                    if (FindMatchingInterfaceSlot(moduleHandle, nativeLayoutReader, ref entryParser, ref extRefs, ref declaringType, ref methodNameAndSignature, openTargetTypeHandle, targetTypeInstantiation, false))
                    {
                        return true;
                    }

                    entryParser.Offset = currentOffset;

                    // Variant dispatch of a variant generic interface generic virtual method.
                    if (FindMatchingInterfaceSlot(moduleHandle, nativeLayoutReader, ref entryParser, ref extRefs, ref declaringType, ref methodNameAndSignature, openTargetTypeHandle, targetTypeInstantiation, true))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private unsafe bool ResolveGenericVirtualMethodTarget(RuntimeTypeHandle targetTypeHandle, RuntimeTypeHandle declaringType, RuntimeTypeHandle[] genericArguments, MethodNameAndSignature callingMethodNameAndSignature, out IntPtr methodPointer, out IntPtr dictionaryPointer)
        {
            methodPointer = dictionaryPointer = IntPtr.Zero;

            // Get the open type definition of the containing type of the generic virtual method being resolved
            RuntimeTypeHandle openCallingTypeHandle;
            RuntimeTypeHandle[] callingTypeInstantiation;
            if (!TryGetOpenTypeDefinition(declaringType, out openCallingTypeHandle, out callingTypeInstantiation))
                return false;

            // Get the open type definition of the current type of the object instance on which the GVM is being resolved
            RuntimeTypeHandle openTargetTypeHandle;
            RuntimeTypeHandle[] targetTypeInstantiation;
            if (!TryGetOpenTypeDefinition(targetTypeHandle, out openTargetTypeHandle, out targetTypeInstantiation))
                return false;

            int hashCode = openCallingTypeHandle.GetHashCode();
            hashCode = ((hashCode << 13) ^ hashCode) ^ openTargetTypeHandle.GetHashCode();

#if REFLECTION_EXECUTION_TRACE
            ReflectionExecutionLogger.WriteLine("GVM Target Resolution = " + GetTypeNameDebug(targetTypeHandle) + "." + callingMethodNameAndSignature.Name);
#endif

            foreach (IntPtr moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(openTargetTypeHandle)))
            {
                NativeReader gvmTableReader;
                if (!TryGetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.GenericVirtualMethodTable, out gvmTableReader))
                    continue;

                NativeReader nativeLayoutReader;
                if (!TryGetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.NativeLayoutInfo, out nativeLayoutReader))
                    continue;

                NativeParser gvmTableParser = new NativeParser(gvmTableReader, 0);
                NativeHashtable gvmHashtable = new NativeHashtable(gvmTableParser);
                ExternalReferencesTable extRefs = default(ExternalReferencesTable);
                extRefs.InitializeCommonFixupsTable(moduleHandle);

                var lookup = gvmHashtable.Lookup(hashCode);

                NativeParser entryParser;
                while (!(entryParser = lookup.GetNext()).IsNull)
                {
                    RuntimeTypeHandle parsedCallingTypeHandle = extRefs.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                    if (!parsedCallingTypeHandle.Equals(openCallingTypeHandle))
                        continue;

                    RuntimeTypeHandle parsedTargetTypeHandle = extRefs.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                    if (!parsedTargetTypeHandle.Equals(openTargetTypeHandle))
                        continue;

                    uint parsedCallingNameAndSigToken = extRefs.GetRvaFromIndex(entryParser.GetUnsigned());
                    MethodNameAndSignature parsedCallingNameAndSignature = GetMethodNameAndSignatureFromNativeReader(nativeLayoutReader, parsedCallingNameAndSigToken);

                    if (!parsedCallingNameAndSignature.Equals(callingMethodNameAndSignature))
                        continue;

                    uint parsedTargetMethodNameAndSigToken = extRefs.GetRvaFromIndex(entryParser.GetUnsigned());
                    MethodNameAndSignature targetMethodNameAndSignature = GetMethodNameAndSignatureFromNativeReader(nativeLayoutReader, parsedTargetMethodNameAndSigToken);

                    Debug.Assert(targetMethodNameAndSignature != null);

                    return TypeLoaderEnvironment.Instance.TryGetGenericVirtualMethodPointer(targetTypeHandle, targetMethodNameAndSignature, genericArguments, out methodPointer, out dictionaryPointer);
                }
            }

            return false;
        }

        public sealed override unsafe bool TryGetGenericVirtualTargetForTypeAndSlot(RuntimeTypeHandle targetHandle, ref RuntimeTypeHandle declaringType, RuntimeTypeHandle[] genericArguments, ref string methodName, ref IntPtr methodSignature, out IntPtr methodPointer, out IntPtr dictionaryPointer, out bool slotUpdated)
        {
            MethodNameAndSignature methodNameAndSignature = new MethodNameAndSignature(methodName, methodSignature);

#if REFLECTION_EXECUTION_TRACE
            ReflectionExecutionLogger.WriteLine("GVM resolution starting for " + GetTypeNameDebug(declaringType) + "." + methodNameAndSignature.Name + "(...)  on a target of type " + GetTypeNameDebug(targetHandle) + " ...");
#endif

            if (RuntimeAugments.IsInterface(declaringType))
            {
                methodPointer = IntPtr.Zero;
                dictionaryPointer = IntPtr.Zero;

                slotUpdated = ResolveInterfaceGenericVirtualMethodSlot(targetHandle, ref declaringType, ref methodNameAndSignature);

                methodName = methodNameAndSignature.Name;
                methodSignature = methodNameAndSignature.Signature;
                return slotUpdated;
            }
            else
            {
                slotUpdated = false;
                return ResolveGenericVirtualMethodTarget(targetHandle, declaringType, genericArguments, methodNameAndSignature, out methodPointer, out dictionaryPointer);
            }
        }
    }
}


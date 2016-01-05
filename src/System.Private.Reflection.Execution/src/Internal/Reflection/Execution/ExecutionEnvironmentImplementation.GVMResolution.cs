namespace Internal.Reflection.Execution
{
    using global::System;
    using global::System.Runtime.InteropServices;

    using global::Internal.Runtime;
    using global::Internal.Runtime.Augments;
    using global::Internal.Runtime.CompilerServices;
    using global::Internal.Runtime.TypeLoader;
    using global::Internal.NativeFormat;
    using CanonicalFormKind = global::Internal.TypeSystem.CanonicalFormKind;
    
    using global::Internal.Reflection.Core.Execution;

    using Debug = System.Diagnostics.Debug;


    //==========================================================================================================
    // This file has all the GVM resolution related logic
    //==========================================================================================================
    internal sealed partial class ExecutionEnvironmentImplementation : ExecutionEnvironment
    {
        #region Data Structures
        [StructLayout(LayoutKind.Sequential)]
        internal struct GVMInterfaceSlotEntry
        {
            [StructLayout(LayoutKind.Sequential)]
            internal struct ImplementingTypeDetails
            {
                public uint ImplementingTypeRva;
                public uint[] ImplementedInterfacesSignatures;
                public uint[] ImplementedInterfaceIds;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct GVMTargetSlotEntry
            {
                public MethodNameAndSignature TargetMethodNameAndSignature;
                public uint TargetTypeRva;
                public ImplementingTypeDetails[] ImplementingTypes;
            }

            public MethodNameAndSignature InterfaceMethodNameAndSignature;
            public uint InterfaceTypeRva;
            public GVMTargetSlotEntry[] GVMTargetSlots;

            internal unsafe static GVMInterfaceSlotEntry ReadEntry(ref uint* pBlob, NativeReader reader)
            {
                GVMInterfaceSlotEntry result = new GVMInterfaceSlotEntry();

                uint methodSignature = *pBlob; pBlob++;
                NativeParser parser = new NativeParser(reader, methodSignature);
                result.InterfaceMethodNameAndSignature = GetMethodNameAndSignatureFromNativeParser(parser);

                result.InterfaceTypeRva = *pBlob; pBlob++;
                int numTargetImplementations = (int)*pBlob; pBlob++;
                result.GVMTargetSlots = new GVMTargetSlotEntry[numTargetImplementations];

                for (int i = 0; i < numTargetImplementations; i++)
                {
                    methodSignature = *pBlob; pBlob++;
                    parser = new NativeParser(reader, methodSignature);
                    result.GVMTargetSlots[i].TargetMethodNameAndSignature = GetMethodNameAndSignatureFromNativeParser(parser);
                    result.GVMTargetSlots[i].TargetTypeRva = *pBlob; pBlob++;
                    int numIfaceImpls = (int)*pBlob; pBlob++;
                    result.GVMTargetSlots[i].ImplementingTypes = new ImplementingTypeDetails[numIfaceImpls];

                    for (int j = 0; j < numIfaceImpls; j++)
                    {
                        result.GVMTargetSlots[i].ImplementingTypes[j].ImplementingTypeRva = *pBlob; pBlob++;
                        
                        int numIfaceSigs = (int)*pBlob; pBlob++;
                        result.GVMTargetSlots[i].ImplementingTypes[j].ImplementedInterfacesSignatures = new uint[numIfaceSigs];
                        for (int k = 0; k < numIfaceSigs; k++)
                        {
                            result.GVMTargetSlots[i].ImplementingTypes[j].ImplementedInterfacesSignatures[k] = *pBlob; pBlob++;
                        }

                        int numIfaceIds = (int)*pBlob; pBlob++;
                        result.GVMTargetSlots[i].ImplementingTypes[j].ImplementedInterfaceIds = new uint[numIfaceIds];
                        for (int k = 0; k < numIfaceIds; k++)
                        {
                            result.GVMTargetSlots[i].ImplementingTypes[j].ImplementedInterfaceIds[k] = *pBlob; pBlob++;
                        }
                    }
                }

                return result;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GVMTypeSlotEntry
        {
            [StructLayout(LayoutKind.Sequential)]
            internal struct TargetMethod
            {
                public uint ImplementingTypeRva;
                public TargetMethodInfo[] TargetMethods;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct TargetMethodInfo
            {
                public uint MethodPointer;
                public uint MethodSignature;
                public uint IsUniversalGenericTargetMethod;
            }

            public MethodNameAndSignature MethodNameAndSignature;
            public uint ContainingTypeRva;
            public TargetMethod[] TargetMethods;

            internal unsafe static GVMTypeSlotEntry ReadEntry(ref uint* pBlob, NativeReader reader)
            {
                GVMTypeSlotEntry result = new GVMTypeSlotEntry();

                uint methodSignature = *pBlob; pBlob++;
                NativeParser parser = new NativeParser(reader, methodSignature);
                result.MethodNameAndSignature = GetMethodNameAndSignatureFromNativeParser(parser);

                result.ContainingTypeRva = *pBlob; pBlob++;
                result.TargetMethods = new TargetMethod[*pBlob]; pBlob++;

                for (int i = 0; i < result.TargetMethods.Length; i++)
                {
                    result.TargetMethods[i].ImplementingTypeRva = *pBlob; pBlob++;
                    result.TargetMethods[i].TargetMethods = new TargetMethodInfo[*pBlob]; pBlob++;

                    for (int j = 0; j < result.TargetMethods[i].TargetMethods.Length; j++)
                    {
                        result.TargetMethods[i].TargetMethods[j].MethodPointer = *pBlob; pBlob++;
                        result.TargetMethods[i].TargetMethods[j].MethodSignature = *pBlob; pBlob++;
                        result.TargetMethods[i].TargetMethods[j].IsUniversalGenericTargetMethod = *pBlob; pBlob++;
                    }
                }

                return result;
            }
        }
        #endregion

        private static MethodNameAndSignature GetMethodNameAndSignatureFromNativeParser(NativeParser parser)
        {
            string methodName = parser.GetString();

            // Signatures are indirected to through a relative offset so that we don't have to parse them
            // when not comparing signatures (parsing them requires resolving types and is tremendously 
            // expensive).
            NativeParser sigParser = parser.GetParserFromRelativeOffset();
            IntPtr methodSignature = sigParser.Reader.OffsetToAddress(sigParser.Offset);

            return new MethodNameAndSignature(methodName, methodSignature);
        }

        private unsafe bool CanTargetGVMShareCodeWithTemplateGVM(NativeReader reader, ref ExternalReferencesTable externalReferencesLookup, uint templateGVMSig, RuntimeTypeHandle targetMethodContainingType, RuntimeTypeHandle[] targetMethodInstantiation, CanonicalFormKind canonSearchStyle, out MethodNameAndSignature templateMethodNameAndSignature, out bool requiresDictionary)
        {
            requiresDictionary = false;

            // Parse the template GVM signature

            NativeParser parser = new NativeParser(reader, templateGVMSig);

            RuntimeTypeHandle templateMethodContainingType = externalReferencesLookup.GetRuntimeTypeHandleFromIndex(parser.GetUnsigned());
            IntPtr signatureAddress = parser.Reader.OffsetToAddress(parser.Offset);
            if(!TypeLoaderEnvironment.Instance.TryGetMethodNameAndSignatureFromNativeLayoutSignature(ref signatureAddress, out templateMethodNameAndSignature))
                return false;
            uint offset = reader.AddressToOffset(signatureAddress);
            parser.Offset = offset;

            RuntimeTypeHandle[] templateMethodInstantiation = GetTypeSequence(ref externalReferencesLookup, ref parser);

#if REFLECTION_EXECUTION_TRACE
            string methodInstStr = "";
            foreach (var arg in templateMethodInstantiation)
                methodInstStr += (methodInstStr == "" ? GetTypeNameDebug(arg) : "," + GetTypeNameDebug(arg));
            ReflectionExecutionLogger.WriteLine(" TEMPLATE GVM  = " + GetTypeNameDebug(templateMethodContainingType) + "::Method(" + templateMethodNameAndSignature.Name + "<" + methodInstStr + ">");

            methodInstStr = "";
            foreach (var arg in targetMethodInstantiation)
                methodInstStr += (methodInstStr == "" ? GetTypeNameDebug(arg) : "," + GetTypeNameDebug(arg));
            ReflectionExecutionLogger.WriteLine(" TARGET GVM  = " + GetTypeNameDebug(targetMethodContainingType) + "::Method(" + templateMethodNameAndSignature.Name + "<" + methodInstStr + ">");
#endif

            CanonicallyEquivalentEntryLocator canonHelper = new CanonicallyEquivalentEntryLocator(templateMethodContainingType, canonSearchStyle);
            if (!canonHelper.IsCanonicallyEquivalent(targetMethodContainingType))
                return false;

            if (!TypeLoaderEnvironment.Instance.CanInstantiationsShareCode(templateMethodInstantiation, targetMethodInstantiation, canonSearchStyle))
                return false;

            if (canonSearchStyle == CanonicalFormKind.Universal)
            {
                requiresDictionary = true;
            }
            else
            {
                bool requiresDictFromType = canonHelper.ConversionToCanonFormIsAChange();
                bool requiresDictFromMethod = TypeLoaderEnvironment.Instance.ConversionToCanonFormIsAChange(templateMethodInstantiation, canonSearchStyle);
                requiresDictionary = requiresDictFromType || requiresDictFromMethod;
            }

            return true;
        }
        
        private unsafe bool FindMatchingInterfaceSlot(ref GVMInterfaceSlotEntry entry, byte* pNativeLayoutInfoBlobPtr, ref RuntimeTypeHandle declaringType, ref RuntimeTypeHandle[] genericArguments, ref MethodNameAndSignature methodNameAndSignature, IntPtr moduleHandle, RuntimeTypeHandle openTargetTypeHandle, RuntimeTypeHandle[] targetTypeInstantiation, bool variantDispatch)
        {
            for (int j = 0; j < entry.GVMTargetSlots.Length; j++)
            {
#if REFLECTION_EXECUTION_TRACE
                ReflectionExecutionLogger.WriteLine("                         => (" + entry.GVMTargetSlots[j].TargetMethodNameAndSignature.Name + ") on target type " + GetTypeNameDebug(RvaToRuntimeTypeHandle(moduleHandle, entry.GVMTargetSlots[j].TargetTypeRva)));
#endif

                for (int k = 0; k < entry.GVMTargetSlots[j].ImplementingTypes.Length; k++)
                {
#if REFLECTION_EXECUTION_TRACE
                    ReflectionExecutionLogger.WriteLine("                              TYPE     = " + GetTypeNameDebug(RvaToRuntimeTypeHandle(moduleHandle, entry.GVMTargetSlots[j].ImplementingTypes[k].ImplementingTypeRva)));
#endif

                    if (openTargetTypeHandle.Equals(RvaToRuntimeTypeHandle(moduleHandle, entry.GVMTargetSlots[j].ImplementingTypes[k].ImplementingTypeRva)))
                    {
#if REFLECTION_EXECUTION_TRACE
                        for (int l = 0; l < entry.GVMTargetSlots[j].ImplementingTypes[k].ImplementedInterfacesSignatures.Length; l++)
                            ReflectionExecutionLogger.WriteLine("                                  IFACE " + l + " signature context = " + entry.GVMTargetSlots[j].ImplementingTypes[k].ImplementedInterfacesSignatures[l]);

                        for (int l = 0; l < entry.GVMTargetSlots[j].ImplementingTypes[k].ImplementedInterfaceIds.Length; l++)
                            ReflectionExecutionLogger.WriteLine("                              Impl IFACE #" + entry.GVMTargetSlots[j].ImplementingTypes[k].ImplementedInterfaceIds[l]);
#endif

                        for (int l = 0; l < entry.GVMTargetSlots[j].ImplementingTypes[k].ImplementedInterfaceIds.Length; l++)
                        {
                            uint currentIfaceId = entry.GVMTargetSlots[j].ImplementingTypes[k].ImplementedInterfaceIds[l];
                            Debug.Assert(currentIfaceId < entry.GVMTargetSlots[j].ImplementingTypes[k].ImplementedInterfacesSignatures.Length);
                            uint currentIfaceSig = entry.GVMTargetSlots[j].ImplementingTypes[k].ImplementedInterfacesSignatures[currentIfaceId];

                            RuntimeTypeHandle currentIfaceTypeHandle = default(RuntimeTypeHandle);
                            IntPtr currentIfaceSigPtr = (IntPtr)(pNativeLayoutInfoBlobPtr + currentIfaceSig);
                            if (TypeLoaderEnvironment.Instance.GetTypeFromSignatureAndContext(currentIfaceSigPtr, targetTypeInstantiation, null, out currentIfaceTypeHandle, out currentIfaceSigPtr))
                            {
                                Debug.Assert(!currentIfaceTypeHandle.IsNull());

#if REFLECTION_EXECUTION_TRACE
                                ReflectionExecutionLogger.WriteLine("SOURCE INTERFACE = " + GetTypeNameDebug(declaringType));
                                ReflectionExecutionLogger.WriteLine("TARGET INTERFACE = " + GetTypeNameDebug(currentIfaceTypeHandle));
#endif
                                if ((!variantDispatch && declaringType.Equals(currentIfaceTypeHandle)) ||
                                   (variantDispatch && RuntimeAugments.IsAssignableFrom(declaringType, currentIfaceTypeHandle)))
                                {
                                    // We found the GVM slot target for the input interface GVM call, so let's update the interface GVM slot and return success to the caller
                                    declaringType = RvaToRuntimeTypeHandle(moduleHandle, entry.GVMTargetSlots[j].TargetTypeRva);
                                    methodNameAndSignature = entry.GVMTargetSlots[j].TargetMethodNameAndSignature;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        private unsafe bool ResolveInterfaceGenericVirtualMethodSlot(RuntimeTypeHandle targetTypeHandle, ref RuntimeTypeHandle declaringType, ref RuntimeTypeHandle[] genericArguments, ref MethodNameAndSignature methodNameAndSignature)
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

            foreach (IntPtr moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(openTargetTypeHandle)))
            {
                uint* pBlob;
                uint cbBlob;
                if (!RuntimeAugments.FindBlob(moduleHandle, (int)ReflectionMapBlob.GenericVirtualMethodTable, new IntPtr(&pBlob), new IntPtr(&cbBlob)))
                {
                    continue;
                }
    
                byte* pNativeLayoutInfoBlob;
                uint cbNativeLayoutInfoBlob;
                if (!RuntimeAugments.FindBlob(moduleHandle, (int)ReflectionMapBlob.NativeLayoutInfo, new IntPtr(&pNativeLayoutInfoBlob), new IntPtr(&cbNativeLayoutInfoBlob)))
                {
                    continue;
                }
    
                // Interfaces section size
                pBlob++;
    
                NativeReader reader = new NativeReader(pNativeLayoutInfoBlob, cbNativeLayoutInfoBlob);
    
                uint numIfaceSlots = *pBlob; pBlob++;
                for (int i = 0; i < numIfaceSlots; i++)
                {
                    GVMInterfaceSlotEntry entry = GVMInterfaceSlotEntry.ReadEntry(ref pBlob, reader);
    
    #if REFLECTION_EXECUTION_TRACE
                    ReflectionExecutionLogger.WriteLine("INTERFACE method call    =  (" + entry.InterfaceMethodNameAndSignature.Name + ") on interface " + GetTypeNameDebug(RvaToRuntimeTypeHandle(moduleHandle, entry.InterfaceTypeRva)));
    #endif
    
                    if (entry.InterfaceMethodNameAndSignature.Equals(methodNameAndSignature) && openCallingTypeHandle.Equals(RvaToRuntimeTypeHandle(moduleHandle, entry.InterfaceTypeRva)))
                    {
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
                        //      InterfaceMethodNameAndSignature = { "M1", SigOf(string M1) }
                        //      InterfaceTypeRva = IFoo<T, U>
                        //      GVMTargetSlots[] = {
                        //
                        //          {
                        //              TargetMethodNameAndSignature = { "M1", SigOf(M1) }
                        //              TargetTypeRva = Foo1<T, U>
                        //              ImplementingTypes[] = {
                        //                  ImplementingTypeRva = Foo1<T, U>
                        //                  ImplementedInterfacesSignatures[] = { SigOf(IFoo<!0, !1>), SigOf(IFoo<Kvp<!0, string>, !1>) }
                        //                  ImplementedInterfaceIds[] = { 0 }
                        //              }
                        //          },
                        //
                        //          {
                        //              TargetMethodNameAndSignature = { "M1", SigOf(M1) }
                        //              TargetTypeRva = Foo1<T, U>
                        //              ImplementingTypes[] = {
                        //                  ImplementingTypeRva = Foo1<T, U>
                        //                  ImplementedInterfacesSignatures[] = { SigOf(IFoo<!0, !1>), SigOf(IFoo<Kvp<!0, string>, !1>) }
                        //                  ImplementedInterfaceIds[] = { 1 }
                        //              }
                        //          },
                        //
                        //          {
                        //              TargetMethodNameAndSignature = { "M1", SigOf(M1) }
                        //              TargetTypeRva = Foo2<T, U>
                        //              ImplementingTypes = {
                        //                  ImplementingTypeRva = Foo2<T, U>
                        //                  ImplementedInterfacesSignatures[] = { SigOf(IFoo<!1, !0>) }
                        //                  ImplementedInterfaceIds[] = { 0 }
                        //              }
                        //          }, 
                        //      }
                        //  }
                        //
    
                        // Non-variant dispatch of a variant generic interface generic virtual method.
                        if (FindMatchingInterfaceSlot(ref entry, pNativeLayoutInfoBlob, ref declaringType, ref genericArguments, ref methodNameAndSignature, moduleHandle, openTargetTypeHandle, targetTypeInstantiation, false))
                        {
                            return true;
                        }
    
                        // Variant dispatch of a variant generic interface generic virtual method.
                        if (FindMatchingInterfaceSlot(ref entry, pNativeLayoutInfoBlob, ref declaringType, ref genericArguments, ref methodNameAndSignature, moduleHandle, openTargetTypeHandle, targetTypeInstantiation, true))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private unsafe bool ResolveGenericVirtualMethodTarget(RuntimeTypeHandle targetTypeHandle, RuntimeTypeHandle declaringType, RuntimeTypeHandle[] genericArguments, MethodNameAndSignature methodNameAndSignature, out IntPtr methodPointer, out IntPtr dictionaryPointer)
        {
            methodPointer = IntPtr.Zero;
            dictionaryPointer = IntPtr.Zero;

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

            foreach (IntPtr moduleHandle in ModuleList.Enumerate(RuntimeAugments.GetModuleFromTypeHandle(openTargetTypeHandle)))
            {
                uint* pBlob;
                uint cbBlob;
                if (!RuntimeAugments.FindBlob(moduleHandle, (int)ReflectionMapBlob.GenericVirtualMethodTable, new IntPtr(&pBlob), new IntPtr(&cbBlob)))
                {
                    continue;
                }
    
                byte* pBlobStart = (byte*)pBlob;
                byte* pBlobEnd = (byte*)pBlob + cbBlob;
    
                NativeReader reader = GetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.NativeLayoutInfo);
                ExternalReferencesTable externalReferencesLookup = new ExternalReferencesTable(moduleHandle, ReflectionMapBlob.NativeReferences);
    
                byte* pNativeLayoutInfoBlob;
                uint cbNativeLayoutInfoBlob;
                if (!RuntimeAugments.FindBlob(moduleHandle, (int)ReflectionMapBlob.NativeLayoutInfo, new IntPtr(&pNativeLayoutInfoBlob), new IntPtr(&cbNativeLayoutInfoBlob)))
                {
                    continue;
                }
    
                // Skip over the interfaces section in the GVM table
                uint ifaceSectionSize = *pBlob; pBlob++;
                pBlob = (uint*)(pBlobStart + ifaceSectionSize);
    
                while (pBlob < pBlobEnd)
                {
                    GVMTypeSlotEntry entry = GVMTypeSlotEntry.ReadEntry(ref pBlob, reader);
    
    #if REFLECTION_EXECUTION_TRACE
                    ReflectionExecutionLogger.WriteLine("TYPE GVM method call     =  (" + entry.MethodNameAndSignature.Name + ") on type " + GetTypeNameDebug(RvaToRuntimeTypeHandle(moduleHandle, entry.ContainingTypeRva)));
                    for (int i = 0; i < entry.TargetMethods.Length; i++)
                        ReflectionExecutionLogger.WriteLine("                         => " + entry.TargetMethods[i].TargetMethods.Length + " TEMPLATES available on type " + GetTypeNameDebug(RvaToRuntimeTypeHandle(moduleHandle, entry.TargetMethods[i].ImplementingTypeRva)));
    #endif
    
                    if (entry.MethodNameAndSignature.Equals(methodNameAndSignature) && openCallingTypeHandle.Equals(RvaToRuntimeTypeHandle(moduleHandle, entry.ContainingTypeRva)))
                    {
                        for (int i = 0; i < entry.TargetMethods.Length; i++)
                        {
                            if (openTargetTypeHandle.Equals(RvaToRuntimeTypeHandle(moduleHandle, entry.TargetMethods[i].ImplementingTypeRva)))
                            {
                                for (int canonSearchStyle = 0; canonSearchStyle < 2; canonSearchStyle++)
                                {
                                    // Search for a non-universal match first
                                    CanonicalFormKind canonSearchRule = canonSearchStyle == 0 ? CanonicalFormKind.Specific : CanonicalFormKind.Universal;
    
                                    // We have found the target GVM slot. Start scanning the different implementations of this method to find a match
                                    for (int j = 0; j < entry.TargetMethods[i].TargetMethods.Length; j++)
                                    {
                                        if ((canonSearchRule == CanonicalFormKind.Universal) != (entry.TargetMethods[i].TargetMethods[j].IsUniversalGenericTargetMethod == 1))
                                            continue;

                                        // Step 1: Check if the current template method can be shared with the target GVM we are trying to invoke
                                        bool requiresDictionary;
                                        MethodNameAndSignature templateMethodNameAndSignature;
                                        if (CanTargetGVMShareCodeWithTemplateGVM(reader, ref externalReferencesLookup, entry.TargetMethods[i].TargetMethods[j].MethodSignature, targetTypeHandle, genericArguments, canonSearchRule, out templateMethodNameAndSignature, out requiresDictionary))
                                        {
                                            dictionaryPointer = IntPtr.Zero;
                                            methodPointer = RvaToFunctionPointer(moduleHandle, entry.TargetMethods[i].TargetMethods[j].MethodPointer);
                                            // Universal canon methods always require a dictionary
                                            Debug.Assert(!(canonSearchRule == CanonicalFormKind.Universal) || requiresDictionary);
    
                                            if (requiresDictionary)
                                            {
                                                Debug.Assert(!string.IsNullOrEmpty(templateMethodNameAndSignature.Name));
    
                                                // Step 2: Get a method dictionary for this GVM
                                                if (!TypeLoaderEnvironment.Instance.TryGetGenericMethodDictionaryForComponents(targetTypeHandle, genericArguments, templateMethodNameAndSignature, out dictionaryPointer))
                                                {
                                                    methodPointer = IntPtr.Zero;
    
                                                    // We couldn't find/build a dictionary... we should fail at this point. 
                                                    // Maybe Debug.Assert(false) here??... we'll AV anyways when the code starts calling the NULL function pointer
                                                    return false;
                                                }
                                            }
    
                                            if (canonSearchRule == CanonicalFormKind.Universal)
                                            {
                                                if (TypeLoaderEnvironment.Instance.MethodSignatureHasVarsNeedingCallingConventionConverter(templateMethodNameAndSignature.Signature))
                                                {
                                                    RuntimeTypeHandle[] typeArgs = Array.Empty<RuntimeTypeHandle>();
    
                                                    if (RuntimeAugments.IsGenericType(targetTypeHandle))
                                                    {
                                                        RuntimeTypeHandle openGenericType;
                                                        bool success = TypeLoaderEnvironment.Instance.TryGetConstructedGenericTypeComponents(targetTypeHandle, out openGenericType, out typeArgs);
                                                        Debug.Assert(success);
                                                    }
    
                                                    // Create a CallingConventionConverter to call the method correctly
                                                    IntPtr thunkPtr = CallConverterThunk.MakeThunk(
                                                        CallConverterThunk.ThunkKind.StandardToGenericInstantiating,
                                                        methodPointer,
                                                        templateMethodNameAndSignature.Signature,
                                                        dictionaryPointer,
                                                        typeArgs,
                                                        genericArguments);
    
                                                    methodPointer = thunkPtr;
                                                    // Set dictionaryPointer to null so we don't make a fat function pointer around the whole thing.
                                                    // TODO! add a new call converter thunk that will pass the instantiating arg through and use a fat function pointer.
                                                    // should allow us to make fewer thunks.
                                                    dictionaryPointer = IntPtr.Zero;
                                                }
                                            }
    
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        public sealed override unsafe bool TryGetGenericVirtualTargetForTypeAndSlot(RuntimeTypeHandle targetHandle, ref RuntimeTypeHandle declaringType, ref RuntimeTypeHandle[] genericArguments, ref string methodName, ref IntPtr methodSignature, out IntPtr methodPointer, out IntPtr dictionaryPointer, out bool slotUpdated)
        {
            MethodNameAndSignature methodNameAndSignature = new MethodNameAndSignature(methodName, methodSignature);

#if REFLECTION_EXECUTION_TRACE
            ReflectionExecutionLogger.WriteLine("GVM resolution starting for " + GetTypeNameDebug(declaringType) + ".Method(" + methodNameAndSignature.Name + ")  on a target of type " + GetTypeNameDebug(targetHandle) + " ...");
#endif

            if (RuntimeAugments.IsInterface(declaringType))
            {
                methodPointer = IntPtr.Zero;
                dictionaryPointer = IntPtr.Zero;
                slotUpdated = ResolveInterfaceGenericVirtualMethodSlot(targetHandle, ref declaringType, ref genericArguments, ref methodNameAndSignature);
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


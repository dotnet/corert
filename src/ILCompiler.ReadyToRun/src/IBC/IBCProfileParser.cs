using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILCompiler.Win32Resources;

namespace ILCompiler.IBC
{
    class IBCProfileParser
    {
        public IBCProfileParser(Logger logger)
        {
            Logger = logger;
        }

        private readonly Logger Logger;

        public ProfileData ParseIBCDataFromModule(EcmaModule ecmaModule)
        {
            ResourceData peResources = new ResourceData(ecmaModule);
            byte[] ibcDataSection = peResources.FindResource("PROFILE_DATA", "IBC", 0);
            if (ibcDataSection == null)
            {
                // If we don't have profile data, return empty ProfileData object
                return EmptyProfileData.Singleton;
            }

            var reader = new IBCDataReader();
            int pos = 0;
            bool minified;
            bool basicBlocksOnly = false;
            var parsedData = reader.Read(ibcDataSection, ref pos, out minified);
            if (parsedData.FormatMajorVersion == 1)
                throw new Exception("Unsupported V1 IBC Format");

            HashSet<uint> ignoredIbcMethodSpecTokens;
            var blobs = GetIBCBlobs(parsedData.BlobStream, out ignoredIbcMethodSpecTokens);

            List<MethodProfileData> methodProfileData = new List<MethodProfileData>();

            IBCModule ibcModule = new IBCModule(ecmaModule, blobs);
            // Parse the token lists
            IBCData.SectionIteratorKind iteratorKind = basicBlocksOnly ? IBCData.SectionIteratorKind.BasicBlocks : IBCData.SectionIteratorKind.TokenFlags;
            foreach (SectionFormat section in IBCData.SectionIterator(iteratorKind))
            {
                List<TokenData> TokenList;
                if (!parsedData.Tokens.TryGetValue(section, out TokenList) ||
                    TokenList.Count == 0)
                {
                    continue;
                }

                // In V1 and minified V3+ files, tokens aren't stored with a
                // scenario mask. In the former case, the scenario mask should
                // be treated as 0 (unprocessed) so that the appropriate flags
                // will be set. In the latter case, it can be treated as
                // anything nonzero--minified files make no guarantee about
                // preserving scenario information, but the flags must be left
                // alone.

                uint scenarioMaskIfMissing = (parsedData.FormatMajorVersion == 1) ? 0u : 1u;

                foreach (var entry in TokenList)
                {
                    //
                    // Discard any token list entries which refer to the ParamMethodSpec blob stream entries
                    // (if any) which were thrown away above.  Note that the MethodProfilingData token list is
                    // the only place anywhere in the IBC data which can ever contain an embedded ibcMethodSpec
                    // token.
                    //

                    if (section == SectionFormat.MethodProfilingData)
                    {
                        if (ignoredIbcMethodSpecTokens.Contains(entry.Token))
                            continue;
                    }

                    uint scenarioMask = entry.ScenarioMask ?? scenarioMaskIfMissing;

                    // scenarioMask will be 0 in unprocessed or V1 IBC data.
                    if (scenarioMask == 0)
                    {
                        throw new NotImplementedException();
                        /*                        Debug.Assert(fullScenarioMask == 1, "Token entry not owned by one scenario");
                                                // We have to compute the RunOnceMethod and RunNeverMethod flags.
                                                entry.Flags = result.GetFlags(entry.Flags, section, entry.Token);
                                                scenarioMask = defaultScenarioMask;*/
                    }

                    //                    Debug.Assert(((~fullScenarioMask & scenarioMask) == 0), "Illegal scenarios mask");

                    MethodDesc associatedMethod = null;

                    switch (Cor.Macros.TypeFromToken(entry.Token))
                    {
                        case CorTokenType.mdtMethodDef:
                        case CorTokenType.mdtMemberRef:
                        case CorTokenType.mdtMethodSpec:
                            associatedMethod = ecmaModule.GetMethod(System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle((int)entry.Token));
                            break;

                        /*                        case CorTokenType.ibcExternalMethod:
                                                    {
                                                        BlobEntry blobEntry;
                                                        if (!blobs.TryGetValue(new IBCBlobKey(entry.Token, BlobType.ExternalMethodDef), out blobEntry))
                                                            throw new Exception($"Missing blob entry for ibcExternalMethod {entry.Token:x}");
                                                        BlobEntry.ExternalMethodEntry externalMethodEntry = blobEntry as BlobEntry.ExternalMethodEntry;
                                                        BlobEntry.ExternalTypeEntry extType;
                                                        extType.
                                                        externalMethodEntry.
                                                        if (externalMethodEntry == null)
                                                            throw new Exception($"Blob entry for {entry.Token:x} is invalid");
                                                        unsafe
                                                        {
                                                            fixed (byte* pb = &externalMethodEntry.Signature[0])
                                                            {
                                                                BlobReader br = new BlobReader(pb, externalMethodEntry.Signature.Length);
                                                                associatedMethod = GetSigMethodInstantiationFromIBCMethodSpec(ibcModule, br);
                                                            }
                                                        }
                                                    }
                                                break;*/

                        case CorTokenType.ibcMethodSpec:
                            {
                                BlobEntry blobEntry;
                                if (!blobs.TryGetValue(new IBCBlobKey(entry.Token, BlobType.ParamMethodSpec), out blobEntry))
                                    throw new Exception($"Missing blob entry for ibcMethodSpec {entry.Token:x}");
                                BlobEntry.SignatureEntry paramSignatureEntry = blobEntry as BlobEntry.SignatureEntry;
                                if (paramSignatureEntry == null)
                                    throw new Exception($"Blob entry for {entry.Token:x} is invalid");
                                unsafe
                                {
                                    fixed (byte* pb = &paramSignatureEntry.Signature[0])
                                    {
                                        BlobReader br = new BlobReader(pb, paramSignatureEntry.Signature.Length);
                                        associatedMethod = GetSigMethodInstantiationFromIBCMethodSpec(ibcModule, br);
                                    }
                                }
                            }
                            break;
                    }

                    if (associatedMethod != null)
                    {
                        methodProfileData.Add(new MethodProfileData(associatedMethod, (MethodProfilingDataFlags)entry.Flags, scenarioMask));
                    }
                }
            }

            return new IBCProfileData(parsedData.PartialNGen, methodProfileData);
        }

        public struct IBCBlobKey : IEquatable<IBCBlobKey>
        {
            public IBCBlobKey(uint token, BlobType type)
            {
                Token = token;
                Type = type;
            }

            public readonly uint Token;
            public readonly BlobType Type;
            public override int GetHashCode()
            {
                return (int)(Token ^ (((uint)Type) << 4));
            }
            public override bool Equals(object obj)
            {
                if (!(obj is IBCBlobKey))
                    return false;
                return Equals((IBCBlobKey)obj);
            }

            public bool Equals(IBCBlobKey other)
            {
                return (other.Token == Token) && (other.Type == Type);
            }
        }

        private static Dictionary<IBCBlobKey, BlobEntry> GetIBCBlobs(List<BlobEntry> inputBlobs, out HashSet<uint> ignoredIbcMethodSpecTokens)
        {
            Dictionary<IBCBlobKey, BlobEntry> blobs = new Dictionary<IBCBlobKey, BlobEntry>();
            ignoredIbcMethodSpecTokens = new HashSet<uint>();

            if (inputBlobs != null)
            {
                foreach (var blob in inputBlobs)
                {
                    bool ignore = false;

                    // Some blob types require special processing
                    switch (blob.Type)
                    {
                        case BlobType.ParamTypeSpec:
                        case BlobType.ParamMethodSpec:
                            if ((Cor.Macros.TypeFromToken(blob.Token) == CorTokenType.ibcTypeSpec) ||
                                (Cor.Macros.TypeFromToken(blob.Token) == CorTokenType.ibcMethodSpec))
                            {
                                if (blob.Type == BlobType.ParamTypeSpec)
                                {
                                    //                                    UTILS.CheckAndSetHighest(ref result.highestIbcTypeSpecToken, blob.Token);
                                }
                                else
                                {
                                    //
                                    // In the ParamMethodSpec case, the full signature was always originally encoded by
                                    // ZapSig::EncodeMethod and therefore always starts with an owning type signature
                                    // originally encoded by ZapSig::GetSignatureForTypeHandle.
                                    //
                                    // If the owning type is one of the well-known primitive types, then the owning type
                                    // signature ends up being written in "flattened" form (e.g., if the owning type is
                                    // System.String, a single ELEMENT_TYPE_STRING byte is written as the owning type
                                    // signature).  This only happens when the ParamMethodSpec describes a generic method
                                    // defined directly on one of the primitive types.  Since .NET 4.0, mscorlib has included
                                    // exactly two such methods (System.String::Join<T> and System.String::Concat<T>), and
                                    // contained no such methods prior to .NET 4.0.
                                    //
                                    // In all other cases, ZapSig::GetSignatureForTypeHandle writes the owning type signature
                                    // in a "standard" form which always starts with bytes that match the following grammar:
                                    //
                                    //      [ELEMENT_TYPE_MODULE_ZAPSIG {Index}] [ELEMENT_TYPE_GENERICINST] ELEMENT_TYPE_(CLASS|VALUETYPE) {Token}
                                    //
                                    // IBCMerge only supports the standard form.  Specifically, if the flattened form is
                                    // ever processed, updateParamSig and remapParamSig are unable to reliably determine
                                    // whether the subsequent method token should be interpreted as a MethodDef or as an
                                    // ibcMethodSpec (because the flattened form "hides" the explicit ELEMENT_TYPE_MODULE_ZAPSIG
                                    // byte that is normally used to determine how to interpret the method token).
                                    //
                                    // Probe the leading bytes of the signature and ignore this ParamMethodSpec if the leading
                                    // bytes prove that the owning type signature was NOT encoded in the standard form.
                                    //

                                    byte[] signature = ((BlobEntry.SignatureEntry)blob).Signature;
                                    bool owningTypeSignatureAppearsToBeEncodedInStandardForm = false;

                                    if (signature.Length >= 2)
                                    {
                                        CorElementType leadingByte = (CorElementType)signature[0];

                                        if ((leadingByte == CorElementType.ELEMENT_TYPE_MODULE_ZAPSIG) ||
                                            (leadingByte == CorElementType.ELEMENT_TYPE_GENERICINST) ||
                                            (leadingByte == CorElementType.ELEMENT_TYPE_CLASS) ||
                                            (leadingByte == CorElementType.ELEMENT_TYPE_VALUETYPE))
                                        {
                                            owningTypeSignatureAppearsToBeEncodedInStandardForm = true;
                                        }
                                    }

                                    if (owningTypeSignatureAppearsToBeEncodedInStandardForm)
                                    {
                                        //                                        UTILS.CheckAndSetHighest(ref result.highestIbcMethodSpecToken, blob.Token);
                                    }
                                    else
                                    {
                                        ignoredIbcMethodSpecTokens.Add(blob.Token);
                                        ignore = true;
                                        //                                        UTILS.Info(String.Format("Ignoring ParamMethodSpec due to non-standard owning type signature: 0x{0:x8}", blob.Token));
                                    }
                                }
                            }
                            else
                            {
                                // We have old V2 IBC data (ignore it)
                                ignore = true;
                            }
                            break;

                        case BlobType.ExternalNamespaceDef:
                            //                            UTILS.CheckAndSetHighest(ref result.highestExternalNamespaceToken, blob.Token);
                            break;

                        case BlobType.ExternalTypeDef:
                            //                            UTILS.CheckAndSetHighest(ref result.highestExternalTypeToken, blob.Token);
                            break;

                        case BlobType.ExternalSignatureDef:
                            //                            UTILS.CheckAndSetHighest(ref result.highestExternalSignatureToken, blob.Token);
                            break;

                        case BlobType.ExternalMethodDef:
                            //                            UTILS.CheckAndSetHighest(ref result.highestExternalMethodToken, blob.Token);
                            break;

                        case BlobType.MetadataStringPool:
                        case BlobType.MetadataGuidPool:
                        case BlobType.MetadataBlobPool:
                        case BlobType.MetadataUserStringPool:
                            // These blob types should not be carried forward.
                            ignore = true;
                            break;
                    }

                    if (!ignore)
                    {
                        blobs.Add(new IBCBlobKey(blob.Token, blob.Type), blob);
                    }
                }
            }
            return blobs;
        }


        /// <summary>
        /// Initialize the moduleId and typeToken
        /// </summary>
        /// <returns>false on failure</returns>
        public static bool InitializeModuleIdAndTypeToken(out ModuleId moduleId, out uint typeToken, BlobReader typeSig)
        {

            moduleId = ModuleId.CurrentModule;
            typeToken = 0;

            byte currentTypeSigByte = typeSig.ReadByte();

            if (currentTypeSigByte == (byte)CorElementType.ELEMENT_TYPE_MODULE_ZAPSIG)
            {
                // Get the external module index
                uint index = (uint)typeSig.ReadCompressedInteger();
                if (index == 0)
                    return false;

                // Get the new moduleId
                moduleId = (ModuleId)(CorTokenType.mdtAssemblyRef + index);

                currentTypeSigByte = typeSig.ReadByte();
            }

            if (currentTypeSigByte == (byte)CorElementType.ELEMENT_TYPE_GENERICINST)
                currentTypeSigByte = typeSig.ReadByte();

            if (currentTypeSigByte == (byte)CorElementType.ELEMENT_TYPE_CLASS ||
                currentTypeSigByte == (byte)CorElementType.ELEMENT_TYPE_VALUETYPE)
            {
                typeToken = (uint)MetadataTokens.GetToken(typeSig.ReadTypeHandle());
                if (moduleId != ModuleId.CurrentModule)
                    typeToken = Cor.Macros.TokenFromRid(Cor.Macros.RidFromToken(typeToken), CorTokenType.ibcExternalType);
            }

            return true;
        }

        uint LookupIbcTypeToken(EcmaModule externalModule, uint ibcToken, Dictionary<IBCBlobKey, BlobEntry> blobs)
        {
            var typeEntry = (BlobEntry.ExternalTypeEntry)blobs[new IBCBlobKey(ibcToken, BlobType.ExternalTypeDef)];
            string typeNamespace = null;
            string typeName = Encoding.UTF8.GetString(typeEntry.Name);
            TypeDefinitionHandle enclosingType = default(TypeDefinitionHandle);
            if (!Cor.Macros.IsNilToken(typeEntry.NamespaceToken))
            {
                if (!Cor.Macros.IsNilToken(typeEntry.NestedClassToken))
                {
                    // Do not support typedef with namespace that is nested
                    throw new Exception($"Ibc TypeToken {ibcToken:x} has both Namespace and NestedClass tokens");
                }

                uint nameSpaceToken = typeEntry.NamespaceToken;
                if (Cor.Macros.TypeFromToken(nameSpaceToken) != CorTokenType.ibcExternalNamespace)
                    throw new Exception($"Ibc TypeToken {ibcToken:x} has Namespace tokens that is not a ibcExternalNamespace");

                var namespaceEntry = (BlobEntry.ExternalNamespaceEntry)blobs[new IBCBlobKey(nameSpaceToken, BlobType.ExternalNamespaceDef)];
                typeNamespace = Encoding.UTF8.GetString(namespaceEntry.Name);
            }
            else if (!Cor.Macros.IsNilToken(typeEntry.NestedClassToken))
            {
                uint enclosingTypeTokenValue = LookupIbcTypeToken(externalModule, typeEntry.NestedClassToken, blobs);
                if (Cor.Macros.TypeFromToken(enclosingTypeTokenValue) != CorTokenType.mdtTypeDef)
                    throw new Exception($"Ibc TypeToken {ibcToken:x} has NestedClass token which does not resolve to a type definition");

                enclosingType = MetadataTokens.TypeDefinitionHandle((int)Cor.Macros.RidFromToken(enclosingTypeTokenValue));
                if (enclosingType.IsNil)
                    throw new Exception($"Ibc TypeToken {ibcToken:x} has NestedClass token which resolves to a nil token");
            }

            if (enclosingType.IsNil)
            {
                return (uint)externalModule.MetadataReader.GetToken(((EcmaType)externalModule.GetType(typeNamespace, typeName)).Handle);
            }
            else
            {
                TypeDefinition nestedClassDefinition = externalModule.MetadataReader.GetTypeDefinition(enclosingType);
                var stringComparer = externalModule.MetadataReader.StringComparer;
                foreach (TypeDefinitionHandle tdNested in nestedClassDefinition.GetNestedTypes())
                {
                    TypeDefinition candidateClassDefinition = externalModule.MetadataReader.GetTypeDefinition(tdNested);
                    if (stringComparer.Equals(candidateClassDefinition.Name, typeName))
                    {
                        return (uint)externalModule.MetadataReader.GetToken(tdNested);
                    }
                }

                throw new Exception($"Ibc TypeToken {ibcToken:x} unable to find nested type '{typeName}' on type '{externalModule.MetadataReader.GetToken(enclosingType):x}'");
            }
        }

        uint LookupIbcMethodToken(MetadataType methodMetadataType, uint ibcToken, Dictionary<IBCBlobKey, BlobEntry> blobs)
        {
            var methodEntry = (BlobEntry.ExternalMethodEntry)blobs[new IBCBlobKey(ibcToken, BlobType.ExternalMethodDef)];
            var signatureEntry = (BlobEntry.ExternalSignatureEntry)blobs[new IBCBlobKey(methodEntry.SignatureToken, BlobType.ExternalSignatureDef)];

            string methodName = Encoding.UTF8.GetString(methodEntry.Name);


            var ecmaType = (EcmaType)methodMetadataType.GetTypeDefinition();

            var lookupClassTokenTypeDef = (int)LookupIbcTypeToken(ecmaType.EcmaModule, methodEntry.ClassToken, blobs);
            if (lookupClassTokenTypeDef != ecmaType.MetadataReader.GetToken(ecmaType.Handle))
                throw new Exception($"Ibc MethodToken {ibcToken:x} incosistent classToken '{ibcToken:x}' with specified exact type '{ecmaType}'");

            foreach (MethodDesc method in ecmaType.GetMethods())
            {
                if (method.Name == methodName)
                {
                    EcmaMethod ecmaCandidateMethod = method as EcmaMethod;
                    if (method == null)
                        continue;

                    var metadataReader = ecmaCandidateMethod.MetadataReader;
                    BlobReader signatureReader = metadataReader.GetBlobReader(metadataReader.GetMethodDefinition(ecmaCandidateMethod.Handle).Signature);

                    // Compare for equality
                    if (signatureReader.RemainingBytes != signatureEntry.Signature.Length)
                        continue;
                    for (int i = 0; i < signatureEntry.Signature.Length; i++)
                    {
                        if (signatureReader.ReadByte() != signatureEntry.Signature[i])
                            continue;
                    }

                    // TODO, consider implementing the fuzzy matching that CrossGen implements

                    // Exact match
                    return (uint)MetadataTokens.GetToken(ecmaCandidateMethod.Handle);
                }
            }

            Logger.Writer.WriteLine("Warning: Unable to find exact match for candidate external method");
            return 0;
        }


        class IBCModule
        {
            public IBCModule(EcmaModule ecmaModule, Dictionary<IBCBlobKey, BlobEntry> blobs)
            {
                EcmaModule = ecmaModule;
                Blobs = blobs;
            }

            public readonly EcmaModule EcmaModule;
            public readonly Dictionary<IBCBlobKey, BlobEntry> Blobs;
            public EcmaModule GetModuleFromIndex(int index) { throw new NotImplementedException(); } // Always returns null for non-version bubble local modules
        }

        // Load type from IBC ZapSig. Returns null for cases where the type is legally defined, but is not used in R2R image generation
        TypeDesc GetSigTypeFromIBCZapSig(IBCModule ibcModule, EcmaModule ecmaModule, BlobReader sig)
        {
            TypeSystemContext context = ibcModule.EcmaModule.Context;

            CorElementType typ = (CorElementType)sig.ReadByte();
            switch (typ)
            {
                case CorElementType.ELEMENT_TYPE_VOID:
                    return context.GetWellKnownType(WellKnownType.Void);
                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    return context.GetWellKnownType(WellKnownType.Boolean);
                case CorElementType.ELEMENT_TYPE_CHAR:
                    return context.GetWellKnownType(WellKnownType.Char);
                case CorElementType.ELEMENT_TYPE_I1:
                    return context.GetWellKnownType(WellKnownType.SByte);
                case CorElementType.ELEMENT_TYPE_U1:
                    return context.GetWellKnownType(WellKnownType.Byte);
                case CorElementType.ELEMENT_TYPE_I2:
                    return context.GetWellKnownType(WellKnownType.Int16);
                case CorElementType.ELEMENT_TYPE_U2:
                    return context.GetWellKnownType(WellKnownType.UInt16);
                case CorElementType.ELEMENT_TYPE_I4:
                    return context.GetWellKnownType(WellKnownType.Int32);
                case CorElementType.ELEMENT_TYPE_U4:
                    return context.GetWellKnownType(WellKnownType.UInt32);
                case CorElementType.ELEMENT_TYPE_I8:
                    return context.GetWellKnownType(WellKnownType.Int64);
                case CorElementType.ELEMENT_TYPE_U8:
                    return context.GetWellKnownType(WellKnownType.UInt64);
                case CorElementType.ELEMENT_TYPE_R4:
                    return context.GetWellKnownType(WellKnownType.Single);
                case CorElementType.ELEMENT_TYPE_R8:
                    return context.GetWellKnownType(WellKnownType.Double);
                case CorElementType.ELEMENT_TYPE_STRING:
                    return context.GetWellKnownType(WellKnownType.String);
                case CorElementType.ELEMENT_TYPE_OBJECT:
                    return context.GetWellKnownType(WellKnownType.Object);
                case CorElementType.ELEMENT_TYPE_TYPEDBYREF:
                    return context.GetWellKnownType(WellKnownType.TypedReference);
                case CorElementType.ELEMENT_TYPE_NATIVE_ARRAY_TEMPLATE_ZAPSIG:
                    throw new Exception("Attempt to parse ELEMENT_TYPE_NATIVE_ARRAY_TEMPLATE_ZAPSIG"); // These shouldn't ever appear in type definition signatures
                case CorElementType.ELEMENT_TYPE_NATIVE_VALUETYPE_ZAPSIG:
                    return null; // Native valuetypes are not part of the R2R file format
                case CorElementType.ELEMENT_TYPE_CANON_ZAPSIG:
                    if (!context.SupportsCanon)
                        return null;
                    return context.CanonType;
                case CorElementType.ELEMENT_TYPE_MODULE_ZAPSIG:
                    // Check version bubble by looking at reference to non-local module
                    // If null, then the remote reference is not permitted.
                    EcmaModule remoteModule = ibcModule.GetModuleFromIndex(sig.ReadCompressedInteger());
                    return GetSigTypeFromIBCZapSig(ibcModule, remoteModule, sig);
                case CorElementType.ELEMENT_TYPE_VAR_ZAPSIG:
                    // VAR_ZAPSIG is not supported in IBC ZapSigs
                    throw new Exception("Attempt to parse ELEMENT_TYPE_VAR_ZAPSIG in an IBC ZapSig");

                case CorElementType.ELEMENT_TYPE_VAR:
                case CorElementType.ELEMENT_TYPE_MVAR:
                    // VAR/MVAR can never appear in a ZapSig
                    throw new Exception("Attempt to parse ELEMENT_TYPE_VAR or ELEMENT_TYPE_MVAR in an IBC ZapSig");

                case CorElementType.ELEMENT_TYPE_GENERICINST:
                    CorElementType genericTyp = (CorElementType)sig.ReadByte();
                    MetadataType genericDefinitionType = LoadTypeFromIBCZapSig(ibcModule, ecmaModule, genericTyp, ref sig);
                    if (genericDefinitionType == null)
                        return null;
                    int numTypeArgs = sig.ReadCompressedInteger();
                    TypeDesc[] typeArgs = new TypeDesc[numTypeArgs];
                    for (int i = 0; i < numTypeArgs; i++)
                    {
                        TypeDesc nextTypeArg = GetSigTypeFromIBCZapSig(ibcModule, ecmaModule, sig);
                        if (nextTypeArg == null)
                            return null;
                        SkipTypeInIBCZapSig(ref sig);
                        typeArgs[i] = nextTypeArg;
                    }
                    return genericDefinitionType.MakeInstantiatedType(new Instantiation(typeArgs));

                case CorElementType.ELEMENT_TYPE_CLASS:
                case CorElementType.ELEMENT_TYPE_VALUETYPE:
                    return LoadTypeFromIBCZapSig(ibcModule, ecmaModule, typ, ref sig);

                case CorElementType.ELEMENT_TYPE_SZARRAY:
                    {
                        TypeDesc arrayElementType = GetSigTypeFromIBCZapSig(ibcModule, ecmaModule, sig);
                        if (arrayElementType == null)
                            return null;
                        return arrayElementType.MakeArrayType();
                    }

                case CorElementType.ELEMENT_TYPE_ARRAY:
                    {
                        TypeDesc arrayElementType = GetSigTypeFromIBCZapSig(ibcModule, ecmaModule, sig);
                        if (arrayElementType == null)
                            return null;
                        SkipTypeInIBCZapSig(ref sig);
                        return arrayElementType.MakeArrayType(sig.ReadCompressedInteger());
                    }

                case CorElementType.ELEMENT_TYPE_PINNED:
                    // Return what follows
                    return GetSigTypeFromIBCZapSig(ibcModule, ecmaModule, sig);

                case CorElementType.ELEMENT_TYPE_BYREF:
                    {
                        TypeDesc byRefToType = GetSigTypeFromIBCZapSig(ibcModule, ecmaModule, sig);
                        if (byRefToType == null)
                            return null;
                        return byRefToType.MakeByRefType();
                    }

                case CorElementType.ELEMENT_TYPE_PTR:
                    {
                        TypeDesc pointerToType = GetSigTypeFromIBCZapSig(ibcModule, ecmaModule, sig);
                        if (pointerToType == null)
                            return null;
                        return pointerToType.MakePointerType();
                    }
                default:
                    throw new Exception($"Invalid element type {typ:x} in IBC ZapSig");
            }
        }

        static void SkipTypeInIBCZapSig(ref BlobReader sig)
        {
            CorElementType typ = (CorElementType)sig.ReadByte();
            switch (typ)
            {
                case CorElementType.ELEMENT_TYPE_VOID:
                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                case CorElementType.ELEMENT_TYPE_CHAR:
                case CorElementType.ELEMENT_TYPE_I1:
                case CorElementType.ELEMENT_TYPE_U1:
                case CorElementType.ELEMENT_TYPE_I2:
                case CorElementType.ELEMENT_TYPE_U2:
                case CorElementType.ELEMENT_TYPE_I4:
                case CorElementType.ELEMENT_TYPE_U4:
                case CorElementType.ELEMENT_TYPE_I8:
                case CorElementType.ELEMENT_TYPE_U8:
                case CorElementType.ELEMENT_TYPE_R4:
                case CorElementType.ELEMENT_TYPE_R8:
                case CorElementType.ELEMENT_TYPE_STRING:
                case CorElementType.ELEMENT_TYPE_OBJECT:
                case CorElementType.ELEMENT_TYPE_TYPEDBYREF:
                    return;

                case CorElementType.ELEMENT_TYPE_NATIVE_ARRAY_TEMPLATE_ZAPSIG:
                    throw new Exception("Attempt to parse ELEMENT_TYPE_NATIVE_ARRAY_TEMPLATE_ZAPSIG"); // These shouldn't ever appear in type definition signatures
                case CorElementType.ELEMENT_TYPE_NATIVE_VALUETYPE_ZAPSIG:
                    SkipTypeInIBCZapSig(ref sig);
                    return;
                case CorElementType.ELEMENT_TYPE_CANON_ZAPSIG:
                    return;
                case CorElementType.ELEMENT_TYPE_MODULE_ZAPSIG:
                    sig.ReadCompressedInteger();
                    SkipTypeInIBCZapSig(ref sig);
                    return;
                case CorElementType.ELEMENT_TYPE_VAR_ZAPSIG:
                    // VAR_ZAPSIG is not supported in IBC ZapSigs
                    throw new Exception("Attempt to parse ELEMENT_TYPE_VAR_ZAPSIG in an IBC ZapSig");

                case CorElementType.ELEMENT_TYPE_VAR:
                case CorElementType.ELEMENT_TYPE_MVAR:
                    // VAR/MVAR can never appear in a ZapSig
                    throw new Exception("Attempt to parse ELEMENT_TYPE_VAR or ELEMENT_TYPE_MVAR in an IBC ZapSig");

                case CorElementType.ELEMENT_TYPE_GENERICINST:
                    CorElementType genericTyp = (CorElementType)sig.ReadByte();
                    sig.ReadTypeHandle();
                    int numTypeArgs = sig.ReadCompressedInteger();
                    for (int i = 0; i < numTypeArgs; i++)
                    {
                        SkipTypeInIBCZapSig(ref sig);
                    }
                    return;

                case CorElementType.ELEMENT_TYPE_CLASS:
                case CorElementType.ELEMENT_TYPE_VALUETYPE:
                    sig.ReadTypeHandle();
                    return;

                case CorElementType.ELEMENT_TYPE_SZARRAY:
                    SkipTypeInIBCZapSig(ref sig);
                    return;

                case CorElementType.ELEMENT_TYPE_ARRAY:
                    SkipTypeInIBCZapSig(ref sig);
                    int rank = sig.ReadCompressedInteger();
                    int numSizes = sig.ReadCompressedInteger();
                    for (int i = 0; i < numSizes; i++)
                        sig.ReadCompressedInteger();
                    int numLoBounds = sig.ReadCompressedInteger();
                    for (int i = 0; i < numLoBounds; i++)
                        sig.ReadCompressedInteger();
                    return;

                case CorElementType.ELEMENT_TYPE_PINNED:
                case CorElementType.ELEMENT_TYPE_BYREF:
                case CorElementType.ELEMENT_TYPE_PTR:
                    SkipTypeInIBCZapSig(ref sig);
                    return;

                default:
                    throw new Exception($"Invalid element type {typ:x} in IBC ZapSig");
            }
        }

        MetadataType LoadTypeFromIBCZapSig(IBCModule ibcModule, EcmaModule ecmaModule, CorElementType typ, ref BlobReader sig)
        {
            switch (typ)
            {
                case CorElementType.ELEMENT_TYPE_CLASS:
                case CorElementType.ELEMENT_TYPE_VALUETYPE:
                    uint token = (uint)ecmaModule.MetadataReader.GetToken(sig.ReadTypeHandle());
                    uint origToken = token;
                    if (ecmaModule != ibcModule.EcmaModule)
                    {
                        // ibcExternalType tokens are actually encoded as mdtTypeDef tokens in the signature
                        uint rid = Cor.Macros.RidFromToken(token);
                        uint ibcToken = Cor.Macros.TokenFromRid(rid, CorTokenType.ibcExternalType);
                        token = LookupIbcTypeToken(ecmaModule, ibcToken, ibcModule.Blobs);
                    }
                    switch (Cor.Macros.TypeFromToken(token))
                    {
                        case CorTokenType.mdtTypeDef:
                        case CorTokenType.mdtTypeRef:
                            // success
                            break;
                        default:
                            throw new Exception("Invalid token found while parsing IBC ZapSig generic instantiation");
                    }
                    if (Cor.Macros.IsNilToken(token))
                        throw new Exception("Nil token found while parsing IBC ZapSig generic instantiation");

                    var result = (MetadataType)ecmaModule.GetType(MetadataTokens.EntityHandle((int)token));
                    if ((typ == CorElementType.ELEMENT_TYPE_VALUETYPE) != result.IsValueType)
                    {
                        throw new Exception("Mismatch between valuetype and reference type in while parsing generic instantiation");
                    }
                    return result;
                default:
                    throw new Exception("Unexpected token type parsing ELEMENT_TYPE_GENERICINST");
            }
        }

        MethodDesc GetSigMethodInstantiationFromIBCMethodSpec(IBCModule ibcModule, BlobReader sig)
        {
            ModuleId moduleId;
            uint typeToken;
            //
            // Initialize moduleId and typeToken
            // 
            if (!InitializeModuleIdAndTypeToken(out moduleId, out typeToken, sig))
            {
                throw new Exception("Unable to process module id and type token");
            }

            TypeDesc methodType = GetSigTypeFromIBCZapSig(ibcModule, ibcModule.EcmaModule, sig);
            SkipTypeInIBCZapSig(ref sig);
            uint flags = (uint)sig.ReadCompressedInteger();
            if (Macros.IsSlotUsedInsteadOfToken(flags))
            {
                int slot = sig.ReadCompressedInteger();
                Logger.Writer.WriteLine($"Warning: IBC Data for `{methodType}` with slot '{slot}' was ignored");
                return null; // Unsupported case thought to be used only for array methods, which don't really matter for R2R codegen
            }
            else
            {
                // Decode method token

                uint methodRid = (uint)sig.ReadCompressedInteger();
                uint methodToken;
                MetadataType methodMetadataType = (MetadataType)methodType;
                if (ibcModule.EcmaModule == methodMetadataType.Module)
                {
                    methodToken = Cor.Macros.TokenFromRid(methodRid, CorTokenType.mdtMethodDef);
                }
                else
                {
                    uint ibcToken = Cor.Macros.TokenFromRid(methodRid, CorTokenType.ibcExternalMethod);
                    methodToken = LookupIbcMethodToken(methodMetadataType, ibcToken, ibcModule.Blobs);
                    if (Cor.Macros.RidFromToken(methodToken) == 0)
                    {
                        Logger.Writer.WriteLine($"Warning: External Method Token {ibcToken:x} on '{methodMetadataType}' could not be found.");
                        return null;
                    }
                }

                var ecmaModuleOfMethod = ((EcmaType)methodMetadataType.GetTypeDefinition()).EcmaModule;
                var ecmaMethod = ecmaModuleOfMethod.GetMethod(MetadataTokens.EntityHandle((int)methodToken));
                var methodOnType = methodType.FindMethodOnTypeWithMatchingTypicalMethod(ecmaMethod);

                var methodFound = methodOnType;
                if (Macros.IsInstantiationNeeded(flags))
                {
                    int instantiationArgumentCount = methodOnType.Instantiation.Length;
                    Debug.Assert(instantiationArgumentCount > 0);
                    List<TypeDesc> instantiationArguments = new List<TypeDesc>();
                    for (int i = 0; i < instantiationArgumentCount; i++)
                    {
                        instantiationArguments.Add(GetSigTypeFromIBCZapSig(ibcModule, ibcModule.EcmaModule, sig));
                        SkipTypeInIBCZapSig(ref sig);
                    }

                    methodFound = methodOnType.MakeInstantiatedMethod(new Instantiation(instantiationArguments.ToArray()));
                }

                if (Macros.IsUnboxingStub(flags))
                {
                    Logger.Writer.WriteLine($"Warning: Skipping IBC data for unboxing stub {methodFound}");
                    return null;
                }

                if (Macros.IsInstantiatingStub(flags))
                {
                    Logger.Writer.WriteLine($"Warning: Skipping IBC data for instantiating stub {methodFound}");
                    return null;
                }

                return methodFound;
            }
        }
    }
}

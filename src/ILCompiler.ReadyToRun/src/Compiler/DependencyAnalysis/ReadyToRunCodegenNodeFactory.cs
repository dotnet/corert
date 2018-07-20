// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ILCompiler.DependencyAnalysisFramework;

using Internal.JitInterface;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILCompiler.DependencyAnalysis.ReadyToRun;

namespace ILCompiler.DependencyAnalysis
{
    using ReadyToRunHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper;

    public sealed class ReadyToRunCodegenNodeFactory : NodeFactory
    {
        private Dictionary<MethodDesc, IMethodNode> _importMethods;

        private Dictionary<mdToken, ISymbolNode> _importStrings;

        public ReadyToRunCodegenNodeFactory(
            CompilerTypeSystemContext context, 
            CompilationModuleGroup compilationModuleGroup,
            MetadataManager metadataManager,
            InteropStubManager interopStubManager, 
            NameMangler nameMangler, 
            VTableSliceProvider vtableSliceProvider, 
            DictionaryLayoutProvider dictionaryLayoutProvider,
            EcmaModule inputModule)
            : base(context,
                  compilationModuleGroup,
                  metadataManager,
                  interopStubManager,
                  nameMangler,
                  new LazyGenericsDisabledPolicy(),
                  vtableSliceProvider,
                  dictionaryLayoutProvider,
                  new ImportedNodeProviderThrowing())
        {
            _importMethods = new Dictionary<MethodDesc, IMethodNode>();
            _importStrings = new Dictionary<mdToken, ISymbolNode>();
            _inputModule = inputModule;
        }

        public PEReader PEReader;

        public HeaderNode Header;

        public RuntimeFunctionsTableNode RuntimeFunctionsTable;

        private RuntimeFunctionsGCInfoNode _runtimeFunctionsGCInfo;

        public MethodEntryPointTableNode MethodEntryPointTable;

        public InstanceEntryPointTableNode InstanceEntryPointTable;

        public TypesTableNode TypesTable;

        public ImportSectionsTableNode ImportSectionsTable;

        public Import ModuleImport;

        public ImportSectionNode EagerImports;

        public ImportSectionNode MethodImports;

        public ImportSectionNode StringImports;

        public ImportSectionNode HelperImports;

        public ImportSectionNode PrecodeImports;

        /// <summary>
        /// TODO: this will need changing when compiling multiple files. Ideally this
        /// should be switched every time we compile a method in a particular module
        /// because this is what provides context for reference token resolution.
        /// </summary>
        EcmaModule _inputModule;

        SignatureContext _signatureContext;

        public SignatureContext SignatureContext
        {
            get
            {
                if (_signatureContext == null)
                {
                    _signatureContext = new SignatureContext(this, _inputModule);
                }
                return _signatureContext;
            }
        }


        Dictionary<MethodDesc, IMethodNode> _methodMap = new Dictionary<MethodDesc, IMethodNode>();

        public IMethodNode GetOrCreateMethodEntrypointNode(MethodDesc method, mdToken token, bool isUnboxingStub = false)
        {
            IMethodNode methodNode;
            if (!_methodMap.TryGetValue(method, out methodNode))
            {
                methodNode = CreateMethodEntrypointNode(method, token, isUnboxingStub);
                _methodMap.Add(method, methodNode);
            }
            return methodNode;
        }

        private IMethodNode CreateMethodEntrypointNode(MethodDesc method, mdToken token, bool isUnboxingStub = false)
        {
            if (method is InstantiatedMethod instantiatedMethod)
            {
                return GetOrAddInstantiatedMethodNode(instantiatedMethod, token);
            }

            MethodWithGCInfo localMethod = null;
            if (CompilationModuleGroup.ContainsMethodBody(method, false))
            {
                localMethod = new MethodWithGCInfo(method, token);
                _runtimeFunctionsGCInfo.AddEmbeddedObject(localMethod.GCInfoNode);
                int methodIndex = RuntimeFunctionsTable.Add(localMethod);
                MethodEntryPointTable.Add(localMethod, methodIndex, this);

                // TODO: hack - how do we distinguish between emitting main entry point and calls between
                // methods?
                if (token == 0)
                {
                    return localMethod;
                }
            }

            return GetOrAddImportedMethodNode(method, unboxingStub: false, token: token, localMethod: localMethod);
        }

        public IMethodNode GetOrCreateStringAllocatorMethodNode(MethodDesc constructor, mdToken token)
        {
            return GetOrCreateMethodEntrypointNode(constructor, token, isUnboxingStub: false);
        }

        public ISymbolNode GetOrCreateStringLiteralNode(mdToken token)
        {
            ISymbolNode stringNode;
            if (!_importStrings.TryGetValue(token, out stringNode))
            {
                StringImport r2rImportNode = new StringImport(StringImports, token);
                StringImports.AddImport(this, r2rImportNode);
                stringNode = r2rImportNode;
                _importStrings.Add(token, stringNode);
            }
            return stringNode;
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(ReadyToRunHelperKey helperCall)
        {
            throw new NotImplementedException();
        }

        public bool CanInline(MethodDesc callerMethod, MethodDesc calleeMethod)
        {
            // By default impose no restrictions on inlining
            return CompilationModuleGroup.ContainsMethodBody(calleeMethod, unboxingStub: false);
        }

        Dictionary<ReadyToRunHelperId, Dictionary<object, ISymbolNode>> _r2rHelpers = new Dictionary<ReadyToRunHelperId, Dictionary<object, ISymbolNode>>();

        public ISymbolNode GetOrCreateReadyToRunHelper(ReadyToRunHelperId id, object target, mdToken token)
        {
            if (id == ReadyToRunHelperId.NecessaryTypeHandle)
            {
                // We treat TypeHandle and NecessaryTypeHandle the same - don't emit two copies of the same import
                id = ReadyToRunHelperId.TypeHandle;
            }

            Dictionary<object, ISymbolNode> helperNodeMap;
            if (!_r2rHelpers.TryGetValue(id, out helperNodeMap))
            {
                helperNodeMap = new Dictionary<object, ISymbolNode>();
                _r2rHelpers.Add(id, helperNodeMap);
            }

            ISymbolNode helperNode;
            if (helperNodeMap.TryGetValue(target, out helperNode))
            {
                return helperNode;
            }

            switch (id)
            {
                case ReadyToRunHelperId.NewHelper:
                    helperNode = CreateNewHelper((TypeDesc)target, token);
                    break;

                case ReadyToRunHelperId.NewArr1:
                    helperNode = CreateNewArrayHelper((ArrayType)target, token);
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    helperNode = CreateGCStaticBaseHelper((TypeDesc)target, token);
                    break;

                case ReadyToRunHelperId.GetNonGCStaticBase:
                    helperNode = CreateNonGCStaticBaseHelper((TypeDesc)target, token);
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    helperNode = CreateThreadStaticBaseHelper((TypeDesc)target, token);
                    break;

                case ReadyToRunHelperId.IsInstanceOf:
                    helperNode = CreateIsInstanceOfHelper((TypeDesc)target, token);
                    break;

                case ReadyToRunHelperId.CastClass:
                    helperNode = CreateCastClassHelper((TypeDesc)target, token);
                    break;

                case ReadyToRunHelperId.TypeHandle:
                    helperNode = CreateTypeHandleHelper((TypeDesc)target, token);
                    break;

                case ReadyToRunHelperId.VirtualCall:
                    helperNode = CreateVirtualCallHelper((MethodDesc)target, token);
                    break;

                case ReadyToRunHelperId.DelegateCtor:
                    helperNode = CreateDelegateCtorHelper((DelegateCreationInfo)target, token);
                    break;

                default:
                    throw new NotImplementedException();
            }

            helperNodeMap.Add(target, helperNode);
            return helperNode;
        }

        private ISymbolNode CreateNewHelper(TypeDesc type, mdToken ctorMemberRefOrTypeRefToken)
        {
            MetadataReader mdReader = PEReader.GetMetadataReader();
            mdToken typeToken;
            EntityHandle handle = (EntityHandle)MetadataTokens.Handle((int)ctorMemberRefOrTypeRefToken);
            switch (handle.Kind)
            {
                case HandleKind.TypeReference:
                    typeToken = ctorMemberRefOrTypeRefToken;
                    break;

                case HandleKind.MemberReference:
                    {
                        MemberReferenceHandle memberRefHandle = (MemberReferenceHandle)handle;
                        MemberReference memberRef = mdReader.GetMemberReference(memberRefHandle);
                        typeToken = (mdToken)MetadataTokens.GetToken(memberRef.Parent);
                    }
                    break;

                case HandleKind.MethodDefinition:
                    {
                        MethodDefinitionHandle methodDefHandle = (MethodDefinitionHandle)handle;
                        MethodDefinition methodDef = mdReader.GetMethodDefinition(methodDefHandle);
                        typeToken = (mdToken)MetadataTokens.GetToken(methodDef.GetDeclaringType());
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }

            return new DelayLoadHelperImport(this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new NewObjectFixupSignature(type, typeToken));
        }

        private ISymbolNode CreateNewArrayHelper(ArrayType type, mdToken typeRefToken)
        {
            Debug.Assert(SignatureBuilder.TypeFromToken(typeRefToken) == CorTokenType.mdtTypeRef);
            return new DelayLoadHelperImport(this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new NewArrayFixupSignature(type, typeRefToken));
        }

        private ISymbolNode CreateGCStaticBaseHelper(TypeDesc type, mdToken token)
        {
            return new DelayLoadHelperImport(
                this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_StaticBaseGC, type, GetTypeToken(token)));
        }

        private ISymbolNode CreateNonGCStaticBaseHelper(TypeDesc type, mdToken token)
        {
            return new DelayLoadHelperImport(
                this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_StaticBaseNonGC, type, GetTypeToken(token)));
        }

        private ISymbolNode CreateThreadStaticBaseHelper(TypeDesc type, mdToken token)
        {
            ReadyToRunFixupKind fixupKind = ReadyToRunFixupKind.READYTORUN_FIXUP_ThreadStaticBaseNonGC;
            return new DelayLoadHelperImport(
                this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(fixupKind, type, GetTypeToken(token)));
        }

        private mdToken GetTypeToken(mdToken token)
        {
            MetadataReader mdReader = PEReader.GetMetadataReader();
            mdToken typeToken;
            EntityHandle handle = (EntityHandle)MetadataTokens.Handle((int)token);
            switch (handle.Kind)
            {
                case HandleKind.TypeReference:
                case HandleKind.TypeDefinition:
                    typeToken = token;
                    break;

                case HandleKind.MemberReference:
                    {
                        MemberReferenceHandle memberRefHandle = (MemberReferenceHandle)handle;
                        MemberReference memberRef = mdReader.GetMemberReference(memberRefHandle);
                        typeToken = (mdToken)MetadataTokens.GetToken(memberRef.Parent);
                    }
                    break;

                case HandleKind.FieldDefinition:
                    {
                        FieldDefinitionHandle fieldDefHandle = (FieldDefinitionHandle)handle;
                        FieldDefinition fieldDef = mdReader.GetFieldDefinition(fieldDefHandle);
                        typeToken = (mdToken)MetadataTokens.GetToken(fieldDef.GetDeclaringType());
                    }
                    break;

                case HandleKind.MethodDefinition:
                    {
                        MethodDefinitionHandle methodDefHandle = (MethodDefinitionHandle)handle;
                        MethodDefinition methodDef = mdReader.GetMethodDefinition(methodDefHandle);
                        typeToken = (mdToken)MetadataTokens.GetToken(methodDef.GetDeclaringType());
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }

            return typeToken;
        }

        private ISymbolNode CreateIsInstanceOfHelper(TypeDesc type, mdToken typeRefToken)
        {
            return new DelayLoadHelperImport(this, 
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_IsInstanceOf, type, typeRefToken));
        }

        private ISymbolNode CreateCastClassHelper(TypeDesc type, mdToken typeRefToken)
        {
            return new DelayLoadHelperImport(this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_Obj,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_ChkCast, type, typeRefToken));
        }

        private ISymbolNode CreateTypeHandleHelper(TypeDesc type, mdToken typeRefToken)
        {
            return new PrecodeHelperImport(this,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_TypeHandle, type, typeRefToken));
        }

        private ISymbolNode CreateVirtualCallHelper(MethodDesc method, mdToken methodToken)
        {
            return new DelayLoadHelperImport(
                this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_Obj,
                GetOrAddMethodSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_VirtualEntry, method, methodToken, MethodFixupSignature.SignatureKind.Signature));
        }

        private ISymbolNode CreateDelegateCtorHelper(DelegateCreationInfo info, mdToken token)
        {
            return info.Constructor;
        }

        Dictionary<ILCompiler.ReadyToRunHelper, ISymbolNode> _helperCache = new Dictionary<ILCompiler.ReadyToRunHelper, ISymbolNode>();

        public ISymbolNode GetOrCreateExternSymbol(ILCompiler.ReadyToRunHelper helper)
        {
            ISymbolNode result;
            if (_helperCache.TryGetValue(helper, out result))
            {
                return result;
            }

            switch (helper)
            {
                case ILCompiler.ReadyToRunHelper.Box:
                    result = CreateBoxHelper();
                    break;

                case ILCompiler.ReadyToRunHelper.Box_Nullable:
                    result = CreateBoxNullableHelper();
                    break;

                case ILCompiler.ReadyToRunHelper.Unbox:
                    result = CreateUnboxHelper();
                    break;

                case ILCompiler.ReadyToRunHelper.Unbox_Nullable:
                    result = CreateUnboxNullableHelper();
                    break;

                case ILCompiler.ReadyToRunHelper.GetRuntimeTypeHandle:
                    result = CreateGetRuntimeTypeHandleHelper();
                    break;

                case ILCompiler.ReadyToRunHelper.RngChkFail:
                    result = CreateRangeCheckFailureHelper();
                    break;

                case ILCompiler.ReadyToRunHelper.WriteBarrier:
                    result = CreateWriteBarrierHelper();
                    break;

                default:
                    throw new NotImplementedException();
            }

            _helperCache.Add(helper, result);
            return result;
        }

        public ISymbolNode GetOrCreateHelperMethodEntrypoint(ILCompiler.ReadyToRunHelper helperId, MethodDesc method)
        {
            return GetOrCreateExternSymbol(helperId);
        }


        private ISymbolNode CreateBoxHelper()
        {
            return GetReadyToRunHelperCell(ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Box);
        }

        private ISymbolNode CreateBoxNullableHelper()
        {
            return GetReadyToRunHelperCell(ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Box_Nullable);
        }

        private ISymbolNode CreateUnboxHelper()
        {
            return GetReadyToRunHelperCell(ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Unbox);
        }

        private ISymbolNode CreateUnboxNullableHelper()
        {
            return GetReadyToRunHelperCell(ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Unbox_Nullable);
        }

        private ISymbolNode CreateGetRuntimeTypeHandleHelper()
        {
            return GetReadyToRunHelperCell(ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GetRuntimeTypeHandle);
        }

        private ISymbolNode CreateRangeCheckFailureHelper()
        {
            return GetReadyToRunHelperCell(ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_RngChkFail);
        }

        private ISymbolNode CreateWriteBarrierHelper()
        {
            return GetReadyToRunHelperCell(ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_WriteBarrier);
        }

        public IMethodNode CreateUnboxingStubNode(MethodDesc method, mdToken token)
        {
            throw new NotImplementedException();
        }

        struct TokenAndCallSite
        {
            public readonly mdToken Token;
            public readonly string CallSite;

            public TokenAndCallSite(mdToken token, string callSite)
            {
                CallSite = callSite;
                Token = token;
            }

            public override bool Equals(object obj)
            {
                TokenAndCallSite other = (TokenAndCallSite)obj;
                return CallSite == other.CallSite && Token == other.Token;
            }

            public override int GetHashCode()
            {
                return (CallSite != null ? CallSite.GetHashCode() : 0) + unchecked(31 * (int)Token);
            }
        }

        Dictionary<TokenAndCallSite, ISymbolNode> _interfaceDispatchCells = new Dictionary<TokenAndCallSite, ISymbolNode>();

        public ISymbolNode GetOrCreateInterfaceDispatchCell(MethodDesc method, mdToken token, string callSite = null)
        {
            TokenAndCallSite cellKey = new TokenAndCallSite(token, callSite);
            ISymbolNode dispatchCell;
            if (!_interfaceDispatchCells.TryGetValue(cellKey, out dispatchCell))
            {
                dispatchCell = new DelayLoadHelperImport(this,
                    ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_MethodCall,
                    GetOrAddMethodSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_VirtualEntry, method, token, MethodFixupSignature.SignatureKind.Signature),
                    callSite);

                _interfaceDispatchCells.Add(cellKey, dispatchCell);
            }
            return dispatchCell;
        }

        private Dictionary<ReadyToRunHelper, ISymbolNode> _constructedHelpers = new Dictionary<ReadyToRunHelper, ISymbolNode>();

        public ISymbolNode GetReadyToRunHelperCell(ReadyToRunHelper helperId)
        {
            ISymbolNode helperCell;
            if (!_constructedHelpers.TryGetValue(helperId, out helperCell))
            {
                helperCell = CreateReadyToRunHelperCell(helperId);
                _constructedHelpers.Add(helperId, helperCell);
            }
            return helperCell;
        }

        private ISymbolNode CreateReadyToRunHelperCell(ReadyToRunHelper helperId)
        {
            Import helperCell = new Import(EagerImports, new ReadyToRunHelperSignature(helperId));
            EagerImports.AddImport(this, helperCell);
            return helperCell;
        }

        public ISymbolNode ComputeConstantLookup(ReadyToRunHelperId helperId, object entity, mdToken token)
        {
            return GetOrCreateReadyToRunHelper(helperId, entity, token);
        }

        Dictionary<MethodDesc, ISortableSymbolNode> _genericDictionaryCache = new Dictionary<MethodDesc, ISortableSymbolNode>();

        public ISortableSymbolNode GetOrCreateMethodGenericDictionary(MethodDesc method, mdToken token)
        {
            ISortableSymbolNode genericDictionary;
            if (!_genericDictionaryCache.TryGetValue(method, out genericDictionary))
            {
                genericDictionary = new DelayLoadHelperImport(this,
                    ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                    GetOrAddMethodSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_MethodDictionary, method, token, MethodFixupSignature.SignatureKind.Signature));
                _genericDictionaryCache.Add(method, genericDictionary);
            }
            return genericDictionary;
        }

        struct TokenAndFixupKind
        {
            public readonly mdToken Token;
            public readonly ReadyToRunFixupKind FixupKind;

            public TokenAndFixupKind(mdToken token, ReadyToRunFixupKind fixupKind)
            {
                Token = token;
                FixupKind = fixupKind;
            }

            public override bool Equals(object obj)
            {
                TokenAndFixupKind other = (TokenAndFixupKind)obj;
                return Token == other.Token && FixupKind == other.FixupKind;
            }

            public override int GetHashCode()
            {
                return (int)Token ^ unchecked(31 * (int)FixupKind);
            }
        }

        Dictionary<TokenAndFixupKind, MethodFixupSignature> _methodSignatures = new Dictionary<TokenAndFixupKind, MethodFixupSignature>();

        public MethodFixupSignature GetOrAddMethodSignature(
            ReadyToRunFixupKind fixupKind,
            MethodDesc methodDesc,
            mdToken token,
            MethodFixupSignature.SignatureKind signatureKind)
        {
            TokenAndFixupKind signatureKey = new TokenAndFixupKind(token, fixupKind);
            MethodFixupSignature signature;
            if (!_methodSignatures.TryGetValue(signatureKey, out signature))
            {
                signature = new MethodFixupSignature(fixupKind, methodDesc, token, signatureKind);
                _methodSignatures.Add(signatureKey, signature);
            }
            return signature;
        }

        public override void AttachToDependencyGraph(DependencyAnalyzerBase<NodeFactory> graph)
        {
            Header = new HeaderNode(Target);

            var compilerIdentifierNode = new CompilerIdentifierNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.CompilerIdentifier, compilerIdentifierNode, compilerIdentifierNode);

            RuntimeFunctionsTable = new RuntimeFunctionsTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.RuntimeFunctions, RuntimeFunctionsTable, RuntimeFunctionsTable);

            _runtimeFunctionsGCInfo = new RuntimeFunctionsGCInfoNode();
            graph.AddRoot(_runtimeFunctionsGCInfo, "GC info is always generated");

            MethodEntryPointTable = new MethodEntryPointTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.MethodDefEntryPoints, MethodEntryPointTable, MethodEntryPointTable);

            InstanceEntryPointTable = new InstanceEntryPointTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.InstanceMethodEntryPoints, InstanceEntryPointTable, InstanceEntryPointTable);

            TypesTable = new TypesTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.AvailableTypes, TypesTable, TypesTable);

            ImportSectionsTable = new ImportSectionsTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.ImportSections, ImportSectionsTable, ImportSectionsTable.StartSymbol);

            EagerImports = new ImportSectionNode(
                "EagerImports", 
                CorCompileImportType.CORCOMPILE_IMPORT_TYPE_UNKNOWN, 
                CorCompileImportFlags.CORCOMPILE_IMPORT_FLAGS_EAGER,
                (byte)Target.PointerSize,
                emitPrecode: false);
            ImportSectionsTable.AddEmbeddedObject(EagerImports);

            // All ready-to-run images have a module import helper which gets patched by the runtime on image load
            ModuleImport = new Import(EagerImports, new ReadyToRunHelperSignature(
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Module));
            EagerImports.AddImport(this, ModuleImport);

            MethodImports = new ImportSectionNode(
                "MethodImports",
                CorCompileImportType.CORCOMPILE_IMPORT_TYPE_EXTERNAL_METHOD,
                CorCompileImportFlags.CORCOMPILE_IMPORT_FLAGS_PCODE,
                (byte)Target.PointerSize,
                emitPrecode: true);
            ImportSectionsTable.AddEmbeddedObject(MethodImports);

            HelperImports = new ImportSectionNode(
                "HelperImports",
                CorCompileImportType.CORCOMPILE_IMPORT_TYPE_UNKNOWN,
                CorCompileImportFlags.CORCOMPILE_IMPORT_FLAGS_PCODE,
                (byte)Target.PointerSize,
                emitPrecode: false);
            ImportSectionsTable.AddEmbeddedObject(HelperImports);

            PrecodeImports = new ImportSectionNode(
                "PrecodeImports",
                CorCompileImportType.CORCOMPILE_IMPORT_TYPE_UNKNOWN,
                CorCompileImportFlags.CORCOMPILE_IMPORT_FLAGS_PCODE,
                (byte)Target.PointerSize,
                emitPrecode: true);
            ImportSectionsTable.AddEmbeddedObject(PrecodeImports);

            StringImports = new ImportSectionNode(
                "StringImports",
                CorCompileImportType.CORCOMPILE_IMPORT_TYPE_STRING_HANDLE,
                CorCompileImportFlags.CORCOMPILE_IMPORT_FLAGS_UNKNOWN,
                (byte)Target.PointerSize,
                emitPrecode: true);
            ImportSectionsTable.AddEmbeddedObject(StringImports);

            graph.AddRoot(ImportSectionsTable, "Import sections table is always generated");
            graph.AddRoot(ModuleImport, "Module import is always generated");
            graph.AddRoot(EagerImports, "Eager imports are always generated");
            graph.AddRoot(MethodImports, "Method imports are always generated");
            graph.AddRoot(HelperImports, "Helper imports are always generated");
            graph.AddRoot(PrecodeImports, "Precode imports are always generated");
            graph.AddRoot(StringImports, "String imports are always generated");
            graph.AddRoot(Header, "ReadyToRunHeader is always generated");
        }

        public IMethodNode GetOrAddImportedMethodNode(MethodDesc method, bool unboxingStub, mdToken token, MethodWithGCInfo localMethod)
        {
            ReadyToRunFixupKind fixupKind;
            MethodFixupSignature.SignatureKind signatureKind;
            CorTokenType tokenType = SignatureBuilder.TypeFromToken(token);
            switch (tokenType)
            {
                case CorTokenType.mdtMethodDef:
                    fixupKind = ReadyToRunFixupKind.READYTORUN_FIXUP_MethodEntry_DefToken;
                    signatureKind = MethodFixupSignature.SignatureKind.DefToken;
                    break;

                case CorTokenType.mdtMemberRef:
                    fixupKind = ReadyToRunFixupKind.READYTORUN_FIXUP_MethodEntry_RefToken;
                    signatureKind = MethodFixupSignature.SignatureKind.RefToken;
                    break;

                default:
                    throw new NotImplementedException();
            }
            IMethodNode methodImport;
            if (!_importMethods.TryGetValue(method, out methodImport))
            {
                // First time we see a given external method - emit indirection cell and the import entry
                ExternalMethodImport indirectionCell = new ExternalMethodImport(this, fixupKind, method, token, localMethod, signatureKind);
                _importMethods.Add(method, indirectionCell);
                methodImport = indirectionCell;
            }
            return methodImport;
        }

        Dictionary<InstantiatedMethod, IMethodNode> _instantiatedMethodImports = new Dictionary<InstantiatedMethod, IMethodNode>();

        private IMethodNode GetOrAddInstantiatedMethodNode(InstantiatedMethod method, mdToken token)
        {
            IMethodNode methodImport;
            if (!_instantiatedMethodImports.TryGetValue(method, out methodImport))
            {
                methodImport = new ExternalMethodImport(
                    this,
                    ReadyToRunFixupKind.READYTORUN_FIXUP_MethodEntry, 
                    method, 
                    token, 
                    localMethod: null,
                    MethodFixupSignature.SignatureKind.Signature);
                _instantiatedMethodImports.Add(method, methodImport);
            }
            return methodImport;
        }

        public IMethodNode ShadowConcreteMethod(MethodDesc method, mdToken token, bool isUnboxingStub = false)
        {
            return GetOrCreateMethodEntrypointNode(method, token, isUnboxingStub);
        }

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (!CompilationModuleGroup.ContainsMethodBody(method, unboxingStub: false))
            {
                // Cannot encode external methods without tokens
                throw new NotImplementedException();
            }
            return GetOrCreateMethodEntrypointNode(method, default(mdToken), isUnboxingStub: false);
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            throw new NotImplementedException();
        }
    }
}

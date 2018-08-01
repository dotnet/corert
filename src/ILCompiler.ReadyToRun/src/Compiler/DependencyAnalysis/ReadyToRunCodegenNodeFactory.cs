// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using ILCompiler.DependencyAnalysis.ReadyToRun;
using ILCompiler.DependencyAnalysisFramework;

using Internal.JitInterface;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis
{
    using ReadyToRunHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper;

    public sealed class ReadyToRunCodegenNodeFactory : NodeFactory
    {
        private Dictionary<MethodDesc, IMethodNode> _importMethods;

        private Dictionary<ModuleToken, ISymbolNode> _importStrings;

        public ReadyToRunCodegenNodeFactory(
            CompilerTypeSystemContext context, 
            CompilationModuleGroup compilationModuleGroup,
            MetadataManager metadataManager,
            InteropStubManager interopStubManager, 
            NameMangler nameMangler, 
            VTableSliceProvider vtableSliceProvider, 
            DictionaryLayoutProvider dictionaryLayoutProvider)
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
            _importStrings = new Dictionary<ModuleToken, ISymbolNode>();
            _r2rHelpers = new Dictionary<ReadyToRunHelperId, Dictionary<object, ISymbolNode>>();
        }

        public HeaderNode Header;

        public RuntimeFunctionsTableNode RuntimeFunctionsTable;

        public RuntimeFunctionsGCInfoNode RuntimeFunctionsGCInfo;

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

        Dictionary<MethodDesc, IMethodNode> _methodMap = new Dictionary<MethodDesc, IMethodNode>();

        public IMethodNode MethodEntrypoint(MethodDesc method, ModuleToken token, bool isUnboxingStub = false)
        {
            IMethodNode methodNode;
            if (!_methodMap.TryGetValue(method, out methodNode))
            {
                methodNode = CreateMethodEntrypointNode(method, token, isUnboxingStub);
                _methodMap.Add(method, methodNode);
            }
            return methodNode;
        }

        private IMethodNode CreateMethodEntrypointNode(MethodDesc method, ModuleToken token, bool isUnboxingStub = false)
        {
            if (method is InstantiatedMethod instantiatedMethod)
            {
                return GetOrAddInstantiatedMethodNode(instantiatedMethod, token);
            }

            MethodWithGCInfo localMethod = null;
            if (CompilationModuleGroup.ContainsMethodBody(method, false))
            {
                localMethod = new MethodWithGCInfo(method, token);

                // TODO: hack - how do we distinguish between emitting main entry point and calls between
                // methods?
                if (token.Token == 0)
                {
                    return localMethod;
                }
            }

            return ImportedMethodNode(method, unboxingStub: false, token: token, localMethod: localMethod);
        }

        public IMethodNode StringAllocator(MethodDesc constructor, ModuleToken token)
        {
            return MethodEntrypoint(constructor, token, isUnboxingStub: false);
        }

        public ISymbolNode StringLiteral(ModuleToken token)
        {
            ISymbolNode stringNode;
            if (!_importStrings.TryGetValue(token, out stringNode))
            {
                stringNode = new StringImport(StringImports, token);
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

        private readonly Dictionary<ReadyToRunHelperId, Dictionary<object, ISymbolNode>> _r2rHelpers;

        public ISymbolNode ReadyToRunHelper(ReadyToRunHelperId id, object target, ModuleToken token)
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

        private ISymbolNode CreateNewHelper(TypeDesc type, ModuleToken memberOrTypeToken)
        {
            MetadataReader mdReader = memberOrTypeToken.MetadataReader;
            EntityHandle handle = (EntityHandle)MetadataTokens.Handle((int)memberOrTypeToken.Token);
            ModuleToken typeToken;
            switch (memberOrTypeToken.TokenType)
            {
                case CorTokenType.mdtTypeRef:
                    typeToken = memberOrTypeToken;
                    break;

                case CorTokenType.mdtMemberRef:
                    {
                        MemberReferenceHandle memberRefHandle = (MemberReferenceHandle)handle;
                        MemberReference memberRef = mdReader.GetMemberReference(memberRefHandle);
                        typeToken = new ModuleToken(memberOrTypeToken.Module, (mdToken)MetadataTokens.GetToken(memberRef.Parent));
                    }
                    break;

                case CorTokenType.mdtMethodDef:
                    {
                        MethodDefinitionHandle methodDefHandle = (MethodDefinitionHandle)handle;
                        MethodDefinition methodDef = mdReader.GetMethodDefinition(methodDefHandle);
                        typeToken = new ModuleToken(memberOrTypeToken.Module, (mdToken)MetadataTokens.GetToken(methodDef.GetDeclaringType()));
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }

            return new DelayLoadHelperImport(this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new NewObjectFixupSignature(this, type, typeToken));
        }

        private ISymbolNode CreateNewArrayHelper(ArrayType type, ModuleToken typeRefToken)
        {
            Debug.Assert(typeRefToken.TokenType == CorTokenType.mdtTypeRef);
            return new DelayLoadHelperImport(
                this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new NewArrayFixupSignature(this, type, typeRefToken));
        }

        private ISymbolNode CreateGCStaticBaseHelper(TypeDesc type, ModuleToken token)
        {
            return new DelayLoadHelperImport(
                this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(this, ReadyToRunFixupKind.READYTORUN_FIXUP_StaticBaseGC, type, GetTypeToken(token)));
        }

        private ISymbolNode CreateNonGCStaticBaseHelper(TypeDesc type, ModuleToken token)
        {
            return new DelayLoadHelperImport(
                this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(this, ReadyToRunFixupKind.READYTORUN_FIXUP_StaticBaseNonGC, type, GetTypeToken(token)));
        }

        private ISymbolNode CreateThreadStaticBaseHelper(TypeDesc type, ModuleToken token)
        {
            ReadyToRunFixupKind fixupKind = ReadyToRunFixupKind.READYTORUN_FIXUP_ThreadStaticBaseNonGC;
            return new DelayLoadHelperImport(
                this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(this, fixupKind, type, GetTypeToken(token)));
        }

        private ModuleToken GetTypeToken(ModuleToken token)
        {
            MetadataReader mdReader = token.MetadataReader;
            EntityHandle handle = (EntityHandle)MetadataTokens.Handle((int)token.Token);
            ModuleToken typeToken;
            switch (token.TokenType)
            {
                case CorTokenType.mdtTypeRef:
                case CorTokenType.mdtTypeDef:
                    typeToken = token;
                    break;

                case CorTokenType.mdtMemberRef:
                    {
                        MemberReferenceHandle memberRefHandle = (MemberReferenceHandle)handle;
                        MemberReference memberRef = mdReader.GetMemberReference(memberRefHandle);
                        typeToken = new ModuleToken(token.Module, (mdToken)MetadataTokens.GetToken(memberRef.Parent));
                    }
                    break;

                case CorTokenType.mdtFieldDef:
                    {
                        FieldDefinitionHandle fieldDefHandle = (FieldDefinitionHandle)handle;
                        FieldDefinition fieldDef = mdReader.GetFieldDefinition(fieldDefHandle);
                        typeToken = new ModuleToken(token.Module, (mdToken)MetadataTokens.GetToken(fieldDef.GetDeclaringType()));
                    }
                    break;

                case CorTokenType.mdtMethodDef:
                    {
                        MethodDefinitionHandle methodDefHandle = (MethodDefinitionHandle)handle;
                        MethodDefinition methodDef = mdReader.GetMethodDefinition(methodDefHandle);
                        typeToken = new ModuleToken(token.Module, (mdToken)MetadataTokens.GetToken(methodDef.GetDeclaringType()));
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }

            return typeToken;
        }

        private ISymbolNode CreateIsInstanceOfHelper(TypeDesc type, ModuleToken typeRefToken)
        {
            return new DelayLoadHelperImport(
                this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(this, ReadyToRunFixupKind.READYTORUN_FIXUP_IsInstanceOf, type, typeRefToken));
        }

        private ISymbolNode CreateCastClassHelper(TypeDesc type, ModuleToken typeRefToken)
        {
            return new DelayLoadHelperImport(
                this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_Obj,
                new TypeFixupSignature(this, ReadyToRunFixupKind.READYTORUN_FIXUP_ChkCast, type, typeRefToken));
        }

        private ISymbolNode CreateTypeHandleHelper(TypeDesc type, ModuleToken typeRefToken)
        {
            return new PrecodeHelperImport(
                this,
                new TypeFixupSignature(this, ReadyToRunFixupKind.READYTORUN_FIXUP_TypeHandle, type, typeRefToken));
        }

        private ISymbolNode CreateVirtualCallHelper(MethodDesc method, ModuleToken methodToken)
        {
            return new DelayLoadHelperImport(
                this,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_Obj,
                GetOrAddMethodSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_VirtualEntry, method, methodToken, MethodFixupSignature.SignatureKind.Signature));
        }

        private ISymbolNode CreateDelegateCtorHelper(DelegateCreationInfo info, ModuleToken token)
        {
            return info.Constructor;
        }

        Dictionary<ILCompiler.ReadyToRunHelper, ISymbolNode> _helperCache = new Dictionary<ILCompiler.ReadyToRunHelper, ISymbolNode>();

        public ISymbolNode ExternSymbol(ILCompiler.ReadyToRunHelper helper)
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

        public ISymbolNode HelperMethodEntrypoint(ILCompiler.ReadyToRunHelper helperId, MethodDesc method)
        {
            return ExternSymbol(helperId);
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

        struct MethodAndCallSite : IEquatable<MethodAndCallSite>
        {
            public readonly MethodDesc Method;
            public readonly string CallSite;

            public MethodAndCallSite(MethodDesc method, string callSite)
            {
                CallSite = callSite;
                Method = method;
            }

            public bool Equals(MethodAndCallSite other)
            {
                return CallSite == other.CallSite && Method == other.Method;
            }

            public override bool Equals(object obj)
            {
                return obj is MethodAndCallSite other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (CallSite != null ? CallSite.GetHashCode() : 0) + unchecked(31 * Method.GetHashCode());
            }
        }

        Dictionary<MethodAndCallSite, ISymbolNode> _interfaceDispatchCells = new Dictionary<MethodAndCallSite, ISymbolNode>();

        public ISymbolNode InterfaceDispatchCell(MethodDesc method, ModuleToken token, string callSite = null)
        {
            MethodAndCallSite cellKey = new MethodAndCallSite(method, callSite);
            ISymbolNode dispatchCell;
            if (!_interfaceDispatchCells.TryGetValue(cellKey, out dispatchCell))
            {
                dispatchCell = new DelayLoadHelperImport(
                    this,
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
            return new Import(EagerImports, new ReadyToRunHelperSignature(helperId));
        }

        public ISymbolNode ComputeConstantLookup(ReadyToRunHelperId helperId, object entity, ModuleToken token)
        {
            return ReadyToRunHelper(helperId, entity, token);
        }

        Dictionary<MethodDesc, ISortableSymbolNode> _genericDictionaryCache = new Dictionary<MethodDesc, ISortableSymbolNode>();

        public ISortableSymbolNode MethodGenericDictionary(MethodDesc method, ModuleToken token)
        {
            ISortableSymbolNode genericDictionary;
            if (!_genericDictionaryCache.TryGetValue(method, out genericDictionary))
            {
                genericDictionary = new DelayLoadHelperImport(
                    this,
                    ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                    GetOrAddMethodSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_MethodDictionary, method, token, MethodFixupSignature.SignatureKind.Signature));
                _genericDictionaryCache.Add(method, genericDictionary);
            }
            return genericDictionary;
        }

        struct MethodAndFixupKind : IEquatable<MethodAndFixupKind>
        {
            public readonly MethodDesc Method;
            public readonly ReadyToRunFixupKind FixupKind;

            public MethodAndFixupKind(MethodDesc method, ReadyToRunFixupKind fixupKind)
            {
                Method = method;
                FixupKind = fixupKind;
            }

            public bool Equals(MethodAndFixupKind other)
            {
                return Method == other.Method && FixupKind == other.FixupKind;
            }

            public override bool Equals(object obj)
            {
                return obj is MethodAndFixupKind other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (int)Method.GetHashCode() ^ unchecked(31 * (int)FixupKind);
            }
        }

        Dictionary<MethodAndFixupKind, MethodFixupSignature> _methodSignatures = new Dictionary<MethodAndFixupKind, MethodFixupSignature>();

        public MethodFixupSignature GetOrAddMethodSignature(
            ReadyToRunFixupKind fixupKind,
            MethodDesc methodDesc,
            ModuleToken token,
            MethodFixupSignature.SignatureKind signatureKind)
        {
            MethodAndFixupKind signatureKey = new MethodAndFixupKind(methodDesc, fixupKind);
            MethodFixupSignature signature;
            if (!_methodSignatures.TryGetValue(signatureKey, out signature))
            {
                signature = new MethodFixupSignature(this, fixupKind, methodDesc, token, signatureKind);
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

            RuntimeFunctionsGCInfo = new RuntimeFunctionsGCInfoNode();
            graph.AddRoot(RuntimeFunctionsGCInfo, "GC info is always generated");

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

        public IMethodNode ImportedMethodNode(MethodDesc method, bool unboxingStub, ModuleToken token, MethodWithGCInfo localMethod)
        {
            ReadyToRunFixupKind fixupKind;
            MethodFixupSignature.SignatureKind signatureKind;
            CorTokenType tokenType = token.TokenType;
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

        private IMethodNode GetOrAddInstantiatedMethodNode(InstantiatedMethod method, ModuleToken token)
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

        public IMethodNode ShadowConcreteMethod(MethodDesc method, ModuleToken token, bool isUnboxingStub = false)
        {
            return MethodEntrypoint(method, token, isUnboxingStub);
        }

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (!CompilationModuleGroup.ContainsMethodBody(method, unboxingStub: false))
            {
                // Cannot encode external methods without tokens
                throw new NotImplementedException();
            }
            return MethodEntrypoint(method, default(ModuleToken), isUnboxingStub: false);
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            throw new NotImplementedException();
        }

        struct TypeAndMethod : IEquatable<TypeAndMethod>
        {
            public readonly TypeDesc Type;
            public readonly MethodDesc Method;

            public TypeAndMethod(TypeDesc type, MethodDesc method)
            {
                Type = type;
                Method = method;
            }

            public bool Equals(TypeAndMethod other)
            {
                return Type == other.Type && Method == other.Method;
            }

            public override bool Equals(object obj)
            {
                return obj is TypeAndMethod other && Equals(other);
            }

            public override int GetHashCode()
            {
                return Type.GetHashCode() ^ unchecked(Method.GetHashCode() * 31);
            }
        }

        private Dictionary<TypeAndMethod, ISymbolNode> _delegateCtors = new Dictionary<TypeAndMethod, ISymbolNode>();

        public ISymbolNode DelegateCtor(TypeDesc delegateType, MethodDesc targetMethod, ModuleToken methodToken)
        {
            ISymbolNode ctorNode;
            TypeAndMethod ctorKey = new TypeAndMethod(delegateType, targetMethod);
            if (!_delegateCtors.TryGetValue(ctorKey, out ctorNode))
            {
                ModuleToken delegateTypeToken;
                if (CompilationModuleGroup.ContainsType(delegateType) && delegateType is EcmaType ecmaType)
                {
                    delegateTypeToken = new ModuleToken(ecmaType.EcmaModule, (mdToken)MetadataTokens.GetToken(ecmaType.Handle));
                }
                else
                {
                    // TODO: reverse typedef lookup within the version bubble
                    throw new NotImplementedException();
                }

                IMethodNode targetMethodNode = MethodEntrypoint(targetMethod, methodToken, isUnboxingStub: false);

                ctorNode = new DelayLoadHelperImport(this,
                    ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                    new DelegateCtorSignature(this, delegateType, delegateTypeToken, targetMethodNode, methodToken));
                _delegateCtors.Add(ctorKey, ctorNode);
            }
            return ctorNode;
        }
    }
}

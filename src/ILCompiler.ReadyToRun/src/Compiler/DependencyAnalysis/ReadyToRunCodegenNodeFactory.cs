// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using ILCompiler.DependencyAnalysis.ReadyToRun;
using ILCompiler.DependencyAnalysisFramework;

using Internal.JitInterface;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis
{
    using System.Collections.Immutable;
    using ReadyToRunHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper;

    public sealed class ReadyToRunCodegenNodeFactory : NodeFactory
    {
        private Dictionary<TypeAndMethod, IMethodNode> _importMethods;

        private Dictionary<ModuleToken, ISymbolNode> _importStrings;

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
            _importMethods = new Dictionary<TypeAndMethod, IMethodNode>();
            _importStrings = new Dictionary<ModuleToken, ISymbolNode>();
            _r2rHelpers = new Dictionary<ReadyToRunHelperId, Dictionary<object, ISymbolNode>>();

            Resolver = new ModuleTokenResolver(compilationModuleGroup);
            InputModuleContext = new SignatureContext(Resolver, inputModule);
        }

        public SignatureContext InputModuleContext;

        public ModuleTokenResolver Resolver;

        public HeaderNode Header;

        public RuntimeFunctionsTableNode RuntimeFunctionsTable;

        public RuntimeFunctionsGCInfoNode RuntimeFunctionsGCInfo;

        public MethodEntryPointTableNode MethodEntryPointTable;

        public InstanceEntryPointTableNode InstanceEntryPointTable;

        public TypesTableNode TypesTable;

        public ImportSectionsTableNode ImportSectionsTable;

        public Import ModuleImport;

        public ISymbolNode PersonalityRoutine;

        public ISymbolNode FilterFuncletPersonalityRoutine;

        public DebugInfoTableNode DebugInfoTable;

        public ImportSectionNode EagerImports;

        public ImportSectionNode MethodImports;

        public ImportSectionNode DispatchImports;

        public ImportSectionNode StringImports;

        public ImportSectionNode HelperImports;

        public ImportSectionNode PrecodeImports;

        public IMethodNode MethodEntrypoint(MethodDesc method, TypeDesc constrainedType, SignatureContext signatureContext, bool isUnboxingStub = false)
        {
            return _methodEntrypoints.GetOrAdd(method, (m) =>
            {
                return CreateMethodEntrypointNode(method, constrainedType, signatureContext, isUnboxingStub);
            });
        }

        private IMethodNode CreateMethodEntrypointNode(MethodDesc method, TypeDesc constrainedType, SignatureContext signatureContext, bool isUnboxingStub)
        {
            if (method is InstantiatedMethod instantiatedMethod)
            {
                return InstantiatedMethodNode(instantiatedMethod, constrainedType, signatureContext, isUnboxingStub);
            }

            MethodWithGCInfo localMethod = null;
            if (CompilationModuleGroup.ContainsMethodBody(method, false))
            {
                localMethod = new MethodWithGCInfo(method, signatureContext);
            }

            return ImportedMethodNode(method, unboxingStub: isUnboxingStub, constrainedType: constrainedType, signatureContext: signatureContext, localMethod: localMethod);
        }

        public IMethodNode StringAllocator(MethodDesc constructor, SignatureContext signatureContext)
        {
            return MethodEntrypoint(constructor, constrainedType: null, signatureContext: signatureContext, isUnboxingStub: false);
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

        public ISymbolNode ReadyToRunHelper(ReadyToRunHelperId id, object target, SignatureContext signatureContext)
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
                    helperNode = CreateNewHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.NewArr1:
                    helperNode = CreateNewArrayHelper((ArrayType)target, signatureContext);
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    helperNode = CreateGCStaticBaseHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.GetNonGCStaticBase:
                    helperNode = CreateNonGCStaticBaseHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    helperNode = CreateThreadGcStaticBaseHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.GetThreadNonGcStaticBase:
                    helperNode = CreateThreadNonGcStaticBaseHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.IsInstanceOf:
                    helperNode = CreateIsInstanceOfHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.CastClass:
                    helperNode = CreateCastClassHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.TypeHandle:
                    helperNode = CreateTypeHandleHelper((TypeDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.FieldHandle:
                    helperNode = CreateFieldHandleHelper((FieldDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.VirtualCall:
                    helperNode = CreateVirtualCallHelper((MethodDesc)target, signatureContext);
                    break;

                case ReadyToRunHelperId.DelegateCtor:
                    helperNode = CreateDelegateCtorHelper((DelegateCreationInfo)target, signatureContext);
                    break;

                default:
                    throw new NotImplementedException();
            }

            helperNodeMap.Add(target, helperNode);
            return helperNode;
        }

        private ISymbolNode CreateNewHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                this,
                HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new NewObjectFixupSignature(type, signatureContext));
        }

        private ISymbolNode CreateNewArrayHelper(ArrayType type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                this,
                HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new NewArrayFixupSignature(type, signatureContext));
        }

        private ISymbolNode CreateGCStaticBaseHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                this,
                HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_StaticBaseGC, type, signatureContext));
        }

        private ISymbolNode CreateNonGCStaticBaseHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                this,
                HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_StaticBaseNonGC, type, signatureContext));
        }

        private ISymbolNode CreateThreadGcStaticBaseHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                this,
                HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_ThreadStaticBaseGC, type, signatureContext));
        }

        private ISymbolNode CreateThreadNonGcStaticBaseHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                this,
                HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_ThreadStaticBaseNonGC, type, signatureContext));
        }

        private ModuleToken GetTypeToken(ModuleToken token)
        {
            if (token.IsNull)
            {
                return token;
            }
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

        private ISymbolNode CreateIsInstanceOfHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                this,
                HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_IsInstanceOf, type, signatureContext));
        }

        private ISymbolNode CreateCastClassHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                this,
                HelperImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_Obj,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_ChkCast, type, signatureContext));
        }

        private ISymbolNode CreateTypeHandleHelper(TypeDesc type, SignatureContext signatureContext)
        {
            return new PrecodeHelperImport(
                this,
                new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_TypeHandle, type, signatureContext));
        }

        private ISymbolNode CreateFieldHandleHelper(FieldDesc field, SignatureContext signatureContext)
        {
            return new PrecodeHelperImport(
                this,
                new FieldFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_FieldHandle, field, signatureContext));
        }

        private ISymbolNode CreateVirtualCallHelper(MethodDesc method, SignatureContext signatureContext)
        {
            return new DelayLoadHelperImport(
                this,
                DispatchImports,
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper_Obj,
                MethodSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_VirtualEntry, method,
                    constrainedType: null, signatureContext: signatureContext, isUnboxingStub: false, isInstantiatingStub: false));
        }

        private ISymbolNode CreateDelegateCtorHelper(DelegateCreationInfo info, SignatureContext signatureContext)
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

            ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper r2rHelper;
            switch (helper)
            {
                // Exception handling helpers
                case ILCompiler.ReadyToRunHelper.Throw:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Throw;
                    break;

                case ILCompiler.ReadyToRunHelper.Rethrow:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Rethrow;
                    break;

                case ILCompiler.ReadyToRunHelper.Overflow:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Overflow;
                    break;

                case ILCompiler.ReadyToRunHelper.RngChkFail:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_RngChkFail;
                    break;

                case ILCompiler.ReadyToRunHelper.FailFast:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_FailFast;
                    break;

                case ILCompiler.ReadyToRunHelper.ThrowNullRef:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ThrowNullRef;
                    break;

                case ILCompiler.ReadyToRunHelper.ThrowDivZero:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ThrowDivZero;
                    break;

                // Write barriers
                case ILCompiler.ReadyToRunHelper.WriteBarrier:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_WriteBarrier;
                    break;

                case ILCompiler.ReadyToRunHelper.CheckedWriteBarrier:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_CheckedWriteBarrier;
                    break;

                case ILCompiler.ReadyToRunHelper.ByRefWriteBarrier:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ByRefWriteBarrier;
                    break;

                // Array helpers
                case ILCompiler.ReadyToRunHelper.Stelem_Ref:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Stelem_Ref;
                    break;

                case ILCompiler.ReadyToRunHelper.Ldelema_Ref:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Ldelema_Ref;
                    break;

                case ILCompiler.ReadyToRunHelper.MemSet:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_MemSet;
                    break;

                case ILCompiler.ReadyToRunHelper.MemCpy:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_MemCpy;
                    break;

                // Get string handle lazily
                case ILCompiler.ReadyToRunHelper.GetString:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GetString;
                    break;

                // Reflection helpers
                case ILCompiler.ReadyToRunHelper.GetRuntimeTypeHandle:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GetRuntimeTypeHandle;
                    break;

                case ILCompiler.ReadyToRunHelper.GetRuntimeMethodHandle:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GetRuntimeMethodHandle;
                    break;

                case ILCompiler.ReadyToRunHelper.GetRuntimeFieldHandle:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GetRuntimeFieldHandle;
                    break;

                case ILCompiler.ReadyToRunHelper.Box:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Box;
                    break;

                case ILCompiler.ReadyToRunHelper.Box_Nullable:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Box_Nullable;
                    break;

                case ILCompiler.ReadyToRunHelper.Unbox:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Unbox;
                    break;

                case ILCompiler.ReadyToRunHelper.Unbox_Nullable:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Unbox_Nullable;
                    break;

                case ILCompiler.ReadyToRunHelper.NewMultiDimArr:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_NewMultiDimArr;
                    break;

                case ILCompiler.ReadyToRunHelper.NewMultiDimArr_NonVarArg:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_NewMultiDimArr_NonVarArg;
                    break;

                // Helpers used with generic handle lookup cases
                case ILCompiler.ReadyToRunHelper.NewObject:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_NewObject;
                    break;

                case ILCompiler.ReadyToRunHelper.NewArray:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_NewArray;
                    break;

                case ILCompiler.ReadyToRunHelper.CheckCastAny:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_CheckCastAny;
                    break;

                case ILCompiler.ReadyToRunHelper.CheckInstanceAny:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_CheckInstanceAny;
                    break;

                case ILCompiler.ReadyToRunHelper.GenericGcStaticBase:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GenericGcStaticBase;
                    break;

                case ILCompiler.ReadyToRunHelper.GenericNonGcStaticBase:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GenericNonGcStaticBase;
                    break;

                case ILCompiler.ReadyToRunHelper.GenericGcTlsBase:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GenericGcTlsBase;
                    break;

                case ILCompiler.ReadyToRunHelper.GenericNonGcTlsBase:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_GenericNonGcTlsBase;
                    break;

                case ILCompiler.ReadyToRunHelper.VirtualFuncPtr:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_VirtualFuncPtr;
                    break;

                // Long mul/div/shift ops
                case ILCompiler.ReadyToRunHelper.LMul:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LMul;
                    break;

                case ILCompiler.ReadyToRunHelper.LMulOfv:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LMulOfv;
                    break;

                case ILCompiler.ReadyToRunHelper.ULMulOvf:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ULMulOvf;
                    break;

                case ILCompiler.ReadyToRunHelper.LDiv:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LDiv;
                    break;

                case ILCompiler.ReadyToRunHelper.LMod:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LMod;
                    break;

                case ILCompiler.ReadyToRunHelper.ULDiv:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ULDiv;
                    break;

                case ILCompiler.ReadyToRunHelper.ULMod:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ULMod;
                    break;

                case ILCompiler.ReadyToRunHelper.LLsh:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LLsh;
                    break;

                case ILCompiler.ReadyToRunHelper.LRsh:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LRsh;
                    break;

                case ILCompiler.ReadyToRunHelper.LRsz:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_LRsz;
                    break;

                case ILCompiler.ReadyToRunHelper.Lng2Dbl:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Lng2Dbl;
                    break;

                case ILCompiler.ReadyToRunHelper.ULng2Dbl:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_ULng2Dbl;
                    break;

                // 32-bit division helpers
                case ILCompiler.ReadyToRunHelper.Div:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Div;
                    break;

                case ILCompiler.ReadyToRunHelper.Mod:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Mod;
                    break;

                case ILCompiler.ReadyToRunHelper.UDiv:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_UDiv;
                    break;

                case ILCompiler.ReadyToRunHelper.UMod:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_UMod;
                    break;

                // Floating point conversions
                case ILCompiler.ReadyToRunHelper.Dbl2Int:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2Int;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2IntOvf:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2IntOvf;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2Lng:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2Lng;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2LngOvf:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2LngOvf;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2UInt:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2UInt;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2UIntOvf:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2UIntOvf;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2ULng:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2ULng;
                    break;

                case ILCompiler.ReadyToRunHelper.Dbl2ULngOvf:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_Dbl2ULngOvf;
                    break;

                // Floating point ops
                case ILCompiler.ReadyToRunHelper.DblRem:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DblRem;
                    break;

                case ILCompiler.ReadyToRunHelper.FltRem:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_FltRem;
                    break;

                case ILCompiler.ReadyToRunHelper.DblRound:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DblRound;
                    break;

                case ILCompiler.ReadyToRunHelper.FltRound:
                    r2rHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_FltRound;
                    break;

                default:
                    throw new NotImplementedException(helper.ToString());
            }

            result = GetReadyToRunHelperCell(r2rHelper);
            _helperCache.Add(helper, result);
            return result;
        }

        public ISymbolNode HelperMethodEntrypoint(ILCompiler.ReadyToRunHelper helperId, MethodDesc method)
        {
            return ExternSymbol(helperId);
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

        public ISymbolNode InterfaceDispatchCell(MethodDesc method, SignatureContext signatureContext, bool isUnboxingStub, string callSite)
        {
            MethodAndCallSite cellKey = new MethodAndCallSite(method, callSite);
            ISymbolNode dispatchCell;
            if (!_interfaceDispatchCells.TryGetValue(cellKey, out dispatchCell))
            {
                dispatchCell = new DelayLoadHelperImport(
                    this,
                    DispatchImports,
                    ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_MethodCall |
                    ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_FLAG_VSD,
                    MethodSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_VirtualEntry, method,
                        constrainedType: null, signatureContext: signatureContext, isUnboxingStub, isInstantiatingStub: false),
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

        public ISymbolNode ComputeConstantLookup(ReadyToRunHelperId helperId, object entity, SignatureContext signatureContext)
        {
            return ReadyToRunHelper(helperId, entity, signatureContext);
        }

        Dictionary<MethodDesc, ISortableSymbolNode> _genericDictionaryCache = new Dictionary<MethodDesc, ISortableSymbolNode>();

        public ISortableSymbolNode MethodGenericDictionary(MethodDesc method, SignatureContext signatureContext)
        {
            ISortableSymbolNode genericDictionary;
            if (!_genericDictionaryCache.TryGetValue(method, out genericDictionary))
            {
                genericDictionary = new PrecodeHelperImport(
                    this,
                    MethodSignature(
                        ReadyToRunFixupKind.READYTORUN_FIXUP_MethodDictionary,
                        method,
                        constrainedType: null,
                        signatureContext: signatureContext,
                        isUnboxingStub: false,
                        isInstantiatingStub: true));
                _genericDictionaryCache.Add(method, genericDictionary);
            }
            return genericDictionary;
        }

        Dictionary<TypeDesc, ISymbolNode> _constructedTypeSymbols = new Dictionary<TypeDesc, ISymbolNode>();

        public ISymbolNode ConstructedTypeSymbol(TypeDesc type, SignatureContext signatureContext)
        {
            ISymbolNode symbol;
            if (!_constructedTypeSymbols.TryGetValue(type, out symbol))
            {
                symbol = new PrecodeHelperImport(
                    this,
                    new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_TypeDictionary, type, signatureContext));
                _constructedTypeSymbols.Add(type, symbol);
            }
            return symbol;
        }

        struct MethodAndFixupKind : IEquatable<MethodAndFixupKind>
        {
            public readonly MethodDesc Method;
            public readonly bool IsUnboxingStub;
            public readonly ReadyToRunFixupKind FixupKind;

            public MethodAndFixupKind(MethodDesc method, bool isUnboxingStub, ReadyToRunFixupKind fixupKind)
            {
                Method = method;
                IsUnboxingStub = isUnboxingStub;
                FixupKind = fixupKind;
            }

            public bool Equals(MethodAndFixupKind other)
            {
                return Method == other.Method && IsUnboxingStub == other.IsUnboxingStub && FixupKind == other.FixupKind;
            }

            public override bool Equals(object obj)
            {
                return obj is MethodAndFixupKind other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (int)Method.GetHashCode() ^ unchecked(31 * (int)FixupKind) ^ (IsUnboxingStub ? -0x80000000 : 0);
            }
        }

        Dictionary<ReadyToRunFixupKind, Dictionary<TypeAndMethod, MethodFixupSignature>> _methodSignatures =
            new Dictionary<ReadyToRunFixupKind, Dictionary<TypeAndMethod, MethodFixupSignature>>();

        public MethodFixupSignature MethodSignature(
            ReadyToRunFixupKind fixupKind,
            MethodDesc methodDesc,
            TypeDesc constrainedType,
            SignatureContext signatureContext,
            bool isUnboxingStub,
            bool isInstantiatingStub)
        {
            Dictionary<TypeAndMethod, MethodFixupSignature> perFixupKindMap;
            if (!_methodSignatures.TryGetValue(fixupKind, out perFixupKindMap))
            {
                perFixupKindMap = new Dictionary<TypeAndMethod, MethodFixupSignature>();
                _methodSignatures.Add(fixupKind, perFixupKindMap);
            }

            TypeAndMethod key = new TypeAndMethod(constrainedType, methodDesc, isUnboxingStub, isInstantiatingStub);
            MethodFixupSignature signature;
            if (!perFixupKindMap.TryGetValue(key, out signature))
            {
                signature = new MethodFixupSignature(fixupKind, methodDesc, constrainedType, signatureContext, isUnboxingStub, isInstantiatingStub);
                perFixupKindMap.Add(key, signature);
            }
            return signature;
        }

        public override void AttachToDependencyGraph(DependencyAnalyzerBase<NodeFactory> graph)
        {
            Header = new HeaderNode(Target);

            var compilerIdentifierNode = new CompilerIdentifierNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.CompilerIdentifier, compilerIdentifierNode, compilerIdentifierNode);

            RuntimeFunctionsTable = new RuntimeFunctionsTableNode(this);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.RuntimeFunctions, RuntimeFunctionsTable, RuntimeFunctionsTable);

            RuntimeFunctionsGCInfo = new RuntimeFunctionsGCInfoNode();
            graph.AddRoot(RuntimeFunctionsGCInfo, "GC info is always generated");

            ExceptionInfoLookupTableNode exceptionInfoLookupTableNode = new ExceptionInfoLookupTableNode(this);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.ExceptionInfo, exceptionInfoLookupTableNode, exceptionInfoLookupTableNode);
            graph.AddRoot(exceptionInfoLookupTableNode, "ExceptionInfoLookupTable is always generated");

            MethodEntryPointTable = new MethodEntryPointTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.MethodDefEntryPoints, MethodEntryPointTable, MethodEntryPointTable);

            InstanceEntryPointTable = new InstanceEntryPointTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.InstanceMethodEntryPoints, InstanceEntryPointTable, InstanceEntryPointTable);

            TypesTable = new TypesTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.AvailableTypes, TypesTable, TypesTable);

            ImportSectionsTable = new ImportSectionsTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.ImportSections, ImportSectionsTable, ImportSectionsTable.StartSymbol);

            DebugInfoTable = new DebugInfoTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.DebugInfo, DebugInfoTable, DebugInfoTable);

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
            graph.AddRoot(ModuleImport, "Module import is required by the R2R format spec");

            Import personalityRoutineImport = new Import(EagerImports, new ReadyToRunHelperSignature(
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_PersonalityRoutine));
            PersonalityRoutine = new ImportThunk(
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_PersonalityRoutine, this, personalityRoutineImport);
            graph.AddRoot(PersonalityRoutine, "Personality routine is faster to root early rather than referencing it from each unwind info");

            Import filterFuncletPersonalityRoutineImport = new Import(EagerImports, new ReadyToRunHelperSignature(
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_PersonalityRoutineFilterFunclet));
            FilterFuncletPersonalityRoutine = new ImportThunk(
                ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_PersonalityRoutineFilterFunclet, this, filterFuncletPersonalityRoutineImport);
            graph.AddRoot(FilterFuncletPersonalityRoutine, "Filter funclet personality routine is faster to root early rather than referencing it from each unwind info");

            MethodImports = new ImportSectionNode(
                "MethodImports",
                CorCompileImportType.CORCOMPILE_IMPORT_TYPE_STUB_DISPATCH,
                CorCompileImportFlags.CORCOMPILE_IMPORT_FLAGS_PCODE,
                (byte)Target.PointerSize,
                emitPrecode: true);
            ImportSectionsTable.AddEmbeddedObject(MethodImports);

            DispatchImports = new ImportSectionNode(
                "DispatchImports",
                CorCompileImportType.CORCOMPILE_IMPORT_TYPE_STUB_DISPATCH,
                CorCompileImportFlags.CORCOMPILE_IMPORT_FLAGS_PCODE,
                (byte)Target.PointerSize,
                emitPrecode: false);
            ImportSectionsTable.AddEmbeddedObject(DispatchImports);

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
            graph.AddRoot(DispatchImports, "Dispatch imports are always generated");
            graph.AddRoot(HelperImports, "Helper imports are always generated");
            graph.AddRoot(PrecodeImports, "Precode imports are always generated");
            graph.AddRoot(StringImports, "String imports are always generated");
            graph.AddRoot(Header, "ReadyToRunHeader is always generated");

            MetadataManager.AttachToDependencyGraph(graph);
        }

        public IMethodNode ImportedMethodNode(MethodDesc method, TypeDesc constrainedType, SignatureContext signatureContext, bool unboxingStub, MethodWithGCInfo localMethod)
        {
            IMethodNode methodImport;
            TypeAndMethod key = new TypeAndMethod(constrainedType, method, unboxingStub, isInstantiatingStub: false);
            if (!_importMethods.TryGetValue(key, out methodImport))
            {
                // First time we see a given external method - emit indirection cell and the import entry
                ExternalMethodImport indirectionCell = new ExternalMethodImport(
                    this,
                    ReadyToRunFixupKind.READYTORUN_FIXUP_MethodEntry,
                    method,
                    constrainedType,
                    signatureContext,
                    unboxingStub,
                    localMethod);
                _importMethods.Add(key, indirectionCell);
                methodImport = indirectionCell;
            }
            return methodImport;
        }

        Dictionary<InstantiatedMethod, IMethodNode> _instantiatedMethodImports = new Dictionary<InstantiatedMethod, IMethodNode>();

        private IMethodNode InstantiatedMethodNode(InstantiatedMethod method, TypeDesc constrainedType, SignatureContext signatureContext, bool isUnboxingStub)
        {
            IMethodNode methodImport;
            if (!_instantiatedMethodImports.TryGetValue(method, out methodImport))
            {
                methodImport = new ExternalMethodImport(
                    this,
                    ReadyToRunFixupKind.READYTORUN_FIXUP_MethodEntry,
                    method,
                    constrainedType,
                    signatureContext,
                    isUnboxingStub,
                    localMethod: null);
                _instantiatedMethodImports.Add(method, methodImport);
            }
            return methodImport;
        }

        private Dictionary<TypeAndMethod, IMethodNode> _shadowConcreteMethods = new Dictionary<TypeAndMethod, IMethodNode>();

        public IMethodNode ShadowConcreteMethod(MethodDesc method, TypeDesc constrainedType, SignatureContext signatureContext, bool isUnboxingStub = false)
        {
            IMethodNode result;
            TypeAndMethod key = new TypeAndMethod(constrainedType, method, isUnboxingStub, isInstantiatingStub: false);
            if (!_shadowConcreteMethods.TryGetValue(key, out result))
            {
                result = MethodEntrypoint(method, constrainedType, signatureContext, isUnboxingStub);
                _shadowConcreteMethods.Add(key, result);
            }
            return result;
        }

        protected override IEETypeNode CreateNecessaryTypeNode(TypeDesc type)
        {
            if (CompilationModuleGroup.ContainsType(type))
            {
                return new AvailableType(this, type);
            }
            else
            {
                return new ExternalTypeNode(this, type);
            }
        }

        protected override IEETypeNode CreateConstructedTypeNode(TypeDesc type)
        {
            // Canonical definition types are *not* constructed types (call NecessaryTypeSymbol to get them)
            Debug.Assert(!type.IsCanonicalDefinitionType(CanonicalFormKind.Any));
            
            if (CompilationModuleGroup.ContainsType(type))
            {
                return new AvailableType(this, type);
            }
            else
            {
                return new ExternalTypeNode(this, type);
            }
        }

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (!CompilationModuleGroup.ContainsMethodBody(method, unboxingStub: false))
            {
                // Cannot encode external methods without tokens
                throw new NotImplementedException();
            }

            return MethodEntrypoint(method, constrainedType: null, signatureContext: InputModuleContext, isUnboxingStub: false);
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            throw new NotImplementedException();
        }

        struct TypeAndMethod : IEquatable<TypeAndMethod>
        {
            public readonly TypeDesc Type;
            public readonly MethodDesc Method;
            public readonly bool IsUnboxingStub;
            public readonly bool IsInstantiatingStub;

            public TypeAndMethod(TypeDesc type, MethodDesc method, bool isUnboxingStub, bool isInstantiatingStub)
            {
                Type = type;
                Method = method;
                IsUnboxingStub = isUnboxingStub;
                IsInstantiatingStub = isInstantiatingStub;
            }

            public bool Equals(TypeAndMethod other)
            {
                return Type == other.Type &&
                    Method == other.Method &&
                    IsUnboxingStub == other.IsUnboxingStub &&
                    IsInstantiatingStub == other.IsInstantiatingStub;
            }

            public override bool Equals(object obj)
            {
                return obj is TypeAndMethod other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (Type?.GetHashCode() ?? 0) ^ unchecked(Method.GetHashCode() * 31) ^ (IsUnboxingStub ? -0x80000000 : 0) ^ (IsInstantiatingStub ? 0x40000000 : 0);
            }
        }

        private Dictionary<TypeAndMethod, ISymbolNode> _delegateCtors = new Dictionary<TypeAndMethod, ISymbolNode>();

        public ISymbolNode DelegateCtor(TypeDesc delegateType, MethodDesc targetMethod, SignatureContext signatureContext)
        {
            ISymbolNode ctorNode;
            TypeAndMethod ctorKey = new TypeAndMethod(delegateType, targetMethod, isUnboxingStub: false, isInstantiatingStub: false);
            if (!_delegateCtors.TryGetValue(ctorKey, out ctorNode))
            {
                IMethodNode targetMethodNode = MethodEntrypoint(targetMethod, constrainedType: null, signatureContext: signatureContext, isUnboxingStub: false);

                ctorNode = new DelayLoadHelperImport(
                    this,
                    HelperImports,
                    ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper.READYTORUN_HELPER_DelayLoad_Helper,
                    new DelegateCtorSignature(delegateType, targetMethodNode, signatureContext));
                _delegateCtors.Add(ctorKey, ctorNode);
            }
            return ctorNode;
        }
    }
}

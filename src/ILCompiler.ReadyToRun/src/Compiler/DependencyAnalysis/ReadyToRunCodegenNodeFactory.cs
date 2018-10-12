﻿// Licensed to the .NET Foundation under one or more agreements.
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

namespace ILCompiler.DependencyAnalysis
{
    using ReadyToRunHelper = ILCompiler.DependencyAnalysis.ReadyToRun.ReadyToRunHelper;

    public sealed class ReadyToRunCodegenNodeFactory : NodeFactory
    {
        private Dictionary<TypeAndMethod, IMethodNode> _importMethods;

        public ReadyToRunCodegenNodeFactory(
            CompilerTypeSystemContext context,
            CompilationModuleGroup compilationModuleGroup,
            MetadataManager metadataManager,
            InteropStubManager interopStubManager,
            NameMangler nameMangler,
            VTableSliceProvider vtableSliceProvider,
            DictionaryLayoutProvider dictionaryLayoutProvider,
            ModuleTokenResolver moduleTokenResolver,
            SignatureContext signatureContext)
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

            Resolver = moduleTokenResolver;
            InputModuleContext = signatureContext;
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

        private readonly Dictionary<ReadyToRunHelper, ISymbolNode> _constructedHelpers = new Dictionary<ReadyToRunHelper, ISymbolNode>();

        public ISymbolNode GetReadyToRunHelperCell(ReadyToRunHelper helperId)
        {
            if (!_constructedHelpers.TryGetValue(helperId, out ISymbolNode helperCell))
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

        public IMethodNode MethodEntrypoint(MethodDesc targetMethod, TypeDesc constrainedType, MethodDesc originalMethod, SignatureContext signatureContext, bool isUnboxingStub = false)
        {
            return _methodEntrypoints.GetOrAdd(targetMethod, (m) =>
            {
                return CreateMethodEntrypointNode(targetMethod, constrainedType, originalMethod, signatureContext, isUnboxingStub);
            });
        }

        private IMethodNode CreateMethodEntrypointNode(MethodDesc targetMethod, TypeDesc constrainedType, MethodDesc originalMethod, SignatureContext signatureContext, bool isUnboxingStub)
        {
            if (constrainedType != null)
            {
                // We may be able to optimize away the constrained call if targetMethod is within the version bubble
                if (CompilationModuleGroup.ContainsMethodBody(targetMethod, isUnboxingStub))
                {
                    constrainedType = null;
                }
                else
                {
                    targetMethod = originalMethod;
                }
            }

            if (targetMethod is InstantiatedMethod instantiatedMethod)
            {
                return InstantiatedMethodNode(instantiatedMethod, constrainedType, signatureContext, isUnboxingStub);
            }

            MethodWithGCInfo localMethod = null;

            if (CompilationModuleGroup.ContainsMethodBody(targetMethod, false))
            {
                localMethod = new MethodWithGCInfo(targetMethod, signatureContext);
            }

            return ImportedMethodNode(targetMethod, unboxingStub: isUnboxingStub, constrainedType: constrainedType, signatureContext: signatureContext, localMethod: localMethod);
        }

        public IMethodNode StringAllocator(MethodDesc constructor, SignatureContext signatureContext)
        {
            return MethodEntrypoint(constructor, constrainedType: null, originalMethod: null, signatureContext: signatureContext, isUnboxingStub: false);
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

        public IMethodNode CreateUnboxingStubNode(MethodDesc method, mdToken token)
        {
            throw new NotImplementedException();
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

            if (Target.Architecture != TargetArchitecture.X86)
            {
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
            }

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

        public IMethodNode ShadowConcreteMethod(MethodDesc method, TypeDesc constrainedType, MethodDesc originalMethod,
            SignatureContext signatureContext, bool isUnboxingStub = false)
        {
            IMethodNode result;
            TypeAndMethod key = new TypeAndMethod(constrainedType, method, isUnboxingStub, isInstantiatingStub: false);
            if (!_shadowConcreteMethods.TryGetValue(key, out result))
            {
                result = MethodEntrypoint(method, constrainedType, originalMethod, signatureContext, isUnboxingStub);
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

            return MethodEntrypoint(method, constrainedType: null, originalMethod: null, signatureContext: InputModuleContext, isUnboxingStub: false);
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            throw new NotImplementedException();
        }   
    }
}

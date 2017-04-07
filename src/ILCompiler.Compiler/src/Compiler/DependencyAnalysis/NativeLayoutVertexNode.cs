﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.NativeFormat;
using Internal.Runtime;
using Internal.TypeSystem;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Wrapper nodes for native layout vertex structures. These wrapper nodes are "abstract" as they do not 
    /// generate any data. They are used to keep track of the dependency nodes required by a Vertex structure.
    /// 
    /// Any node in the graph that references data in the native layout blob needs to create one of these
    /// NativeLayoutVertexNode nodes, and track it as a dependency of itself.
    /// Example: MethodCodeNodes that are saved to the table in the ExactMethodInstantiationsNode reference 
    /// signatures stored in the native layout blob, so a NativeLayoutPlacedSignatureVertexNode node is created
    /// and returned as a static dependency of the associated MethodCodeNode (in the GetStaticDependencies API).
    /// 
    /// Each NativeLayoutVertexNode that gets marked in the graph will register itself with the NativeLayoutInfoNode,
    /// so that the NativeLayoutInfoNode can write it later to the native layout blob during the call to its GetData API.
    /// </summary>
    public abstract class NativeLayoutVertexNode : DependencyNodeCore<NodeFactory>
    {
        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;


        [Conditional("DEBUG")]
        public virtual void CheckIfMarkedEnoughToWrite()
        {
            Debug.Assert(Marked);
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
        {
            return Array.Empty<CombinedDependencyListEntry>();
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
        {
            return Array.Empty<CombinedDependencyListEntry>();
        }

        protected override void OnMarked(NodeFactory context)
        {
            context.MetadataManager.NativeLayoutInfo.AddVertexNodeToNativeLayout(this);
        }

        public abstract Vertex WriteVertex(NodeFactory factory);

        protected NativeWriter GetNativeWriter(NodeFactory factory)
        {
            // There is only one native layout info blob, so only one writer for now...
            return factory.MetadataManager.NativeLayoutInfo.Writer;
        }
    }

    /// <summary>
    /// Any NativeLayoutVertexNode that needs to expose the native layout Vertex after it has been saved
    /// needs to derive from this NativeLayoutSavedVertexNode class.
    /// 
    /// A nativelayout Vertex should typically only be exposed for Vertex offset fetching purposes, after the native
    /// writer is saved (Vertex offsets get generated when the native writer gets saved).
    /// 
    /// It is important for whoever derives from this class to produce unified Vertices. Calling the WriteVertex method
    /// multiple times should always produce the same exact unified Vertex each time (hence the assert in SetSavedVertex).
    /// All nativewriter.Getxyz methods return unified Vertices.
    /// 
    /// When exposing a saved Vertex that is a result of a section placement operation (Section.Place(...)), always make 
    /// sure a unified Vertex is being placed in the section (Section.Place creates a PlacedVertex structure that wraps the 
    /// Vertex to be placed, so if the Vertex to be placed is unified, there will only be a single unified PlacedVertex 
    /// structure created for that placed Vertex).
    /// </summary>
    public abstract class NativeLayoutSavedVertexNode : NativeLayoutVertexNode
    {
        public Vertex SavedVertex { get; private set; }
        protected Vertex SetSavedVertex(Vertex value)
        {
            Debug.Assert(SavedVertex == null || Object.ReferenceEquals(SavedVertex, value));
            SavedVertex = value;
            return value;
        }
    }

    internal abstract class NativeLayoutMethodEntryVertexNode : NativeLayoutSavedVertexNode
    {
        [Flags]
        public enum MethodEntryFlags
        {
            CreateInstantiatedSignature = 1,
            SaveEntryPoint = 2,
            /// <summary>
            /// IsUnboxingStub is not set for template methods (all template lookups performed at runtime are done with this flag not set,
            /// since it can't always be conveniently computed for a concrete method before looking up its template).
            /// </summary>
            DisableUnboxingStub = 4
        }

        protected readonly MethodDesc _method;
        private MethodEntryFlags _flags;
        private NativeLayoutTypeSignatureVertexNode _containingTypeSig;
        private NativeLayoutMethodSignatureVertexNode _methodSig;
        private NativeLayoutTypeSignatureVertexNode[] _instantiationArgsSig;

        public NativeLayoutMethodEntryVertexNode(NodeFactory factory, MethodDesc method, MethodEntryFlags flags)
        {
            _method = method;
            _flags = flags;
            _methodSig = factory.NativeLayout.MethodSignatureVertex(method.GetTypicalMethodDefinition().Signature);

            if ((_flags & MethodEntryFlags.CreateInstantiatedSignature) == 0)
            {
                _containingTypeSig = factory.NativeLayout.TypeSignatureVertex(method.OwningType);
                if (method.HasInstantiation && !method.IsMethodDefinition)
                {
                    _instantiationArgsSig = new NativeLayoutTypeSignatureVertexNode[method.Instantiation.Length];
                    for (int i = 0; i < _instantiationArgsSig.Length; i++)
                        _instantiationArgsSig[i] = factory.NativeLayout.TypeSignatureVertex(method.Instantiation[i]);
                }
            }
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            DependencyList dependencies = new DependencyList();

            dependencies.Add(new DependencyListEntry(_methodSig, "NativeLayoutMethodEntryVertexNode method signature"));
            if ((_flags & MethodEntryFlags.CreateInstantiatedSignature) != 0)
            {
                dependencies.Add(new DependencyListEntry(context.NecessaryTypeSymbol(_method.OwningType), "NativeLayoutMethodEntryVertexNode containing type"));
                foreach (var arg in _method.Instantiation)
                    dependencies.Add(new DependencyListEntry(context.NecessaryTypeSymbol(arg), "NativeLayoutMethodEntryVertexNode instantiation argument type"));
            }
            else
            {
                dependencies.Add(new DependencyListEntry(_containingTypeSig, "NativeLayoutMethodEntryVertexNode containing type signature"));
                if (_method.HasInstantiation && !_method.IsMethodDefinition)
                {
                    foreach (var arg in _instantiationArgsSig)
                        dependencies.Add(new DependencyListEntry(arg, "NativeLayoutMethodEntryVertexNode instantiation argument signature"));
                }
            }

            if ((_flags & MethodEntryFlags.SaveEntryPoint) != 0)
            {
                bool unboxingStub;
                IMethodNode methodEntryPointNode = GetMethodEntrypointNode(context, out unboxingStub);
                dependencies.Add(new DependencyListEntry(methodEntryPointNode, "NativeLayoutMethodEntryVertexNode entrypoint"));
            }

            return dependencies;
        }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

            Vertex containingType = GetContainingTypeVertex(factory);
            Vertex methodSig = _methodSig.WriteVertex(factory);
            Vertex methodNameAndSig = GetNativeWriter(factory).GetMethodNameAndSigSignature(_method.Name, methodSig);

            Vertex[] args = null;
            MethodFlags flags = 0;
            if (_method.HasInstantiation && !_method.IsMethodDefinition)
            {
                Debug.Assert(_instantiationArgsSig == null || (_instantiationArgsSig != null && _method.Instantiation.Length == _instantiationArgsSig.Length));

                flags |= MethodFlags.HasInstantiation;
                args = new Vertex[_method.Instantiation.Length];

                for (int i = 0; i < args.Length; i++)
                {
                    if ((_flags & MethodEntryFlags.CreateInstantiatedSignature) != 0)
                    {
                        IEETypeNode eetypeNode = factory.NecessaryTypeSymbol(_method.Instantiation[i]);
                        uint typeIndex = factory.MetadataManager.NativeLayoutInfo.ExternalReferences.GetIndex(eetypeNode);
                        args[i] = GetNativeWriter(factory).GetExternalTypeSignature(typeIndex);
                    }
                    else
                    {
                        args[i] = _instantiationArgsSig[i].WriteVertex(factory);
                    }
                }
            }

            uint fptrReferenceId = 0;
            if ((_flags & MethodEntryFlags.SaveEntryPoint) != 0)
            {
                flags |= MethodFlags.HasFunctionPointer;

                bool unboxingStub;
                IMethodNode methodEntryPointNode = GetMethodEntrypointNode(factory, out unboxingStub);
                fptrReferenceId = factory.MetadataManager.NativeLayoutInfo.ExternalReferences.GetIndex(methodEntryPointNode);

                if (unboxingStub)
                    flags |= MethodFlags.IsUnboxingStub;
                if (_method.IsCanonicalMethod(CanonicalFormKind.Universal))
                    flags |= MethodFlags.FunctionPointerIsUSG;
            }

            return GetNativeWriter(factory).GetMethodSignature((uint)flags, fptrReferenceId, containingType, methodNameAndSig, args);
        }

        private Vertex GetContainingTypeVertex(NodeFactory factory)
        {
            if ((_flags & MethodEntryFlags.CreateInstantiatedSignature) != 0)
            {
                IEETypeNode eetypeNode = factory.NecessaryTypeSymbol(_method.OwningType);
                uint typeIndex = factory.MetadataManager.NativeLayoutInfo.ExternalReferences.GetIndex(eetypeNode);
                return GetNativeWriter(factory).GetExternalTypeSignature(typeIndex);
            }
            else
            {
                return _containingTypeSig.WriteVertex(factory);
            }
        }

        protected virtual IMethodNode GetMethodEntrypointNode(NodeFactory factory, out bool unboxingStub)
        {
            unboxingStub = (_flags & MethodEntryFlags.DisableUnboxingStub) != 0 ? false : _method.OwningType.IsValueType && !_method.Signature.IsStatic;
            IMethodNode methodEntryPointNode = factory.MethodEntrypoint(_method, unboxingStub);

            return methodEntryPointNode;
        }
    }

    internal sealed class NativeLayoutMethodLdTokenVertexNode : NativeLayoutMethodEntryVertexNode
    {
        protected override string GetName(NodeFactory factory) => "NativeLayoutMethodLdTokenVertexNode_" + factory.NameMangler.GetMangledMethodName(_method);

        public NativeLayoutMethodLdTokenVertexNode(NodeFactory factory, MethodDesc method)
            : base(factory, method, 0)
        {
        }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

            Vertex methodEntryVertex = base.WriteVertex(factory);
            return SetSavedVertex(factory.MetadataManager.NativeLayoutInfo.LdTokenInfoSection.Place(methodEntryVertex));
        }
    }

    internal sealed class NativeLayoutFieldLdTokenVertexNode : NativeLayoutSavedVertexNode
    {
        private readonly FieldDesc _field;
        private readonly NativeLayoutTypeSignatureVertexNode _containingTypeSig;

        public NativeLayoutFieldLdTokenVertexNode(NodeFactory factory, FieldDesc field)
        {
            _field = field;
            _containingTypeSig = factory.NativeLayout.TypeSignatureVertex(field.OwningType);
        }

        protected override string GetName(NodeFactory factory) => "NativeLayoutFieldLdTokenVertexNode_" + factory.NameMangler.GetMangledFieldName(_field);

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return new DependencyListEntry[]
            {
                new DependencyListEntry(_containingTypeSig, "NativeLayoutFieldLdTokenVertexNode containing type signature"),
            };
        }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

            Vertex containingType = _containingTypeSig.WriteVertex(factory);

            Vertex unplacedVertex = GetNativeWriter(factory).GetFieldSignature(containingType, _field.Name);

            return SetSavedVertex(factory.MetadataManager.NativeLayoutInfo.LdTokenInfoSection.Place(unplacedVertex));
        }
    }

    internal sealed class NativeLayoutMethodSignatureVertexNode : NativeLayoutVertexNode
    {
        private Internal.TypeSystem.MethodSignature _signature;
        private NativeLayoutTypeSignatureVertexNode _returnTypeSig;
        private NativeLayoutTypeSignatureVertexNode[] _parametersSig;

        protected override string GetName(NodeFactory factory) => "NativeLayoutMethodSignatureVertexNode " + _signature.GetMangledName(factory.NameMangler);

        public NativeLayoutMethodSignatureVertexNode(NodeFactory factory, Internal.TypeSystem.MethodSignature signature)
        {
            _signature = signature;
            _returnTypeSig = factory.NativeLayout.TypeSignatureVertex(signature.ReturnType);
            _parametersSig = new NativeLayoutTypeSignatureVertexNode[signature.Length];
            for (int i = 0; i < _parametersSig.Length; i++)
                _parametersSig[i] = factory.NativeLayout.TypeSignatureVertex(signature[i]);
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            DependencyList dependencies = new DependencyList();

            dependencies.Add(new DependencyListEntry(_returnTypeSig, "NativeLayoutMethodSignatureVertexNode return type signature"));
            foreach (var arg in _parametersSig)
                dependencies.Add(new DependencyListEntry(arg, "NativeLayoutMethodSignatureVertexNode parameter signature"));

            return dependencies;
        }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

            MethodCallingConvention methodCallingConvention = default(MethodCallingConvention);

            if (_signature.GenericParameterCount > 0)
                methodCallingConvention |= MethodCallingConvention.Generic;
            if (_signature.IsStatic)
                methodCallingConvention |= MethodCallingConvention.Static;

            Debug.Assert(_signature.Length == _parametersSig.Length);

            Vertex returnType = _returnTypeSig.WriteVertex(factory);
            Vertex[] parameters = new Vertex[_parametersSig.Length];
            for (int i = 0; i < _parametersSig.Length; i++)
                parameters[i] = _parametersSig[i].WriteVertex(factory);

            Vertex signature = GetNativeWriter(factory).GetMethodSigSignature((uint)methodCallingConvention, (uint)_signature.GenericParameterCount, returnType, parameters);
            return factory.MetadataManager.NativeLayoutInfo.SignaturesSection.Place(signature);
        }
    }

    internal sealed class NativeLayoutMethodNameAndSignatureVertexNode : NativeLayoutVertexNode
    {
        private MethodDesc _method;
        private NativeLayoutMethodSignatureVertexNode _methodSig;

        protected override string GetName(NodeFactory factory) => "NativeLayoutMethodNameAndSignatureVertexNode" + factory.NameMangler.GetMangledMethodName(_method);

        public NativeLayoutMethodNameAndSignatureVertexNode(NodeFactory factory, MethodDesc method)
        {
            _method = method;
            _methodSig = factory.NativeLayout.MethodSignatureVertex(method.Signature);
        }
        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return new DependencyListEntry[] { new DependencyListEntry(_methodSig, "NativeLayoutMethodNameAndSignatureVertexNode signature vertex") };
        }
        public override Vertex WriteVertex(NodeFactory factory)
        {
            Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

            Vertex methodSig = _methodSig.WriteVertex(factory);
            return GetNativeWriter(factory).GetMethodNameAndSigSignature(_method.Name, methodSig);
        }
    }

    internal abstract class NativeLayoutTypeSignatureVertexNode : NativeLayoutVertexNode
    {
        protected readonly TypeDesc _type;

        protected NativeLayoutTypeSignatureVertexNode(TypeDesc type)
        {
            _type = type;
        }

        protected override string GetName(NodeFactory factory) => "NativeLayoutTypeSignatureVertexNode: " + _type.ToString();

        public static NativeLayoutTypeSignatureVertexNode NewTypeSignatureVertexNode(NodeFactory factory, TypeDesc type)
        {
            switch (type.Category)
            {
                case Internal.TypeSystem.TypeFlags.Array:
                case Internal.TypeSystem.TypeFlags.SzArray:
                case Internal.TypeSystem.TypeFlags.Pointer:
                case Internal.TypeSystem.TypeFlags.ByRef:
                    return new NativeLayoutParameterizedTypeSignatureVertexNode(factory, type);

                case Internal.TypeSystem.TypeFlags.SignatureTypeVariable:
                case Internal.TypeSystem.TypeFlags.SignatureMethodVariable:
                    return new NativeLayoutGenericVarSignatureVertexNode(factory, type);

                // TODO Internal.TypeSystem.TypeFlags.FunctionPointer (Runtime parsing also not yet implemented)
                case Internal.TypeSystem.TypeFlags.FunctionPointer:
                    throw new NotImplementedException("FunctionPointer signature");

                default:
                    {
                        Debug.Assert(type.IsDefType);

                        if (type.HasInstantiation && !type.IsGenericDefinition)
                            return new NativeLayoutInstantiatedTypeSignatureVertexNode(factory, type);
                        else
                            return new NativeLayoutEETypeSignatureVertexNode(factory, type);
                    }
            }
        }

        sealed class NativeLayoutParameterizedTypeSignatureVertexNode : NativeLayoutTypeSignatureVertexNode
        {
            private NativeLayoutVertexNode _parameterTypeSig;

            public NativeLayoutParameterizedTypeSignatureVertexNode(NodeFactory factory, TypeDesc type) : base(type)
            {
                _parameterTypeSig = factory.NativeLayout.TypeSignatureVertex(((ParameterizedType)type).ParameterType);
            }
            public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
            {
                return new DependencyListEntry[] { new DependencyListEntry(_parameterTypeSig, "NativeLayoutParameterizedTypeSignatureVertexNode parameter type signature") };
            }
            public override Vertex WriteVertex(NodeFactory factory)
            {
                Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

                switch (_type.Category)
                {
                    case Internal.TypeSystem.TypeFlags.SzArray:
                        return GetNativeWriter(factory).GetModifierTypeSignature(TypeModifierKind.Array, _parameterTypeSig.WriteVertex(factory));

                    case Internal.TypeSystem.TypeFlags.Pointer:
                        return GetNativeWriter(factory).GetModifierTypeSignature(TypeModifierKind.Pointer, _parameterTypeSig.WriteVertex(factory));

                    case Internal.TypeSystem.TypeFlags.ByRef:
                        return GetNativeWriter(factory).GetModifierTypeSignature(TypeModifierKind.ByRef, _parameterTypeSig.WriteVertex(factory));

                    case Internal.TypeSystem.TypeFlags.Array:
                        {
                            Vertex elementType = _parameterTypeSig.WriteVertex(factory);

                            // Skip bounds and lobounds (TODO)
                            var bounds = Array.Empty<uint>();
                            var lobounds = Array.Empty<uint>();

                            return GetNativeWriter(factory).GetMDArrayTypeSignature(elementType, (uint)((ArrayType)_type).Rank, bounds, lobounds);
                        }
                }

                Debug.Assert(false, "UNREACHABLE");
                return null;
            }
        }

        sealed class NativeLayoutGenericVarSignatureVertexNode : NativeLayoutTypeSignatureVertexNode
        {
            public NativeLayoutGenericVarSignatureVertexNode(NodeFactory factory, TypeDesc type) : base(type)
            {
            }
            public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
            {
                return Array.Empty<DependencyListEntry>();
            }
            public override Vertex WriteVertex(NodeFactory factory)
            {
                Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

                switch (_type.Category)
                {
                    case Internal.TypeSystem.TypeFlags.SignatureTypeVariable:
                        return GetNativeWriter(factory).GetVariableTypeSignature((uint)((SignatureVariable)_type).Index, false);

                    case Internal.TypeSystem.TypeFlags.SignatureMethodVariable:
                        return GetNativeWriter(factory).GetVariableTypeSignature((uint)((SignatureMethodVariable)_type).Index, true);
                }

                Debug.Assert(false, "UNREACHABLE");
                return null;
            }
        }

        sealed class NativeLayoutInstantiatedTypeSignatureVertexNode : NativeLayoutTypeSignatureVertexNode
        {
            private NativeLayoutTypeSignatureVertexNode _genericTypeDefSig;
            private NativeLayoutTypeSignatureVertexNode[] _instantiationArgs;

            public NativeLayoutInstantiatedTypeSignatureVertexNode(NodeFactory factory, TypeDesc type) : base(type)
            {
                Debug.Assert(type.HasInstantiation && !type.IsGenericDefinition);

                _genericTypeDefSig = factory.NativeLayout.TypeSignatureVertex(type.GetTypeDefinition());
                _instantiationArgs = new NativeLayoutTypeSignatureVertexNode[type.Instantiation.Length];
                for (int i = 0; i < _instantiationArgs.Length; i++)
                    _instantiationArgs[i] = factory.NativeLayout.TypeSignatureVertex(type.Instantiation[i]);

            }
            public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
            {
                DependencyList dependencies = new DependencyList();

                dependencies.Add(new DependencyListEntry(_genericTypeDefSig, "NativeLayoutInstantiatedTypeSignatureVertexNode generic definition signature"));
                foreach (var arg in _instantiationArgs)
                    dependencies.Add(new DependencyListEntry(arg, "NativeLayoutInstantiatedTypeSignatureVertexNode instantiation argument signature"));

                return dependencies;
            }
            public override Vertex WriteVertex(NodeFactory factory)
            {
                Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

                Vertex genericDefVertex = _genericTypeDefSig.WriteVertex(factory);
                Vertex[] args = new Vertex[_instantiationArgs.Length];
                for (int i = 0; i < args.Length; i++)
                    args[i] = _instantiationArgs[i].WriteVertex(factory);

                return GetNativeWriter(factory).GetInstantiationTypeSignature(genericDefVertex, args);
            }
        }

        sealed class NativeLayoutEETypeSignatureVertexNode : NativeLayoutTypeSignatureVertexNode
        {
            public NativeLayoutEETypeSignatureVertexNode(NodeFactory factory, TypeDesc type) : base(type)
            {
                Debug.Assert(!type.IsRuntimeDeterminedSubtype);
                Debug.Assert(!type.HasInstantiation || type.IsGenericDefinition);
            }
            public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
            {
                return new DependencyListEntry[]
                {
                    new DependencyListEntry(context.NecessaryTypeSymbol(_type), "NativeLayoutEETypeVertexNode containing type signature")
                };
            }
            public override Vertex WriteVertex(NodeFactory factory)
            {
                Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

                IEETypeNode eetypeNode = factory.NecessaryTypeSymbol(_type);
                uint typeIndex = factory.MetadataManager.NativeLayoutInfo.ExternalReferences.GetIndex(eetypeNode);
                return GetNativeWriter(factory).GetExternalTypeSignature(typeIndex);
            }
        }
    }

    public sealed class NativeLayoutExternalReferenceVertexNode : NativeLayoutVertexNode
    {
        private ISymbolNode _symbol;

        public NativeLayoutExternalReferenceVertexNode(NodeFactory factory, ISymbolNode symbol)
        {
            _symbol = symbol;
        }

        protected override string GetName(NodeFactory factory) => "NativeLayoutISymbolNodeReferenceVertexNode " + _symbol.GetMangledName(factory.NameMangler);

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return new DependencyListEntry[]
            {
                new DependencyListEntry(_symbol, "NativeLayoutISymbolNodeReferenceVertexNode containing symbol")
            };
        }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

            uint symbolIndex = factory.MetadataManager.NativeLayoutInfo.ExternalReferences.GetIndex(_symbol);
            return GetNativeWriter(factory).GetUnsignedConstant(symbolIndex);
        }
    }

    internal sealed class NativeLayoutPlacedSignatureVertexNode : NativeLayoutSavedVertexNode
    {
        private NativeLayoutVertexNode _signatureToBePlaced;

        protected override string GetName(NodeFactory factory) => "NativeLayoutPlacedSignatureVertexNode";

        public NativeLayoutPlacedSignatureVertexNode(NativeLayoutVertexNode signatureToBePlaced)
        {
            _signatureToBePlaced = signatureToBePlaced;
        }
        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return new DependencyListEntry[] { new DependencyListEntry(_signatureToBePlaced, "NativeLayoutPlacedSignatureVertexNode placed signature") };
        }
        public override Vertex WriteVertex(NodeFactory factory)
        {
            // This vertex doesn't need to assert as marked, as it simply represents the concept of an existing vertex which has been placed.

            // Always use the NativeLayoutInfo blob for names and sigs, even if the associated types/methods are written elsewhere.
            // This saves space, since we can Unify more signatures, allows optimizations in comparing sigs in the same module, and 
            // prevents the dynamic type loader having to know about other native layout sections (since sigs contain types). If we are 
            // using a non-native layout info writer, write the sig to the native layout info, and refer to it by offset in its own 
            // section.  At runtime, we will assume all names and sigs are in the native layout and find it.

            Vertex signature = _signatureToBePlaced.WriteVertex(factory);
            return SetSavedVertex(factory.MetadataManager.NativeLayoutInfo.SignaturesSection.Place(signature));
        }
    }

    internal sealed class NativeLayoutPlacedVertexSequenceOfUIntVertexNode : NativeLayoutSavedVertexNode
    {
        private List<uint> _uints;

        protected override string GetName(NodeFactory factory) => "NativeLayoutPlacedVertexSequenceVertexNode";
        public NativeLayoutPlacedVertexSequenceOfUIntVertexNode(List<uint> uints)
        {
            _uints = uints;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            // There are no interesting dependencies
            return null;
        }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            // Eagerly return the SavedVertex so that we can unify the VertexSequence
            if (SavedVertex != null)
                return SavedVertex;

            // This vertex doesn't need to assert as marked, as it simply represents the concept of an existing vertex which has been placed.

            NativeWriter writer = GetNativeWriter(factory);

            VertexSequence sequence = new VertexSequence();
            foreach (uint value in _uints)
            {
                sequence.Append(writer.GetUnsignedConstant(value));
            }

            return SetSavedVertex(factory.MetadataManager.NativeLayoutInfo.SignaturesSection.Place(sequence));
        }
    }

    internal sealed class NativeLayoutPlacedVertexSequenceVertexNode : NativeLayoutSavedVertexNode
    {
        private List<NativeLayoutVertexNode> _vertices;

        protected override string GetName(NodeFactory factory) => "NativeLayoutPlacedVertexSequenceVertexNode";
        public NativeLayoutPlacedVertexSequenceVertexNode(List<NativeLayoutVertexNode> vertices)
        {
            _vertices = vertices;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            DependencyListEntry[] dependencies = new DependencyListEntry[_vertices.Count];
            for (int i = 0; i < _vertices.Count; i++)
            {
                dependencies[i] = new DependencyListEntry(_vertices[i], "NativeLayoutPlacedVertexSequenceVertexNode element");
            }

            return dependencies;
        }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            // Eagerly return the SavedVertex so that we can unify the VertexSequence
            if (SavedVertex != null)
                return SavedVertex;

            // This vertex doesn't need to assert as marked, as it simply represents the concept of an existing vertex which has been placed.

            VertexSequence sequence = new VertexSequence();
            foreach (NativeLayoutVertexNode vertex in _vertices)
            {
                sequence.Append(vertex.WriteVertex(factory));
            }

            return SetSavedVertex(factory.MetadataManager.NativeLayoutInfo.SignaturesSection.Place(sequence));
        }
    }

    internal sealed class NativeLayoutTemplateMethodSignatureVertexNode : NativeLayoutMethodEntryVertexNode
    {
        protected override string GetName(NodeFactory factory) => "NativeLayoutTemplateMethodSignatureVertexNode_" + factory.NameMangler.GetMangledMethodName(_method);

        public NativeLayoutTemplateMethodSignatureVertexNode(NodeFactory factory, MethodDesc method)
            : base(factory, method, MethodEntryFlags.CreateInstantiatedSignature | MethodEntryFlags.SaveEntryPoint | MethodEntryFlags.DisableUnboxingStub)
        {
        }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

            Vertex methodEntryVertex = base.WriteVertex(factory);
            return SetSavedVertex(factory.MetadataManager.NativeLayoutInfo.TemplatesSection.Place(methodEntryVertex));
        }
    }


    public sealed class NativeLayoutDictionarySignatureNode : NativeLayoutSavedVertexNode
    {
        private TypeSystemEntity _owningMethodOrType;
        public NativeLayoutDictionarySignatureNode(TypeSystemEntity owningMethodOrType)
        {
            if (owningMethodOrType is MethodDesc)
            {
                MethodDesc owningMethod = (MethodDesc)owningMethodOrType;
                Debug.Assert(owningMethod.IsCanonicalMethod(CanonicalFormKind.Universal));
                Debug.Assert(owningMethod.HasInstantiation);
            }
            else
            {
                TypeDesc owningType = (TypeDesc)owningMethodOrType;
                Debug.Assert(owningType.IsCanonicalSubtype(CanonicalFormKind.Universal));
            }

            _owningMethodOrType = owningMethodOrType;
        }

        private GenericContextKind ContextKind
        {
            get
            {
                if (_owningMethodOrType is MethodDesc)
                {
                    MethodDesc owningMethod = (MethodDesc)_owningMethodOrType;
                    Debug.Assert(owningMethod.HasInstantiation);
                    return GenericContextKind.FromMethodHiddenArg | GenericContextKind.NeedsUSGContext;
                }
                else
                {
                    TypeDesc owningType = (TypeDesc)_owningMethodOrType;
                    if (owningType.IsSzArray || owningType.IsValueType || owningType.IsSealed())
                    {
                        return GenericContextKind.FromHiddenArg | GenericContextKind.NeedsUSGContext;
                    }
                    else
                    {
                        return GenericContextKind.FromHiddenArg | GenericContextKind.NeedsUSGContext | GenericContextKind.HasDeclaringType;
                    }
                }
            }
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            if ((ContextKind & GenericContextKind.HasDeclaringType) != 0)
            {
                return new DependencyListEntry[] { new DependencyListEntry(context.NativeLayout.TypeSignatureVertex((TypeDesc)_owningMethodOrType), "DeclaringType signature") };
            }
            else
            {
                return Array.Empty<DependencyListEntry>();
            }
        }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

            VertexSequence sequence = new VertexSequence();

            DictionaryLayoutNode associatedLayout = factory.GenericDictionaryLayout(_owningMethodOrType);
            ICollection<NativeLayoutVertexNode> templateLayout = associatedLayout.GetTemplateEntries(factory);

            foreach (NativeLayoutVertexNode dictionaryEntry in templateLayout)
            {
                dictionaryEntry.CheckIfMarkedEnoughToWrite();
                sequence.Append(dictionaryEntry.WriteVertex(factory));
            }

            Vertex signature;

            GenericContextKind contextKind = ContextKind;
            NativeWriter nativeWriter = GetNativeWriter(factory);

            if ((ContextKind & GenericContextKind.HasDeclaringType) != 0)
            {
                signature = nativeWriter.GetTuple(factory.NativeLayout.TypeSignatureVertex((TypeDesc)_owningMethodOrType).WriteVertex(factory), sequence);
            }
            else
            {
                signature = sequence;
            }

            Vertex signatureWithContextKind = nativeWriter.GetTuple(nativeWriter.GetUnsignedConstant((uint)contextKind), signature);
            return SetSavedVertex(factory.MetadataManager.NativeLayoutInfo.SignaturesSection.Place(signatureWithContextKind));
        }

        protected override string GetName(NodeFactory factory) => $"Dictionary layout signature for {_owningMethodOrType.ToString()}";
    }

    public sealed class NativeLayoutTemplateMethodLayoutVertexNode : NativeLayoutSavedVertexNode
    {
        private MethodDesc _method;

        protected override string GetName(NodeFactory factory) => "NativeLayoutTemplateMethodLayoutVertexNode" + factory.NameMangler.GetMangledMethodName(_method);

        public NativeLayoutTemplateMethodLayoutVertexNode(NodeFactory factory, MethodDesc method)
        {
            _method = method;
            Debug.Assert(method.HasInstantiation);
            Debug.Assert(method.IsCanonicalMethod(CanonicalFormKind.Any));
            Debug.Assert(method.GetCanonMethodTarget(CanonicalFormKind.Specific) == method, "Assert that the canonical method passed in is in standard canonical form");
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return Array.Empty<DependencyListEntry>();
        }

        private int CompareDictionaryEntries(KeyValuePair<int, NativeLayoutVertexNode> left, KeyValuePair<int, NativeLayoutVertexNode> right)
        {
            return left.Key - right.Key;
        }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

            VertexBag layoutInfo = new VertexBag();

            DictionaryLayoutNode associatedLayout = factory.GenericDictionaryLayout(_method);
            ICollection<NativeLayoutVertexNode> templateLayout = associatedLayout.GetTemplateEntries(factory);
            
            if (!_method.IsCanonicalMethod(CanonicalFormKind.Universal) && (templateLayout.Count > 0))
            {
                List<NativeLayoutVertexNode> dictionaryVertices = new List<NativeLayoutVertexNode>();

                foreach (NativeLayoutVertexNode dictionaryEntry in templateLayout)
                {
                    dictionaryEntry.CheckIfMarkedEnoughToWrite();
                    dictionaryVertices.Add(dictionaryEntry);
                }
                NativeLayoutVertexNode dictionaryLayout = factory.NativeLayout.PlacedVertexSequence(dictionaryVertices);

                layoutInfo.Append(BagElementKind.DictionaryLayout, dictionaryLayout.WriteVertex(factory));
            }

            factory.MetadataManager.NativeLayoutInfo.TemplatesSection.Place(layoutInfo);

            return SetSavedVertex(layoutInfo);
        }
    }

    public sealed class NativeLayoutTemplateTypeLayoutVertexNode : NativeLayoutSavedVertexNode
    {
        private DefType _type;
        private bool _isUniversalCanon;

        protected override string GetName(NodeFactory factory) => "NativeLayoutTemplateTypeLayoutVertexNode_" + factory.NameMangler.GetMangledTypeName(_type);

        public NativeLayoutTemplateTypeLayoutVertexNode(NodeFactory factory, DefType type)
        {
            Debug.Assert(type.IsCanonicalSubtype(CanonicalFormKind.Any));
            Debug.Assert(type.ConvertToCanonForm(CanonicalFormKind.Specific) == type, "Assert that the canonical type passed in is in standard canonical form");
            _isUniversalCanon = type.IsCanonicalSubtype(CanonicalFormKind.Universal);
            _type = type.ConvertToSharedRuntimeDeterminedForm();
        }

        private ISymbolNode GetStaticsNode(NodeFactory context, out BagElementKind staticsBagKind)
        {
            ISymbolNode symbol;

            if (context is UtcNodeFactory)
            {
                symbol = ((UtcNodeFactory)context).TypeGCStaticDescSymbol((MetadataType)_type.ConvertToCanonForm(CanonicalFormKind.Specific));
                staticsBagKind = BagElementKind.GcStaticDesc;
            }
            else
            {
                symbol = context.GCStaticEEType(GCPointerMap.FromStaticLayout(_type));
                staticsBagKind = BagElementKind.GcStaticEEType;
            }

            return symbol;
        }

        private ISymbolNode GetThreadStaticsNode(NodeFactory context, out BagElementKind staticsBagKind)
        {
            ISymbolNode symbol;

            if (context is UtcNodeFactory)
            {
                symbol = ((UtcNodeFactory)context).TypeThreadStaticGCDescNode((MetadataType)_type.ConvertToCanonForm(CanonicalFormKind.Specific));
                staticsBagKind = BagElementKind.ThreadStaticDesc;
            }
            else
            {
                symbol = context.GCStaticEEType(GCPointerMap.FromThreadStaticLayout(_type));
                staticsBagKind = BagElementKind.End; // GC static EETypes not yet implemented in type loader
            }

            return symbol;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            foreach (TypeDesc iface in _type.RuntimeInterfaces)
            {
                yield return new DependencyListEntry(context.NativeLayout.TypeSignatureVertex(iface), "template interface list");
            }

            if (context.TypeSystemContext.HasLazyStaticConstructor(_type))
            {
                yield return new DependencyListEntry(context.MethodEntrypoint(_type.GetStaticConstructor().GetCanonMethodTarget(CanonicalFormKind.Specific)), "cctor for template");
            }

            if (!_isUniversalCanon)
            {
                if (_type.GCStaticFieldSize.AsInt > 0)
                {
                    BagElementKind ignored;
                    yield return new DependencyListEntry(GetStaticsNode(context, out ignored), "type gc static info");
                }

                if (_type.ThreadStaticFieldSize.AsInt > 0)
                {
                    BagElementKind ignored;
                    yield return new DependencyListEntry(GetThreadStaticsNode(context, out ignored), "type thread static info");
                }
            }

            if (_type.BaseType != null && _type.BaseType.IsRuntimeDeterminedSubtype)
            {
                yield return new DependencyListEntry(context.NativeLayout.PlacedSignatureVertex(context.NativeLayout.TypeSignatureVertex(_type.BaseType)), "template base type");
            }
            else if (_type.IsDelegate && _isUniversalCanon)
            {
                // For USG delegate, we need to write the signature of the Invoke method to the native layout.
                // This signature is used by the calling convention converter to marshal parameters during delegate calls.
                yield return new DependencyListEntry(context.NativeLayout.MethodSignatureVertex(_type.GetMethod("Invoke", null).GetTypicalMethodDefinition().Signature), "invoke method signature");
            }

            if (_isUniversalCanon)
            {
                // For universal canonical template types, we need to write out field layout information so that we 
                // can correctly compute the type sizes for dynamically created types at runtime, and construct
                // their GCDesc info
                foreach (FieldDesc field in _type.GetFields())
                {
                    // If this field does not contribute to layout, skip
                    if (field.HasRva || field.IsLiteral)
                    {
                        continue;
                    }

                    DependencyListEntry typeForFieldLayout;

                    if (field.FieldType.IsGCPointer)
                    {
                        typeForFieldLayout = new DependencyListEntry(context.NativeLayout.PlacedSignatureVertex(context.NativeLayout.TypeSignatureVertex(field.Context.GetWellKnownType(WellKnownType.Object))), "universal field layout type object sized");
                    }
                    else if (field.FieldType.IsPointer || field.FieldType.IsFunctionPointer)
                    {
                        typeForFieldLayout = new DependencyListEntry(context.NativeLayout.PlacedSignatureVertex(context.NativeLayout.TypeSignatureVertex(field.Context.GetWellKnownType(WellKnownType.IntPtr))), "universal field layout type IntPtr sized");
                    }
                    else
                    {
                        typeForFieldLayout = new DependencyListEntry(context.NativeLayout.PlacedSignatureVertex(context.NativeLayout.TypeSignatureVertex(field.FieldType)), "universal field layout type");
                    }

                    yield return typeForFieldLayout;
                }

                // We also need to write out the signatures of interesting methods in the type's vtable, which
                // will be needed by the calling convention translation logic at runtime, when the type's methods
                // get invoked. This logic gathers nodes for entries *unconditionally* present. (entries may be conditionally
                // present if a type has a vtable which has a size computed by usage not by IL contents)
                List<NativeLayoutVertexNode> vtableSignatureNodeEntries = null;
                int currentVTableIndexUnused = 0;
                ProcessVTableEntriesForCallingConventionSignatureGeneration(context, VTableEntriesToProcess.AllOnTypesThatShouldProduceFullVTables, ref currentVTableIndexUnused,
                    (int vtableIndex, MethodDesc declMethod, MethodDesc implMethod) =>
                    {
                        if (implMethod.IsAbstract)
                            return;

                        if (UniversalGenericParameterLayout.MethodSignatureHasVarsNeedingCallingConventionConverter(implMethod.Signature))
                        {
                            if (vtableSignatureNodeEntries == null)
                                vtableSignatureNodeEntries = new List<NativeLayoutVertexNode>();

                            vtableSignatureNodeEntries.Add(context.NativeLayout.MethodSignatureVertex(implMethod.GetTypicalMethodDefinition().Signature));
                        }
                    }
                    , _type, _type);

                if (vtableSignatureNodeEntries != null)
                {
                    foreach (NativeLayoutVertexNode node in vtableSignatureNodeEntries)
                        yield return new DependencyListEntry(node, "vtable cctor sig");
                }
            }
        }

        public override bool HasConditionalStaticDependencies => _isUniversalCanon;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
        {
            List<CombinedDependencyListEntry> conditionalDependencies = null;

            if (_isUniversalCanon)
            {
                // We also need to write out the signatures of interesting methods in the type's vtable, which
                // will be needed by the calling convention translation logic at runtime, when the type's methods
                // get invoked. This logic gathers nodes for entries *conditionally* present. (entries may be conditionally
                // present if a type has a vtable which has a size computed by usage not by IL contents)

                int currentVTableIndexUnused = 0;
                ProcessVTableEntriesForCallingConventionSignatureGeneration(context, VTableEntriesToProcess.AllOnTypesThatProducePartialVTables, ref currentVTableIndexUnused,
                    (int vtableIndex, MethodDesc declMethod, MethodDesc implMethod) =>
                    {
                        if (implMethod.IsAbstract)
                            return;

                        if (UniversalGenericParameterLayout.MethodSignatureHasVarsNeedingCallingConventionConverter(implMethod.Signature))
                        {
                            if (conditionalDependencies == null)
                                conditionalDependencies = new List<CombinedDependencyListEntry>();

                            conditionalDependencies.Add(
                                new CombinedDependencyListEntry(context.NativeLayout.MethodSignatureVertex(implMethod.GetTypicalMethodDefinition().Signature),
                                                                context.VirtualMethodUse(declMethod),
                                                                "conditional vtable cctor sig"));
                        }
                    }
                    , _type, _type);
            }

            if (conditionalDependencies != null)
                return conditionalDependencies;
            else
                return Array.Empty<CombinedDependencyListEntry>();
        }

        private int CompareDictionaryEntries(KeyValuePair<int, NativeLayoutVertexNode> left, KeyValuePair<int, NativeLayoutVertexNode> right)
        {
            return left.Key - right.Key;
        }

        private bool HasInstantiationDeterminedSize()
        {
            Debug.Assert(_isUniversalCanon);
            return _type.InstanceFieldSize.IsIndeterminate;
        }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            Debug.Assert(Marked, "WriteVertex should only happen for marked vertices");

            VertexBag layoutInfo = new VertexBag();

            DictionaryLayoutNode associatedLayout = factory.GenericDictionaryLayout(_type.ConvertToCanonForm(CanonicalFormKind.Specific));
            ICollection<NativeLayoutVertexNode> templateLayout = associatedLayout.GetTemplateEntries(factory);
            
            NativeWriter writer = GetNativeWriter(factory);

            // Interfaces
            if (_type.RuntimeInterfaces.Length > 0)
            {
                List<NativeLayoutVertexNode> implementedInterfacesList = new List<NativeLayoutVertexNode>();

                foreach (TypeDesc iface in _type.RuntimeInterfaces)
                {
                    implementedInterfacesList.Add(factory.NativeLayout.TypeSignatureVertex(iface));
                }
                NativeLayoutVertexNode implementedInterfaces = factory.NativeLayout.PlacedVertexSequence(implementedInterfacesList);

                layoutInfo.Append(BagElementKind.ImplementedInterfaces, implementedInterfaces.WriteVertex(factory));
            }

            if (!_isUniversalCanon && (templateLayout.Count > 0))
            {
                List<NativeLayoutVertexNode> dictionaryVertices = new List<NativeLayoutVertexNode>();

                foreach (NativeLayoutVertexNode dictionaryEntry in templateLayout)
                {
                    dictionaryEntry.CheckIfMarkedEnoughToWrite();
                    dictionaryVertices.Add(dictionaryEntry);
                }
                NativeLayoutVertexNode dictionaryLayout = factory.NativeLayout.PlacedVertexSequence(dictionaryVertices);

                layoutInfo.Append(BagElementKind.DictionaryLayout, dictionaryLayout.WriteVertex(factory));
            }

            Internal.NativeFormat.TypeFlags typeFlags = default(Internal.NativeFormat.TypeFlags);

            if (factory.TypeSystemContext.HasLazyStaticConstructor(_type))
            {
                MethodDesc cctorMethod = _type.GetStaticConstructor();
                MethodDesc canonCctorMethod = cctorMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);
                ISymbolNode cctorSymbol = factory.MethodEntrypoint(canonCctorMethod);
                uint cctorStaticsIndex = factory.MetadataManager.NativeLayoutInfo.StaticsReferences.GetIndex(cctorSymbol);
                layoutInfo.AppendUnsigned(BagElementKind.ClassConstructorPointer, cctorStaticsIndex);

                typeFlags = typeFlags | Internal.NativeFormat.TypeFlags.HasClassConstructor;
            }

            if (!_isUniversalCanon)
            {
                if (_type.NonGCStaticFieldSize.AsInt != 0)
                {
                    layoutInfo.AppendUnsigned(BagElementKind.NonGcStaticDataSize, checked((uint)_type.NonGCStaticFieldSize.AsInt));
                }

                if (_type.GCStaticFieldSize.AsInt != 0)
                {
                    layoutInfo.AppendUnsigned(BagElementKind.GcStaticDataSize, checked((uint)_type.GCStaticFieldSize.AsInt));
                    BagElementKind staticDescBagType;
                    ISymbolNode staticsDescSymbol = GetStaticsNode(factory, out staticDescBagType);
                    uint gcStaticsSymbolIndex = factory.MetadataManager.NativeLayoutInfo.StaticsReferences.GetIndex(staticsDescSymbol);
                    layoutInfo.AppendUnsigned(staticDescBagType, gcStaticsSymbolIndex);
                }

                if (_type.ThreadStaticFieldSize.AsInt != 0)
                {
                    layoutInfo.AppendUnsigned(BagElementKind.ThreadStaticDataSize, checked((uint)_type.ThreadStaticFieldSize.AsInt));
                    BagElementKind threadStaticDescBagType;
                    ISymbolNode threadStaticsDescSymbol = GetThreadStaticsNode(factory, out threadStaticDescBagType);
                    uint threadStaticsSymbolIndex = factory.MetadataManager.NativeLayoutInfo.StaticsReferences.GetIndex(threadStaticsDescSymbol);
                    layoutInfo.AppendUnsigned(threadStaticDescBagType, threadStaticsSymbolIndex);
                }
            }
            else
            {
                Debug.Assert(_isUniversalCanon);
                // Determine if type has instantiation determined size
                if (!_type.IsInterface && HasInstantiationDeterminedSize())
                {
                    typeFlags = typeFlags | Internal.NativeFormat.TypeFlags.HasInstantiationDeterminedSize;
                }
            }

            if (_type.BaseType != null && _type.BaseType.IsRuntimeDeterminedSubtype)
            {
                layoutInfo.Append(BagElementKind.BaseType, factory.NativeLayout.PlacedSignatureVertex(factory.NativeLayout.TypeSignatureVertex(_type.BaseType)).WriteVertex(factory));
            }
            else if (_type.IsDelegate && _isUniversalCanon)
            {
                // For USG delegate, we need to write the signature of the Invoke method to the native layout.
                // This signature is used by the calling convention converter to marshal parameters during delegate calls.
                MethodDesc delegateInvokeMethod = _type.GetMethod("Invoke", null).GetTypicalMethodDefinition();
                NativeLayoutMethodSignatureVertexNode invokeSignatureVertexNode = factory.NativeLayout.MethodSignatureVertex(delegateInvokeMethod.Signature);
                layoutInfo.Append(BagElementKind.DelegateInvokeSignature, invokeSignatureVertexNode.WriteVertex(factory));
            }

            if (typeFlags != default(Internal.NativeFormat.TypeFlags))
                layoutInfo.AppendUnsigned(BagElementKind.TypeFlags, (uint)typeFlags);

            if (_type.GetTypeDefinition().HasVariance)
            {
                List<uint> varianceFlags = new List<uint>();
                foreach (GenericParameterDesc param in _type.GetTypeDefinition().Instantiation)
                {
                    varianceFlags.Add((uint)param.Variance);
                }

                layoutInfo.Append(BagElementKind.GenericVarianceInfo, factory.NativeLayout.PlacedUIntVertexSequence(varianceFlags).WriteVertex(factory));
            }
            else if (_type.GetTypeDefinition() == factory.ArrayOfTEnumeratorType)
            {
                // Generic array enumerators use special variance rules recognized by the runtime
                List<uint> varianceFlag = new List<uint>();
                varianceFlag.Add((uint)Internal.Runtime.GenericVariance.ArrayCovariant);
                layoutInfo.Append(BagElementKind.GenericVarianceInfo, factory.NativeLayout.PlacedUIntVertexSequence(varianceFlag).WriteVertex(factory));
            }

            if (_isUniversalCanon)
            {
                // For universal canonical template types, we need to write out field layout information so that we 
                // can correctly compute the type sizes for dynamically created types at runtime, and construct
                // their GCDesc info
                VertexSequence fieldsSequence = null;

                foreach (FieldDesc field in _type.GetFields())
                {
                    // If this field does contribute to layout, skip
                    if (field.HasRva || field.IsLiteral)
                        continue;

                    // NOTE: The order and contents of the signature vertices emitted here is what we consider a field ordinal for the
                    // purpose of NativeLayoutFieldOffsetGenericDictionarySlotNode. 

                    FieldStorage fieldStorage = FieldStorage.Instance;
                    if (field.IsStatic)
                    {
                        if (field.IsThreadStatic)
                            fieldStorage = FieldStorage.TLSStatic;
                        else if (field.HasGCStaticBase)
                            fieldStorage = FieldStorage.GCStatic;
                        else
                            fieldStorage = FieldStorage.NonGCStatic;
                    }


                    NativeLayoutVertexNode fieldTypeSignature;
                    if (field.FieldType.IsGCPointer)
                    {
                        fieldTypeSignature = factory.NativeLayout.PlacedSignatureVertex(factory.NativeLayout.TypeSignatureVertex(field.Context.GetWellKnownType(WellKnownType.Object)));
                    }
                    else if (field.FieldType.IsPointer || field.FieldType.IsFunctionPointer)
                    {
                        fieldTypeSignature = factory.NativeLayout.PlacedSignatureVertex(factory.NativeLayout.TypeSignatureVertex(field.Context.GetWellKnownType(WellKnownType.IntPtr)));
                    }
                    else
                    {
                        fieldTypeSignature = factory.NativeLayout.PlacedSignatureVertex(factory.NativeLayout.TypeSignatureVertex(field.FieldType));
                    }

                    Vertex staticFieldVertexData = writer.GetTuple(fieldTypeSignature.WriteVertex(factory), writer.GetUnsignedConstant((uint)fieldStorage));

                    if (fieldsSequence == null)
                        fieldsSequence = new VertexSequence();
                    fieldsSequence.Append(staticFieldVertexData);
                }

                if (fieldsSequence != null)
                {
                    Vertex placedFieldsLayout = factory.MetadataManager.NativeLayoutInfo.SignaturesSection.Place(fieldsSequence);
                    layoutInfo.Append(BagElementKind.FieldLayout, placedFieldsLayout);
                }

                // We also need to write out the signatures of interesting methods in the type's vtable, which
                // will be needed by the calling convention translation logic at runtime, when the type's methods
                // get invoked.
                int currentVTableIndexUnused = 0;
                VertexSequence vtableSignaturesSequence = null;
                
                ProcessVTableEntriesForCallingConventionSignatureGeneration(factory, VTableEntriesToProcess.AllInVTable, ref currentVTableIndexUnused,
                    (int vtableIndex, MethodDesc declMethod, MethodDesc implMethod) =>
                    {
                        if (implMethod.IsAbstract)
                            return;

                        if (UniversalGenericParameterLayout.MethodSignatureHasVarsNeedingCallingConventionConverter(implMethod.Signature))
                        {
                            if (vtableSignaturesSequence == null)
                                vtableSignaturesSequence = new VertexSequence();

                            NativeLayoutVertexNode signatureVertex = factory.NativeLayout.MethodSignatureVertex(implMethod.GetTypicalMethodDefinition().Signature);
                            NativeLayoutVertexNode placedSignatureVertex = factory.NativeLayout.PlacedSignatureVertex(signatureVertex);

                            Vertex vtableSignatureEntry = writer.GetTuple(writer.GetUnsignedConstant(((uint)vtableIndex) << 1), // We currently do not use sealed vtable entries yet. Update when that happens
                                            placedSignatureVertex.WriteVertex(factory));

                            vtableSignaturesSequence.Append(vtableSignatureEntry);
                        }
                    }
                    , _type, _type);

                if (vtableSignaturesSequence != null)
                {
                    Vertex placedVtableSigs = factory.MetadataManager.NativeLayoutInfo.SignaturesSection.Place(vtableSignaturesSequence);
                    layoutInfo.Append(BagElementKind.VTableMethodSignatures, placedVtableSigs);
                }
            }

            factory.MetadataManager.NativeLayoutInfo.TemplatesSection.Place(layoutInfo);

            return SetSavedVertex(layoutInfo);
        }

        private enum VTableEntriesToProcess
        {
            AllInVTable,
            AllOnTypesThatShouldProduceFullVTables,
            AllOnTypesThatProducePartialVTables
        }

        private static IEnumerable<MethodDesc> EnumVirtualSlotsDeclaredOnType(TypeDesc declType)
        {
            // VirtualMethodUse of Foo<SomeType>.Method will bring in VirtualMethodUse
            // of Foo<__Canon>.Method. This in turn should bring in Foo<OtherType>.Method.
            DefType defType = declType.GetClosestDefType();

            Debug.Assert(!declType.IsInterface);

            IEnumerable<MethodDesc> allSlots = defType.EnumAllVirtualSlots();

            foreach (var method in allSlots)
            {
                // Generic virtual methods are tracked by an orthogonal mechanism.
                if (method.HasInstantiation)
                    continue;

                // Current type doesn't define this slot. Another VTableSlice will take care of this.
                if (method.OwningType != defType)
                    continue;

                yield return method;
            }
        }

        /// <summary>
        /// Process the vtable entries of a type by calling operation with the vtable index, declaring method, and implementing method
        /// Process them in order from 0th entry to last. 
        /// Skip generic virtual methods, as they are not present in the vtable itself
        /// Do not adjust vtable index for generic dictionary slot
        /// The vtable index is only actually valid if whichEntries is set to VTableEntriesToProcess.AllInVTable
        /// </summary>
        private void ProcessVTableEntriesForCallingConventionSignatureGeneration(NodeFactory factory, VTableEntriesToProcess whichEntries, ref int currentVTableIndex, Action<int, MethodDesc, MethodDesc> operation, TypeDesc implType, TypeDesc declType)
        {
            if (implType.IsInterface)
                return;

            declType = declType.GetClosestDefType();

            var baseType = declType.BaseType;
            if (baseType != null)
                ProcessVTableEntriesForCallingConventionSignatureGeneration(factory, whichEntries, ref currentVTableIndex, operation, implType, baseType);

            IEnumerable<MethodDesc> vtableEntriesToProcess;

            switch (whichEntries)
            {
                case VTableEntriesToProcess.AllInVTable:
                    vtableEntriesToProcess = factory.VTable(declType).Slots;
                    break;

                case VTableEntriesToProcess.AllOnTypesThatShouldProduceFullVTables:
                    if (factory.CompilationModuleGroup.ShouldProduceFullVTable(declType))
                    {
                        vtableEntriesToProcess = factory.VTable(declType).Slots;
                    }
                    else
                    {
                        vtableEntriesToProcess = Array.Empty<MethodDesc>();
                    }
                    break;

                case VTableEntriesToProcess.AllOnTypesThatProducePartialVTables:
                    if (factory.CompilationModuleGroup.ShouldProduceFullVTable(declType))
                    {
                        vtableEntriesToProcess = Array.Empty<MethodDesc>();
                    }
                    else
                    {
                        vtableEntriesToProcess = EnumVirtualSlotsDeclaredOnType(declType);
                    }
                    break;

                default:
                    throw new Exception();
            }

            // Actual vtable slots follow
            foreach (MethodDesc declMethod in vtableEntriesToProcess)
            {
                // No generic virtual methods can appear in the vtable!
                Debug.Assert(!declMethod.HasInstantiation);

                MethodDesc implMethod = implType.GetClosestDefType().FindVirtualFunctionTargetMethodOnObjectType(declMethod);

                operation(currentVTableIndex, declMethod, implMethod);
                currentVTableIndex++;
            }
        }
    }

    public abstract class NativeLayoutGenericDictionarySlotNode : NativeLayoutVertexNode
    {
        public abstract override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context);
        protected abstract Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory);
        protected abstract FixupSignatureKind SignatureKind { get; }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            CheckIfMarkedEnoughToWrite();

            NativeWriter writer = GetNativeWriter(factory);
            return writer.GetFixupSignature(SignatureKind, WriteSignatureVertex(writer, factory));
        }
    }

    public abstract class NativeLayoutTypeSignatureBasedGenericDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        NativeLayoutTypeSignatureVertexNode _signature;
        TypeDesc _type;

        public NativeLayoutTypeSignatureBasedGenericDictionarySlotNode(NodeFactory factory, TypeDesc type)
        {
            _signature = factory.NativeLayout.TypeSignatureVertex(type);
            _type = type;
        }

        protected abstract string NodeTypeName { get; }
        protected sealed override string GetName(NodeFactory factory) => NodeTypeName + factory.NameMangler.GetMangledTypeName(_type);

        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[1] { new DependencyListEntry(_signature, "TypeSignature") };
        }

        protected sealed override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            return _signature.WriteVertex(factory);
        }
    }

    public sealed class NativeLayoutTypeHandleGenericDictionarySlotNode : NativeLayoutTypeSignatureBasedGenericDictionarySlotNode
    {
        public NativeLayoutTypeHandleGenericDictionarySlotNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
        }

        protected override string NodeTypeName => "NativeLayoutTypeHandleGenericDictionarySlotNode_";

        protected override FixupSignatureKind SignatureKind => FixupSignatureKind.TypeHandle;
    }

    public sealed class NativeLayoutUnwrapNullableGenericDictionarySlotNode : NativeLayoutTypeSignatureBasedGenericDictionarySlotNode
    {
        public NativeLayoutUnwrapNullableGenericDictionarySlotNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
        }

        protected override string NodeTypeName => "NativeLayoutUnwrapNullableGenericDictionarySlotNode_";

        protected override FixupSignatureKind SignatureKind => FixupSignatureKind.UnwrapNullableType;
    }

    public sealed class NativeLayoutTypeSizeGenericDictionarySlotNode : NativeLayoutTypeSignatureBasedGenericDictionarySlotNode
    {
        public NativeLayoutTypeSizeGenericDictionarySlotNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
        }

        protected override string NodeTypeName => "NativeLayoutTypeSizeGenericDictionarySlotNode_";

        protected override FixupSignatureKind SignatureKind => FixupSignatureKind.TypeSize;
    }

    public sealed class NativeLayoutAllocateObjectGenericDictionarySlotNode : NativeLayoutTypeSignatureBasedGenericDictionarySlotNode
    {
        public NativeLayoutAllocateObjectGenericDictionarySlotNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
        }

        protected override string NodeTypeName => "NativeLayoutAllocateObjectGenericDictionarySlotNode_";

        protected override FixupSignatureKind SignatureKind => FixupSignatureKind.AllocateObject;
    }

    public sealed class NativeLayoutTlsIndexGenericDictionarySlotNode : NativeLayoutTypeSignatureBasedGenericDictionarySlotNode
    {
        public NativeLayoutTlsIndexGenericDictionarySlotNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
        }

        protected override string NodeTypeName => "NativeLayoutTlsIndexGenericDictionarySlotNode_";

        protected override FixupSignatureKind SignatureKind => FixupSignatureKind.TlsIndex;
    }

    public sealed class NativeLayoutTlsOffsetGenericDictionarySlotNode : NativeLayoutTypeSignatureBasedGenericDictionarySlotNode
    {
        public NativeLayoutTlsOffsetGenericDictionarySlotNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
        }

        protected override string NodeTypeName => "NativeLayoutTlsOffsetGenericDictionarySlotNode_";

        protected override FixupSignatureKind SignatureKind => FixupSignatureKind.TlsOffset;
    }

    public sealed class NativeLayoutDefaultConstructorGenericDictionarySlotNode : NativeLayoutTypeSignatureBasedGenericDictionarySlotNode
    {
        public NativeLayoutDefaultConstructorGenericDictionarySlotNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
        }

        protected override string NodeTypeName => "NativeLayoutDefaultConstructorGenericDictionarySlotNode_";

        protected override FixupSignatureKind SignatureKind => FixupSignatureKind.DefaultConstructor;
    }

    public sealed class NativeLayoutAllocateArrayGenericDictionarySlotNode : NativeLayoutTypeSignatureBasedGenericDictionarySlotNode
    {
        public NativeLayoutAllocateArrayGenericDictionarySlotNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
            Debug.Assert(type.IsArray); // TODO! Verify that the passed in type is the array type and not the element type of the array.
        }

        protected override string NodeTypeName => "NativeLayoutAllocateArrayGenericDictionarySlotNode_";

        protected override FixupSignatureKind SignatureKind => FixupSignatureKind.AllocateArray;
    }

    public abstract class NativeLayoutStaticsGenericDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        NativeLayoutTypeSignatureVertexNode _signature;
        TypeDesc _type;

        public NativeLayoutStaticsGenericDictionarySlotNode(NodeFactory factory, TypeDesc type)
        {
            _signature = factory.NativeLayout.TypeSignatureVertex(type);
            _type = type;
        }

        protected abstract StaticDataKind StaticDataKindFlag { get; }
        protected abstract string NodeTypeName { get; }

        protected sealed override string GetName(NodeFactory factory) => NodeTypeName + factory.NameMangler.GetMangledTypeName(_type);

        protected sealed override FixupSignatureKind SignatureKind => FixupSignatureKind.StaticData;
        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[1] { new DependencyListEntry(_signature, "TypeSignature") };
        }

        protected sealed override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            return writer.GetStaticDataSignature(_signature.WriteVertex(factory), StaticDataKindFlag);
        }
    }

    public sealed class NativeLayoutGcStaticsGenericDictionarySlotNode : NativeLayoutStaticsGenericDictionarySlotNode
    {
        public NativeLayoutGcStaticsGenericDictionarySlotNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        { }

        protected override StaticDataKind StaticDataKindFlag => StaticDataKind.Gc;
        protected override string NodeTypeName => "NativeLayoutGcStaticsGenericDictionarySlotNode_";
    }

    public sealed class NativeLayoutNonGcStaticsGenericDictionarySlotNode : NativeLayoutStaticsGenericDictionarySlotNode
    {
        public NativeLayoutNonGcStaticsGenericDictionarySlotNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        { }

        protected override StaticDataKind StaticDataKindFlag => StaticDataKind.NonGc;
        protected override string NodeTypeName => "NativeLayoutNonGcStaticsGenericDictionarySlotNode_";
    }

    public sealed class NativeLayoutInterfaceDispatchGenericDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        NativeLayoutTypeSignatureVertexNode _signature;
        MethodDesc _method;

        public NativeLayoutInterfaceDispatchGenericDictionarySlotNode(NodeFactory factory, MethodDesc method)
        {
            _signature = factory.NativeLayout.TypeSignatureVertex(method.OwningType);
            _method = method;
        }

        protected sealed override string GetName(NodeFactory factory) => "NativeLayoutInterfaceDispatchGenericDictionarySlotNode_" + factory.NameMangler.GetMangledMethodName(_method);

        protected sealed override FixupSignatureKind SignatureKind => FixupSignatureKind.InterfaceCall;
        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[1] { new DependencyListEntry(_signature, "TypeSignature") };
        }

        protected sealed override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            int slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, _method);

            return writer.GetMethodSlotSignature(_signature.WriteVertex(factory), checked((uint)slot));
        }
    }

    public sealed class NativeLayoutMethodDictionaryGenericDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        MethodDesc _method;
        WrappedMethodDictionaryVertexNode _wrappedNode;

        private class WrappedMethodDictionaryVertexNode : NativeLayoutMethodEntryVertexNode
        {
            public WrappedMethodDictionaryVertexNode(NodeFactory factory, MethodDesc method) :
                base(factory, method, default(MethodEntryFlags))
            {
            }

            protected override IMethodNode GetMethodEntrypointNode(NodeFactory factory, out bool unboxingStub)
            {
                throw new NotSupportedException();
            }

            protected sealed override string GetName(NodeFactory factory) => "WrappedMethodEntryVertexNodeForDictionarySlot_" + factory.NameMangler.GetMangledMethodName(_method);
        }


        public NativeLayoutMethodDictionaryGenericDictionarySlotNode(NodeFactory factory, MethodDesc method)
        {
            Debug.Assert(method.HasInstantiation);
            _method = method;
            _wrappedNode = new WrappedMethodDictionaryVertexNode(factory, method);
        }

        protected sealed override string GetName(NodeFactory factory) => "NativeLayoutMethodDictionaryGenericDictionarySlotNode_" + factory.NameMangler.GetMangledMethodName(_method);
        protected sealed override FixupSignatureKind SignatureKind => FixupSignatureKind.MethodDictionary;
        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[] { new DependencyListEntry(_wrappedNode, "wrappednode") };
        }

        protected sealed override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            return _wrappedNode.WriteVertex(factory);
        }
    }

    public sealed class NativeLayoutFieldOffsetGenericDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        FieldDesc _field;

        public NativeLayoutFieldOffsetGenericDictionarySlotNode(FieldDesc field)
        {
            _field = field;
        }

        protected sealed override string GetName(NodeFactory factory) => "NativeLayoutFieldOffsetGenericDictionarySlotNode_" + factory.NameMangler.GetMangledFieldName(_field);

        protected sealed override FixupSignatureKind SignatureKind => FixupSignatureKind.FieldOffset;

        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[1] { new DependencyListEntry(factory.NativeLayout.TypeSignatureVertex(_field.OwningType), "Field Containing Type Signature") };
        }

        protected sealed override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            NativeWriter nativeWriter = GetNativeWriter(factory);

            uint fieldOrdinal = 0;

            foreach (FieldDesc field in _field.OwningType.GetFields())
            {
                // If this field does contribute to layout, skip
                if (field.HasRva || field.IsLiteral)
                    continue;

                // NOTE: The order and contents of the field ordinal emitted here is based on the order of emission for fields
                // in the USG template generation.

                if (field == _field)
                {
                    Vertex typeVertex = factory.NativeLayout.TypeSignatureVertex(_field.OwningType).WriteVertex(factory);
                    return nativeWriter.GetTuple(typeVertex, nativeWriter.GetUnsignedConstant(fieldOrdinal));
                }
                fieldOrdinal++;
            }

            // If we reach here, we were unable to calculate field ordinal.
            Debug.Assert(false, "This should be unreachable, as we should have found a field ordinal above");
            throw new Exception("Internal Compiler Error");
        }
    }

    public sealed class NativeLayoutFieldLdTokenGenericDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        FieldDesc _field;

        public NativeLayoutFieldLdTokenGenericDictionarySlotNode(FieldDesc field)
        {
            _field = field;
        }

        protected sealed override string GetName(NodeFactory factory) => "NativeLayoutFieldLdTokenGenericDictionarySlotNode_" + factory.NameMangler.GetMangledFieldName(_field);

        protected sealed override FixupSignatureKind SignatureKind => FixupSignatureKind.FieldLdToken;

        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[1] { new DependencyListEntry(factory.NativeLayout.FieldLdTokenVertex(_field), "Field Signature") };
        }

        protected sealed override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            return factory.NativeLayout.FieldLdTokenVertex(_field).WriteVertex(factory);
        }
    }

    public sealed class NativeLayoutVTableOffsetGenericDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        MethodDesc _method;
        MethodDesc _slotDefiningMethod;

        public NativeLayoutVTableOffsetGenericDictionarySlotNode(MethodDesc method)
        {
            _method = method;
            MethodDesc typicalSlotDefiningMethod = MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(method.GetTypicalMethodDefinition());
            _slotDefiningMethod = _method.OwningType.FindMethodOnTypeWithMatchingTypicalMethod(typicalSlotDefiningMethod).GetCanonMethodTarget(CanonicalFormKind.Specific);
            Debug.Assert(!method.HasInstantiation);
            Debug.Assert(!method.OwningType.IsInterface);
            Debug.Assert(method.OwningType.IsDefType);
            Debug.Assert(method.IsVirtual);
        }

        protected sealed override string GetName(NodeFactory factory) => "NativeLayoutVTableOffsetGenericDictionarySlotNode_" + factory.NameMangler.GetMangledMethodName(_method);

        protected sealed override FixupSignatureKind SignatureKind => FixupSignatureKind.VTableOffset;

        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[1] { new DependencyListEntry(factory.NativeLayout.TypeSignatureVertex(_slotDefiningMethod.OwningType), "Method VTableOffset Containing Type Signature") };
        }

        protected sealed override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            NativeWriter nativeWriter = GetNativeWriter(factory);
            
            int slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, _slotDefiningMethod);
            Vertex typeVertex = factory.NativeLayout.TypeSignatureVertex(_slotDefiningMethod.OwningType).WriteVertex(factory);
            return nativeWriter.GetTuple(typeVertex, nativeWriter.GetUnsignedConstant((uint)slot));
        }
    }

    public sealed class NativeLayoutMethodLdTokenGenericDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        MethodDesc _method;

        public NativeLayoutMethodLdTokenGenericDictionarySlotNode(MethodDesc method)
        {
            _method = method;
        }

        protected sealed override string GetName(NodeFactory factory) => "NativeLayoutMethodLdTokenGenericDictionarySlotNode_" + factory.NameMangler.GetMangledMethodName(_method);

        protected sealed override FixupSignatureKind SignatureKind => FixupSignatureKind.MethodLdToken;

        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[1] { new DependencyListEntry(factory.NativeLayout.MethodLdTokenVertex(_method), "Method Signature") };
        }

        protected sealed override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            return factory.NativeLayout.MethodLdTokenVertex(_method).WriteVertex(factory);
        }
    }

    public sealed class NativeLayoutCallingConventionConverterGenericDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        Internal.TypeSystem.MethodSignature _signature;
        CallingConventionConverterKind _converterKind;

        public NativeLayoutCallingConventionConverterGenericDictionarySlotNode(Internal.TypeSystem.MethodSignature signature, CallingConventionConverterKind converterKind)
        {
            _signature = signature;
            _converterKind = converterKind;
        }

        protected sealed override string GetName(NodeFactory factory) => 
            "NativeLayoutCallingConventionConverterGenericDictionarySlotNode" + _converterKind.ToString() +
             _signature.GetMangledName(factory.NameMangler);

        protected sealed override FixupSignatureKind SignatureKind => FixupSignatureKind.MethodLdToken;

        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[1] { new DependencyListEntry(factory.NativeLayout.MethodSignatureVertex(_signature), "Method Signature") };
        }

        protected sealed override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            Vertex signatureStream = factory.NativeLayout.MethodSignatureVertex(_signature).WriteVertex(factory);
            return GetNativeWriter(factory).GetCallingConventionConverterSignature((uint)_converterKind, signatureStream);
        }
    }

    public sealed class NativeLayoutConstrainedMethodDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        MethodDesc _constrainedMethod;
        TypeDesc _constraintType;
        bool _directCall;

        public NativeLayoutConstrainedMethodDictionarySlotNode(MethodDesc constrainedMethod, TypeDesc constraintType, bool directCall)
        {
            _constrainedMethod = constrainedMethod;
            _constraintType = constraintType;
            _directCall = directCall;
            Debug.Assert(_constrainedMethod.OwningType.IsInterface);
            Debug.Assert(!_constrainedMethod.HasInstantiation || !directCall);
        }

        protected sealed override string GetName(NodeFactory factory) =>
            "NativeLayoutConstrainedMethodDictionarySlotNode_"
            + (_directCall ? "Direct" : "")
            + factory.NameMangler.GetMangledMethodName(_constrainedMethod) 
            + ","
            + factory.NameMangler.GetMangledTypeName(_constraintType);

        protected sealed override FixupSignatureKind SignatureKind => _constrainedMethod.HasInstantiation ? FixupSignatureKind.GenericConstrainedMethod :
            (_directCall ? FixupSignatureKind.NonGenericDirectConstrainedMethod : FixupSignatureKind.NonGenericConstrainedMethod);

        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            DependencyNodeCore<NodeFactory> constrainedMethodDescriptorNode;
            if (_constrainedMethod.HasInstantiation)
            {
                constrainedMethodDescriptorNode = factory.NativeLayout.MethodLdTokenVertex(_constrainedMethod);
            }
            else
            {
                constrainedMethodDescriptorNode = factory.NativeLayout.TypeSignatureVertex(_constrainedMethod.OwningType);
            }

            return new DependencyListEntry[] {
                new DependencyListEntry(factory.NativeLayout.TypeSignatureVertex(_constraintType), "ConstraintType"),
                new DependencyListEntry(constrainedMethodDescriptorNode, "ConstrainedMethodType"),
            };
        }

        protected sealed override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            Vertex constraintType = factory.NativeLayout.TypeSignatureVertex(_constraintType).WriteVertex(factory);
            if (_constrainedMethod.HasInstantiation)
            {
                Debug.Assert(SignatureKind == FixupSignatureKind.GenericConstrainedMethod);
                Vertex constrainedMethodVertex = factory.NativeLayout.MethodLdTokenVertex(_constrainedMethod).WriteVertex(factory);
                return writer.GetTuple(constraintType, constrainedMethodVertex);
            }
            else
            {
                Debug.Assert((SignatureKind == FixupSignatureKind.NonGenericConstrainedMethod) || (SignatureKind == FixupSignatureKind.NonGenericDirectConstrainedMethod));
                Vertex methodType = factory.NativeLayout.TypeSignatureVertex(_constrainedMethod.OwningType).WriteVertex(factory);
                int interfaceSlot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, _constrainedMethod.GetCanonMethodTarget(CanonicalFormKind.Specific));
                Vertex interfaceSlotVertex = writer.GetUnsignedConstant(checked((uint)interfaceSlot));
                return writer.GetTuple(constraintType, methodType, interfaceSlotVertex);
            }
        }
    }

    public sealed class NativeLayoutMethodEntrypointGenericDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        MethodDesc _method;
        WrappedMethodEntryVertexNode _wrappedNode;

        private class WrappedMethodEntryVertexNode : NativeLayoutMethodEntryVertexNode
        {
            public bool _unboxingStub;
            public IMethodNode _functionPointerNode;

            public WrappedMethodEntryVertexNode(NodeFactory factory, MethodDesc method, bool unboxingStub, IMethodNode functionPointerNode) :
                base(factory, method, functionPointerNode != null ? MethodEntryFlags.SaveEntryPoint : default(MethodEntryFlags))
            {
                _unboxingStub = unboxingStub;
                _functionPointerNode = functionPointerNode;
            }

            protected override IMethodNode GetMethodEntrypointNode(NodeFactory factory, out bool unboxingStub)
            {
                unboxingStub = _unboxingStub;
                return _functionPointerNode;
            }

            protected sealed override string GetName(NodeFactory factory) => "WrappedMethodEntryVertexNodeForDictionarySlot_" + (_unboxingStub ? "Unboxing_" : "") + factory.NameMangler.GetMangledMethodName(_method);
        }


        public NativeLayoutMethodEntrypointGenericDictionarySlotNode(NodeFactory factory, MethodDesc method, IMethodNode functionPointerNode, bool unboxingStub)
        {
            _method = method;
            _wrappedNode = new WrappedMethodEntryVertexNode(factory, method, unboxingStub, functionPointerNode);
        }

        protected sealed override string GetName(NodeFactory factory) => "NativeLayoutMethodEntrypointGenericDictionarySlotNode_" + (_wrappedNode._unboxingStub ? "Unboxing_" : "") + factory.NameMangler.GetMangledMethodName(_method);
        protected sealed override FixupSignatureKind SignatureKind => FixupSignatureKind.Method;
        public sealed override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[] { new DependencyListEntry(_wrappedNode, "wrappednode") };
        }

        protected sealed override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            return _wrappedNode.WriteVertex(factory);
        }
    }

    public sealed class NativeLayoutIntegerDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        int _value;

        public NativeLayoutIntegerDictionarySlotNode(int value)
        {
            _value = value;
        }

        protected override FixupSignatureKind SignatureKind => FixupSignatureKind.IntValue;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return null;
        }

        protected override string GetName(NodeFactory context) => "NativeLayoutIntegerDictionarySlotNode_" + _value.ToStringInvariant();

        protected override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            return writer.GetUnsignedConstant((uint)_value);
        }

        public override void CheckIfMarkedEnoughToWrite()
        {
            // Do nothing, this node does not need marking
        }
    }

    public sealed class NativeLayoutPointerToOtherSlotDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        int _otherSlotIndex;

        public NativeLayoutPointerToOtherSlotDictionarySlotNode(int otherSlotIndex)
        {
            _otherSlotIndex = otherSlotIndex;
        }

        protected override FixupSignatureKind SignatureKind => FixupSignatureKind.PointerToOtherSlot;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return null;
        }

        protected override string GetName(NodeFactory context) => "NativeLayoutPointerToOtherSlotDictionarySlotNode_" + _otherSlotIndex.ToStringInvariant();

        protected override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            return writer.GetUnsignedConstant((uint)_otherSlotIndex);
        }

        public override void CheckIfMarkedEnoughToWrite()
        {
            // Do nothing, this node does not need marking
        }
    }

    public sealed class NativeLayoutNotSupportedDictionarySlotNode : NativeLayoutGenericDictionarySlotNode
    {
        protected override FixupSignatureKind SignatureKind => FixupSignatureKind.NotYetSupported;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return null;
        }

        protected override string GetName(NodeFactory context) => "NativeLayoutNotSupportedDictionarySlotNode";
        
        protected override Vertex WriteSignatureVertex(NativeWriter writer, NodeFactory factory)
        {
            return writer.GetUnsignedConstant(0xDEADBEEF);
        }
    }
}

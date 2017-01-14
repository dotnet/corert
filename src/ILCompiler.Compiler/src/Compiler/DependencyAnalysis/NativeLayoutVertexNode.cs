// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.NativeFormat;
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
    internal abstract class NativeLayoutVertexNode : DependencyNodeCore<NodeFactory>
    {
        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;

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
    internal abstract class NativeLayoutSavedVertexNode : NativeLayoutVertexNode
    {
        public Vertex SavedVertex { get; private set; }
        protected Vertex SetSavedVertex(Vertex value)
        {
            Debug.Assert(SavedVertex == null || Object.ReferenceEquals(SavedVertex, value));
            SavedVertex = value;
            return value;
        }
    }

    internal sealed class NativeLayoutMethodLdTokenVertexNode : NativeLayoutSavedVertexNode
    {
        private MethodDesc _method;
        private NativeLayoutTypeSignatureVertexNode _containingTypeSig;
        private NativeLayoutMethodSignatureVertexNode _methodSig;
        private NativeLayoutTypeSignatureVertexNode[] _instantiationArgsSig;

        protected override string GetName() => "NativeLayoutMethodLdTokenVertexNode_" + NodeFactory.NameMangler.GetMangledMethodName(_method);

        public NativeLayoutMethodLdTokenVertexNode(NodeFactory factory, MethodDesc method)
        {
            _method = method;
            _containingTypeSig = factory.NativeLayout.TypeSignatureVertex(method.OwningType);
            _methodSig = factory.NativeLayout.MethodSignatureVertex(method.GetTypicalMethodDefinition());
            if (method.HasInstantiation)
            {
                _instantiationArgsSig = new NativeLayoutTypeSignatureVertexNode[method.Instantiation.Length];
                for (int i = 0; i < _instantiationArgsSig.Length; i++)
                    _instantiationArgsSig[i] = factory.NativeLayout.TypeSignatureVertex(method.Instantiation[i]);
            }
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            DependencyList dependencies = new DependencyList();

            dependencies.Add(new DependencyListEntry(_containingTypeSig, "NativeLayoutLdTokenVertexNode containing type signature"));
            dependencies.Add(new DependencyListEntry(_methodSig, "NativeLayoutLdTokenVertexNode method signature"));
            foreach (var arg in _instantiationArgsSig)
                dependencies.Add(new DependencyListEntry(arg, "NativeLayoutLdTokenVertexNode instantiation argument signature"));

            return dependencies;
        }

        public override Vertex WriteVertex(NodeFactory factory)
        {
            Vertex containingType = _containingTypeSig.WriteVertex(factory);
            Vertex methodSig = _methodSig.WriteVertex(factory);
            Vertex methodNameAndSig = GetNativeWriter(factory).GetMethodNameAndSigSignature(_method.Name, methodSig);

            Debug.Assert(_instantiationArgsSig == null || (_method.HasInstantiation && _method.Instantiation.Length == _instantiationArgsSig.Length));

            Vertex[] args = null;
            MethodFlags flags = 0;
            if (_method.HasInstantiation)
            {
                flags |= MethodFlags.HasInstantiation;
                args = new Vertex[_method.Instantiation.Length];
                for (int i = 0; i < args.Length; i++)
                    args[i] = _instantiationArgsSig[i].WriteVertex(factory);
            }

            Vertex signature = GetNativeWriter(factory).GetMethodSignature((uint)flags, 0, containingType, methodNameAndSig, args);
            return SetSavedVertex(factory.MetadataManager.NativeLayoutInfo.LdTokenInfoSection.Place(signature));
        }
    }

    internal sealed class NativeLayoutMethodSignatureVertexNode : NativeLayoutVertexNode
    {
        private MethodDesc _method;
        private NativeLayoutTypeSignatureVertexNode _returnTypeSig;
        private NativeLayoutTypeSignatureVertexNode[] _parametersSig;

        protected override string GetName() => "NativeLayoutMethodSignatureVertexNode" + NodeFactory.NameMangler.GetMangledMethodName(_method);

        public NativeLayoutMethodSignatureVertexNode(NodeFactory factory, MethodDesc method)
        {
            _method = method;
            _returnTypeSig = factory.NativeLayout.TypeSignatureVertex(method.Signature.ReturnType);
            _parametersSig = new NativeLayoutTypeSignatureVertexNode[method.Signature.Length];
            for (int i = 0; i < _parametersSig.Length; i++)
                _parametersSig[i] = factory.NativeLayout.TypeSignatureVertex(method.Signature[i]);
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
            MethodCallingConvention methodCallingConvention = default(MethodCallingConvention);

            if (_method.Signature.GenericParameterCount > 0)
                methodCallingConvention |= MethodCallingConvention.Generic;
            if (_method.Signature.IsStatic)
                methodCallingConvention |= MethodCallingConvention.Static;

            Debug.Assert(_method.Signature.Length == _parametersSig.Length);

            Vertex returnType = _returnTypeSig.WriteVertex(factory);
            Vertex[] parameters = new Vertex[_parametersSig.Length];
            for (int i = 0; i < _parametersSig.Length; i++)
                parameters[i] = _parametersSig[i].WriteVertex(factory);

            Vertex signature = GetNativeWriter(factory).GetMethodSigSignature((uint)methodCallingConvention, (uint)_method.Signature.GenericParameterCount, returnType, parameters);
            return factory.MetadataManager.NativeLayoutInfo.SignaturesSection.Place(signature);
        }
    }

    internal sealed class NativeLayoutMethodNameAndSignatureVertexNode : NativeLayoutVertexNode
    {
        private MethodDesc _method;
        private NativeLayoutMethodSignatureVertexNode _methodSig;

        protected override string GetName() => "NativeLayoutMethodNameAndSignatureVertexNode" + NodeFactory.NameMangler.GetMangledMethodName(_method);

        public NativeLayoutMethodNameAndSignatureVertexNode(NodeFactory factory, MethodDesc method)
        {
            _method = method;
            _methodSig = factory.NativeLayout.MethodSignatureVertex(method);
        }
        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return new DependencyListEntry[] { new DependencyListEntry(_methodSig, "NativeLayoutMethodNameAndSignatureVertexNode signature vertex") };
        }
        public override Vertex WriteVertex(NodeFactory factory)
        {
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

        protected override string GetName() => "NativeLayoutTypeSignatureVertexNode" + NodeFactory.NameMangler.GetMangledTypeName(_type);

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
                IEETypeNode eetypeNode = factory.NecessaryTypeSymbol(_type);
                uint typeIndex = factory.MetadataManager.NativeLayoutInfo.ExternalReferences.GetIndex(eetypeNode);
                return GetNativeWriter(factory).GetExternalTypeSignature(typeIndex);
            }
        }
    }

    internal sealed class NativeLayoutPlacedSignatureVertexNode : NativeLayoutSavedVertexNode
    {
        private NativeLayoutVertexNode _signatureToBePlaced;

        protected override string GetName() => "NativeLayoutTypeSignatureVertexNode";

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
            // Always use the NativeLayoutInfo blob for names and sigs, even if the associated types/methods are written elsewhere.
            // This saves space, since we can Unify more signatures, allows optimizations in comparing sigs in the same module, and 
            // prevents the dynamic type loader having to know about other native layout sections (since sigs contain types). If we are 
            // using a non-native layout info writer, write the sig to the native layout info, and refer to it by offset in its own 
            // section.  At runtime, we will assume all names and sigs are in the native layout and find it.

            Vertex signature = _signatureToBePlaced.WriteVertex(factory);
            return SetSavedVertex(factory.MetadataManager.NativeLayoutInfo.SignaturesSection.Place(signature));
        }
    }
}
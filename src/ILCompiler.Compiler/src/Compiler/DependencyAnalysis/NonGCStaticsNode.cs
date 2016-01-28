// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a node with non-GC static data associated with a type, along
    /// with it's class constructor context. The non-GC static data region shall be prefixed
    /// with the class constructor context if the type has a class constructor that
    /// needs to be triggered before the type members can be accessed.
    /// </summary>
    internal class NonGCStaticsNode : ObjectNode, ISymbolNode
    {
        private MetadataType _type;
        private ISymbolNode _classConstructorContext;

        public NonGCStaticsNode(MetadataType type)
        {
            _type = type;

            if (HasClassConstructorContext)
            {
                _classConstructorContext = new ObjectAndOffsetSymbolNode(this, 0,
                    "__CCtorContext_" + NodeFactory.NameMangler.GetMangledTypeName(_type));
            }
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override string Section
        {
            get
            {
                return "data";
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return "__NonGCStaticBase_" + NodeFactory.NameMangler.GetMangledTypeName(_type);
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                if (!HasClassConstructorContext)
                {
                    return 0;
                }
                else
                {
                    // Prepend the context to the existing fields without messing up the alignment of those fields.
                    int alignmentRequired = Math.Max(_type.NonGCStaticFieldAlignment, ClassConstructorContextAlignment);
                    int classConstructorContextStorageSize = AlignmentHelper.AlignUp(ClassConstructorContextSize, alignmentRequired);
                    return classConstructorContextStorageSize;
                }
            }
        }

        public bool HasClassConstructorContext
        {
            get
            {
                return _type.HasStaticConstructor;
            }
        }

        public ISymbolNode ClassConstructorContext
        {
            get
            {
                Debug.Assert(HasClassConstructorContext);
                return _classConstructorContext;
            }
        }

        private int ClassConstructorContextSize
        {
            get
            {
                // TODO: Assert that StaticClassConstructionContext type has the expected size
                //       (need to make it a well known type?)
                return _type.Context.Target.PointerSize * 2;
            }
        }

        private int ClassConstructorContextAlignment
        {
            get
            {
                // TODO: Assert that StaticClassConstructionContext type has the expected alignment
                //       (need to make it a well known type?)
                return _type.Context.Target.PointerSize;
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory);

            // If the type has a class constructor, it's non-GC statics section is prefixed  
            // by System.Runtime.CompilerServices.StaticClassConstructionContext struct.
            if (HasClassConstructorContext)
            {
                int alignmentRequired = Math.Max(_type.NonGCStaticFieldAlignment, ClassConstructorContextAlignment);
                builder.RequireAlignment(alignmentRequired);

                Debug.Assert(((ISymbolNode)this).Offset >= ClassConstructorContextSize);

                // Add padding before the context if alignment forces us to do so
                builder.EmitZeros(((ISymbolNode)this).Offset - ClassConstructorContextSize);

                // Emit the actual StaticClassConstructionContext                
                var cctorMethod = _type.GetStaticConstructor();
                builder.EmitPointerReloc(factory.MethodEntrypoint(cctorMethod));
                builder.EmitZeroPointer();

                builder.DefinedSymbols.Add(_classConstructorContext);
            }
            else
            {
                builder.RequireAlignment(_type.NonGCStaticFieldAlignment);
            }

            builder.EmitZeros(_type.NonGCStaticFieldSize);
            builder.DefinedSymbols.Add(this);

            return builder.ToObjectData();
        }
    }
}

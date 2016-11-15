﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using GenericVariance = Internal.Runtime.GenericVariance;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Describes how a generic type instance is composed - the number of generic arguments, their types,
    /// and variance information.
    /// </summary>
    internal class GenericCompositionNode : ObjectNode, ISymbolNode
    {
        private GenericCompositionDetails _details;

        public GenericCompositionNode(GenericCompositionDetails details)
        {
            _details = details;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__GenericInstance");

            bool hasVariance = false;

            for (int i = 0; i < _details.Instantiation.Length; i++)
            {
                sb.Append('_');
                sb.Append(NodeFactory.NameMangler.GetMangledTypeName(_details.Instantiation[i]));

                hasVariance |= _details.Variance[i] != 0;
            }

            if (hasVariance)
            {
                for (int i = 0; i < _details.Variance.Length; i++)
                {
                    sb.Append('_');
                    sb.Append((checked((byte)_details.Variance[i])).ToStringInvariant());
                }
            }
        }

        public int Offset
        {
            get
            {
                return 0;
            }
        }

        public override ObjectNodeSection Section
        {
            get
            {
                if (_details.Instantiation[0].Context.Target.IsWindows)
                    return ObjectNodeSection.ReadOnlyDataSection;
                else
                    return ObjectNodeSection.DataSection;
            }
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return true;
        }

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            bool hasVariance = false;
            foreach (var argVariance in _details.Variance)
            {
                if (argVariance != 0)
                {
                    hasVariance = true;
                    break;
                }
            }

            var builder = new ObjectDataBuilder(factory);
            builder.DefinedSymbols.Add(this);

            builder.RequirePointerAlignment();

            builder.EmitShort((short)checked((UInt16)_details.Instantiation.Length));

            builder.EmitByte((byte)(hasVariance ? 1 : 0));

            // TODO: general purpose padding
            builder.EmitByte(0);
            if (factory.Target.PointerSize == 8)
                builder.EmitInt(0);

            foreach (var typeArg in _details.Instantiation)
                builder.EmitPointerReloc(factory.NecessaryTypeSymbol(typeArg));

            if (hasVariance)
            {
                foreach (var argVariance in _details.Variance)
                    builder.EmitByte(checked((byte)argVariance));
            }

            return builder.ToObjectData();
        }

        protected override string GetName() => this.GetMangledName();
    }

    internal struct GenericCompositionDetails : IEquatable<GenericCompositionDetails>
    {
        public readonly Instantiation Instantiation;

        public readonly GenericVariance[] Variance;

        public GenericCompositionDetails(TypeDesc genericTypeInstance)
        {
            Debug.Assert(!genericTypeInstance.IsTypeDefinition);

            Debug.Assert((byte)Internal.TypeSystem.GenericVariance.Contravariant == (byte)GenericVariance.Contravariant);
            Debug.Assert((byte)Internal.TypeSystem.GenericVariance.Covariant == (byte)GenericVariance.Covariant);

            Instantiation = genericTypeInstance.Instantiation;

            Variance = new GenericVariance[Instantiation.Length];
            int i = 0;
            foreach (GenericParameterDesc param in genericTypeInstance.GetTypeDefinition().Instantiation)
            {
                Variance[i++] = (GenericVariance)param.Variance;
            }
        }

        public GenericCompositionDetails(Instantiation instantiation, GenericVariance[] variance)
        {
            Instantiation = instantiation;
            Variance = variance;
            Debug.Assert(Instantiation.Length == Variance.Length);
        }

        public bool Equals(GenericCompositionDetails other)
        {
            if (Instantiation.Length != other.Instantiation.Length)
                return false;

            for (int i = 0; i < Instantiation.Length; i++)
            {
                if (Instantiation[i] != other.Instantiation[i])
                    return false;

                if (Variance[i] != other.Variance[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is GenericCompositionDetails && Equals((GenericCompositionDetails)obj);
        }

        public override int GetHashCode()
        {
            int hashCode = 13;
            foreach (var element in Variance)
            {
                int value = (int)element * 0x5498341 + 0x832424;
                hashCode = hashCode * 31 + value;
            }

            return Instantiation.ComputeGenericInstanceHashCode(hashCode);
        }
    }
}

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Internal.TypeSystem
{
    /// <summary>
    /// Type with metadata available that is equivalent to a TypeDef record in an ECMA 335 metadata stream.
    /// </summary>
    public abstract partial class MetadataType : DefType
    {
        public override bool HasStaticConstructor
        {
            get
            {
                return GetStaticConstructor() != null;
            }
        }

        /// <summary>
        /// Gets metadata that controls instance layout of this type.
        /// </summary>
        public abstract ClassLayoutMetadata GetClassLayout();

        /// <summary>
        /// If true, the type layout is dictated by the explicit layout rules provided.
        /// Corresponds to the definition of explicitlayout semantic defined in the ECMA-335 specification.
        /// </summary>
        public abstract bool IsExplicitLayout { get; }

        /// <summary>
        /// If true, the order of the fields needs to be preserved. Corresponds to the definition
        /// of sequentiallayout semantic defined in the ECMA-335 specification.
        /// </summary>
        public abstract bool IsSequentialLayout { get; }

        /// <summary>
        /// If true, the type initializer of this type has a relaxed semantic. Corresponds
        /// to the definition of beforefieldinit semantic defined in the ECMA-335 specification.
        /// </summary>
        public abstract bool IsBeforeFieldInit { get; }

        /// <summary>
        /// If true, this is the special &lt;Module&gt; type that contains the definitions
        /// of global fields and methods in the module.
        /// </summary>
        public abstract bool IsModuleType { get; }

        /// <summary>
        /// Same as <see cref="TypeDesc.BaseType"/>, but the result is a MetadataType (avoids casting).
        /// </summary>
        public abstract MetadataType MetadataBaseType { get; }

        /// <summary>
        /// If true, the type cannot be used as a base type of any other type.
        /// </summary>
        public abstract bool IsSealed { get; }

        /// <summary>
        /// Returns true if the type has given custom attribute.
        /// </summary>
        public abstract bool HasCustomAttribute(string attributeNamespace, string attributeName);

        /// <summary>
        /// Adapt current to the <paramref name="typeInstantiation"/> context. For example, if you have C <T> and a
        /// context [int], it will yield C <int>.
        /// </summary>
        /// <param name="typeInstantiation">Context</param>
        /// <param name="methodInstantiation">Not used for types.</param>
        /// <returns></returns>
        public override TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            // Type is either not generic, or no valid context is provided.
            if (!HasInstantiation || typeInstantiation.IsNull)
            {
                return this;
            }
            else
            {
                return new InstantiatedType(this, typeInstantiation);
            }
        }
    }

    public struct ClassLayoutMetadata
    {
        public int PackingSize;
        public int Size;
        public FieldAndOffset[] Offsets;
    }

    public struct FieldAndOffset
    {
        public const int InvalidOffset = -1;

        public readonly FieldDesc Field;
        public readonly int Offset;
        public FieldAndOffset(FieldDesc field, int offset)
        {
            Field = field;
            Offset = offset;
        }
    }
}

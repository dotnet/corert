// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    /// <summary>
    /// Structure that specifies how a generic dictionary lookup should be performed.
    /// </summary>
    public struct GenericDictionaryLookup
    {
        private const short UseHelperOffset = -1;

        private short _offset1;
        private short _offset2;

        public readonly GenericContextSource ContextSource;

        public bool UseHelper
        {
            get
            {
                return _offset1 == UseHelperOffset;
            }
        }

        public int NumberOfIndirections
        {
            get
            {
                Debug.Assert(!UseHelper);
                return ContextSource == GenericContextSource.MethodParameter ? 1 : 2;
            }
        }

        public int this[int index]
        {
            get
            {
                Debug.Assert(!UseHelper);
                Debug.Assert(index < NumberOfIndirections);
                switch (index)
                {
                    case 0:
                        return _offset1;
                    case 1:
                        return _offset2;
                }

                // Should be unreachable.
                throw new NotSupportedException();
            }
        }

        private GenericDictionaryLookup(GenericContextSource contextSource, int offset1, int offset2)
        {
            ContextSource = contextSource;
            _offset1 = checked((short)offset1);
            _offset2 = checked((short)offset2);
        }

        public static GenericDictionaryLookup CreateFixedLookup(GenericContextSource contextSource, int offset1, int offset2 = UseHelperOffset)
        {
            Debug.Assert(offset1 != UseHelperOffset);
            return new GenericDictionaryLookup(contextSource, offset1, offset2);
        }

        public static GenericDictionaryLookup CreateHelperLookup(GenericContextSource contextSource)
        {
            return new GenericDictionaryLookup(contextSource, UseHelperOffset, UseHelperOffset);
        }
    }

    /// <summary>
    /// Specifies to source of the generic context.
    /// </summary>
    public enum GenericContextSource
    {
        /// <summary>
        /// Generic context is specified by a hidden parameter that has a method dictionary.
        /// </summary>
        MethodParameter,

        /// <summary>
        /// Generic context is specified by a hidden parameter that has a type.
        /// </summary>
        TypeParameter,

        /// <summary>
        /// Generic context is specified implicitly by the `this` object.
        /// </summary>
        ThisObject,
    }
}

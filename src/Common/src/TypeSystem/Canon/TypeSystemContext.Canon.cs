// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Interlocked = System.Threading.Interlocked;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    // Includes canonicalization objects local to a particular context
    public partial class TypeSystemContext
    {
        private CanonType _canonType = null;
        /// <summary>
        /// Instance of System.__Canon for this context
        /// </summary>
        public CanonBaseType CanonType
        {
            get
            {
                if (_canonType == null)
                {
                    Interlocked.CompareExchange(ref _canonType, new CanonType(this), null);
                }
                return _canonType;
            }
        }

        private UniversalCanonType _universalCanonType = null;
        /// <summary>
        /// Instance of System.__UniversalCanon for this context
        /// </summary>
        public CanonBaseType UniversalCanonType
        {
            get
            {
                if (_universalCanonType == null)
                {
                    Interlocked.CompareExchange(ref _universalCanonType, new UniversalCanonType(this), null);
                }
                return _universalCanonType;
            }
        }

        public bool IsCanonicalDefinitionType(TypeDesc type, CanonicalFormKind kind)
        {
            if (kind == CanonicalFormKind.Any)
            {
                return type == CanonType || type == UniversalCanonType;
            }
            else if (kind == CanonicalFormKind.Specific)
            {
                return type == CanonType;
            }
            else
            {
                Debug.Assert(kind == CanonicalFormKind.Universal);
                return type == UniversalCanonType;
            }
        }
    }
}
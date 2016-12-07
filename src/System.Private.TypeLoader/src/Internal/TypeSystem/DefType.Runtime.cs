// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.NativeFormat;
using Internal.Runtime.TypeLoader;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Internal.TypeSystem
{
    // Includes functionality for runtime type loading
    public partial class DefType
    {
        internal const int MaximumAlignmentPossible = 8;

        public override IEnumerable<FieldDesc> GetFields()
        {
            // TODO davidwr! This isn't right for types with metadata, although it will work for now.
            // Right now it serves to verify that none of our loading paths for the template type loader use it.
            throw new NotImplementedException();
        }

        internal IEnumerable<FieldDesc> GetDiagnosticFields()
        {
            if (HasNativeLayout)
            {
                // Universal template fields get diagnostic info, but normal canon templates do not
                if (IsTemplateUniversal())
                {
                    NativeLayoutFieldAlgorithm.EnsureFieldLayoutLoadedForGenericType(this);
                    return NativeLayoutFields;
                }
                return FieldDesc.EmptyFields;
            }
            else
            {
                // This will only happen for fully formed metadata based loads...
                return GetFields();
            }
        }

        public FieldDesc GetFieldByNativeLayoutOrdinal(uint ordinal)
        {
            NativeLayoutFieldAlgorithm.EnsureFieldLayoutLoadedForGenericType(this);
            return NativeLayoutFields[ordinal];
        }

        public virtual bool HasNativeLayout
        {
            get
            {
                // Attempt to compute the template, if there isn't one, then there isn't native layout
                return ComputeTemplate(false) != null;
            }
        }
    }
}
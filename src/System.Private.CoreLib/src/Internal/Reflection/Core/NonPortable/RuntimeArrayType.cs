// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Diagnostics;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This class represents an array (zero lower bounds).
    //
    internal abstract class RuntimeArrayType : RuntimeHasElementType
    {
        protected RuntimeArrayType(bool multiDim, int rank)
            : base()
        {
            _multiDim = multiDim;
            Rank = rank;
        }

        protected RuntimeArrayType(RuntimeType runtimeElementType, bool multiDim, int rank)
            : base(runtimeElementType)
        {
            _multiDim = multiDim;
            Rank = rank;
        }

        public sealed override bool IsArray
        {
            get
            {
                return true;
            }
        }

        public sealed override int GetArrayRank()
        {
            return Rank;
        }

        protected sealed override String Suffix
        {
            get
            {
                if (_multiDim)
                {
                    if (Rank == 1)
                        return "[*]";
                    else
                        return "[" + new string(',', Rank - 1) + "]";
                }
                else
                {
                    return "[]";
                }
            }
        }

        public sealed override bool InternalIsMultiDimArray
        {
            get
            {
                return _multiDim;
            }
        }

        protected int Rank { get; private set; }

        private bool _multiDim;
    }
}



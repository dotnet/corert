// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Reflection
{
    public partial struct ParameterModifier
    {
        public ParameterModifier(int parameterCount)
        {
            if (parameterCount <= 0)
                throw new ArgumentException(SR.Arg_ParmArraySize);

            _byRef = new bool[parameterCount];
        }

        public bool this[int index]
        {
            get
            {
                return _byRef[index];
            }
            set
            {
                _byRef[index] = value;
            }
        }

        private readonly bool[] _byRef;
    }
}

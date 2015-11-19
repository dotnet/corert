// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    class RvaFieldData
    {
        private Compilation _compilation;

        public FieldDesc Field { get; private set; }

        public byte[] Data
        {
            get
            {
                return ((EcmaField)Field).GetFieldRvaData();
            }
        }

        public string MangledName
        {
            get
            {
                return _compilation.NameMangler.GetMangledFieldName(Field);
            }
        }

        public RvaFieldData(Compilation compilation, FieldDesc field)
        {
            Debug.Assert(field.HasRva);
            Field = field;
            _compilation = compilation;
        }
    }
}

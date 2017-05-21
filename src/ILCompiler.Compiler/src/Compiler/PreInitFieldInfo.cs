// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Debug = System.Diagnostics.Debug;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public class PreInitFieldInfo
    {
        public FieldDesc Field { get; }

        /// <summary>
        /// Points to the underlying contents of the data.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Number of elements, if this is a frozen array.
        /// </summary>
        public int Length { get; }

        public PreInitFieldInfo(FieldDesc field, byte[] data, int length)
        {
            Field = field;
            Data = data;
            Length = length;
        }

        public static List<PreInitFieldInfo> GetPreInitFieldInfos(TypeDesc type)
        {
            List<PreInitFieldInfo> list = null;

            foreach (var field in type.GetFields())
            {
                if (!field.IsStatic)
                    continue;

                var dataField = GetPreInitDataField(field);
                if (dataField != null)
                {
                    if (list == null)
                        list = new List<PreInitFieldInfo>();
                    list.Add(ConstructPreInitFieldInfo(field, dataField));
                }
            }

            return list;
        }

        /// <summary>
        /// Retrieves the corresponding static preinitialized data field by looking at various attributes
        /// </summary>
        private static FieldDesc GetPreInitDataField(FieldDesc thisField)
        {
            Debug.Assert(thisField.IsStatic);

            var field = thisField as EcmaField;
            if (field == null)
                return null;

            if (!field.HasCustomAttribute("System.Runtime.CompilerServices", "PreInitializedAttribute"))
                return null;

            var decoded = field.GetDecodedCustomAttribute("System.Runtime.CompilerServices", "InitDataBlobAttribute");
            if (decoded == null)
                return null;

            var decodedValue = decoded.Value;
            if (decodedValue.FixedArguments.Length != 2)
                return null;

            var typeDesc = decodedValue.FixedArguments[0].Value as TypeDesc;
            if (typeDesc == null)
                return null;

            if (decodedValue.FixedArguments[1].Type != field.Context.GetWellKnownType(WellKnownType.String))
                return null;

            var fieldName = (string)decodedValue.FixedArguments[1].Value;
            return typeDesc.GetField(fieldName);
        }

        /// <summary>
        /// Extract preinitialize data as byte[] from a RVA field, and perform necessary validations.
        /// </summary>
        private static PreInitFieldInfo ConstructPreInitFieldInfo(FieldDesc field, FieldDesc dataField)
        {
            var arrType = field.FieldType as ArrayType;
            if (arrType == null || !arrType.IsSzArray)
            {
                // We only support single dimensional arrays
                throw new NotSupportedException();
            }

            if (!dataField.HasRva)
                throw new BadImageFormatException();
            
            var ecmaDataField = dataField as EcmaField;
            if (ecmaDataField == null)
                throw new NotSupportedException();
            
            var rvaData = ecmaDataField.GetFieldRvaData();
            int elementSize = arrType.ElementType.GetElementSize().AsInt;
            if (rvaData.Length % elementSize != 0)
                throw new BadImageFormatException();

            return new PreInitFieldInfo(field, rvaData, rvaData.Length / elementSize);
        }
    }
}

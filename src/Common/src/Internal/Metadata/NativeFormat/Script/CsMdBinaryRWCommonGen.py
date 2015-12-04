# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

"""
Generates the C# classes MdBinaryWriterGen.cs and MdBinaryReaderGen.cs, which define the classes
MdBinaryReader and MdBinaryWriter. These classes are responsible for correctly encoding and decoding
data members into the .metadata file. See NativeFormatReaderGen.cs and NativeFormatWriterGen.cs for
how the MetadataReader and MetadataWriter use these classes.
"""

import sys
import os

if __name__ == '__main__':
    #Dynamically append current script path to PYTHONPATH
    sys.path.append(os.path.dirname(sys.argv[0]))

import SchemaDef2 as sd
from CsCommonGen2 import *
from CsNativeFormatGen2 import *
from odict import odict

#==========================================================================================================
class MdBinaryRWGen(object):
    #------------------------------------------------------------------------------------------------------
    def __init__(self):
        self.Ty = TypeContainer()
        PublishWellKnownTypes(self.Ty)
        self.recs = odict([(rName, self.Ty.get(rName)) for rName in sd.recordSchema.iterkeys()])
        self.hnds = odict([(hName + 'Handle', self.Ty.get(hName + 'Handle')) for hName in sd.handleSchema])
        for rec in self.recs.itervalues():
            rec.handle = self.hnds[str(rec) + 'Handle']
        self.Ty.NativeReader = ClassDef('NativeReader')
        self.Ty.NativeWriter = ClassDef('NativeWriter')

    #------------------------------------------------------------------------------------------------------
    def CreateMdBinaryReaderClass(self):

        c = ClassDef(
            'MdBinaryReader',
            flags = AccessFlags.Internal | TypeFlags.Partial | TypeFlags.Static)

        def GenArrayReadMethodWithOffset(typeName):
            return MethodDef(
                'Read',
                sig = [self.Ty.uint, [(self.Ty.NativeReader, 'reader'), (self.Ty.uint, 'offset'), ('out ' + str(typeName) + '[]', 'values')]],
                flags = AccessFlags.Public | MemberFlags.Extension,
                body = '''
uint count;
offset = reader.DecodeUnsigned(offset, out count);
values = new {0}[count];
for (uint i = 0; i < count; ++i)
{{
    {0} tmp;
    offset = reader.Read(offset, out tmp);
    values[i] = tmp;
}}
return offset;'''.format(typeName))

        def GenArrayReadMethodWithOffset_Empty(typeName):
            return MethodDef(
                'Read',
                sig = [self.Ty.uint, [(self.Ty.NativeReader, 'reader'), (self.Ty.uint, 'offset'), ('out ' + str(typeName) + '[]', 'values')]],
                flags = AccessFlags.Public | MemberFlags.Extension,
                body = '''
uint count;
offset = reader.DecodeUnsigned(offset, out count);
if (count == 0)
{{
    values = s_empty{0}Array;
}}
else
{{
    values = new {0}[count];
    for (uint i = 0; i < count; ++i)
    {{
        {0} tmp;
        offset = reader.Read(offset, out tmp);
        values[i] = tmp;
    }}
}}
return offset;'''.format(typeName))

        for pCsType,pType in sd.primitiveTypes.iteritems():
            c.members.add(GenArrayReadMethodWithOffset(pCsType))

        hType = self.Ty.Handle
        c.members.add(GenArrayReadMethodWithOffset_Empty(hType))
        c.members.add(EmptyArrayDef('s_empty' + str(hType) + 'Array', hType, AccessFlags.Private))

        for eType in map(lambda eName: self.Ty[eName], sd.enumTypes.iterkeys()):

            c.members.add(MethodDef(
                'Read',
                sig = [self.Ty.uint, [(self.Ty.NativeReader, 'reader'), (self.Ty.uint, 'offset'), ('out ' + str(eType), 'value')]],
                flags = AccessFlags.Public | MemberFlags.Extension,
                body = '''
uint ivalue;
offset = reader.DecodeUnsigned(offset, out ivalue);
value = ({0})ivalue;
return offset;'''.format(eType, eType.underlyingType)))

            # c.members.add(GenArrayReadMethodWithOffset(eType))

        for hType in map(lambda hnd: hnd + 'Handle', sd.handleSchema):

            c.members.add(MethodDef(
                'Read',
                sig = [self.Ty.uint, [(self.Ty.NativeReader, 'reader'), (self.Ty.uint, 'offset'), ('out ' + hType, 'handle')]],
                flags = AccessFlags.Public | MemberFlags.Extension,
                body = '''
uint value;
offset = reader.DecodeUnsigned(offset, out value);
handle = new {}((int)value);
handle._Validate();
return offset;'''.format(hType)))

            c.members.add(GenArrayReadMethodWithOffset_Empty(hType))
            c.members.add(EmptyArrayDef('s_empty' + str(hType) + 'Array', hType, AccessFlags.Private))

        return c

    #------------------------------------------------------------------------------------------------------
    def CreateMdBinaryWriterClass(self):
        c = self.Ty.MdBinaryWriter = ClassDef(
            'MdBinaryWriter',
            flags = AccessFlags.Internal | TypeFlags.Partial | TypeFlags.Static)

        def GenArrayWriteMethod(typeName):
            return MethodDef(
                'Write',
                sig = [self.Ty.void, [(self.Ty.NativeWriter, 'writer'), ('IEnumerable<' + str(typeName) + '>', 'values')]],
                flags = AccessFlags.Public | MemberFlags.Extension,
                body = '''
if (values == null)
{{
    writer.WriteUnsigned(0);
    return;
}}
writer.WriteUnsigned((uint)values.Count());
foreach ({0} value in values)
{{
    writer.Write(value);
}}'''.format(typeName))

        for pType in sd.primitiveTypes.iterkeys():
            c.members.add(GenArrayWriteMethod(pType))

        for eType in map(lambda (eName, eType): self.Ty.get(eName, EnumDef(eName, underlyingType = self.Ty[eType])), sd.enumTypes.iteritems()):
            c.members.add(MethodDef(
                'Write',
                sig = [self.Ty.void, [(self.Ty.NativeWriter, 'writer'), (eType, 'value')]],
                flags = AccessFlags.Public | MemberFlags.Extension,
                body = 'writer.WriteUnsigned((uint)value);'))

        c.members.add(GenArrayWriteMethod('MetadataRecord'))

        for rec in self.recs.itervalues():
            c.members.add(MethodDef(
                'Write',
                sig = [self.Ty.void, [(self.Ty.NativeWriter, 'writer'), (str(rec), 'record')]],
                flags = AccessFlags.Public | MemberFlags.Extension,
                body = '''
if (record != null)
    writer.WriteUnsigned((uint)record.Handle.Offset);
else
    writer.WriteUnsigned(0);
'''.format(rec.handle)))

            c.members.add(GenArrayWriteMethod(str(rec)))

        return c

    #------------------------------------------------------------------------------------------------------
    def CsEmitSource(self):
        # Source MdBinaryReaderGen.cs
        with open(r'MdBinaryReaderGen.cs', 'w') as output :
            ns = NamespaceDef('Internal.Metadata.NativeFormat')
            ns.members.add(self.CreateMdBinaryReaderClass())

            iprint = IPrint(output)
            CsEmitFileHeader(iprint)
            iprint('#pragma warning disable 649')
            iprint()
            iprint('using System;')
            iprint('using System.IO;')
            iprint('using System.Collections.Generic;')
            iprint('using System.Reflection;')
            iprint('using Internal.NativeFormat;')
            iprint('using Debug = System.Diagnostics.Debug;')
            iprint()

            ns.CsDefine(iprint)

        # Source MdBinaryWriterGen.cs
        with open(r'..\..\..\..\..\ILCompiler.MetadataWriter\src\Internal\Metadata\NativeFormat\Writer\MdBinaryWriterGen.cs', 'w') as output :
            ns = NamespaceDef('Internal.Metadata.NativeFormat.Writer')
            ns.members.add(self.CreateMdBinaryWriterClass())

            iprint = IPrint(output)
            CsEmitFileHeader(iprint)
            iprint('#pragma warning disable 649')
            iprint()
            iprint('using System;')
            iprint('using System.IO;')
            iprint('using System.Linq;')
            iprint('using System.Collections.Generic;')
            iprint('using System.Reflection;')
            iprint('using Internal.NativeFormat;')
            iprint('using Debug = System.Diagnostics.Debug;')
            iprint()

            ns.CsDefine(iprint)

#==========================================================================================================
if __name__ == '__main__':
    MdBinaryRWGen().CsEmitSource()

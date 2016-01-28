# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""
Template for consuming SchemaDef2 module contents and generating scenario-specific C# code.
"""

import sys
import os
import copy
import itertools

if __name__ == '__main__':
    #Dynamically append current script path to PYTHONPATH
    sys.path.append(os.path.dirname(sys.argv[0]))

from odict import odict
import SchemaDef2 as sd
from CsCommonGen2 import *
from CsNativeFormatGen2 import *

#==========================================================================================================
class GenTemplate(object):
    #==========================================================================================================
    def __init__(self, sourceFile, rootNamespace):
        self.sourceFile = sourceFile
        self.rootNamespace = rootNamespace

    #==========================================================================================================
    def CreateRecordMembers(self, rec):
        pass

    #==========================================================================================================
    def CreateHandleMembers(self, hnd):
        pass

    #==========================================================================================================
    def CsEmitSource(self):
        self.Ty = TypeContainer()
        PublishWellKnownTypes(self.Ty)

        ns = NamespaceDef(self.rootNamespace)

        recs = odict()
        hnds = odict()
        
        self.Ty.Handle = hnds['Handle'] = StructDef('Handle', flags = TypeFlags.Partial)
        ns.members.add(self.Ty.Handle)
        
        for (rName,rMembers) in sd.recordSchema.iteritems():
            rec = recs[rName] = StructDef(rName, flags = TypeFlags.Partial)
            hnd = hnds[rName + 'Handle'] = StructDef(rName + 'Handle', flags = TypeFlags.Partial)
            rec.handle = hnd
            rec.schemaMembers = rMembers
            hnd.record = rec
            ns.members.add(rec)
            ns.members.add(hnd)
            
        for hName in sd.handleSchema:
            if hName + 'Handle' not in hnds:
                hnd = hnds[hName + 'Handle'] = StructDef(hName + 'Handle', flags = TypeFlags.Partial)
                hnd.record = None
                ns.members.add(hnd)
                
        for rec in recs.itervalues():
            self.CreateRecordMembers(rec)

        for hnd in hnds.itervalues():
            self.CreateHandleMembers(hnd)

        # Source NativeFormatReaderHashEqualGen.cs
        with open(self.sourceFile, 'w') as output :
            iprint = IPrint(output)
            CsEmitFileHeader(iprint)
            iprint('using System;')
            iprint('using System.Linq;')
            iprint('using System.Reflection;')
            iprint('using System.Reflection.Metadata;')
            iprint('using System.Collections.Generic;')
            iprint()

            ns.CsDefine(iprint)

if __name__ == '__main__':
    GenTemplate(r'System\Reflection\Metadata\TemplateGen.cs', r'System.Reflection.Metadata.Template').CsEmitSource()

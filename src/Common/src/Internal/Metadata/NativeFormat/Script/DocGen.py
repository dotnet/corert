# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""
Quick script used to generate documentation-like output for all of the metadata records
defined in SchemaDef2.py
"""
import sys
import os
import re

if __name__ == '__main__':
    #Dynamically append current script path to PYTHONPATH
    sys.path.append(os.path.dirname(sys.argv[0]))

import SchemaDef2 as sd

allConstantHandles = list()
allConstantValueHandles = list()
allConstantArrayHandles = list()
for record in sd.recordDefs.itervalues():
    if record.name.startswith('Constant'):
        allConstantHandles.append(record.name + 'Handle')
        if record.name.endswith('Array'):
            allConstantArrayHandles.append(record.name + 'Handle')
        elif record.name.endswith('Value'):
            allConstantValueHandles.append(record.name + 'Handle')

for record in sorted(sd.recordDefs.itervalues(), lambda r1, r2: cmp(r1.name, r2.name)):
    sys.stdout.write(record.name)
    # members = filter(lambda member: member.name != 'Value', record.members)
    members = record.members
    # if len(members) == 1:
        # print ' {{ {} }}'.format(member.name)
    # elif len(members) > 1:
    if len(members) > 0:
        print ' {'
        for member in members:
            typeNamePrefix = ''
            typeNameSuffix = ''
            if member.flags.IsRef() and member.typeName != 'Handle':
                typeNameSuffix += 'Handle'
            if type(member.typeName) == str:
                typeName = member.typeName + typeNameSuffix
            else:
                constants = list()
                others = list()
                types = sorted(map(lambda name: typeNamePrefix + name + typeNameSuffix, member.typeName))
                for t in types:
                    m = re.match(r'Constant(.*)(Array|Value)Handle', t)
                    if m:
                        constants.append(t)
                    else:
                        others.append(t)
                typeName = ' { \n' + ' ' * 8
                typeName += (' |\n' + ' ' * 8).join(others)
                if constants == allConstantHandles:
                    typeName += (' |\n' + ' ' * 8) + 'Constant*ValueHandle'
                    typeName += (' |\n' + ' ' * 8) + 'Constant*ArrayHandle'
                elif constants == allConstantValueHandles:
                    typeName += (' |\n' + ' ' * 8) + 'Constant*ValueHandle'
                elif constants == allConstantArrayHandles:
                    typeName += (' |\n' + ' ' * 8) + 'Constant*ArrayHandle'
                typeName += '\n' + ' ' * 4 + '}'
            if member.flags.IsSequence():
                typeName += '[]'
            if member.flags.IsMap():
                typeName = 'Dictionary<string, ' + typeName + '>'
            print '{}{} : {}'.format(' ' * 4, member.name, typeName)
        if sorted(constants) == sorted(allConstantHandles):
            print '{}{} : {}'.format(' ' * 4, member.name, typeName)
        print '}'
    

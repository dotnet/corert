# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""
Provides an ordered dictionary (odict) that can be used as a standard dict but will also
enumerate the values and keys in the order in which they were added.
"""

def _isiterable(obj):
    if type(obj) == str:
        return False
    try:
        iter(obj)
    except TypeError:
        return False
    return True

class odict(dict):
    def __ParseArg(self, arg):
        if isinstance(arg, odict):
            for k,v in arg.iteritems():
                self[k] = v
        if isinstance(arg, dict):
            for k in sorted(arg.iterkeys()):
                self[k] = arg[k]
        elif isinstance(arg, tuple) and len(arg) == 2:
            k,v = arg
            self[k] = v
        elif _isiterable(arg):
            for arg2 in arg:
                self.__ParseArg(arg2)
        else:
            raise Exception('Unexpected argument {0}.'.format(arg))

    def __init__(self, *args, **kv):
        super(odict, self).__init__()
        self.__keys = []
        for arg in args:
            self.__ParseArg(arg)

    def __delitem__(self, key):
        super(odict, self).__delitem__(key)
        self.__keys.remove(key)

    def __setitem__(self, key, item):
        super(odict, self).__setitem__(key, item)
        if key not in self.__keys: self.__keys.append(key)
        return self[key]

    def clear(self):
        super(odict, self).clear()
        self.__keys = []

    def copy(self):
        new_dict = odict(super(odict, self).copy())
        new_dict.__keys = self.__keys[:]
        return new_dict

    def items(self):
        return zip(self.keys(), self.values())
        
    def iteritems(self):
        return self.items()

    def keys(self):
        return self.__keys
        
    def iterkeys(self):
        return self.keys()

    def popitem(self):
        try:
            key = self.__keys[-1]
        except IndexError:
            raise KeyError('dictionary is empty')

        val = self[key]
        del self[key]

        return (key, val)

    def setdefault(self, key, failobj = None):
        val = super(odict, self).setdefault(key, failobj)
        if key not in self.__keys: self.__keys.append(key)
        return val

    def update(self, dict):
        super(odict, self).update(dict)
        for key in super(odict, self).keys():
            if key not in self.__keys: self.__keys.append(key)

    def values(self):
        return map(self.get, self.__keys)
        
    def itervalues(self):
        return self.values()

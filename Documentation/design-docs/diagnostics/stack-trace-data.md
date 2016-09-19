# StackTrace Data

In order to support showing textual stack trace when `exception.ToString()` is called, a blob named StackTraceData is introduced. This is produced at compile time and embedded into the binary, and consumed at runtime to produce the textual string. In this document, we will talk about the design goal of the data format, and we will describe the format in detail.

# Design Goal
Beyond providing the necessary information for building up the stack trace string, one would like the format to have these characteristics, in priority order:

1. It is space efficient - the data is embedded in the binary, which mean it is memory mapped to the process image, and that take time to read from the hard disk, which impact start up time.
2. It is fast to produce - you don't want to wait for long for a debug build.
3. Consuming it requires low memory footprint, especially so if there are any memory usage that is used even when an exception is not thrown.
4. Consuming it does not take long. It is obvious why we will want this, but this is least prioritized because you have already incurred the cost of throwing an exception in the first place, as long as the speed is not too slow, we should be fine.

# Basic concepts
The overall scheme for this data is a serialized object graph. The objects are the type system objects, such as methods, types and so on. A method contains a declaring type, a collection of generic types, and a collection of argument types. A type may have a enclosing type (in the nested type scenario), a runtime type, a wrapped type (in the array/pointer/byref scenario), an a name. To further compress the names, the names are tokenized by the namespace separator dot, as well as by the camel casing. Then these tokens are arranged in a trie to make sure common prefix are shared.

## References
In the serialized representation, an object reference another object by index. For example, the declared type of the 7th method reference the 2nd type, then we just output 2 in the serialized representation of the 7th method. At runtime, we can deserialize the data easily by just reading off the 2nd row of the table when we need the declared type.

## Numbers
In the serialized representation, numbers of variable length encoded. The scheme is basically a "null" terminated integer. If a number is less than 128 (i.e. representable in 7 bits), we represent it with a byte. If not, we represent the most significant 7 bits in a byte, and set the 1st bit to 1. Then we recursively encode the remaining bits. The scheme make the representation of an object variable length, so we cannot use a simple rule to find out the offset of an object, and this is okay because at runtime we can do a single scan and build an index to these objects, and any subsequent access will be fast.

## Strings
Strings are represented as UTF-8 because code is mostly in English and therefore take only one byte per character. This incurs some runtime cost for reading the string because strings are represented as UTF-16. To avoid scanning the string when building the index, we prefix the string with its length. For most strings, this is just 1 byte and is the same cost for a null terminating character.

## Optimization
In order to reduce the number of bits used in references, we sort the object by their incoming references count. That way a lot of references will take much less space.

## Special bytes
There are some bytes that we specially designed to pack more information in one byte. An example would be the type encoding byte. We used 1 bit of it to represent whether or not it is being nested (i.e. has an enclosing type), 1 bit to represent if it has a runtime type, 3 bits to represent if it is array/byref/..., and finally the remaining 3 bits to represent the number of generic arguments. There are only two such special representation and will be documented in depth when we encounter one.

# Detailed Specification
The data begins with a magic 32 bit integer (in little endian) 0x000000C0, the number serve both as a sanity check (so that we are not reading a wrong blob) and also for versioning (for whatever reason we need to revise the format, we can change the magic number and does not break the dependency)

The data continues with the table sizes in terms of number of records, starting with the method table size, the type table size, the string table size and the node table size, all in 32 bit integers (again little endian order)

The data continues with the method table. Each method is emitted as follow:

1. Reference to the trie node that has the method name
2. Reference to the declaring type
3. A special byte that encode the number of arguments and number of generics (see below)
4. For each argument, the reference to the argument type
5. For each generic argument, the reference to the generic argument type

The special byte represents the number of arguments using the most significant 4 bits, and the number of generic arguments using the least signficant 4 bits. Suppose we have 15 or more arguments, we will encode the number of arguments - 15 after it, and then if we have more than 15 generic arguments, we will encode the number of generic arguments - 15 after it.

After all the methods are encoded, the data continues with the type table. There are actually two types of types (in terms of encoding), and we can determine that using the 1st byte.

The 1st byte is a special byte. The most significant bit represents if it is a runtime type. The next bit represents if it is a nested type so that it has an enclosing type. The next 3 bits represents the type of the type, the next 3 bits represents the number of generic arguments.

Suppose we have 7 or more generic type arguments, we will encode the number of generic type argument - 7.

The middle 3 bits is interesting, there are 5 types of type as follow:

None = 0x000
ByRef = 0x001
Ptr = 0x010
SzArray = 0x011
Array = 0x100

The none type is actually the predominant type. The rest are special. We about the none case first.

In the none case, we will encode as follow

1. Reference to the trie node that has the type name
2. Reference to the runtime type if any (i.e. if the first bit on)
3. Reference to the enclosing type if any (i.e. if the second bit on)
1. For each generic type, the reference to the generic argument type

For the other case, we will simply encode the wrapped type.

After the type table, we have the strings. The strings are simply encoded with their length as prefix.

After the string table, we have the trie nodes. The trie node is a little different because we need to be able to encode a NULL reference. any node that does not have a parent points to 0, and the trie node references start from 1

We first encode the parent reference, and then the string reference.

That's all how the encoding works!
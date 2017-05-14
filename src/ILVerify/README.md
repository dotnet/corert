# ILVerify

### This directory contains the implementation of ILVerify

## Intention of this project:
The goal is to create a standalone, cross platform, open-source tool, which is capable of verifying MSIL code based on [ECMA-335](https://www.ecma-international.org/publications/standards/Ecma-335.htm).

The main users of this tool are people working on software, which emits MSIL code. These are typically complier and profiler writers.

## Other tools
Historically on Full Framework IL generators used PEVerify to make sure that they generated correct IL. PEVErify has some major limitations (e.g. it is tied to the Full Framework, it cannot verify mscorlib.dll, etc.), which initiated this project.

## Main properties of ILVerify:
- No coupling with CoreLib: PEVErify can point to any assembly and verify it. This also includes the full framework base assemblies (especially mscorlib).
- Cross-platform, Open-Source
- It should be easy to add new verification rules
- Fast spin up/tear down. 

## How to contribute
All ILVerify issues are labeled with [area-ILVerification](https://github.com/search?utf8=%E2%9C%93&q=label%3Aarea-ILVerification&type=).

ILVerify basically runs through the IL commands in an assembly and does all the verification steps, which are specified in ECMA-335.

Currently every IL command falls into one of these categories:

 - Not implemented: the implementation is completely missing. The easiest way is to pick one of them (look for NotImplentedException in the code) and implement it. First you should 100% understand the spec. (see [ECMA-335](https://www.ecma-international.org/publications/standards/Ecma-335.htm)), then try to port an existing implementation (sources below).
 - Partially implemented: These are typically methods with TODOs in it. As the first phase we want to make sure that for every command the stack is correctly maintained, therefore for some commands we either have no verification or we have only a not complete verification. You can also pick one of these and finish it 
 - Implemented: find and fix bugs ;)  

Useful sources:
 - [PEVerify source code](https://github.com/lewischeng-ms/sscli/blob/master/clr/src/jit64/newverify.cpp)
 - [RyuJIT source code](https://github.com/dotnet/coreclr/blob/master/src/jit/jiteh.cpp)

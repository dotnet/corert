#ifndef JIT_DEBUG_INFO_H
#define JIT_DEBUG_INFO_H

typedef unsigned int DWORD;
#define TARGET_AMD64 1

#include "cordebuginfo.h"
#include "cvconst.h"
#include "llvm/DebugInfo/CodeView/SymbolRecord.h"

struct DebugLocInfo {
  int NativeOffset;
  int FileId;
  int LineNumber;
  int ColNumber;
};

struct DebugVarInfo {
  std::string Name;
  int TypeIndex;
  bool IsParam;
  std::vector<ICorDebugInfo::NativeVarInfo> Ranges;

  DebugVarInfo() {}
  DebugVarInfo(char *ArgName, int ArgTypeIndex, bool ArgIsParam)
      : Name(ArgName), TypeIndex(ArgTypeIndex), IsParam(ArgIsParam) {}
};

struct DebugEHClauseInfo {
  unsigned TryOffset;
  unsigned TryLength;
  unsigned HandlerOffset;
  unsigned HandlerLength;

  DebugEHClauseInfo(unsigned TryOffset, unsigned TryLength,
                    unsigned HandlerOffset, unsigned HandlerLength) :
                    TryOffset(TryOffset), TryLength(TryLength),
                    HandlerOffset(HandlerOffset), HandlerLength(HandlerLength) {}
};

typedef unsigned short CVRegMapping;

#define CVREGDAT(p2, cv) cv

const CVRegMapping cvRegMapAmd64[] = {
    CVREGDAT(REGNUM_RAX, CV_AMD64_RAX), CVREGDAT(REGNUM_RCX, CV_AMD64_RCX),
    CVREGDAT(REGNUM_RDX, CV_AMD64_RDX), CVREGDAT(REGNUM_RBX, CV_AMD64_RBX),
    CVREGDAT(REGNUM_RSP, CV_AMD64_RSP), CVREGDAT(REGNUM_RBP, CV_AMD64_RBP),
    CVREGDAT(REGNUM_RSI, CV_AMD64_RSI), CVREGDAT(REGNUM_RDI, CV_AMD64_RDI),
    CVREGDAT(REGNUM_R8, CV_AMD64_R8),   CVREGDAT(REGNUM_R9, CV_AMD64_R9),
    CVREGDAT(REGNUM_R10, CV_AMD64_R10), CVREGDAT(REGNUM_R11, CV_AMD64_R11),
    CVREGDAT(REGNUM_R12, CV_AMD64_R12), CVREGDAT(REGNUM_R13, CV_AMD64_R13),
    CVREGDAT(REGNUM_R14, CV_AMD64_R14), CVREGDAT(REGNUM_R15, CV_AMD64_R15)};

#endif // JIT_DEBUG_INFO_H

//===------------------------- __libunwind_config.h -----------------------===//
//
//                     The LLVM Compiler Infrastructure
//
// This file is dual licensed under the MIT and the University of Illinois Open
// Source Licenses. See LICENSE.TXT for details.
//
//===----------------------------------------------------------------------===//

#ifndef ____LIBUNWIND_CONFIG_H__
#define ____LIBUNWIND_CONFIG_H__

#if defined(__arm__) && !defined(__USING_SJLJ_EXCEPTIONS__) && \
    !defined(__ARM_DWARF_EH__)
#define _LIBUNWIND_ARM_EHABI 1
#else
#define _LIBUNWIND_ARM_EHABI 0
#endif

#if defined(_LIBUNWIND_IS_NATIVE_ONLY)
# if defined(__i386__)
#  define _LIBUNWIND_TARGET_I386 1
#  define _LIBUNWIND_CONTEXT_SIZE 13
#  define _LIBUNWIND_CURSOR_SIZE 23
#  define _LIBUNWIND_HIGHEST_DWARF_REGISTER 9
# elif defined(__x86_64__)
#  define _LIBUNWIND_TARGET_X86_64 1
#  define _LIBUNWIND_CONTEXT_SIZE 38
#  define _LIBUNWIND_CURSOR_SIZE 50
#  define _LIBUNWIND_HIGHEST_DWARF_REGISTER 17
# elif defined(__ppc__)
#  define _LIBUNWIND_TARGET_PPC 1
#  define _LIBUNWIND_CONTEXT_SIZE 117
#  define _LIBUNWIND_CURSOR_SIZE 128
#  define _LIBUNWIND_HIGHEST_DWARF_REGISTER 113
# elif defined(__aarch64__)
#  define _LIBUNWIND_TARGET_AARCH64 1
#  define _LIBUNWIND_CONTEXT_SIZE 100
#  define _LIBUNWIND_CURSOR_SIZE 112
#  define _LIBUNWIND_HIGHEST_DWARF_REGISTER 96
# elif defined(__arm__)
#  define _LIBUNWIND_TARGET_ARM 1
#  if defined(__ARM_WMMX)
#    define _LIBUNWIND_CONTEXT_SIZE 76
#    define _LIBUNWIND_CURSOR_SIZE 83
#  else
#    define _LIBUNWIND_CONTEXT_SIZE 50
#    define _LIBUNWIND_CURSOR_SIZE 57
#  endif
#  define _LIBUNWIND_HIGHEST_DWARF_REGISTER 96
# elif defined(__or1k__)
#  define _LIBUNWIND_TARGET_OR1K 1
#  define _LIBUNWIND_CONTEXT_SIZE 16
#  define _LIBUNWIND_CURSOR_SIZE 28
#  define _LIBUNWIND_HIGHEST_DWARF_REGISTER 32
# elif defined (_WASM_)
#  define _LIBUNWIND_TARGET_WASM 1
// TODO: Determine the right values
#  define _LIBUNWIND_CONTEXT_SIZE 0xbadf00d
#  define _LIBUNWIND_CURSOR_SIZE 0xbadf00d
# else
#  error "Unsupported architecture."
# endif
#else // !_LIBUNWIND_IS_NATIVE_ONLY
# define _LIBUNWIND_TARGET_I386 1
# define _LIBUNWIND_TARGET_X86_64 1
# define _LIBUNWIND_TARGET_PPC 1
# define _LIBUNWIND_TARGET_AARCH64 1
# define _LIBUNWIND_TARGET_ARM 1
# define _LIBUNWIND_TARGET_OR1K 1
# define _LIBUNWIND_CONTEXT_SIZE 128
# define _LIBUNWIND_CURSOR_SIZE 140
# define _LIBUNWIND_HIGHEST_DWARF_REGISTER 120
#endif // _LIBUNWIND_IS_NATIVE_ONLY

#endif // ____LIBUNWIND_CONFIG_H__

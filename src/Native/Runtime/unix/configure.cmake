include(CheckCXXSourceCompiles)
include(CheckCXXSourceRuns)
include(CheckCXXSymbolExists)
include(CheckFunctionExists)
include(CheckIncludeFiles)
include(CheckStructHasMember)
include(CheckTypeSize)
include(CheckLibraryExists)

if(CMAKE_SYSTEM_NAME STREQUAL FreeBSD)
  set(CMAKE_REQUIRED_INCLUDES /usr/local/include)
elseif(NOT CMAKE_SYSTEM_NAME STREQUAL Darwin)
  set(CMAKE_REQUIRED_DEFINITIONS "-D_BSD_SOURCE -D_SVID_SOURCE -D_DEFAULT_SOURCE -D_POSIX_C_SOURCE=200809L")
endif()

list(APPEND CMAKE_REQUIRED_DEFINITIONS -D_FILE_OFFSET_BITS=64)

check_include_files(sys/vmparam.h HAVE_SYS_VMPARAM_H)
check_include_files(mach/vm_types.h HAVE_MACH_VM_TYPES_H)
check_include_files(mach/vm_param.h HAVE_MACH_VM_PARAM_H)
check_include_files(libunwind.h HAVE_LIBUNWIND_H)

check_library_exists(pthread pthread_attr_get_np "" HAVE_PTHREAD_ATTR_GET_NP)
check_library_exists(pthread pthread_getattr_np "" HAVE_PTHREAD_GETATTR_NP)
check_library_exists(pthread pthread_condattr_setclock "" HAVE_PTHREAD_CONDATTR_SETCLOCK)
check_library_exists(pthread pthread_getthreadid_np "" HAVE_PTHREAD_GETTHREADID_NP)

check_function_exists(clock_nanosleep HAVE_CLOCK_NANOSLEEP)
check_function_exists(sysctl HAVE_SYSCTL)
check_function_exists(sysconf HAVE_SYSCONF)

set(CMAKE_REQUIRED_LIBRARIES unwind unwind-generic)
check_cxx_source_compiles("
#include <libunwind.h>

int main(int argc, char **argv) {
  unw_cursor_t cursor;
  unw_save_loc_t saveLoc;
  int reg = UNW_REG_IP;
  unw_get_save_loc(&cursor, reg, &saveLoc);

  return 0;
}" HAVE_UNW_GET_SAVE_LOC)
set(CMAKE_REQUIRED_LIBRARIES)

check_struct_has_member ("ucontext_t" uc_mcontext.gregs[0] ucontext.h HAVE_GREGSET_T)
check_struct_has_member ("ucontext_t" uc_mcontext.__gregs[0] ucontext.h HAVE___GREGSET_T)

set(CMAKE_EXTRA_INCLUDE_FILES)
set(CMAKE_EXTRA_INCLUDE_FILES signal.h)
check_type_size(siginfo_t SIGINFO_T)
set(CMAKE_EXTRA_INCLUDE_FILES)
set(CMAKE_EXTRA_INCLUDE_FILES ucontext.h)
check_type_size(ucontext_t UCONTEXT_T)

check_cxx_source_compiles("
#include <libunwind.h>
#include <ucontext.h>

int main(int argc, char **argv)
{
        unw_context_t libUnwindContext;
        ucontext_t uContext;

        libUnwindContext = uContext;
        return 0;
}" UNWIND_CONTEXT_IS_UCONTEXT_T)

check_cxx_symbol_exists(_SC_PHYS_PAGES unistd.h HAVE__SC_PHYS_PAGES)
check_cxx_symbol_exists(_SC_AVPHYS_PAGES unistd.h HAVE__SC_AVPHYS_PAGES)

check_cxx_source_compiles("
#include <lwp.h>

int main(int argc, char **argv)
{
    return (int)_lwp_self();
}" HAVE_LWP_SELF)

set(CMAKE_REQUIRED_LIBRARIES pthread)
check_cxx_source_runs("
#include <stdlib.h>
#include <sched.h>

int main(void)
{
  if (sched_getcpu() >= 0)
  {
    exit(0);
  }
  exit(1);
}" HAVE_SCHED_GETCPU)
set(CMAKE_REQUIRED_LIBRARIES)

check_cxx_source_runs("
#include <stdlib.h>
#include <time.h>
#include <sys/time.h>

int main()
{
  int ret;
  struct timespec ts;
  ret = clock_gettime(CLOCK_MONOTONIC, &ts);

  exit(ret);
}" HAVE_CLOCK_MONOTONIC)

check_cxx_source_runs("
#include <stdlib.h>
#include <time.h>
#include <sys/time.h>

int main()
{
  int ret;
  struct timespec ts;
  ret = clock_gettime(CLOCK_MONOTONIC_COARSE, &ts);

  exit(ret);
}" HAVE_CLOCK_MONOTONIC_COARSE)

check_cxx_source_runs("
#include <stdlib.h>
#include <mach/mach_time.h>

int main()
{
  int ret;
  mach_timebase_info_data_t timebaseInfo;
  ret = mach_timebase_info(&timebaseInfo);
  mach_absolute_time();
  exit(ret);
}" HAVE_MACH_ABSOLUTE_TIME)

if(NOT HAVE_LIBUNWIND_H)
  unset(HAVE_LIBUNWIND_H CACHE)
  message(FATAL_ERROR "Cannot find libunwind. Try installing libunwind8 and libunwind8-dev (or the appropriate packages for your platform)")
endif()

configure_file(${CMAKE_CURRENT_LIST_DIR}/config.h.in ${CMAKE_CURRENT_BINARY_DIR}/config.h)

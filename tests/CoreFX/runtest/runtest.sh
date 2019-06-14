# Exit code constants
readonly EXIT_CODE_SUCCESS=0       # Script ran normally.
readonly EXIT_CODE_EXCEPTION=1     # Script exited because something exceptional happened (e.g. bad arguments, Ctrl-C interrupt).
readonly EXIT_CODE_TEST_FAILURE=2  # Script completed successfully, but one or more tests failed.

function print_usage {
    echo ''
    echo 'CoreFX test runner script.'
    echo ''
    echo 'Typical command line:'
    echo ''
    echo 'corefx/tests/runtest.sh'
    echo '    --testRootDir="tests_downloaded/CoreFX"'
    echo '    --testLauncher="tests/CoreFX/corerun"'    
    echo ''
    echo 'Required arguments:'
    echo '  --testRootDir=<path>             : Root directory of the CoreFX test build '
    echo '  --testLauncher=<path>            : Path to the test launcher script'
}

function run_tests_in_directory {
    local savedErroLevel=$EXIT_CODE_SUCCESS
    local testDir=$1
    for testSubDir in ${testDir}/* ; do
      # Build and run each test
      echo Building ${testSubDir}
      ${FXCustomTestLauncher} ${testSubDir} ${__LogDir}
      if [ $? != 0 ]; 
      then 
        savedErroLevel=$EXIT_CODE_TEST_FAILURE
      fi 
    done
    return $savedErroLevel
    
}



# Argument variables
testRootDir=
__LogDir=
FXCustomTestLauncher=

for i in "$@"
do
    case $i in
        --testRootDir=*)
            testRootDir=${i#*=}
            ;;
        --logdir=*)
            __LogDir=${i#*=}
            ;;
        --testLauncher=*)
            FXCustomTestLauncher=${i#*=}
            ;;
        *)
            echo "Unknown switch: $i"
            print_usage
            exit $EXIT_CODE_SUCCESS
            ;;
    esac
done

if [ -z "$testRootDir" ]; then
    echo "--testRootDir is required."
    print_usage
    exit $EXIT_CODE_EXCEPTION
fi
if [ ! -d "$testRootDir" ]; then
    echo "Directory specified by --testRootDir does not exist: $testRootDir"
    exit $EXIT_CODE_EXCEPTION
fi

if [ -z "$__LogDir" ]; then
    __LogDir=$testRootDir
fi

run_tests_in_directory ${testRootDir}

exit 

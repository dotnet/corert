// Import the utility functionality.
import jobs.generation.Utilities;
import jobs.generation.InternalUtilities;

// Defines a the new of the repo, used elsewhere in the file
def project = 'dotnet/corert' 

// Map of OS's to labels.  TODO: Maybe move this into the Utils

def machineLabelMap = ['Ubuntu':'ubuntu',
                       'OSX':'mac',
                       'Windows_NT':'windows',
                       'FreeBSD': 'freebsd',
                       'CentOS7.1': 'centos-71',
                       'OpenSUSE13.2': 'openSuSE-132']

// Innerloop build OS's
def osList = ['Ubuntu', 'OSX', 'Windows_NT']

// Generate the builds for debug and release, commit and PRJob
[true, false].each { isPR -> // Defines a closure over true and false, value assigned to isPR
    ['Debug', 'Release'].each { configuration ->
        osList.each { os ->

            // Define build string
            def lowercaseConfiguration = configuration.toLowerCase()
            
            // Determine the name for the new job.  The first parameter is the project,
            // the second parameter is the base name for the job, and the last parameter
            // is a boolean indicating whether the job will be a PR job.  If true, the
            // suffix _prtest will be appended.
            def newJobName = InternalUtilities.getFullJobName(project, lowercaseConfiguration, isPR)+ '_' + os.toLowerCase()
            def buildString = "";

            // Calculate the build commands
            if (os == 'Windows_NT') {
                buildString = "build.cmd ${lowercaseConfiguration}"
            }
            else {
                // On other OS's we skipmscorlib but run the pal tests
                buildString = "./build.sh ${lowercaseConfiguration}"
            }

            // Create a new job with the specified name.  The brace opens a new closure
            // and calls made within that closure apply to the newly created job.
            def newJob = job(newJobName) {
                // Indicate what kind of machine this job runs on.
                // Indicates that a batch script should be run with the build string (see above)
                label(machineLabelMap[os])

                // This opens the set of build steps that will be run.
                steps {
                    if (os == 'Windows_NT') {
                        // Indicates that a batch script should be run with the build string (see above)
                        batchFile(buildString)
                    }
                    else {
                        shell(buildString)
                    }
                }
            }

            // This call performs test run checks for the CI.
            Utilities.addXUnitDotNETResults(newJob, '**/testResults.xml')

            // This call performs remaining common job setup on the newly created job.
            // This is used most commonly for simple inner loop testing.
            // It does the following:
            //   1. Sets up source control for the project.
            //   2. Adds a push trigger if the job is a PR job
            //   3. Adds a github PR trigger if the job is a PR job.
            //      The optional context (label that you see on github in the PR checks) is added.
            //      If not provided the context defaults to the job name.
            //   4. Adds standard options for build retention and timeouts
            //   5. Adds standard parameters for PR and push jobs.
            //      These allow PR jobs to be used for simple private testing, for instance.
            // See the documentation for this function to see additional optional parameters.
            InternalUtilities.simpleInnerLoopJobSetup(newJob, project, isPR, "${os} ${configuration}")
        }
    }
}

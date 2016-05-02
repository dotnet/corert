// Import the utility functionality.

import jobs.generation.Utilities;
import jobs.generation.JobReport;

// The input project name (e.g. dotnet/coreclr)
def project = GithubProject
// The input branch name (e.g. master)
def branch = GithubBranchName

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
            def newJobName = Utilities.getFullJobName(project, lowercaseConfiguration + '_' + os.toLowerCase(), isPR)
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
            Utilities.setMachineAffinity(newJob, os, 'latest-or-auto')
            Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")
            if (isPR) {
                Utilities.addGithubPRTriggerForBranch(newJob, branch, "${os} ${configuration}")
            }
            else {
                Utilities.addGithubPushTrigger(newJob)
            }
        }
    }
}

JobReport.Report.generateJobReport(out)
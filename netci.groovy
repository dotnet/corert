// Import the utility functionality.

import jobs.generation.Utilities;
import jobs.generation.JobReport;

// The input project name (e.g. dotnet/coreclr)
def project = GithubProject
// The input branch name (e.g. master)
def branch = GithubBranchName

def imageVersionMap = ['Windows_NT':'latest-or-auto',
                       'OSX10.12':'latest-or-auto',
                       'Ubuntu':'20170118']
 
// Innerloop build OS's
def osList = ['Ubuntu', 'OSX10.12', 'Windows_NT']

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
            def prJobDescription = "${os} ${configuration}";
            if (configuration == 'Debug') {
                prJobDescription += " and CoreCLR tests"
            }
            
            // Calculate the build commands
            if (os == 'Windows_NT') {
                buildString = "build.cmd ${lowercaseConfiguration}"
                testScriptString = "tests\\runtest.cmd ${configuration} /coreclr "
            }
            else {
                buildString = "./build.sh ${lowercaseConfiguration}"
                testScriptString = "tests/runtest.sh ${configuration} -coredumps -coreclr "
            }

            // Create a new job with the specified name.  The brace opens a new closure
            // and calls made within that closure apply to the newly created job.
            def newJob = job(newJobName) {
                // This opens the set of build steps that will be run.
                steps {
                    if (os == 'Windows_NT') {
                        // Indicates that a batch script should be run with the build string (see above)
                        batchFile(buildString)
                        batchFile("tests\\runtest.cmd ${configuration} /multimodule")

                        if (configuration == 'Debug') {
                            if (isPR) {
                                // Run a small set of BVTs during PR validation
                                batchFile(testScriptString + "Top200")
                            }
                            else {
                                // Run the full set of known passing tests in the post-commit job
                                batchFile(testScriptString + "KnownGood /multimodule")
                            }
                        }
                    }
                    else {
                        shell(buildString)

                        if (configuration == 'Debug') {
                            if (isPR) {
                                // Run a small set of BVTs during PR validation
                                shell(testScriptString + "top200")
                            }
                            else {
                                // Run the full set of known passing tests in the post-commit job

                                // Todo: Enable push test jobs once we establish a reasonable passing set of tests
                                // shell(testScriptString + "KnownGood")
                            }
                        }
                    }
                }
            }

            // This call performs test run checks for the CI.
            Utilities.addXUnitDotNETResults(newJob, '**/testResults.xml')
            Utilities.setMachineAffinity(newJob, os, imageVersionMap[os])
            Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")
            if (isPR) {
                Utilities.addGithubPRTriggerForBranch(newJob, branch, prJobDescription)
            }
            else {
                // Set a large timeout since the default (2 hours) is insufficient
                Utilities.setJobTimeout(newJob, 1440)
                Utilities.addGithubPushTrigger(newJob)
            }
        }
    }
}

JobReport.Report.generateJobReport(out)

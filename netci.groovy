// Import the utility functionality.

import jobs.generation.Utilities;
import jobs.generation.JobReport;

// The input project name (e.g. dotnet/coreclr)
def project = GithubProject
// The input branch name (e.g. master)
def branch = GithubBranchName

class Constants {

    def static imageVersionMap = ['Windows_NT':'latest-or-auto',
                           'OSX10.12':'latest-or-auto',
                           'Ubuntu':'20170118']

    def static scenarios = ['coreclr', 'corefx']
    
    // Innerloop build OS's
    def static osList = ['Ubuntu', 'OSX10.12', 'Windows_NT']

}

// Generate the builds for debug and release, commit and PRJob
Constants.scenarios.each { scenario ->
    [true, false].each { isPR -> // Defines a closure over true and false, value assigned to isPR
        ['Debug', 'Release'].each { configuration ->
            Constants.osList.each { os ->

                if (configuration == 'Release' && scenario == 'corefx') {
                    return
                }

                // Define build string
                def lowercaseConfiguration = configuration.toLowerCase()

                // Determine the name for the new job.  The first parameter is the project,
                // the second parameter is the base name for the job, and the last parameter
                // is a boolean indicating whether the job will be a PR job.  If true, the
                // suffix _prtest will be appended.
                def baseJobName = lowercaseConfiguration + '_' + os.toLowerCase()
                if (scenario != 'coreclr') {
                    baseJobName += '_' + scenario
                }
                def newJobName = Utilities.getFullJobName(project, baseJobName, isPR)
                def buildString = "";
                def prJobDescription = "${os} ${configuration}";
                if (configuration == 'Debug') {
                    if (scenario == 'coreclr') {
                        prJobDescription += " and CoreCLR tests"
                    }
                    if (scenario == 'corefx') {
                        prJobDescription += " and CoreFX tests"
                    }
                }
                
                def buildCommands = calculateBuildCommands(os, configuration, scenario, isPR)

                // Create a new job with the specified name.  The brace opens a new closure
                // and calls made within that closure apply to the newly created job.
                def newJob = job(newJobName) {
                    // This opens the set of build steps that will be run.
                    steps {
                        if (os == 'Windows_NT') {
                        // Indicates that a batch script should be run with each build command
                            buildCommands.each { buildCommand -> 
                                batchFile(buildCommand) 
                            }
                        }
                        else {
                            buildCommands.each { buildCommand -> 
                                shell(buildCommand)
                            }
                        }
                    }
                }

                // This call performs test run checks for the CI.
                Utilities.addXUnitDotNETResults(newJob, '**/testResults.xml')
                Utilities.addArchival(newJob, "${workspaceRelativeFxRootLinux}/bin/**/testResults.xml")
                Utilities.addXUnitDotNETResults(newJob, "${workspaceRelativeFxRootLinux}/bin/**/testResults.xml")
                Utilities.setMachineAffinity(newJob, os, Constants.imageVersionMap[os])
                Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")

                if (scenario == 'corefx') {
                    Utilities.addArchival(newJob, '**/testResults.xml');
                }
                
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
}

def static calculateBuildCommands(def os, def configuration, def scenario, def isPR) {
    
    def buildCommands = []
    def lowercaseConfiguration = configuration.toLowerCase()
    def testScriptString= ''

    if (os == 'Windows_NT') {
        // Calculate the build commands
        buildCommands += "build.cmd ${lowercaseConfiguration}"

        // Calculate the test commands
        buildCommands += "tests\\runtest.cmd ${configuration} /multimodule"
        if (configuration == 'Debug')
        {
            if (scenario == 'coreclr'){
                testScriptString = "tests\\runtest.cmd ${configuration} /coreclr "
                if (isPR) {
                    // Run a small set of BVTs during PR validation
                    buildCommands += testScriptString + "Top200"
                }
                else {
                    // Run the full set of known passing tests in the post-commit job
                    buildCommands += testScriptString + "KnownGood /multimodule"
                }
            }
            else if (scenario == 'corefx')
            {
                // Disable Simple tests when running a CoreFX scenario
                buildCommands.last() += "skiptests "
                testScriptString = "tests\\runtest.cmd ${configuration} /corefx "
                
                //Todo: Add json config files for different testing scenarios
                buildCommands += testScriptString 
            }
        }
    }
    else {
        // Calculate the build commands        
        buildCommands += "./build.sh ${lowercaseConfiguration}"
        
        // Calculate the test commands
        if (configuration == 'Debug')
        {
            if (scenario == 'coreclr')
            {
                testScriptString = "tests/runtest.sh ${configuration} -coredumps -coreclr "
                if (isPR) {
                    // Run a small set of BVTs during PR validation
                    buildCommands += testScriptString + "top200"
                }
                else {
                    // Run the full set of known passing tests in the post-commit job

                    // Todo: Enable push test jobs once we establish a reasonable passing set of tests
                    // shell(testScriptString + "KnownGood")
                }
            }
            else if (scenario == 'corefx')
            {
                testScriptString = "tests/runtest.sh ${configuration} -corefx "
                
                //Todo: Add json config files for different testing scenarios
                buildCommands += testScriptString                 
            }
        }
    }

    return buildCommands
}

JobReport.Report.generateJobReport(out)

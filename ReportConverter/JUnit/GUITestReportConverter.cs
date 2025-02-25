/*
 * Certain versions of software accessible here may contain branding from Hewlett-Packard Company (now HP Inc.) and Hewlett Packard Enterprise Company.
 * This software was acquired by Micro Focus on September 1, 2017, and is now offered by OpenText.
 * Any reference to the HP and Hewlett Packard Enterprise/HPE marks is historical in nature, and the HP and Hewlett Packard Enterprise/HPE marks are the property of their respective owners.
 * __________________________________________________________________
 * MIT License
 *
 * Copyright 2012-2024 Open Text
 *
 * The only warranties for products and services of Open Text and
 * its affiliates and licensors ("Open Text") are as may be set forth
 * in the express warranty statements accompanying such products and services.
 * Nothing herein should be construed as constituting an additional warranty.
 * Open Text shall not be liable for technical or editorial errors or
 * omissions contained herein. The information contained herein is subject
 * to change without notice.
 *
 * Except as specifically indicated otherwise, this document contains
 * confidential information and a valid license is required for possession,
 * use or copying. If this work is provided to the U.S. Government,
 * consistent with FAR 12.211 and 12.212, Commercial Computer Software,
 * Computer Software Documentation, and Technical Data for Commercial Items are
 * licensed to the U.S. Government under vendor's standard commercial license.
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * ___________________________________________________________________
 */

using ReportConverter.XmlReport;
using ReportConverter.XmlReport.GUITest;
using System;
using System.Collections.Generic;

namespace ReportConverter.JUnit
{
    /// <summary>
    /// Junit-testsuites <==> GUI test
    /// Junit-testsuite <==> Action iteration / Action
    /// Junit-testcase <==> Step
    /// </summary>
    class GUITestReportConverter : ConverterBase
    {
        private const string COMMA = ", ";
        private const string LOCALHOST = "localhost";

        public GUITestReportConverter(CommandArguments args, TestReport input) : base(args)
        {
            Input = input;
            TestSuites = new testsuites();
        }

        public TestReport Input { get; private set; }

        public testsuites TestSuites { get; private set; }

        public override bool SaveFile()
        {
            return SaveFileInternal(TestSuites);
        }

        public override bool Convert()
        {
            List<testsuitesTestsuite> list = [];

            int index = -1;
            foreach (IterationReport iterationReport in Input.Iterations)
            {
                foreach (ActionReport actionReport in iterationReport.Actions)
                {
                    if (actionReport.ActionIterations.Length == 0)
                    {
                        // action -> testsuite
                        index++;
                        list.Add(ConvertTestsuite(actionReport, index));
                        continue;
                    }

                    foreach (ActionIterationReport actionIterationReport in actionReport.ActionIterations)
                    {
                        // action iteration -> testsuite
                        index++;
                        list.Add(ConvertTestsuite(actionIterationReport, index));
                        continue;
                    }
                }
            }

            TestSuites.testsuite = list.ToArray();
            return true;
        }

        /// <summary>
        /// Converts the specified <see cref="ActionReport"/> to the corresponding JUnit <see cref="testsuitesTestsuite"/>.
        /// </summary>
        /// <param name="actionReport">The <see cref="ActionReport"/> instance contains the data of an action.</param>
        /// <param name="index">The index, starts from 0, to identify the order of the testsuites.</param>
        /// <returns>The converted JUnit <see cref="testsuitesTestsuite"/> instance.</returns>
        private testsuitesTestsuite ConvertTestsuite(ActionReport actionReport, int index)
        {
            // get owner iteration data
            int iterationIndex = 0;
            if (actionReport.OwnerIteration != null)
            {
                iterationIndex = actionReport.OwnerIteration.Index;
            }

            // a GUI test action is converted to a JUnit testsuite
            testsuitesTestsuite ts = new testsuitesTestsuite
            {
                id = index, // Starts at '0' for the first testsuite and is incremented by 1 for each following testsuite 
                package = Input.TestAndReportName, // Derived from testsuite/@name in the non-aggregated documents

                // sample: GUI-00012: Iteration 1 / Action 3
                name = string.Format("GUI-{0,5:00000}: {1} {2} / {3}",
                index + 1,
                Properties.Resources.PropName_Iteration,
                iterationIndex,
                actionReport.Name),

                // other JUnit required fields
                timestamp = actionReport.StartTime,
                hostname = Input.HostName
            };
            if (string.IsNullOrWhiteSpace(ts.hostname)) ts.hostname = LOCALHOST;
            ts.time = actionReport.DurationSeconds;

            // properties
            List<testsuiteProperty> properties = new(ConvertTestsuiteCommonProperties(actionReport));
            properties.AddRange(ConvertTestsuiteProperties(actionReport));
            ts.properties = properties.ToArray();

            // JUnit testcases
            ts.testcase = ConvertTestcases(actionReport, out int testcaseCount, out int failureCount);
            ts.tests = testcaseCount;
            ts.failures = failureCount;

            return ts;
        }

        /// <summary>
        /// Converts the specified <see cref="ActionIterationReport"/> to the corresponding JUnit <see cref="testsuitesTestsuite"/>.
        /// </summary>
        /// <param name="actionIterationReport">The <see cref="ActionIterationReport"/> instance contains the data of an action iteration.</param>
        /// <param name="index">The index, starts from 0, to identify the order of the testsuites.</param>
        /// <returns>The converted JUnit <see cref="testsuitesTestsuite"/> instance.</returns>
        private testsuitesTestsuite ConvertTestsuite(ActionIterationReport actionIterationReport, int index)
        {
            // get owner action and iteration data
            string actionName = string.Empty;
            int iterationIndex = 0;
            if (actionIterationReport.OwnerAction != null)
            {
                actionName = actionIterationReport.OwnerAction.Name;

                // owner iteration
                if (actionIterationReport.OwnerAction.OwnerIteration != null)
                {
                    iterationIndex = actionIterationReport.OwnerAction.OwnerIteration.Index;
                }
            }

            // a GUI test action iteration is converted to a JUnit testsuite
            testsuitesTestsuite ts = new()
            {
                id = index, // Starts at '0' for the first testsuite and is incremented by 1 for each following testsuite 
                package = Input.TestAndReportName, // Derived from testsuite/@name in the non-aggregated documents

                // sample: GUI-00012: Iteration 1 / Action 3 / Action Iteration 2
                name = string.Format("GUI-{0,5:00000}: {1} {2} / {3} / {4} {5}",
                                    index + 1,
                                    Properties.Resources.PropName_Iteration,
                                    iterationIndex,
                                    actionName,
                                    Properties.Resources.PropName_ActionIteration,
                                    actionIterationReport.Index),
                // other JUnit required fields
                timestamp = actionIterationReport.StartTime,
                hostname = Input.HostName
            };
            if (string.IsNullOrWhiteSpace(ts.hostname)) ts.hostname = LOCALHOST;
            ts.time = actionIterationReport.DurationSeconds;

            // properties
            List<testsuiteProperty> properties = new(ConvertTestsuiteCommonProperties(actionIterationReport));
            properties.AddRange(ConvertTestsuiteProperties(actionIterationReport));
            ts.properties = [.. properties];

            // JUnit testcases
            ts.testcase = ConvertTestcases(actionIterationReport, out int testcaseCount, out int failureCount);
            ts.tests = testcaseCount;
            ts.failures = failureCount;

            return ts;
        }

        private IEnumerable<testsuiteProperty> ConvertTestsuiteCommonProperties(GeneralReportNode reportNode)
        {
            return
            [
                new testsuiteProperty(Properties.Resources.PropName_TestingTool, Input.TestingToolNameVersion),
                new testsuiteProperty(Properties.Resources.PropName_OSInfo, Input.OSInfo),
                new testsuiteProperty(Properties.Resources.PropName_Locale, Input.Locale),
                new testsuiteProperty(Properties.Resources.PropName_LoginUser, Input.LoginUser),
                new testsuiteProperty(Properties.Resources.PropName_CPUInfo, Input.CPUInfoAndCores),
                new testsuiteProperty(Properties.Resources.PropName_Memory, Input.TotalMemory)
            ];
        }

        private static IEnumerable<testsuiteProperty> ConvertTestsuiteProperties(IterationReport iterationReport)
        {
            List<testsuiteProperty> list =
            [
                // iteration index
                new testsuiteProperty(Properties.Resources.PropName_IterationIndex, iterationReport.Index.ToString()),
            ];

            // iteration input/output parameters
            foreach (ParameterType pt in iterationReport.InputParameters)
            {
                list.Add(new testsuiteProperty(Properties.Resources.PropName_Prefix_IterationInputParam + pt.NameAndType, pt.value));
            }
            foreach (ParameterType pt in iterationReport.OutputParameters)
            {
                list.Add(new testsuiteProperty(Properties.Resources.PropName_Prefix_IterationOutputParam + pt.NameAndType, pt.value));
            }

            // iteration AUTs
            int i = 0;
            foreach (TestedApplicationType aut in iterationReport.AUTs)
            {
                i++;
                string propValue = aut.Name;
                if (!string.IsNullOrWhiteSpace(aut.Version))
                {
                    propValue += $" {aut.Version}";
                }
                if (!string.IsNullOrWhiteSpace(aut.Path))
                {
                    propValue += $" {aut.Path}";     }
                list.Add(new testsuiteProperty($"{Properties.Resources.PropName_Prefix_AUT} {i}", propValue));
            }

            return list;
        }

        private static IEnumerable<testsuiteProperty> ConvertTestsuiteProperties(ActionReport actionReport)
        {
            List<testsuiteProperty> list = [];

            // action input/output parameters
            foreach (ParameterType pt in actionReport.InputParameters)
            {
                list.Add(new testsuiteProperty(Properties.Resources.PropName_Prefix_ActionInputParam + pt.NameAndType, pt.value));
            }
            foreach (ParameterType pt in actionReport.OutputParameters)
            {
                list.Add(new testsuiteProperty(Properties.Resources.PropName_Prefix_ActionInputParam + pt.NameAndType, pt.value));
            }

            // owner - iteration
            IterationReport iterationReport = actionReport.OwnerIteration;
            if (iterationReport != null)
            {
                // iteration properties
                list.AddRange(ConvertTestsuiteProperties(iterationReport));
            }

            return list.ToArray();
        }

        private static IEnumerable<testsuiteProperty> ConvertTestsuiteProperties(ActionIterationReport actionIterationReport)
        {
            List<testsuiteProperty> list = [];

            // action iteration index
            list.Add(new testsuiteProperty(Properties.Resources.PropName_ActionIterationIndex, actionIterationReport.Index.ToString()));

            // iteration input/output parameters
            foreach (ParameterType pt in actionIterationReport.InputParameters)
            {
                list.Add(new testsuiteProperty(Properties.Resources.PropName_Prefix_ActionIterationInputParam + pt.NameAndType, pt.value));
            }
            foreach (ParameterType pt in actionIterationReport.OutputParameters)
            {
                list.Add(new testsuiteProperty(Properties.Resources.PropName_Prefix_ActionIterationOutputParam + pt.NameAndType, pt.value));
            }

            // owner - action
            ActionReport actionReport = actionIterationReport.OwnerAction;
            if (actionReport != null)
            {
                // action name
                list.Add(new testsuiteProperty(Properties.Resources.PropName_Action, actionReport.Name));
                // action properties
                list.AddRange(ConvertTestsuiteProperties(actionReport));
            }

            return list;
        }

        private static testsuiteTestcase[] ConvertTestcases(ActionReport actionReport, out int count, out int numOfFailures)
        {
            count = 0;
            numOfFailures = 0;

            List<testsuiteTestcase> list = [];
            EnumerableReportNodes<StepReport> steps = new(actionReport.AllStepsEnumerator);
            foreach (StepReport step in steps)
            {
                testsuiteTestcase tc = ConvertTestcase(step, count);
                if (tc == null)
                {
                    continue;
                }

                list.Add(tc);
                if (step.Status == ReportStatus.Failed)
                {
                    numOfFailures++;
                }
                count++;
            }

            return list.ToArray();
        }

        private static testsuiteTestcase[] ConvertTestcases(ActionIterationReport actionIterationReport, out int count, out int numOfFailures)
        {
            count = 0;
            numOfFailures = 0;

            List<testsuiteTestcase> list = [];
            EnumerableReportNodes<StepReport> steps = new(actionIterationReport.AllStepsEnumerator);
            foreach (StepReport step in steps)
            {
                testsuiteTestcase tc = ConvertTestcase(step, count);
                if (tc == null)
                {
                    continue;
                }

                list.Add(tc);
                if (step.Status == ReportStatus.Failed)
                {
                    numOfFailures++;
                }
                count++;
            }

            return [.. list];
        }

        /// <summary>
        /// Converts the specified <see cref="StepReport"/> to the corresponding JUnit <see cref="testsuiteTestcase"/>.
        /// </summary>
        /// <param name="stepReport">The <see cref="StepReport"/> instance contains the data of a GUI test step.</param>
        /// <param name="index">The index, starts from 0, to identify the order of the testcases.</param>
        /// <returns>The converted JUnit <see cref="testsuiteTestcase"/> instance.</returns>
        public static testsuiteTestcase ConvertTestcase(StepReport stepReport, int index)
        {
            // the step might be a checkpoint
            CheckpointReport checkpointReport = CheckpointReport.FromStepReport(stepReport);
            if (checkpointReport != null)
            {
                return ConvertTestcase(checkpointReport, index);
            }

            // a step with smart identification?
            if (stepReport.SmartIdentification != null)
            {
                return ConvertTestcaseWithSmartIdentificationInfo(stepReport, index);
            }

            // a general step
            testsuiteTestcase tc = new() { 
                name = string.Format("#{0,5:00000}: {1}", index + 1, stepReport.Name),
                classname = stepReport.TestObjectPath,
                time = stepReport.DurationSeconds
            };

            if (stepReport.Status == ReportStatus.Failed)
            {
                testsuiteTestcaseFailure failure = new()
                {
                    message = stepReport.ErrorText,
                    type = string.Empty
                };
                tc.Item = failure;
            }

            return tc;
        }

        private static testsuiteTestcase ConvertTestcase(CheckpointReport checkpointReport, int index)
        {
            if (checkpointReport == null || string.IsNullOrWhiteSpace(checkpointReport.CheckpointType))
            {
                // not a checkpoint or checkpoint type is empty - ignore
                return null;
            }

            // sample: Standard Checkpoint (DB Checkpoint) - "checkpoint 1"
            string checkpointDisplayName = checkpointReport.CheckpointType;
            if (!string.IsNullOrWhiteSpace(checkpointReport.CheckpointSubType))
            {
                checkpointDisplayName += $" ({checkpointReport.CheckpointSubType})";
            }
            checkpointDisplayName += $" - {checkpointReport.Name}";

            testsuiteTestcase tc = new()
            {
                name = string.Format("#{0,5:00000}: {1}", index + 1, checkpointDisplayName),
                classname = checkpointReport.StepReport.TestObjectPath,
                time = checkpointReport.StepReport.DurationSeconds
            };

            if (checkpointReport.Status == ReportStatus.Failed)
            {
                testsuiteTestcaseFailure failure = new()
                {
                    message = checkpointReport.FailedDescription,
                    type = string.Empty
                };
                tc.Item = failure;
            }

            return tc;
        }

        private static StepReport _lastSkippedSIDStep;

        private static testsuiteTestcase ConvertTestcaseWithSmartIdentificationInfo(StepReport stepReport, int index)
        {
            SmartIdentificationInfoExtType sid = stepReport.SmartIdentification ?? throw new ArgumentNullException("stepReport.SmartIdentification");
            if (stepReport.Status == ReportStatus.Warning)
            {
                // a step with smart identification info and warning status can be ignored
                // since the next step is the official smart identification info report node
                _lastSkippedSIDStep = stepReport;
                return null;
            }

            // a step with smart identification info
            int basicMatches = 0;
            if (sid.SIDBasicProperties != null)
            {
                basicMatches = sid.SIDBasicProperties.BasicMatch;
            }

            List<string> optList = [];
            if (sid.SIDOptionalProperties != null)
            {
                foreach (SIDOptionalPropertyExtType property in sid.SIDOptionalProperties)
                {
                    if (property.Matches > 0)
                    {
                        optList.Add(string.Format("{0}=\"{1}\"", property.Name, property.Value));
                    }
                }
            }
            string sidDesc = string.Format(Properties.Resources.GUITest_SID_Description, basicMatches, string.Join(COMMA, optList));
            string sidName = stepReport.Node.Data.Name;

            testsuiteTestcase tc = new()
            {
                name = string.Format("#{0,5:00000}: {1} ({2})", index + 1, sidName, sidDesc),
                classname = stepReport.TestObjectPath,
                time = stepReport.DurationSeconds + (_lastSkippedSIDStep != null ? _lastSkippedSIDStep.DurationSeconds : 0)
            };

            // clear last skipped SID step
            _lastSkippedSIDStep = null;

            return tc;
        }
    }
}

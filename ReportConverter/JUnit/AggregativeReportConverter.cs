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
using System.Collections.Generic;

namespace ReportConverter.JUnit
{
    /// <summary>
    /// Junit-testsuites <==> aggregative tests
    /// Junit-testsuite <==> GUI / API / BPT test
    /// Junit-testcase <==> Step / Activity
    /// </summary>
    class AggregativeReportConverter : ConverterBase
    {

        private const string SLASH = " / ";
        private const string LOCALHOST = "localhost";

        public AggregativeReportConverter(CommandArguments args, IEnumerable<TestReportBase> testReports) : base(args)
        {
            TestSuites = new testsuites();
            TestReports = testReports;
            TestReports ??= [];
        }

        public IEnumerable<TestReportBase> TestReports { get; private set; }

        public testsuites TestSuites { get; private set; }

        public override bool SaveFile()
        {
            return SaveFileInternal(TestSuites);
        }

        public override bool Convert()
        {
            List<testsuitesTestsuite> list = [];

            int index = -1;
            foreach (TestReportBase testReport in this.TestReports)
            {
                index++;
                list.Add(ConvertTestsuite(testReport, index));
            }

            TestSuites.testsuite = list.ToArray();
            return true;
        }

        private testsuitesTestsuite ConvertTestsuite(TestReportBase testReport, int index)
        {
            // GUI test?
            testsuitesTestsuite ts = TryConvertTestsuite(testReport as XmlReport.GUITest.TestReport, index);
            if (ts != null)
            {
                return ts;
            }

            // API test?
            ts = TryConvertTestsuite(testReport as XmlReport.APITest.TestReport, index);
            if (ts != null)
            {
                return ts;
            }

            // BPT test?
            ts = TryConvertTestsuite(testReport as XmlReport.BPT.TestReport, index);
            if (ts != null)
            {
                return ts;
            }

            // none of above, return the default testsuite with only common data, that is, no testcases
            ts = new testsuitesTestsuite();
            FillTestsuiteCommonData(ts, testReport, index);
            return ts;
        }

        #region For GUI test only
        // For GUI test only
        private static testsuitesTestsuite TryConvertTestsuite(XmlReport.GUITest.TestReport testReport, int index)
        {
            if (testReport == null)
            {
                return null;
            }

            testsuitesTestsuite ts = new();
            FillTestsuiteCommonData(ts, testReport, index);

            // JUnit testcases
            ts.testcase = ConvertTestcases(testReport.AllStepsEnumerator, out int testcaseCount, out int failureCount);
            ts.tests = testcaseCount;
            ts.failures = failureCount;

            return ts;
        }

        // For GUI test only
        private static testsuiteTestcase[] ConvertTestcases(ReportNodeEnumerator<XmlReport.GUITest.StepReport> steps, out int count, out int numOfFailures)
        {
            count = 0;
            numOfFailures = 0;

            List<testsuiteTestcase> list = [];
            if (steps != null)
            {
                EnumerableReportNodes<XmlReport.GUITest.StepReport> stepReports = new(steps);
                foreach (XmlReport.GUITest.StepReport step in stepReports)
                {
                    testsuiteTestcase tc = GUITestReportConverter.ConvertTestcase(step, count);
                    if (tc == null)
                    {
                        continue;
                    }

                    // update step name with the hierarchy full name
                    string stepFullName = GetHierarchyFullName(step);
                    tc.name = string.Format("#{0,7:0000000}: {1}", count + 1, stepFullName);
                    if (string.IsNullOrWhiteSpace(tc.classname))
                        tc.classname = stepFullName;

                    list.Add(tc);
                    if (step.Status == ReportStatus.Failed)
                    {
                        numOfFailures++;
                    }
                    count++;
                }
            }

            return list.ToArray();
        }

        // For GUI test only
        private static string GetHierarchyFullName(XmlReport.GUITest.StepReport stepReport)
        {
            string hierarchyName = stepReport.Name;

            GeneralReportNode parentReport = stepReport.Owner as GeneralReportNode;
            while (parentReport != null)
            {
                string name = parentReport.Name;

                if (parentReport is XmlReport.GUITest.ContextReport)
                {
                    parentReport = parentReport.Owner as GeneralReportNode;
                    continue;
                }
                else if (parentReport is XmlReport.GUITest.ActionIterationReport actionIterationReport)
                {
                    // if it is GUI action iteration, prepend "Action Iteration" term
                    name = $"{Properties.Resources.PropName_ActionIteration} {actionIterationReport.Index}";
                }
                else if (parentReport is XmlReport.GUITest.IterationReport iterationReport)
                {
                    // if it is GUI iteration, prepend "Iteration" term
                    name = $"{Properties.Resources.PropName_Iteration} {iterationReport.Index}";     }

                // concat hierarchy name
                hierarchyName = name + SLASH + hierarchyName;

                parentReport = parentReport.Owner as GeneralReportNode;
            }

            return hierarchyName;
        }
        #endregion

        #region For API test only
        // For API test only
        private static testsuitesTestsuite TryConvertTestsuite(XmlReport.APITest.TestReport testReport, int index)
        {
            if (testReport == null)
            {
                return null;
            }

            testsuitesTestsuite ts = new();
            FillTestsuiteCommonData(ts, testReport, index);

            // JUnit testcases
            ts.testcase = ConvertTestcases(testReport.Iterations, out int testcaseCount, out int failureCount);
            ts.tests = testcaseCount;
            ts.failures = failureCount;

            return ts;
        }

        // For API test only
        private static testsuiteTestcase[] ConvertTestcases(ReportNodeCollection<XmlReport.APITest.IterationReport> iterationReports, out int count, out int numOfFailures)
        {
            count = 0;
            numOfFailures = 0;

            List<testsuiteTestcase> list = [];
            if (iterationReports != null)
            {
                int iterationNum = 0;
                foreach (XmlReport.APITest.IterationReport iteration in iterationReports)
                {
                    iterationNum++;

                    if (iteration.AllActivitiesEnumerator != null)
                    {
                        EnumerableReportNodes<XmlReport.APITest.ActivityReport> activities = new(iteration.AllActivitiesEnumerator);
                        foreach (XmlReport.APITest.ActivityReport activity in activities)
                        {
                            testsuiteTestcase tc = APITestReportConverter.ConvertTestcase(activity, count);
                            if (tc == null)
                            {
                                continue;
                            }

                            // update activity name with the hierarchy full name
                            tc.name = string.Format("#{0,7:0000000}: {1}", count + 1, GetHierarchyFullName(activity, iterationNum));

                            list.Add(tc);
                            if (activity.Status == ReportStatus.Failed)
                            {
                                numOfFailures++;
                            }
                            count++;
                        }
                    }
                }
            }

            return list.ToArray();
        }

        // For API test only
        private static string GetHierarchyFullName(XmlReport.APITest.ActivityReport activityReport, int iterationNum)
        {
            string hierarchyName = activityReport.Name;

            GeneralReportNode parentReport = activityReport.Owner as GeneralReportNode;
            while (parentReport != null)
            {
                string name = parentReport.Name;

                if (parentReport is XmlReport.APITest.IterationReport iterationReport)
                {
                    // if it is API iteration, prepend "Iteration" term
                    name = $"{Properties.Resources.PropName_Iteration} {iterationNum}";     }

                // concat hierarchy name
                hierarchyName = name + SLASH + hierarchyName;

                parentReport = parentReport.Owner as GeneralReportNode;
            }

            return hierarchyName;
        }
        #endregion

        #region For BPT test only
        // For BPT test only
        private static testsuitesTestsuite TryConvertTestsuite(XmlReport.BPT.TestReport testReport, int index)
        {
            if (testReport == null)
            {
                return null;
            }

            testsuitesTestsuite ts = new();
            FillTestsuiteCommonData(ts, testReport, index);

            // JUnit testcases
            ts.testcase = ConvertTestcases(testReport.AllBCsEnumerator, out int testcaseCount, out int failureCount);
            ts.tests = testcaseCount;
            ts.failures = failureCount;

            return ts;
        }

        // For BPT test only
        private static testsuiteTestcase[] ConvertTestcases(ReportNodeEnumerator<XmlReport.BPT.BusinessComponentReport> bcs, out int count, out int numOfFailures)
        {
            count = 0;
            numOfFailures = 0;

            List<testsuiteTestcase> list = [];
            if (bcs != null)
            {
                EnumerableReportNodes<XmlReport.BPT.BusinessComponentReport> bcReports = new EnumerableReportNodes<XmlReport.BPT.BusinessComponentReport>(bcs);
                foreach (XmlReport.BPT.BusinessComponentReport bc in bcReports)
                {
                    if (bc.AllBCStepsEnumerator != null)
                    {
                        EnumerableReportNodes<XmlReport.BPT.BCStepReport> steps = new EnumerableReportNodes<XmlReport.BPT.BCStepReport>(bc.AllBCStepsEnumerator);
                        foreach (XmlReport.BPT.BCStepReport step in steps)
                        {
                            if (step.IsContext)
                            {
                                continue;
                            }

                            testsuiteTestcase tc = BPTReportConverter.ConvertTestcase(step, count);
                            if (tc == null)
                            {
                                continue;
                            }

                            // update step name with the hierarchy full name
                            tc.name = string.Format("#{0,7:0000000}: {1}", count + 1, GetHierarchyFullName(step, bc));

                            list.Add(tc);
                            if (step.Status == ReportStatus.Failed)
                            {
                                numOfFailures++;
                            }
                            count++;
                        }
                    }
                }
            }

            return [.. list];
        }

        // For BPT test only
        private static string GetHierarchyFullName(XmlReport.BPT.BCStepReport stepReport, XmlReport.BPT.BusinessComponentReport bc)
        {
            return BPTReportConverter.GetBCHierarchyName(bc) + SLASH + stepReport.Name;
        }
        #endregion

        // For GUI / API / BPT tests
        private static void FillTestsuiteCommonData(testsuitesTestsuite ts, TestReportBase testReport, int index)
        {
            ts.id = index; // Starts at '0' for the first testsuite and is incremented by 1 for each following testsuite
            ts.package = testReport.TestAndReportName;
            ts.name = string.Format("TEST-{0,3:000}: {1}", index + 1, testReport.TestAndReportName);

            // other JUnit required fields
            ts.timestamp = testReport.TestRunStartTime;
            ts.hostname = testReport.HostName;
            if (string.IsNullOrWhiteSpace(ts.hostname)) ts.hostname = LOCALHOST;
            ts.time = testReport.TestDurationSeconds;

            // properties
            List<testsuiteProperty> properties = new(ConvertTestsuiteCommonProperties(testReport));
            ts.properties = [.. properties];
        }

        // For GUI / API / BPT tests
        private static IEnumerable<testsuiteProperty> ConvertTestsuiteCommonProperties(TestReportBase testReport)
        {
            yield return new testsuiteProperty(Properties.Resources.PropName_TestingTool, testReport.TestingToolNameVersion);
            yield return new testsuiteProperty(Properties.Resources.PropName_OSInfo, testReport.OSInfo);
            yield return new testsuiteProperty(Properties.Resources.PropName_Locale, testReport.Locale);
            yield return new testsuiteProperty(Properties.Resources.PropName_LoginUser, testReport.LoginUser);
            yield return new testsuiteProperty(Properties.Resources.PropName_CPUInfo, testReport.CPUInfoAndCores);
            yield return new testsuiteProperty(Properties.Resources.PropName_Memory, testReport.TotalMemory);

            if (testReport.TestInputParameters != null)
            {
                foreach (ParameterType pt in testReport.TestInputParameters)
                {
                    // sample of property name:
                    //   Test input: param1 (System.String)
                    yield return new testsuiteProperty(Properties.Resources.PropName_Prefix_TestInputParam + pt.NameAndType, pt.value);
                }
            }

            if (testReport.TestOutputParameters != null)
            {
                foreach (ParameterType pt in testReport.TestOutputParameters)
                {
                    // sample of property name:
                    //   Test output: param1 (System.String)
                    yield return new testsuiteProperty(Properties.Resources.PropName_Prefix_TestOutputParam + pt.NameAndType, pt.value);
                }
            }

            if (testReport.TestAUTs != null)
            {
                int i = 0;
                foreach (TestedApplicationType aut in testReport.TestAUTs)
                {
                    i++;
                    string propValue = aut.Name;
                    if (!string.IsNullOrWhiteSpace(aut.Version))
                    {
                        propValue += $" {aut.Version}";
                    }
                    if (!string.IsNullOrWhiteSpace(aut.Path))
                    {
                        propValue += $" {aut.Path}";
                    }
                    yield return new testsuiteProperty($"{Properties.Resources.PropName_Prefix_AUT} {i}", propValue);
                }
            }
        }

    }
}

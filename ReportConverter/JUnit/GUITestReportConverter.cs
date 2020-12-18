﻿using ReportConverter.XmlReport;
using ReportConverter.XmlReport.GUITest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportConverter.JUnit
{
    class GUITestReportConverter : ConverterBase
    {
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
            List<testsuitesTestsuite> list = new List<testsuitesTestsuite>();

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
            testsuitesTestsuite ts = new testsuitesTestsuite();

            ts.id = index; // Starts at '0' for the first testsuite and is incremented by 1 for each following testsuite 
            ts.package = Input.TestAndReportName; // Derived from testsuite/@name in the non-aggregated documents

            // sample: Iteration 1 / Action 3
            ts.name = string.Format("{0} {1} / {2}", 
                Properties.Resources.PropName_Iteration,
                iterationIndex,
                actionReport.Name);

            // other JUnit required fields
            ts.timestamp = actionReport.StartTime;
            ts.hostname = Input.HostName;
            if (string.IsNullOrWhiteSpace(ts.hostname)) ts.hostname = "localhost";
            ts.time = actionReport.DurationSeconds;

            // properties
            List<testsuiteProperty> properties = new List<testsuiteProperty>(ConvertTestsuiteCommonProperties(actionReport));
            properties.AddRange(ConvertTestsuiteProperties(actionReport));
            ts.properties = properties.ToArray();

            // JUnit testcases
            int testcaseCount = 0;
            int failureCount = 0;
            ts.testcase = ConvertTestcases(actionReport, out testcaseCount, out failureCount);
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
            testsuitesTestsuite ts = new testsuitesTestsuite();

            ts.id = index; // Starts at '0' for the first testsuite and is incremented by 1 for each following testsuite 
            ts.package = Input.TestAndReportName; // Derived from testsuite/@name in the non-aggregated documents

            // sample: Iteration 1 / Action 3 / Action Iteration 2
            ts.name = string.Format("{0} {1} / {2} / {3} {4}",
                Properties.Resources.PropName_Iteration,
                iterationIndex,
                actionName,
                Properties.Resources.PropName_ActionIteration,
                actionIterationReport.Index);

            // other JUnit required fields
            ts.timestamp = actionIterationReport.StartTime;
            ts.hostname = Input.HostName;
            if (string.IsNullOrWhiteSpace(ts.hostname)) ts.hostname = "localhost";
            ts.time = actionIterationReport.DurationSeconds;

            // properties
            List<testsuiteProperty> properties = new List<testsuiteProperty>(ConvertTestsuiteCommonProperties(actionIterationReport));
            properties.AddRange(ConvertTestsuiteProperties(actionIterationReport));
            ts.properties = properties.ToArray();

            // JUnit testcases
            int testcaseCount = 0;
            int failureCount = 0;
            ts.testcase = ConvertTestcases(actionIterationReport, out testcaseCount, out failureCount);
            ts.tests = testcaseCount;
            ts.failures = failureCount;

            return ts;
        }

        private IEnumerable<testsuiteProperty> ConvertTestsuiteCommonProperties(GeneralReportNode reportNode)
        {
            return new testsuiteProperty[]
            {
                new testsuiteProperty(Properties.Resources.PropName_TestingTool, Input.TestingToolNameVersion),
                new testsuiteProperty(Properties.Resources.PropName_OSInfo, Input.OSInfo),
                new testsuiteProperty(Properties.Resources.PropName_Locale, Input.Locale),
                new testsuiteProperty(Properties.Resources.PropName_LoginUser, Input.LoginUser),
                new testsuiteProperty(Properties.Resources.PropName_CPUInfo, Input.CPUInfoAndCores),
                new testsuiteProperty(Properties.Resources.PropName_Memory, Input.TotalMemory)
            };
        }

        private IEnumerable<testsuiteProperty> ConvertTestsuiteProperties(IterationReport iterationReport)
        {
            List<testsuiteProperty> list = new List<testsuiteProperty>();

            // iteration index
            list.Add(new testsuiteProperty(Properties.Resources.PropName_IterationIndex, iterationReport.Index.ToString()));

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
                    propValue += string.Format(" {0}", aut.Version);
                }
                if (!string.IsNullOrWhiteSpace(aut.Path))
                {
                    propValue += string.Format(" ({0})", aut.Path);
                }
                list.Add(new testsuiteProperty(string.Format("{0} {1}", Properties.Resources.PropName_Prefix_AUT, i), propValue));
            }

            return list;
        }

        private IEnumerable<testsuiteProperty> ConvertTestsuiteProperties(ActionReport actionReport)
        {
            List<testsuiteProperty> list = new List<testsuiteProperty>();

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

        private IEnumerable<testsuiteProperty> ConvertTestsuiteProperties(ActionIterationReport actionIterationReport)
        {
            List<testsuiteProperty> list = new List<testsuiteProperty>();

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

        private testsuiteTestcase[] ConvertTestcases(ActionReport actionReport, out int count, out int numOfFailures)
        {
            count = 0;
            numOfFailures = 0;

            List<testsuiteTestcase> list = new List<testsuiteTestcase>();
            EnumerableReportNodes<StepReport> steps = new EnumerableReportNodes<StepReport>(actionReport.AllStepsEnumerator);
            foreach (StepReport step in steps)
            {
                list.Add(ConvertTestcase(step, count));
                if (step.Status == ReportStatus.Failed)
                {
                    numOfFailures++;
                }
                count++;
            }

            return list.ToArray();
        }

        private testsuiteTestcase[] ConvertTestcases(ActionIterationReport actionIterationReport, out int count, out int numOfFailures)
        {
            count = 0;
            numOfFailures = 0;

            List<testsuiteTestcase> list = new List<testsuiteTestcase>();
            EnumerableReportNodes<StepReport> steps = new EnumerableReportNodes<StepReport>(actionIterationReport.AllStepsEnumerator);
            foreach (StepReport step in steps)
            {
                list.Add(ConvertTestcase(step, count));
                if (step.Status == ReportStatus.Failed)
                {
                    numOfFailures++;
                }
                count++;
            }

            return list.ToArray();
        }

        /// <summary>
        /// Converts the specified <see cref="StepReport"/> to the corresponding JUnit <see cref="testsuiteTestcase"/>.
        /// </summary>
        /// <param name="stepReport">The <see cref="StepReport"/> instance contains the data of a GUI test step.</param>
        /// <param name="index">The index, starts from 0, to identify the order of the testcases.</param>
        /// <returns>The converted JUnit <see cref="testsuiteTestcase"/> instance.</returns>
        private testsuiteTestcase ConvertTestcase(StepReport stepReport, int index)
        {
            testsuiteTestcase tc = new testsuiteTestcase();
            tc.name = string.Format("#{0,5:00000}: {1}", index + 1, stepReport.Name);
            tc.classname = stepReport.TestObjectPath;
            tc.time = stepReport.DurationSeconds;

            if (stepReport.Status == ReportStatus.Failed)
            {
                testsuiteTestcaseFailure failure = new testsuiteTestcaseFailure();
                failure.message = stepReport.ErrorText;
                failure.type = string.Empty;
                tc.Item = failure;
            }

            return tc;
        }
    }
}

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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using HpToolsLauncher.Common;
using HpToolsLauncher.Properties;
using Mercury.TD.Client.Ota.QC9;

namespace HpToolsLauncher
{
    public class AlmTestSetsRunner : RunnerBase, IDisposable
    {
        private ITDConnection13 _tdConnection;
        private ITDConnection2 _tdConnectionOld;

        private const string LF = "\n";
        private const string CR = "\r";
        private const string TAB = "\t";
        private const string LOCALHOST = "localhost";
        private const char BLANK = ' ';
        private const char BACKSLAH = '\\';
        private const string FAILED = "Failed";
        private const string COMMA = ",";
        private readonly char[] BACKSLASH_CHAR_ARR = [BACKSLAH];
        private readonly char[] COMMA_CHAR_ARR = [','];

        public ITDConnection13 TdConnection
        {
            get
            {
                if (_tdConnection == null)
                    CreateTdConnection();
                return _tdConnection;
            }
        }

        public ITDConnection2 TdConnectionOld
        {
            get
            {
                if (_tdConnectionOld == null)
                    CreateTdConnectionOld();
                return _tdConnectionOld;
            }
        }

        public bool Connected { get; set; }

        public string MQcServer { get; set; }

        public string MQcUser { get; set; }

        public string MQcProject { get; set; }

        public string MQcDomain { get; set; }

        public string FilterByName { get; set; }

        public bool IsFilterSelected { get; set; }

        public bool InitialTestRun { get; set; }

        public List<string> FilterByStatuses { get; set; }

        public List<string> TestSets { get; set; }

        public QcRunMode RunMode { get; set; }

        public string RunHost { get; set; }

        public TestStorageType Storage { get; set; }

        public double Timeout { get; set; }

        public bool SSOEnabled { get; set; }

        public string ClientID { get; set; }

        public string ApiKey { get; set; }



        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="qcServer"></param>
        /// <param name="qcUser"></param>
        /// <param name="qcPassword"></param>
        /// <param name="qcDomain"></param>
        /// <param name="qcProject"></param>
        /// <param name="intQcTimeout"></param>
        /// <param name="enmQcRunMode"></param>
        /// <param name="runHost"></param>
        /// <param name="qcTestSets"></param>
        /// <param name="isFilterSelected"></param>
        /// <param name="filterByName"></param>
        /// <param name="filterByStatuses"></param>
        /// <param name="initialTestRun"></param>
        /// <param name="testStorageType"></param>
        /// <param name="isSSOEnabled"></param>
        public AlmTestSetsRunner(string qcServer,
                                string qcUser,
                                string qcPassword,
                                string qcDomain,
                                string qcProject,
                                double intQcTimeout,
                                QcRunMode enmQcRunMode,
                                string runHost,
                                List<string> qcTestSets,
                                bool isFilterSelected,
                                string filterByName,
                                List<string> filterByStatuses,
                                bool initialTestRun,
                                TestStorageType testStorageType,
                                bool isSSOEnabled,
                                string qcClientId,
                                string qcApiKey)
        {

            Timeout = intQcTimeout;
            RunMode = enmQcRunMode;
            RunHost = runHost;

            MQcServer = qcServer;
            MQcUser = qcUser;
            MQcProject = qcProject;
            MQcDomain = qcDomain;

            IsFilterSelected = isFilterSelected;
            FilterByName = filterByName;
            FilterByStatuses = filterByStatuses;
            InitialTestRun = initialTestRun;
            SSOEnabled = isSSOEnabled;
            ClientID = qcClientId;
            ApiKey = qcApiKey;

            Connected = ConnectToProject(MQcServer, MQcUser, qcPassword, MQcDomain, MQcProject, SSOEnabled, ClientID, ApiKey);
            TestSets = qcTestSets;
            Storage = testStorageType;
            if (!Connected)
            {
                Console.WriteLine("ALM Test set runner not connected");
                Environment.Exit((int)Launcher.ExitCodeEnum.AlmNotConnected);
            }
        }

        /// <summary>
        /// destructor - ensures dispose of connection
        /// </summary>
        ~AlmTestSetsRunner()
        {
            Dispose(false);
        }


        //------------------------------- Connection to QC --------------------------

        /// <summary>
        /// Creates a connection to QC (for ALM 12.60 and 15)
        /// </summary>
        private void CreateTdConnection()
        {
            Type type = Type.GetTypeFromProgID("TDApiOle80.TDConnection");

            if (type == null)
            {
                ConsoleWriter.WriteLine(GetAlmNotInstalledError());
                Environment.Exit((int)Launcher.ExitCodeEnum.Failed);
            }

            try
            {
                object conn = Activator.CreateInstance(type);
                _tdConnection = conn as ITDConnection13;

            }
            catch (FileNotFoundException ex)
            {
                ConsoleWriter.WriteLine(GetAlmNotInstalledError());
                ConsoleWriter.WriteLine(ex.Message);
                Environment.Exit((int)Launcher.ExitCodeEnum.Failed);
            }
        }

        /// <summary>
        /// Creates a connection to QC (for ALM 12.55)
        /// </summary>
        private void CreateTdConnectionOld()
        {
            Type type = Type.GetTypeFromProgID("TDApiOle80.TDConnection");

            if (type == null)
            {
                ConsoleWriter.WriteLine(GetAlmNotInstalledError());
                Environment.Exit((int)Launcher.ExitCodeEnum.Failed);
            }

            try
            {
                object conn = Activator.CreateInstance(type);
                _tdConnectionOld = conn as ITDConnection2;
            }
            catch (FileNotFoundException ex)
            {
                ConsoleWriter.WriteLine(GetAlmNotInstalledError());
                ConsoleWriter.WriteLine(ex.Message);
                Environment.Exit((int)Launcher.ExitCodeEnum.Failed);
            }
        }

        /// <summary>
        /// Returns ALM QC installation URL
        /// </summary>
        /// <param name="qcServerUrl"></param>
        /// <returns></returns>
        private static string GetQcCommonInstallationUrl(string qcServerUrl)
        {
            return qcServerUrl + "/CommonMode_index.html";
        }


        /// <summary>
        /// checks Qc version (used for link format, 10 and smaller is old) 
        /// </summary>
        /// <returns>true if this QC is an old one, false otherwise</returns>
        private bool CheckIsOldQc()
        {
            string ver;
            string build;
            bool oldQc = false;
            if (TdConnection != null)
            {
                TdConnection.GetTDVersion(out ver, out build);

                if (ver != null)
                {
                    int intver;
                    int.TryParse(ver, out intver);
                    if (intver <= 10)
                        oldQc = true;
                }
                else
                {
                    oldQc = true;
                }
            }

            return oldQc;
        }

        /// <summary>
        /// connects to QC and logs in
        /// </summary>
        /// <param name="qcServerUrl"></param>
        /// <param name="qcLogin"></param>
        /// <param name="qcPass"></param>
        /// <param name="qcDomain"></param>
        /// <param name="qcProject"></param>
        /// <param name="SSOEnabled"></param>
        /// <returns></returns>
        public bool ConnectToProject(string qcServerUrl, string qcLogin, string qcPass, string qcDomain, string qcProject, bool SSOEnabled, string qcClientID, string qcApiKey)
        {
            string error;
            if (string.IsNullOrWhiteSpace(qcServerUrl)
                || (string.IsNullOrWhiteSpace(qcLogin) && !SSOEnabled)
                || string.IsNullOrWhiteSpace(qcDomain)
                || string.IsNullOrWhiteSpace(qcProject)
                || (SSOEnabled && (string.IsNullOrWhiteSpace(qcClientID)
                || string.IsNullOrWhiteSpace(qcApiKey))))
            {
                error = Resources.AlmRunnerConnParamEmpty;
                ConsoleWriter.WriteErrLine(error);
                return false;
            }

            if (TdConnection != null)
            {
                try
                {
                    if (!SSOEnabled)
                    {
                        TdConnection.InitConnectionEx(qcServerUrl);
                    }
                    else
                    {
                        TdConnection.InitConnectionWithApiKey(qcServerUrl, qcClientID, qcApiKey);
                    }
                }
                catch (Exception ex)
                {
                    ConsoleWriter.WriteLine(ex.Message);
                }
                if (TdConnection.Connected)
                {
                    try
                    {
                        if (!SSOEnabled)
                        {
                            TdConnection.Login(qcLogin, qcPass);
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleWriter.WriteLine(ex.Message);
                    }

                    if (TdConnection.LoggedIn)
                    {
                        try
                        {
                            TdConnection.Connect(qcDomain, qcProject);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }

                        if (TdConnection.ProjectConnected)
                        {
                            return true;
                        }

                        error = Resources.AlmRunnerErrorConnectToProj;
                    }
                    else
                    {
                        error = Resources.AlmRunnerErrorAuthorization;
                    }
                }
                else
                {
                    error = string.Format(Resources.AlmRunnerServerUnreachable, qcServerUrl);
                }
            }
            else //older versions of ALM (< 12.60) 
            {
                try
                {
                    TdConnectionOld.InitConnectionEx(qcServerUrl);
                }
                catch (Exception ex)
                {
                    ConsoleWriter.WriteLine(ex.Message);
                }

                if (TdConnectionOld.Connected)
                {
                    try
                    {
                        TdConnectionOld.Login(qcLogin, qcPass);
                    }
                    catch (Exception ex)
                    {
                        ConsoleWriter.WriteLine(ex.Message);
                    }

                    if (TdConnectionOld.LoggedIn)
                    {
                        try
                        {
                            TdConnectionOld.Connect(qcDomain, qcProject);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }

                        if (TdConnectionOld.ProjectConnected)
                        {
                            return true;
                        }

                        error = Resources.AlmRunnerErrorConnectToProj;
                    }
                    else
                    {
                        error = Resources.AlmRunnerErrorAuthorization;
                    }
                }
                else
                {
                    error = string.Format(Resources.AlmRunnerServerUnreachable, qcServerUrl);
                }
            }
            ConsoleWriter.WriteErrLine(error);
            return false;
        }

        /// <summary>
        /// Returns error message for incorrect installation of Alm QC.
        /// </summary>
        /// <returns></returns>
        private string GetAlmNotInstalledError()
        {
            const string warning = "Could not create scheduler, please follow the instructions on the page to register ALM client on the run machine: ";
            return warning + GetQcCommonInstallationUrl(MQcServer);
        }


        /// <summary>
        /// summarizes test steps after test has run
        /// </summary>
        /// <param name="test"></param>
        /// <returns>a string containing descriptions of step states and messages</returns>
        private string GetTestStepsDescFromQc(ITSTest test)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                //get runs for the test
                RunFactory runFactory = test.RunFactory;
                List runs = runFactory.NewList(string.Empty);
                if (runs.Count == 0)
                    return string.Empty;

                //get steps from run
                StepFactory stepFact = runs[runs.Count].StepFactory;
                List steps = stepFact.NewList(string.Empty);
                if (steps.Count == 0)
                    return string.Empty;

                //go over steps and format a string
                foreach (IStep step in steps)
                {
                    sb.Append("Step: " + step.Name);

                    if (!string.IsNullOrWhiteSpace(step.Status))
                        sb.Append(", Status: " + step.Status);

                    string desc = step["ST_DESCRIPTION"] as string;

                    if (string.IsNullOrEmpty(desc)) continue;

                    desc = string.Format("\n\t{0}", desc.Trim().Replace(LF, TAB).Replace(CR, string.Empty));
                    if (!string.IsNullOrWhiteSpace(desc))
                        sb.AppendLine(desc);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("Exception while reading step data: " + ex.Message);
            }
            return sb.ToString().TrimEnd();
        }


        //------------------------------- Retrieve test sets, test lists and filter tests --------------------------
        /// <summary>
        /// Get a QC folder
        /// </summary>
        /// <param name="testSet"></param>
        /// <returns>the folder object</returns>
        private ITestSetFolder GetFolder(string testSet)
        {
            ITestSetTreeManager tsTreeManager;
            if (TdConnection != null)
            {
                tsTreeManager = (ITestSetTreeManager)TdConnection.TestSetTreeManager;
            }
            else
            {
                tsTreeManager = (ITestSetTreeManager)TdConnectionOld.TestSetTreeManager;
            }


            ITestSetFolder tsFolder = null;
            try
            {
                tsFolder = (ITestSetFolder)tsTreeManager.get_NodeByPath(testSet);
            }
            catch
            {
                //Console.WriteLine("The path '{0}' is not a test set folder or does not exist.", testSet);
            }

            return tsFolder;
        }

        /// <summary>
        /// Finds all folders in the TestSet list, scans their tree and adds all sets under the given folders.
        /// Updates the test sets by expanding the folders, and removing them, so only test sets remain in the collection.
        /// </summary>
        private void FindAllTestSetsUnderFolders()
        {
            List<string> extraSetsList = [];
            List<string> removeSetsList = [];

            //go over all the test sets / testSetFolders and check which is which
            foreach (string testSetOrFolder in TestSets)
            {
                //try getting the folder
                ITestSetFolder tsFolder = GetFolder(@"Root\" + testSetOrFolder.TrimEnd(BACKSLASH_CHAR_ARR));

                //if it exists it's a folder and should be traversed to find all sets
                if (tsFolder != null)
                {
                    removeSetsList.Add(testSetOrFolder);

                    List<string> setList = GetAllTestSetsFromDirTree(tsFolder);
                    extraSetsList.AddRange(setList);
                }

            }

            TestSets.RemoveAll((a) => removeSetsList.Contains(a));
            TestSets.AddRange(extraSetsList);
        }

        /// <summary>
        /// Recursively find all test sets in the QC directory tree, starting from a given folder
        /// </summary>
        /// <param name="tsFolder"></param>
        /// <returns>the list of test sets</returns>
        private List<string> GetAllTestSetsFromDirTree(ITestSetFolder tsFolder)
        {
            List<string> retVal = [];
            List children = tsFolder.FindChildren(string.Empty);
            List testSets = tsFolder.FindTestSets(string.Empty);

            if (testSets != null)
            {
                foreach (ITestSet childSet in testSets)
                {
                    string tsPath = childSet.TestSetFolder.Path.Substring(5).Trim(BACKSLASH_CHAR_ARR);
                    string tsFullPath = string.Format(@"{0}\{1}", tsPath, childSet.Name);
                    retVal.Add(tsFullPath.TrimEnd());
                }
            }

            if (children != null)
            {
                foreach (ITestSetFolder childFolder in children)
                {
                    GetAllTestSetsFromDirTree(childFolder);
                }
            }
            return retVal;
        }

        /// <summary>
        /// Returns the test scheduled to run
        /// </summary>
        /// <param name="testSetList"></param>
        /// <param name="testSuiteName"></param>
        /// <param name="tsFolder"></param>
        /// <returns>the target test set</returns>
        public ITestSet GetTargetTestSet(List testSetList, string testSuiteName, ITestSetFolder tsFolder)
        {
            ITestSet targetTestSet = null;

            if (testSetList != null)
            {
                foreach (ITestSet testSet in testSetList)
                {
                    string tempName = testSet.Name;
                    var testSetFolder = testSet.TestSetFolder as ITestSetFolder;
                    try
                    {
                        if (tempName.Equals(testSuiteName, StringComparison.OrdinalIgnoreCase) && testSetFolder.NodeID == tsFolder.NodeID)
                        {
                            targetTestSet = testSet;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleWriter.WriteLine(ex.Message);
                    }
                }
            }

            if (targetTestSet != null) { return targetTestSet; }

            ConsoleWriter.WriteLine(string.Format(Resources.AlmRunnerCantFindTestSet, testSuiteName));

            //this will make sure run will fail at the end. (since there was an error)
            //Console.WriteLine("Null target test set");
            Launcher.ExitCode = Launcher.ExitCodeEnum.Failed;
            return null;

        }


        /// <summary>
        /// Returns the list of tests in the set
        /// </summary>
        /// <param name="testStorageType"></param>
        /// <param name="tsFolder"></param>
        /// <param name="testSet"></param>
        /// <param name="tsName"></param>
        /// <param name="testSuiteName"></param>
        /// <param name="tsPath"></param>
        /// <param name="isTestPath"></param>
        /// <param name="testName"></param>
        /// <returns>list of tests in set</returns>
        public List GetTestListFromTestSet(TestStorageType testStorageType, ref ITestSetFolder tsFolder,
                                           string testSet, string tsName, ref string testSuiteName,
                                           string tsPath, ref bool isTestPath, ref string testName)
        {
            if (testSuiteName == null) throw new ArgumentNullException("Missing test suite name");
            ITestSetTreeManager tsTreeManager;
            if (TdConnection != null)
            {
                _tdConnection.KeepConnection = true;
                tsTreeManager = (ITestSetTreeManager)_tdConnection.TestSetTreeManager;
            }
            else
            {
                _tdConnectionOld.KeepConnection = true;
                tsTreeManager = (ITestSetTreeManager)_tdConnectionOld.TestSetTreeManager;
            }

            try
            {
                //check test storage type
                if (testStorageType.Equals(TestStorageType.AlmLabManagement))
                {
                    tsFolder = (ITestSetFolder)tsTreeManager.NodeByPath["Root"];
                    testSet = GetTestSetById(tsFolder, Convert.ToInt32(tsName), ref testSuiteName);
                }
                else
                {
                    tsFolder = (ITestSetFolder)tsTreeManager.get_NodeByPath(tsPath);
                }

                isTestPath = false;
            }
            catch (COMException ex)
            {
                //not found
                tsFolder = null;
                Console.WriteLine(ex.Message);
            }

            // test set not found, try to find specific test by path

            if (tsFolder == null)
            {
                // if test set path was not found, the path may points to specific test
                // remove the test name and try find test set with parent path
                try
                {
                    int pos = tsPath.LastIndexOf("\\", StringComparison.Ordinal) + 1;
                    testName = testSuiteName;
                    testSuiteName = tsPath.Substring(pos, tsPath.Length - pos);
                    tsPath = tsPath.Substring(0, pos - 1);
                    tsFolder = (ITestSetFolder)tsTreeManager.get_NodeByPath(tsPath);
                    isTestPath = true;
                }
                catch (COMException ex)
                {
                    tsFolder = null;
                    Console.WriteLine("Exception: " + ex.Message);
                }
            }
            if (tsFolder != null)
            {
                if (tsFolder.NodeID == 0) // this is the Root folder, which cannot contain TestSets
                {
                    ConsoleWriter.WriteErrLine(Resources.AlmRunnerMissingOrInvalidTestSetPath);
                    Launcher.ExitCode = Launcher.ExitCodeEnum.Failed;
                    return null;
                }
                List testList = tsFolder.FindTestSets(testSuiteName);

                if (testList == null)
                {
                    ConsoleWriter.WriteErrLine(string.Format(Resources.AlmRunnerCantFindTestSet, testSuiteName));
                    //this will make sure run will fail at the end. (since there was an error)
                    Launcher.ExitCode = Launcher.ExitCodeEnum.Failed;
                    return null;
                }
                foreach (ITestSet t in testList)
                {
                    Console.WriteLine(string.Format("ID = {0}, TestSet = {1}, TestSetFolder = {2}", t.ID, t.Name, t.TestSetFolder.Name));
                }
                return testList;
            }

            //node wasn't found, folder = null
            ConsoleWriter.WriteErrLine(string.Format(Resources.AlmRunnerNoSuchFolder, tsFolder));

            //this will make sure run will fail at the end. (since there was an error)
            Launcher.ExitCode = Launcher.ExitCodeEnum.Failed;
            return null;
        }

        /// <summary>
        /// Check if some test is contained or not in a tests list
        /// </summary>
        /// <param name="testList"></param>
        /// <param name="test"></param>
        /// <returns></returns>
        public bool ListContainsTest(List<ITSTest> testList, ITSTest test)
        {
            for (var index = testList.Count - 1; index >= 0; index--)
            {
                if (testList[index].TestName.Equals(test.TestName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Filter a list of tests by different by name and/or status
        /// </summary>
        /// <param name="targetTestSet"></param>
        /// <param name="isTestPath"></param>
        /// <param name="testName"></param>
        /// <param name="isFilterSelected"></param>
        /// <param name="filterByStatuses"></param>
        /// <param name="filterByName"></param>
        /// <returns>the filtered list of tests</returns>
        public IList FilterTests(ITestSet targetTestSet, bool isTestPath, string testName, bool isFilterSelected, List<string> filterByStatuses, string filterByName)
        {
            TSTestFactory tsTestFactory = targetTestSet.TSTestFactory;

            ITDFilter2 tdFilter = tsTestFactory.Filter;

            tdFilter["TC_CYCLE_ID"] = targetTestSet.ID.ToString();
            IList testList = tsTestFactory.NewList(tdFilter.Text);

            List<ITSTest> testsFilteredByStatus = [];

            if (isFilterSelected && (!string.IsNullOrEmpty(filterByName) || filterByStatuses.Count > 0))
            {
                //filter by status
                foreach (string status in filterByStatuses)
                {
                    tdFilter["TC_STATUS"] = status;
                    IList statusList1 = tsTestFactory.NewList(tdFilter.Text);
                    for (int index = statusList1.Count; index > 0; index--)
                    {
                        testsFilteredByStatus.Add(statusList1[index]);
                    }
                }

                //filter by name
                for (int index = testList.Count; index > 0; index--)
                {
                    string tListIndexName = testList[index].Name;
                    string tListIndexTestName = testList[index].TestName;

                    if (!string.IsNullOrEmpty(filterByName))
                    {
                        if (filterByStatuses.Count == 0)
                        {
                            //only by name
                            if (!tListIndexName.ToLower().Contains(filterByName.ToLower()) &&
                            !tListIndexTestName.ToLower().Contains(filterByName.ToLower()))
                            {
                                testList.Remove(index);
                            }
                        }
                        else //by name and statuses
                        {
                            if (!tListIndexName.ToLower().Contains(filterByName.ToLower()) &&
                                !tListIndexTestName.ToLower().Contains(filterByName.ToLower()) &&
                                !ListContainsTest(testsFilteredByStatus, testList[index]))
                            {
                                testList.Remove(index);
                            }
                        }
                    }
                    else
                    {   //only by statuses
                        if (!ListContainsTest(testsFilteredByStatus, testList[index]))
                        {
                            testList.Remove(index);
                        }
                    }
                }
            }

            if (isTestPath)
            {
                // index starts from 1 !!!
                int tListCount = 0;
                tListCount = testList.Count;

                // must loop from end to begin
                for (var index = tListCount; index > 0; index--)
                {
                    string tListIndexName = testList[index].Name;
                    string tListIndexTestName = testList[index].TestName;
                    if (!string.IsNullOrEmpty(tListIndexName) && !string.IsNullOrEmpty(testName) && !testName.Equals(tListIndexTestName))
                    {
                        testList.Remove(index);
                    }
                }
            }

            return testList;
        }

        /// <summary>
        /// Search test set in QC by the given ID
        /// </summary>
        /// <param name="tsFolder"></param>
        /// <param name="testSetId"></param>
        /// <param name="testSuiteName"></param>
        /// <returns>the test set identified by the given id or empty string in case the test set was not found</returns>
        private string GetTestSetById(ITestSetFolder tsFolder, int testSetId, ref string testSuiteName)
        {
            List children = tsFolder.FindChildren(string.Empty);
            List testSets = tsFolder.FindTestSets(string.Empty);

            if (testSets != null)
            {
                foreach (ITestSet childSet in testSets)
                {
                    if (childSet.ID != testSetId) continue;
                    string tsPath = childSet.TestSetFolder.Path.Substring(5).Trim(BACKSLASH_CHAR_ARR);
                    string tsFullPath = string.Format(@"{0}\{1}", tsPath, childSet.Name);
                    testSuiteName = childSet.Name;
                    return tsFullPath.TrimEnd();
                }
            }

            if (children != null)
            {
                foreach (ITestSetFolder childFolder in children)
                {
                    GetAllTestSetsFromDirTree(childFolder);
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets test index given it's name
        /// </summary>
        /// <param name="strName"></param>
        /// <param name="results"></param>
        /// <returns>the test index</returns>
        public int GetIndexOfTestIdentifiedByName(string strName, TestSuiteRunResults results)
        {
            var retVal = -1;

            for (var i = 0; i < results.TestRuns.Count; ++i)
            {
                var res = results.TestRuns[i];
                if (res == null || res.TestName != strName) continue;
                retVal = i;
                break;
            }
            return retVal;
        }

        //------------------------------- Identify and set test parameters --------------------------
        /// <summary>
        /// Set the parameters for a list of tests
        /// </summary>
        /// <param name="tList"></param>
        /// <param name="testParameters"></param>
        /// <param name="runHost"></param>
        /// <param name="runMode"></param>
        /// <param name="runDesc"></param>
        /// <param name="scheduler"></param>
        public void SetTestParameters(IList tList, string testParameters, string runHost, QcRunMode runMode, TestSuiteRunResults runDesc, ITSScheduler scheduler)
        {
            var i = 1;
            foreach (ITSTest3 test in tList)
            {
                if (test.Type.Equals("SERVICE-TEST")) //API test
                {
                    if (!string.IsNullOrEmpty(testParameters))
                    {
                        SetApiTestParameters(test, testParameters);
                    }
                }

                if (test.Type.Equals("QUICKTEST_TEST")) //GUI test
                {
                    if (!(string.IsNullOrEmpty(testParameters)))
                    {
                        SetGuiTestParameters(test, testParameters);
                    }
                }

                var runOnHost = runHost;
                if (runMode == QcRunMode.RUN_PLANNED_HOST)
                {
                    runOnHost = test.HostName; //test["TC_HOST_NAME"]; //runHost;
                    if (string.IsNullOrWhiteSpace(runOnHost))
                    {
                        runOnHost = LOCALHOST;
                    }
                }

                //if host isn't taken from QC (PLANNED) and not from the test definition (REMOTE), take it from LOCAL (machineName)
                var hostName = runOnHost;
                if (runMode == QcRunMode.RUN_LOCAL)
                {
                    hostName = Environment.MachineName;
                }

                if (runMode == QcRunMode.RUN_PLANNED_HOST)
                {
                    ConsoleWriter.WriteLine(string.Format(Resources.AlmRunnerDisplayTestRunOnPlannedHost, i, test.Name, hostName));
                }
                else
                {
                    ConsoleWriter.WriteLine(string.Format(Resources.AlmRunnerDisplayTestRunOnHost, i, test.Name, hostName));
                }

                scheduler.RunOnHost[test.ID] = runOnHost;

                var testResults = new TestRunResults { TestName = test.Name };

                runDesc.TestRuns.Add(testResults);

                i += 1;
            }
        }

        /// <summary>
        /// Checks if test parameters list is valid or not
        /// </summary>
        /// <param name="paramsString"></param>
        /// <param name="parameters"></param>
        /// <param name="parameterNames"></param>
        /// <param name="parameterValues"></param>
        /// <returns>true if parameters the list of parameters is valid, false otherwise</returns>
        public bool ValidateListOfParams(string paramsString, string[] parameters, List<string> parameterNames, List<string> parameterValues)
        {
            if (parameters == null) throw new ArgumentNullException("parameters");

            if (!string.IsNullOrEmpty(paramsString))
            {
                parameters = paramsString.Split(COMMA_CHAR_ARR);
                foreach (var parameterPair in parameters)
                {
                    if (!string.IsNullOrEmpty(parameterPair))
                    {
                        string[] pair = parameterPair.Split(':');

                        bool isValidParameter = ValidateParameters(pair[0], parameterNames, true);

                        if (!isValidParameter)
                        {
                            Console.WriteLine(Resources.MissingParameterName);
                            return false;
                        }

                        isValidParameter = ValidateParameters(pair[1], parameterValues, false);
                        if (!isValidParameter)
                        {
                            Console.WriteLine(Resources.MissingParameterValue);
                            return false;
                        }
                    }
                }
            }

            return true;
        }


        /// <summary>
        /// Validates test parameters
        /// </summary>
        /// <param name="param"></param>
        /// <param name="parameterList"></param>
        /// <param name="isParameter"></param>
        /// <returns>true if parameter is valid, false otherwise</returns>
        public bool ValidateParameters(string param, List<string> parameterList, bool isParameter)
        {
            if (!string.IsNullOrEmpty(param) && param != " ")
            {
                param = param.Trim();
                param = param.Remove(param.Length - 1, 1);
                param = param.Remove(0, 1);
                parameterList.Add(param);
            }
            else
            {
                return false;
            }
            return true;
        }


        /// <summary>
        /// Set test parameters for an API test
        /// </summary>
        /// <param name="test"></param>
        /// <param name="paramsString"></param>
        private void SetApiTestParameters(ITSTest3 test, string paramsString)
        {
            List<string> parameterNames = [];
            List<string> parameterValues = [];

            if (!string.IsNullOrEmpty(paramsString))
            {
                string[] parameters = paramsString.Split(COMMA_CHAR_ARR);
                bool validParameters = ValidateListOfParams(paramsString, parameters, parameterNames, parameterValues);

                ISupportParameterValues paramTestValues = (ISupportParameterValues)test;
                ParameterValueFactory parameterValueFactory = paramTestValues.ParameterValueFactory;
                List listOfParameters = parameterValueFactory.NewList(string.Empty);
                var index = 0;
                if (parameterValues.Count <= 0 || listOfParameters.Count != parameterValues.Count) return;
                foreach (ParameterValue parameter in listOfParameters)
                {
                    parameter.ActualValue = parameterValues.ElementAt(index++);
                    parameter.Post();
                }
            }
        }

        /// <summary>
        /// Set test parameters for a GUI test
        /// </summary>
        /// <param name="test"></param>
        /// <param name="strParams"></param>
        private void SetGuiTestParameters(ITSTest3 test, string strParams)
        {
            string xmlParams = string.Empty;
            List<string> paramNames = [];
            List<string> paramValues = [];

            if (!string.IsNullOrEmpty(strParams))
            {
                string[] @params = strParams.Split(COMMA_CHAR_ARR);

                bool validParams = ValidateListOfParams(strParams, @params, paramNames, paramValues);

                if (validParams)
                {
                    xmlParams = "<?xml version=\"1.0\"?><Parameters>";
                    for (int i = 0; i < @params.Length; i++)
                    {
                        xmlParams = xmlParams + "<Parameter><Name><![CDATA[" + paramNames.ElementAt(i) + "]]></Name>"
                                        + "<Value><![CDATA[" + paramValues.ElementAt(i) + "]]>"
                                        + "</Value></Parameter>";
                    }

                    xmlParams += "</Parameters>";
                }

            }

            if (xmlParams != string.Empty)
            {
                test["TC_EPARAMS"] = xmlParams;
                test.Post();
            }
        }

        /// <summary>
        /// gets the type for a QC test
        /// </summary>
        /// <param name="currentTest"></param>
        /// <returns></returns>
        private string GetTestType(dynamic currentTest)
        {
            string testType = currentTest.Test.Type;

            testType = testType.ToUpper() == "SERVICE-TEST" ? TestType.ST.ToString() : TestType.QTP.ToString();

            return testType;
        }


        // ------------------------- Run tests and update test results --------------------------------

        /// <summary>
        /// runs the tests given to the object.
        /// </summary>
        /// <returns></returns>
        public override TestSuiteRunResults Run()
        {
            if (!Connected)
                return null;

            TestSuiteRunResults activeRunDescription = new TestSuiteRunResults();

            //find all the testSets under given folders
            try
            {
                FindAllTestSetsUnderFolders();
            }
            catch (Exception ex)
            {

                ConsoleWriter.WriteErrLine(string.Format(Resources.AlmRunnerErrorBadQcInstallation, ex.Message, ex.StackTrace));
                return null;
            }

            //run all the TestSets
            foreach (string testSetItem in TestSets)
            {
                string testSet = testSetItem.TrimEnd(BACKSLASH_CHAR_ARR);
                string tsName = testSet;
                int pos = testSetItem.LastIndexOf(BACKSLAH);

                string testSetDir = string.Empty;
                string testParameters = string.Empty;

                if (pos != -1)
                {
                    testSetDir = testSet.Substring(0, pos).Trim(BACKSLASH_CHAR_ARR);
                    if (testSetItem.IndexOf(" ", StringComparison.Ordinal) != -1 && testSet.Count(x => x == BLANK) >= 1)
                    {
                        if (!testSet.Contains(':'))//test has no parameters attached
                        {
                            tsName = testSet.Substring(pos, testSet.Length - pos).Trim(BACKSLASH_CHAR_ARR);
                        }
                        else
                        {
                            int quotationMarkIndex = testSet.IndexOf("\"", StringComparison.Ordinal);
                            if (quotationMarkIndex > pos)
                            {
                                tsName = testSet.Substring(pos, quotationMarkIndex - pos).Trim(BACKSLASH_CHAR_ARR).TrimEnd(BLANK);
                                testParameters = testSet.Substring(quotationMarkIndex, testSet.Length - quotationMarkIndex).Trim(BACKSLASH_CHAR_ARR);
                            }
                        }
                    }
                    else
                    {
                        tsName = testSet.Substring(pos, testSet.Length - pos).Trim(BACKSLASH_CHAR_ARR);
                    }
                }

                TestSuiteRunResults runResults = RunTestSet(testSetDir, tsName, testParameters, Timeout, RunMode, RunHost, IsFilterSelected, FilterByName, FilterByStatuses, Storage);
                if (runResults != null)
                    activeRunDescription.AppendResults(runResults);
            }

            return activeRunDescription;
        }


        /// <summary>
        /// Runs a test set with given parameters (and a valid connection to the QC server)
        /// </summary>
        /// <param name="tsFolderName">testSet folder name</param>
        /// <param name="tsName">testSet name</param>
        /// <param name="testParameters"></param>
        /// <param name="timeout">-1 for unlimited, or number of milliseconds</param>
        /// <param name="runMode">run on LocalMachine or remote</param>
        /// <param name="runHost">if run on remote machine - remote machine name</param>
        /// <param name="isFilterSelected"></param>
        /// <param name="filterByName"></param>
        /// <param name="filterByStatuses"></param>
        /// <param name="testStorageType"></param>
        /// <returns></returns>
        public TestSuiteRunResults RunTestSet(string tsFolderName, string tsName, string testParameters, double timeout, QcRunMode runMode, string runHost,
                                              bool isFilterSelected, string filterByName, List<string> filterByStatuses, TestStorageType testStorageType)
        {

            string testSuiteName = tsName.TrimEnd();
            ITestSetFolder tsFolder = null;
            string testSet = string.Empty;
            string tsPath = "Root\\" + tsFolderName;
            bool isTestPath = false;
            string currentTestSetInstances = string.Empty;
            string testName = string.Empty;
            TestSuiteRunResults runDesc = new TestSuiteRunResults();
            TestRunResults activeTestDesc = null;
            List testSetList;

            //get list of test sets
            try
            {
                testSetList = GetTestListFromTestSet(testStorageType, ref tsFolder, testSet, tsName, ref testSuiteName, tsPath, ref isTestPath, ref testName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("Unable to retrieve the list of tests");
                ConsoleWriter.WriteLine(string.Format(Resources.AlmRunnerCantFindTestSet, testSuiteName));
                Console.WriteLine(ex.Message);
                //this will make sure run will fail at the end. (since there was an error)
                Launcher.ExitCode = Launcher.ExitCodeEnum.Failed;
                return null;
            }

            //get target test set
            ITestSet targetTestSet = null;
            try
            {
                targetTestSet = GetTargetTestSet(testSetList, testSuiteName, tsFolder);
            }
            catch (Exception)
            {
                Console.WriteLine("Empty target test set list");
            }

            if (targetTestSet == null)
            {
                return null;
            }

            ConsoleWriter.WriteLine(Resources.GeneralDoubleSeperator);
            ConsoleWriter.WriteLine(Resources.AlmRunnerStartingExecution);
            ConsoleWriter.WriteLine(string.Format(Resources.AlmRunnerDisplayTest, testSuiteName, targetTestSet.ID));

            //start execution
            ITSScheduler scheduler = null;
            try
            {
                //need to run this to install everything needed http://AlmServer:8080/qcbin/start_a.jsp?common=true
                //start the scheduler
                scheduler = targetTestSet.StartExecution(string.Empty);
                if (targetTestSet == null)
                {
                    Console.WriteLine("empty target test set");
                }
                currentTestSetInstances = GetTestInstancesString(targetTestSet);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            if (scheduler == null)
            {
                Console.WriteLine(GetAlmNotInstalledError());

                //proceeding with program execution is tasteless, since nothing will run without a properly installed QC.
                Environment.Exit((int)Launcher.ExitCodeEnum.Failed);
            }

            //filter tests
            IList filteredTestList = FilterTests(targetTestSet, isTestPath, testName, isFilterSelected, filterByStatuses, filterByName);

            //set run host
            try
            {
                //set up for the run depending on where the test instances are to execute
                switch (runMode)
                {
                    case QcRunMode.RUN_LOCAL:
                        // run all tests on the local machine
                        scheduler.RunAllLocally = true;
                        break;
                    case QcRunMode.RUN_REMOTE:
                        // run tests on a specified remote machine
                        scheduler.TdHostName = runHost;
                        break;
                    // RunAllLocally must not be set for remote invocation of tests. As such, do not do this: Scheduler.RunAllLocally = False
                    case QcRunMode.RUN_PLANNED_HOST:
                        // run on the hosts as planned in the test set
                        scheduler.RunAllLocally = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                ConsoleWriter.WriteLine(string.Format(Resources.AlmRunnerProblemWithHost, ex.Message));
            }


            //set test parameters
            if (filteredTestList.Count > 0)
            {
                SetTestParameters(filteredTestList, testParameters, runHost, runMode, runDesc, scheduler);
            }

            //start test runner
            if (filteredTestList.Count == 0)
            {
                //ConsoleWriter.WriteErrLine("Specified test not found on ALM, please check your test path.");
                //this will make sure run will fail at the end. (since there was an error)
                //Launcher.ExitCode = Launcher.ExitCodeEnum.Failed;
                Console.WriteLine(Resources.AlmTestSetsRunnerNoTestAfterApplyingFilters);
                return null;
            }

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                //tests are actually run
                scheduler.Run(filteredTestList);
            }
            catch (Exception ex)
            {
                ConsoleWriter.WriteLine(Resources.AlmRunnerRunError + ex.Message);
            }

            ConsoleWriter.WriteLine(Resources.AlmRunnerSchedStarted + DateTime.Now.ToString(Launcher.DateFormat));
            ConsoleWriter.WriteLine(Resources.SingleSeperator);

            IExecutionStatus executionStatus = scheduler.ExecutionStatus;

            ITSTest prevTest = null;
            ITSTest currentTest = null;
            string abortFilename = string.Format(@"{0}\stop{1}.txt", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Launcher.UniqueTimeStamp);

            if (testStorageType == TestStorageType.AlmLabManagement)
            {
                timeout *= 60;
            }
            //update run result description
            UpdateTestsResultsDescription(ref activeTestDesc, runDesc, scheduler, targetTestSet, currentTestSetInstances, timeout, executionStatus, sw, ref prevTest, ref currentTest, abortFilename);

            //close last test
            if (prevTest != null)
            {
                WriteTestRunSummary(prevTest);
            }

            //done with all tests, stop collecting output in the testRun object.
            ConsoleWriter.ActiveTestRun = null;

            string testPath = string.Format(@"Root\{0}\{1}\", tsFolderName, testSuiteName);
            SetTestResults(ref currentTest, executionStatus, targetTestSet, activeTestDesc, runDesc, testPath, abortFilename);

            //update the total runtime
            runDesc.TotalRunTime = sw.Elapsed;

            // test has executed in time
            if (timeout == -1 || sw.Elapsed.TotalSeconds <= timeout)
            {
                ConsoleWriter.WriteLine(string.Format(Resources.AlmRunnerTestsetDone, testSuiteName, DateTime.Now.ToString(Launcher.DateFormat)));
            }
            else
            {
                _blnRunCancelled = true;
                ConsoleWriter.WriteLine(Resources.GeneralTimedOut);

                scheduler.Stop(currentTestSetInstances);

                Launcher.ExitCode = Launcher.ExitCodeEnum.Aborted;
            }
            return runDesc;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentTest"></param>
        /// <param name="executionStatus"></param>
        /// <param name="targetTestSet"></param>
        /// <param name="activeTestDesc"></param>
        /// <param name="runDesc"></param>
        /// <param name="testPath"></param>
        /// <param name="abortFilename"></param>
        private void SetTestResults(ref ITSTest currentTest, IExecutionStatus executionStatus, ITestSet targetTestSet, TestRunResults activeTestDesc, TestSuiteRunResults runDesc, string testPath, string abortFilename)
        {
            if (currentTest == null) throw new ArgumentNullException("Current test set is null.");

            if (activeTestDesc == null) throw new ArgumentNullException("The test run results are empty.");

            // write the status for each test
            for (var k = 1; k <= executionStatus.Count; ++k)
            {
                if (File.Exists(abortFilename))
                {
                    break;
                }

                TestExecStatus testExecStatusObj = executionStatus[k];
                currentTest = targetTestSet.TSTestFactory[testExecStatusObj.TSTestId];

                if (currentTest == null)
                {
                    ConsoleWriter.WriteLine(string.Format("currentTest is null for test.{0} after whole execution", k));
                    continue;
                }

                activeTestDesc = UpdateTestStatus(runDesc, targetTestSet, testExecStatusObj, false);
                UpdateCounters(activeTestDesc, runDesc);

                activeTestDesc.TestPath = testPath + currentTest.TestName;
            }
        }

        /// <summary>
        /// updates the test status in our list of tests
        /// </summary>
        /// <param name="runResults"></param>
        /// <param name="targetTestSet"></param>
        /// <param name="testExecStatusObj"></param>
        /// <param name="onlyUpdateState"></param>
        private TestRunResults UpdateTestStatus(TestSuiteRunResults runResults, ITestSet targetTestSet, TestExecStatus testExecStatusObj, bool onlyUpdateState)
        {
            TestRunResults qTest = null;
            ITSTest currentTest = null;
            try
            {
                //find the test for the given status object
                currentTest = targetTestSet.TSTestFactory[testExecStatusObj.TSTestId];

                //find the test in our list
                var testIndex = GetIndexOfTestIdentifiedByName(currentTest.Name, runResults);
                if (testIndex == -1)
                {
                    Console.WriteLine(string.Format("No test index exist for the test [{0}]", currentTest.Name));
                    return null;
                }

                qTest = runResults.TestRuns[testIndex];
                if (qTest.TestType == null)
                {
                    qTest.TestType = GetTestType(currentTest);
                }

                //update the state
                qTest.PrevTestState = qTest.TestState;
                qTest.TestState = GetTsStateFromQcState(testExecStatusObj.Status);

                if (!onlyUpdateState)
                {
                    try
                    {
                        //duration and status are updated according to the run
                        qTest.Runtime = TimeSpan.FromSeconds(currentTest.LastRun.Field("RN_DURATION"));
                    }
                    catch
                    {
                        //a problem getting duration, maybe the test isn't done yet - don't stop the flow..
                    }

                    switch (qTest.TestState)
                    {
                        case TestState.Failed:
                            qTest.FailureDesc = GenerateFailedLog(currentTest.LastRun);

                            if (string.IsNullOrWhiteSpace(qTest.FailureDesc))
                                qTest.FailureDesc = string.Format("{0} : {1}", testExecStatusObj.Status, testExecStatusObj.Message);
                            break;
                        case TestState.Error:
                            qTest.ErrorDesc = string.Format("{0} : {1}", testExecStatusObj.Status, testExecStatusObj.Message);
                            break;
                        case TestState.Waiting:
                        case TestState.Running:
                        case TestState.NoRun:
                        case TestState.Passed:
                        case TestState.Warning:
                        case TestState.Unknown:
                        default:
                            break;
                    }

                    var runId = GetTestRunId(currentTest);
                    string linkStr = GetTestRunLink(runId);

                    string statusString = GetTsStateFromQcState(testExecStatusObj.Status).ToString();
                    ConsoleWriter.WriteLine(string.Format(Resources.AlmRunnerTestStat, currentTest.Name, statusString, testExecStatusObj.Message, linkStr));
                    ConsoleWriter.WriteLine(string.Empty);
                    runResults.TestRuns[testIndex] = qTest;
                }
            }
            catch (Exception ex)
            {
                if (currentTest != null)
                    ConsoleWriter.WriteLine(string.Format(Resources.AlmRunnerErrorGettingStat, currentTest.Name,
                        ex.Message));
            }

            return qTest;
        }

        /// <summary>
        /// Update run results description
        /// </summary>
        /// <param name="activeTestDesc"></param>
        /// <param name="runDesc"></param>
        /// <param name="scheduler"></param>
        /// <param name="targetTestSet"></param>
        /// <param name="currentTestSetInstances"></param>
        /// <param name="timeout"></param>
        /// <param name="executionStatus"></param>
        /// <param name="sw"></param>
        /// <param name="prevTest"></param>
        /// <param name="currentTest"></param>
        /// <param name="abortFilename"></param>
        public void UpdateTestsResultsDescription(ref TestRunResults activeTestDesc, TestSuiteRunResults runDesc,
                                             ITSScheduler scheduler, ITestSet targetTestSet,
                                             string currentTestSetInstances, double timeout,
                                             IExecutionStatus executionStatus, Stopwatch sw,
                                             ref ITSTest prevTest, ref ITSTest currentTest, string abortFilename)
        {
            var tsExecutionFinished = false;

            while (!tsExecutionFinished)
            {
                executionStatus.RefreshExecStatusInfo("all", true);
                tsExecutionFinished = executionStatus.Finished;

                if (File.Exists(abortFilename))
                {
                    break;
                }
                for (var j = 1; j <= executionStatus.Count; ++j)
                {
                    try
                    {
                        ITestExecStatus baseTestExecObj = executionStatus[j];
                        TestExecStatus testExecStatusObj = (TestExecStatus)baseTestExecObj;

                        if (testExecStatusObj == null)
                        {
                            Console.WriteLine("testExecStatusObj is null");
                            continue;
                        }
                        else
                        {
                            currentTest = targetTestSet.TSTestFactory[testExecStatusObj.TSTestId];
                        }
                        if (currentTest == null)
                        {
                            ConsoleWriter.WriteLine(string.Format("currentTest is null for test.{0} during execution", j));
                            continue;
                        }
                        activeTestDesc = UpdateTestStatus(runDesc, targetTestSet, testExecStatusObj, true);

                        if (activeTestDesc != null)
                        {
                            if (activeTestDesc.PrevTestState != activeTestDesc.TestState)
                            {
                                TestState testState = activeTestDesc.TestState;
                                if (testState == TestState.Running)
                                {
                                    if (activeTestDesc.StartDateTime == null)
                                    {
                                        activeTestDesc.StartDateTime = DateTime.Now;
                                    }
                                    int prevRunId = GetTestRunId(currentTest);
                                    if (prevRunId == -1)
                                    {
                                        //Console.WriteLine("No test runs exist for this test");
                                        continue;
                                    }
                                    activeTestDesc.PrevRunId = prevRunId;

                                    //closing previous test
                                    if (prevTest != null)
                                    {
                                        WriteTestRunSummary(prevTest);
                                    }

                                    //starting new test
                                    prevTest = currentTest;
                                    //assign the new test the console writer so it will gather the output

                                    ConsoleWriter.ActiveTestRun = activeTestDesc;

                                    ConsoleWriter.WriteLine(string.Format("{0} Running: {1}", DateTime.Now.ToString(Launcher.DateFormat), currentTest.Name));
                                    activeTestDesc.TestName = currentTest.Name;
                                    //tell user that the test is running
                                    ConsoleWriter.WriteLine(string.Format("{0} Running test: {1}, Test id: {2}, Test instance id: {3}", DateTime.Now.ToString(Launcher.DateFormat), activeTestDesc.TestName, testExecStatusObj.TestId, testExecStatusObj.TSTestId));

                                    //start timing the new test run
                                    string folderName = string.Empty;
                                    ITestSetFolder folder = targetTestSet.TestSetFolder as ITestSetFolder;

                                    if (folder != null)
                                        folderName = folder.Name.Replace(".", "_");

                                    //the test group is it's test set. (dots are problematic since jenkins parses them as separators between package and class)
                                    activeTestDesc.TestGroup = string.Format(@"{0}\{1}", folderName, targetTestSet.Name).Replace(".", "_");
                                }

                                TestState enmState = GetTsStateFromQcState(testExecStatusObj.Status);
                                string statusString = enmState.ToString();

                                if (enmState == TestState.Running)
                                {
                                    ConsoleWriter.WriteLine(string.Format(Resources.AlmRunnerStat, activeTestDesc.TestName, testExecStatusObj.TSTestId, statusString));
                                }
                                else if (enmState != TestState.Waiting)
                                {
                                    ConsoleWriter.WriteLine(string.Format(Resources.AlmRunnerStatWithMessage, activeTestDesc.TestName, testExecStatusObj.TSTestId, statusString, testExecStatusObj.Message));
                                }
                                if (File.Exists(abortFilename))
                                {
                                    scheduler.Stop(currentTestSetInstances);
                                    //stop working
                                    Environment.Exit((int)Launcher.ExitCodeEnum.Aborted);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }
                    catch (InvalidCastException ex)
                    {
                        Console.WriteLine("Conversion failed: " + ex.Message);
                    }
                    finally
                    {
                    }
                }

                //wait 0.2 seconds
                Thread.Sleep(200);

                //check for abortion
                if (File.Exists(abortFilename))
                {
                    _blnRunCancelled = true;

                    ConsoleWriter.WriteLine(Resources.GeneralStopAborted);

                    //stop all test instances in this testSet.
                    scheduler.Stop(currentTestSetInstances);

                    ConsoleWriter.WriteLine(Resources.GeneralAbortedByUser);

                    //stop working 
                    Environment.Exit((int)Launcher.ExitCodeEnum.Aborted);
                }

                // check timeout
                if (timeout != -1)
                {
                    double elpSecs = sw.Elapsed.TotalSeconds;
                    if (elpSecs > timeout)
                    {
                        // timeout
                        ConsoleWriter.WriteErrLine(string.Format("Timeout! Elapsed: {0} seconds; Timeout: {1} seconds.", Math.Ceiling(elpSecs), timeout));
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// gets a link string for the test run in Qc
        /// </summary>
        /// <param name="runId"></param>
        /// <returns></returns>
        private string GetTestRunLink(int runId)
        {
            if (CheckIsOldQc())
            {
                return string.Empty;
            }
            const string URL_FORMAT = "{0}://{1}.{2}.{3}/TestRunsModule-00000000090859589?EntityType=IRun&EntityID={4}";
            var mQcServer = MQcServer.Trim();
            var prefix = mQcServer.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "tds" : "td";
            mQcServer = Regex.Replace(mQcServer, "^http[s]?://", string.Empty, RegexOptions.IgnoreCase);
            return string.Format(URL_FORMAT, prefix, MQcProject, MQcDomain, mQcServer, runId);
        }

        /// <summary>
        /// gets the runId for the given test
        /// </summary>
        /// <param name="currentTest">a test instance</param>
        /// <returns>the run id</returns>
        private static int GetTestRunId(ITSTest currentTest)
        {
            int runId = -1;

            if (currentTest == null) return runId;
            if (currentTest.LastRun != null)
            {
                IRun lastRun = currentTest.LastRun as IRun;
                runId = lastRun.ID;
                return runId;
            }

            return runId;
        }


        /// <summary>
        /// writes a summary of the test run after it's over
        /// </summary>
        /// <param name="prevTest"></param>
        private void WriteTestRunSummary(ITSTest prevTest)
        {
            int prevRunId = ConsoleWriter.ActiveTestRun.PrevRunId;
            if (TdConnection != null)
            {
                _tdConnection.KeepConnection = true;
            }
            else
            {
                _tdConnectionOld.KeepConnection = true;
            }


            int runId = GetTestRunId(prevTest);

            if (runId > prevRunId)
            {
                string stepsString = GetTestStepsDescFromQc(prevTest);

                if (string.IsNullOrWhiteSpace(stepsString) && ConsoleWriter.ActiveTestRun.TestState != TestState.Error)
                    stepsString = GetTestRunLog(prevTest);

                if (!string.IsNullOrWhiteSpace(stepsString))
                    ConsoleWriter.WriteLine(stepsString);

                string linkStr = GetTestRunLink(runId);
                if (string.IsNullOrEmpty(linkStr))
                {
                    Console.WriteLine(Resources.OldVersionOfQC);
                }
                else
                {
                    ConsoleWriter.WriteLine(LF + string.Format(Resources.AlmRunnerDisplayLink, LF + linkStr + LF));
                }
            }
            ConsoleWriter.WriteLine(DateTime.Now.ToString(Launcher.DateFormat) + " " + Resources.AlmRunnerTestCompleteCaption + " " + prevTest.Name +
                ((runId > prevRunId) ? ", " + Resources.AlmRunnerRunIdCaption + " " + runId : string.Empty)
                + "\n-------------------------------------------------------------------------------------------------------");
        }


        /// <summary>
        /// Writes a summary of the test run after it's over
        /// </summary>
        private string GetTestInstancesString(ITestSet set)
        {
            var retVal = string.Empty;
            try
            {
                TSTestFactory factory = set.TSTestFactory;
                List list = factory.NewList(string.Empty);

                if (list == null)
                    return string.Empty;
                retVal = string.Join(COMMA, list.Cast<ITSTest>().Select(t => t.ID as string));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return retVal;
        }


        /// <summary>
        /// Update test run summary
        /// </summary>
        /// <param name="test"></param>
        /// <param name="testSuite"></param>
        private void UpdateCounters(TestRunResults test, TestSuiteRunResults testSuite)
        {
            if (test.TestState != TestState.Running &&
                test.TestState != TestState.Waiting &&
                test.TestState != TestState.Unknown)
                ++testSuite.NumTests;

            switch (test.TestState)
            {
                case TestState.Failed:
                    ++testSuite.NumFailures;
                    break;
                case TestState.Error:
                    ++testSuite.NumErrors;
                    break;
            }
        }

        /// <summary>
        /// translate the qc states into a state enum
        /// </summary>
        /// <param name="qcTestStatus"></param>
        /// <returns></returns>
        private TestState GetTsStateFromQcState(string qcTestStatus)
        {
            if (TdConnection == null && TdConnectionOld == null)
            {
                return TestState.Failed;
            }

            if (qcTestStatus == null)
                return TestState.Unknown;
            switch (qcTestStatus)
            {
                case "Waiting":
                    return TestState.Waiting;
                case "Error":
                    return TestState.Error;
                case "No Run":
                    return TestState.NoRun;
                case "Running":
                case "Connecting":
                    return TestState.Running;
                case "Success":
                case "Finished":
                case "FinishedPassed":
                    return TestState.Passed;
                case "FinishedFailed":
                    return TestState.Failed;
            }
            return TestState.Unknown;
        }


        // ------------------------- Logs -----------------------------

        /// <summary>
        /// Returns a description of the failure
        /// </summary>
        /// <param name="pTest"></param>
        /// <returns></returns>
        private string GenerateFailedLog(IRun pTest)
        {
            try
            {
                StepFactory sf = pTest.StepFactory as StepFactory;
                if (sf == null)
                    return string.Empty;

                IList stepList = sf.NewList(string.Empty);
                if (stepList == null)
                    return string.Empty;

                var failedMsg = new StringBuilder();

                //loop on each step in the steps
                foreach (IStep s in stepList)
                {
                    if (s.Status == FAILED)
                        failedMsg.AppendLine(s["ST_DESCRIPTION"]);
                }
                return failedMsg.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return string.Empty;
            }
        }


        /// <summary>
        /// retrieves the run logs for the test when the steps are not reported to Qc (like in ST)
        /// </summary>
        /// <param name="currentTest"></param>
        /// <returns>the test run log</returns>
        private string GetTestRunLog(ITSTest currentTest)
        {
            const string testLog = @"log\vtd_user.log";

            IRun lastRun = currentTest.LastRun as IRun;
            string retVal = string.Empty;
            if (lastRun != null)
            {
                try
                {
                    IExtendedStorage storage = lastRun.ExtendedStorage as IExtendedStorage;

                    if (storage != null)
                    {
                        List list;
                        bool wasFatalError;
                        var path = storage.LoadEx(testLog, true, out list, out wasFatalError);
                        string logPath = Path.Combine(path, testLog);

                        if (File.Exists(logPath))
                        {
                            retVal = File.ReadAllText(logPath).TrimEnd();
                        }
                    }
                }
                catch (Exception ex)
                {
                    retVal = string.Empty;
                    Console.WriteLine(ex.Message);
                }
            }
            retVal = ConsoleWriter.FilterXmlProblematicChars(retVal);
            return retVal;
        }

        public void Dispose(bool managed)
        {
            //Console.WriteLine("Dispose ALM connection");
            if (Connected)
            {
                if (TdConnection != null)
                {
                    _tdConnection.Disconnect();
                    Marshal.ReleaseComObject(_tdConnection);
                }
                else
                {
                    _tdConnectionOld.Disconnect();
                    Marshal.ReleaseComObject(_tdConnectionOld);
                }
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public class QCFailure
    {
        public string Name { get; set; }
        public string Desc { get; set; }
    }

    public enum QcRunMode
    {
        RUN_LOCAL,
        RUN_REMOTE,
        RUN_PLANNED_HOST
    }
}

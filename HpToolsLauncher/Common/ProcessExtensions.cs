﻿/*
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
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HpToolsLauncher.Common
{
    public static class ProcessExtensions
    {
        /// <summary>
        /// Get the parent process for a given process handle.
        /// </summary>
        /// <param name="hProcess">the process handle</param>
        /// <returns>The parent process</returns>
        private static Process GetParentProcess(IntPtr hProcess)
        {
            NativeProcess.PROCESS_BASIC_INFORMATION pbi = new NativeProcess.PROCESS_BASIC_INFORMATION();
            int pbiLength = Marshal.SizeOf(pbi);
            int returnLength = 0;

            int status = NativeProcess.NtQueryInformationProcess(hProcess,NativeProcess.PROCESSINFOCLASS.ProcessBasicInformation,
                ref pbi,pbiLength,out returnLength);

            if(status != 0)
            {
                throw new Win32Exception(status);
            }

            try
            {
                return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
            }
            catch (ArgumentException)
            { // Not found
                return null;
            }
        }
        /// <summary>
        /// Returns the parent process of a given process
        /// </summary>
        /// <param name="process">the process for which to find the parent</param>
        /// <returns>the parent process</returns>
        public static Process Parent(this Process process)
        {
            return GetParentProcess(process.Handle);
        }
    }
}

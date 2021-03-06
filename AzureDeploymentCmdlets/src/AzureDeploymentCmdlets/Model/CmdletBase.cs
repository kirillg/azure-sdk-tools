﻿using System;
using System.Diagnostics;
using System.Management.Automation;
using AzureDeploymentCmdlets.Utilities;

namespace AzureDeploymentCmdlets.Model
{
    public class CmdletBase : PSCmdlet
    {
        private bool hasOutput = false;
        private IMessageWriter writer;

        public CmdletBase()
        {
            // This instantiation will throw if user is running with incompatible Windows Azure SDK version.
            new AzureTools.AzureTool();
        }

        public CmdletBase(IMessageWriter writer) :this()
        {
            this.writer = writer;
        }

        protected string GetServiceRootPath() { return PathUtility.FindServiceRootDirectory(CurrentPath()); }

        protected string CurrentPath() { return SessionState.Path.CurrentLocation.Path; }

        public string ResolvePath(string path)
        {
            if (SessionState == null)
            {
                return path;
            }

            var result = SessionState.Path.GetResolvedPSPathFromPSPath(path);
            string fullPath = string.Empty;

            if (result != null && result.Count > 0)
            {
                fullPath = result[0].Path;
            }

            return fullPath;
        }

        private void SafeWriteObjectInternal(object sendToPipeline)
        {
            if (CommandRuntime != null)
            {
                WriteObject(sendToPipeline);
            }
            else
            {
                Trace.WriteLine(sendToPipeline);
            }
        }

        private void WriteLineIfFirstOutput()
        {
            if (!hasOutput)
            {
                hasOutput = true;
                SafeWriteObjectInternal(Environment.NewLine);
            }
        }

        protected void SafeWriteObject(string message, params object[] args)
        {
            object sendToPipeline = message;
            WriteLineIfFirstOutput();
            if (args.Length > 0)
            {
                sendToPipeline = string.Format(message, args);
            }
            SafeWriteObjectInternal(sendToPipeline);

            if (writer != null)
            {
                writer.Write(sendToPipeline.ToString());
            }
        }

        protected void SafeWriteObjectWithTimestamp(string message, params object[] args)
        {
            SafeWriteObject(string.Format("{0:T} - {1}", DateTime.Now, string.Format(message, args)));
        }


        /// <summary>
        /// Wrap the base Cmdlet's SafeWriteProgress call so that it will not
        /// throw a NotSupportedException when called without a CommandRuntime
        /// (i.e., when not called from within Powershell).
        /// </summary>
        /// <param name="progress">The progress to write.</param>
        protected void SafeWriteProgress(ProgressRecord progress)
        {
            WriteLineIfFirstOutput();

            if (CommandRuntime != null)
            {
                WriteProgress(progress);
            }
            else
            {
                Trace.WriteLine(string.Format("{0}% Complete", progress.PercentComplete));
            }
        }
        
        /// <summary>
        /// Wrap the base Cmdlet's WriteError call so that it will not throw
        /// a NotSupportedException when called without a CommandRuntime (i.e.,
        /// when not called from within Powershell).
        /// </summary>
        /// <param name="errorRecord">The error to write.</param>
        protected void SafeWriteError(ErrorRecord errorRecord)
        {
            Debug.Assert(errorRecord != null, "errorRecord cannot be null.");
            
            // If the exception is an Azure Service Management error, pull the
            // Azure message out to the front instead of the generic response.
            errorRecord = AzureServiceManagementException.WrapExistingError(errorRecord);

            if (CommandRuntime != null)
            {
                WriteError(errorRecord);
            }
            else
            {
                Trace.WriteLine(errorRecord);
            }
        }

        /// <summary>
        /// Write an error message for a given exception.
        /// </summary>
        /// <param name="ex">The exception resulting from the error.</param>
        protected void SafeWriteError(Exception ex)
        {
            Debug.Assert(ex != null, "ex cannot be null or empty.");
            SafeWriteError(new ErrorRecord(ex, string.Empty, ErrorCategory.CloseError, null));
        }

        protected override void EndProcessing()
        {
            base.EndProcessing();
            if (hasOutput)
            {
                SafeWriteObjectInternal(Environment.NewLine);
            }
        }
    }
}

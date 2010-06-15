// Copyright 2007-2010 The Apache Software Foundation.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace dropkick.Tasks.Files
{
    using System;
    using System.IO;
    using DeploymentModel;
    using Exceptions;
    using log4net;

    public class CopyDirectoryTask :
        Task
    {
        readonly ILog _fileLog = LogManager.GetLogger("dropkick.filewrite");
        readonly ILog _log = LogManager.GetLogger(typeof (CopyDirectoryTask));
        readonly DestinationCleanOptions _options;
        string _from;
        string _to;

        public CopyDirectoryTask(string @from, string to, DestinationCleanOptions options)
        {
            _from = from;
            _to = to;
            _options = options;
        }

        #region Task Members

        public string Name
        {
            get { return string.Format("Copy '{0}' to '{1}'", _from, _to); }
        }

        public DeploymentResult Execute()
        {
            var result = new DeploymentResult();

            ValidatePath(result, _to);
            ValidatePath(result, _from);

            _from = Path.GetFullPath(_from);
            _to = Path.GetFullPath(_to);

            if (_options == DestinationCleanOptions.Delete) DeleteDestinationFirst(new DirectoryInfo(_to), result);

            CopyDirectory(new DirectoryInfo(_from), new DirectoryInfo(_to));

            result.AddGood(Name);

            return result;
        }


        public DeploymentResult VerifyCanRun()
        {
            var result = new DeploymentResult();

            ValidatePath(result, _to);
            ValidatePath(result, _from);

            _from = Path.GetFullPath(_from);
            _to = Path.GetFullPath(_to);

            //check can write from _to
            if (!Directory.Exists(_to))
                result.AddAlert(string.Format("'{0}' doesn't exist and will be created", _to));

            if (_options == DestinationCleanOptions.Delete)
                result.AddAlert("The files and directories in '{0}' will be deleted before deploying", _to);

            if (Directory.Exists(_from))
            {
                result.AddGood(string.Format("'{0}' exists", _from));
                //check can read from _from
                string[] readFiles = Directory.GetFiles(_from);
                foreach (var file in readFiles)
                {
                    Stream fs = new MemoryStream();
                    try
                    {
                        fs = File.Open(file, FileMode.Open, FileAccess.Read);
                        _log.DebugFormat("Going to copy '{0}' to '{1}'", file, _to);
                    }
                    catch (Exception)
                    {
                        result.AddAlert("CopyDirectoryTask: Can't read file '{0}'");
                    }
                    finally
                    {
                        fs.Dispose();
                    }
                }
            }
            else
            {
                result.AddAlert(string.Format("'{0}' doesn't exist", _from));
            }

            return result;
        }

        #endregion

        void ValidatePath(DeploymentResult result, string path)
        {
            try
            {
                Path.GetFullPath(_to);
                //TODO: add directory test
            }
            catch (Exception ex)
            {
                throw new DeploymentException("'{0}' is not an acceptable path. Must be a directory".FormatWith(_to));
            }
        }

        void CopyDirectory(DirectoryInfo source, DirectoryInfo destination)
        {
            if (!destination.Exists)
            {
                destination.Create();
            }

            // Copy all files.
            FileInfo[] files = source.GetFiles();
            foreach (var file in files)
            {
                string fileDestination = Path.Combine(destination.FullName,
                                                      file.Name);
                file.CopyTo(fileDestination);
                _log.DebugFormat("Copy file '{0}' to '{1}'", file.FullName, fileDestination);
                _fileLog.Info(fileDestination); //log where files are copied for tripwire
            }

            // Process subdirectories.
            DirectoryInfo[] dirs = source.GetDirectories();
            foreach (var dir in dirs)
            {
                // Get destination directory.
                string destinationDir = Path.Combine(destination.FullName, dir.Name);

                // Call CopyDirectory() recursively.
                CopyDirectory(dir, new DirectoryInfo(destinationDir));
            }
        }

        static void DeleteDestinationFirst(DirectoryInfo directory, DeploymentResult result)
        {
            if (directory.Exists)
            {
                directory.Delete(true);
                result.AddGood("'{0}' was successfully deleted");
            }
        }
    }
}
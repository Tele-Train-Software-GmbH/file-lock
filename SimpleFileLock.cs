using FileLock.FileSys;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;

namespace FileLock
{
    public class SimpleFileLock : IFileLock
    {
        protected SimpleFileLock(string lockFilePath)
        {
            LockName = lockFilePath;
            LockFilePath = lockFilePath;
        }

        public string LockName { get; private set; }

        private string LockFilePath { get; set; }
        private FileStream? stream = null;

        public bool TryAcquireLock()
        {
            if (stream?.CanRead ?? false)
            {
                Log.Debug("SimpleFileLock: It is already my lock. " + LockFilePath);
                return true;
            }
                
            if (File.Exists(LockFilePath))
            {
                var lockContent = LockIO.ReadLock(LockFilePath);

                //Someone else owns the lock
                if (lockContent.GetType() == typeof(OtherProcessOwnsFileLockContent))
                {
                    Log.Debug("SimpleFileLock: Could not get the lock, because another process owns the lock ." + LockFilePath);
                    return false;
                }

                //the file no longer exists
                if (lockContent.GetType() == typeof(MissingFileLockContent))
                {
                    Log.Debug("SimpleFileLock: The file does not exists. " + LockFilePath);
                    return AcquireLock();
                }
            }

            //Acquire the lock
            
            return AcquireLock();
        }

        public void ReleaseLock()
        {
            if(stream?.CanRead ?? false)
            {
                stream.Dispose();
                LockIO.DeleteLock(LockFilePath);
            }
        }

        #region Internal methods

        protected FileLockContent CreateLockContent()
        {
            var process = Process.GetCurrentProcess();
            return new FileLockContent
            {
                PID = process.Id,
                ProcessName = process.ProcessName,
                MachineName = Environment.MachineName,
                Timestamp = DateTime.Now.Ticks
            };
        }

        private bool AcquireLock()
        {
            if (stream?.CanRead ?? false)
            {
                Log.Debug("SimpleFileLock: It is already my lock. " + LockFilePath);
                return true;
            }

            return LockIO.WriteLock(LockFilePath, ref stream, CreateLockContent());
        }

        #endregion

        #region Create methods

        public static SimpleFileLock Create(string lockName)
        {
            return new SimpleFileLock(lockName);
        }

        #endregion
    }
}
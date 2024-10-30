using System;
using System.Diagnostics;
using FileLock.FileSys;
using Serilog;

namespace FileLock
{
    public class SimpleFileLock : IFileLock
    {
        protected SimpleFileLock(string lockFilePath, TimeSpan lockTimeout)
        {
            LockName = lockFilePath;
            LockFilePath = lockFilePath;
            LockTimeout = lockTimeout;
        }

        public TimeSpan LockTimeout { get; private set; }

        public string LockName { get; private set; }

        private string LockFilePath { get; set; }

        public bool TryAcquireLock()
        {
            if (LockIO.LockExists(LockFilePath))
            {
                var lockContent = LockIO.ReadLock(LockFilePath);

                //Someone else owns the lock
                if (lockContent.GetType() == typeof(OtherProcessOwnsFileLockContent))
                {
                    Log.Debug("SimpleFileLock: Could not get the lock, because another process owns the lock.");
                    return false;
                }

                //the file no longer exists
                if (lockContent.GetType() == typeof(MissingFileLockContent))
                {
                    Log.Debug("SimpleFileLock: The file does not exists.");
                    return AcquireLock();
                }


                var lockWriteTime = new DateTime(lockContent.Timestamp);

                //This lock belongs to this process - we can reacquire the lock
                if ((lockContent.PID == Process.GetCurrentProcess().Id) && (lockContent.MachineName == Environment.MachineName))
                {
                    Log.Debug("SimpleFileLock: It is my lock.");
                    return AcquireLock();
                }

                //The lock has not timed out - we can't acquire it
                if (!(Math.Abs((DateTime.Now - lockWriteTime).TotalSeconds) > LockTimeout.TotalSeconds))
                {
                    Log.Debug("SimpleFileLock: Could not get the lock, because the lock has not timed out.");
                    return false;
                }
            }

            //Acquire the lock
            
            return AcquireLock();
        }



        public bool ReleaseLock()
        {
            //Need to own the lock in order to release it (and we can reacquire the lock inside the current process)
            if (LockIO.LockExists(LockFilePath) && TryAcquireLock())
                LockIO.DeleteLock(LockFilePath);
            return true;
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
            return LockIO.WriteLock(LockFilePath, CreateLockContent());
        }

        #endregion

        #region Create methods

        public static SimpleFileLock Create(string lockName, TimeSpan lockTimeout)
        {
            return new SimpleFileLock(lockName, lockTimeout);
        }

        #endregion
    }
}
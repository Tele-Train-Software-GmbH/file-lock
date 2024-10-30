using Serilog;
using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace FileLock.FileSys
{
    internal static class LockIO
    {
        private static readonly DataContractJsonSerializer JsonSerializer;

        static LockIO()
        {
            JsonSerializer = new DataContractJsonSerializer(typeof(FileLockContent), new[] { typeof(FileLockContent) });
        }

        public static bool LockExists(string lockFilePath)
        {
            return File.Exists(lockFilePath);
        }

        public static FileLockContent ReadLock(string lockFilePath)
        {
            try
            {
                using (var stream = File.OpenRead(lockFilePath))
                {
                    var obj = JsonSerializer.ReadObject(stream);
                    return (FileLockContent) obj ?? new MissingFileLockContent();
                }
            }
            catch (FileNotFoundException)
            {
                return new MissingFileLockContent();
            }
            catch (IOException)
            {
                return new OtherProcessOwnsFileLockContent();
            }
            catch (Exception ex) //We have no idea what went wrong - reacquire this lock
            {
                Log.Debug(ex, "LockIO: ReadLock unsuccessful.");
                return new MissingFileLockContent();
            }
        }

        public static bool WriteLock(string lockFilePath, FileLockContent lockContent)
        {
            try
            {
                using (var stream = File.Create(lockFilePath))
                {
                    JsonSerializer.WriteObject(stream, lockContent);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "LockIO: WriteLock unsuccessful.");
                return false;
            }
        }

        public static void DeleteLock(string lockFilePath)
        {
            try
            {
                File.Delete(lockFilePath);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "LockIO: DeleteLock unsuccessful.");
            }
        }
    }
}

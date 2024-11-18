using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WSNet;

namespace WSNetFramework.WSNet.Modules.Fileops
{
    internal class Fileops
    {
        public static void MoveDirectory(string sourceDir, string destinationDir)
        {
            // Check if the source directory exists
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDir}");
            }

            // Check if the destination directory already exists
            if (Directory.Exists(destinationDir))
            {
                throw new IOException($"Destination directory already exists: {destinationDir}");
            }

            // Move the directory
            Directory.Move(sourceDir, destinationDir);
        }

        public static void CopyDirectory(string sourceDir, string destinationDir)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDir);

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Copy files
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true); // true to overwrite if it exists
            }

            // Copy subdirectories
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newSubDirPath = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newSubDirPath); // Recursively copy subdirectories
            }
        }

        static public IEnumerable<WSNFileEntry> ListDirectoryContents(string path)
        {
            // Get all files in the directory
            DirectoryInfo directoryInfo = new DirectoryInfo(path);

            foreach (FileInfo file in directoryInfo.GetFiles())
            {
                WSNFileEntry fe = new WSNFileEntry();
                fe.root = path;
                fe.name = file.Name;
                fe.size = (ulong)file.Length;
                fe.ctime = file.CreationTime;
                fe.mtime = file.LastWriteTime;
                fe.atime = file.LastAccessTime;
                fe.is_dir = false;
                yield return fe;
            }

            // Get all subdirectories in the directory
            foreach (DirectoryInfo directory in directoryInfo.GetDirectories())
            {
                WSNFileEntry fe = new WSNFileEntry();
                fe.root = path;
                fe.name = directory.Name;
                fe.size = 0;
                fe.ctime = directory.CreationTime;
                fe.mtime = directory.LastWriteTime;
                fe.atime = directory.LastAccessTime;
                fe.is_dir = true;
                yield return fe;
            }
        }

    }
}

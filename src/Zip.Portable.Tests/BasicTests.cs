// BasicTests.cs
// ------------------------------------------------------------------
//
// Copyright (c) 2009-2011 Dino Chiesa
// All rights reserved.
//
// This code module is part of DotNetZip, a zipfile class library.
//
// ------------------------------------------------------------------
//
// This code is licensed under the Microsoft Public License.
// See the file License.txt for the license details.
// More info on: http://dotnetzip.codeplex.com
//
// ------------------------------------------------------------------
//
// last saved (in emacs):
// Time-stamp: <2011-July-26 11:39:21>
//
// ------------------------------------------------------------------
//
// This module defines basic unit tests for DotNetZip.
//
// ------------------------------------------------------------------

using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RE = System.Text.RegularExpressions;

using Ionic.Zip;
using Ionic.Zip.Tests.Utilities;
using System.IO;
using System.Linq;

namespace Ionic.Zip.Tests.Basic
{
    /// <summary>
    /// Summary description for UnitTest1
    /// </summary>
    [TestClass]
    public class BasicTests : IonicTestClass
    {
        EncryptionAlgorithm[] crypto =
        {
            EncryptionAlgorithm.None,
            EncryptionAlgorithm.PkzipWeak,
#if AESCRYPTO
            EncryptionAlgorithm.WinZipAes128,
            EncryptionAlgorithm.WinZipAes256,
#endif
        };


        public BasicTests() : base() { }


        [TestMethod]
        public void CreateZip_AddItem_WithDirectory()
        {
            // select the name of the zip file
            string zipFileToCreate = "CreateZip_AddItem.zip";

            // create a bunch of files
            string subdir = "files";
            string[] filesToZip = TestUtilities.GenerateFilesFlat(subdir);

            // Create the zip archive
            using (ZipFile zip1 = new ZipFile())
            {
                //zip.StatusMessageTextWriter = System.Console.Out;
                for (int i = 0; i < filesToZip.Length; i++)
                    zip1.AddItem(filesToZip[i], "files");
                zip1.Save(zipFileToCreate);
            }

            // Verify the number of files in the zip
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                 filesToZip.Length);
        }

        [TestMethod]
        public void CreateZip_AddItem_NoDirectory()
        {
            string zipFileToCreate = "CreateZip_AddItem.zip";
            string[] filesToZip = TestUtilities.GenerateFilesFlat(".");

            // Create the zip archive
            using (ZipFile zip1 = new ZipFile())
            {
                foreach (var f in filesToZip)
                    zip1.AddItem(f);
                zip1.Save(zipFileToCreate);
            }

            // Verify the number of files in the zip
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                 filesToZip.Length);
        }


        [TestMethod]
        [ExpectedException(typeof(Ionic.Zip.ZipException))]
        public void FileNotAvailableFails_wi10387()
        {
            string zipFileToCreate = Path.Combine(TopLevelDir, "FileNotAvailableFails.zip");
            using (var zip1 = new ZipFile()) { zip1.Save(zipFileToCreate); }
            try
            {
                using (new FileStream(zipFileToCreate, FileMode.Open,
                                      FileAccess.ReadWrite,
                                      FileShare.None))
                {
                    using (FileSystemZip.Read(zipFileToCreate)) { }
                }
            }
            finally
            {
            }
        }


        [TestMethod]
        public void CreateZip_AddFile()
        {
            int i;
            string zipFileToCreate = "CreateZip_AddFile.zip";
            string subdir = "files";
            string[] filesToZip = TestUtilities.GenerateFilesFlat(subdir);

            using (ZipFile zip1 = new ZipFile())
            {
                for (i = 0; i < filesToZip.Length; i++)
                    zip1.AddFile(filesToZip[i], "files");
                zip1.Save(zipFileToCreate);
            }
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                 filesToZip.Length);
        }

        [TestMethod]
        public void CreateZip_AddFile_CharCase_wi13481()
        {
            string zipFileToCreate = "AddFile.zip";
            string subdir = "files";
            string[] filesToZip = TestUtilities.GenerateFilesFlat(subdir);
            Directory.SetCurrentDirectory(subdir);
            Array.ForEach(filesToZip, x => { File.Move(Path.GetFileName(x),Path.GetFileName(x).ToUpper()); });
            Directory.SetCurrentDirectory(TopLevelDir);
            filesToZip= Directory.GetFiles(subdir);

            using (ZipFile zip1 = new ZipFile())
            {
                for (int i = 0; i < filesToZip.Length; i++)
                    zip1.AddFile(filesToZip[i], "files");
                zip1.Save(zipFileToCreate);
            }
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                 filesToZip.Length);

            int nEntries = 0;
            // now, verify that we have not downcased the filenames
            using (ZipFile zip2 = FileSystemZip.Read(zipFileToCreate))
            {
                foreach (var entry in zip2.Entries)
                {
                    var fname1 = Path.GetFileName(entry.FileName);
                    var fnameLower = fname1.ToLower();
                    Assert.IsFalse(fname1.Equals(fnameLower),
                                   "{0} is all lowercase.", fname1);
                    nEntries++;
                }
            }
            Assert.IsFalse(nEntries < 2, "not enough entries");
        }


        [TestMethod]
        public void CreateZip_AddFileInDirectory()
        {
            string subdir = "fodder";
            TestUtilities.GenerateFilesFlat(subdir);
            for (int m = 0; m < 2; m++)
            {
                string directoryName = "";
                for (int k = 0; k < 4; k++)
                {
                    // select the name of the zip file
                    string zipFileToCreate = String.Format("CreateZip_AddFileInDirectory-trial{0}.{1}.zip", m, k);
                    TestContext.WriteLine("=====================");
                    TestContext.WriteLine("Trial {0}", k);
                    TestContext.WriteLine("Zipfile: {0}", zipFileToCreate);

                    directoryName = Path.Combine(directoryName,
                                                 String.Format("{0:D2}", k));

                    string[] filesToSelectFrom =
                        Directory.GetFiles(subdir, "*.*", SearchOption.AllDirectories);

                    TestContext.WriteLine("using dirname: {0}", directoryName);

                    int n = _rnd.Next(filesToSelectFrom.Length / 2) + 2;
                    TestContext.WriteLine("Zipping {0} files", n);

                    // Create the zip archive
                    var addedFiles = new List<String>();
                    using (ZipFile zip1 = new ZipFile())
                    {
                        // add n files
                        int j=0;
                        for (int i = 0; i < n; i++)
                        {
                            // select files at random
                            while (addedFiles.Contains(filesToSelectFrom[j]))
                                j = _rnd.Next(filesToSelectFrom.Length);
                            zip1.AddFile(filesToSelectFrom[j], directoryName);
                            addedFiles.Add(filesToSelectFrom[j]);
                        }
                        zip1.Save(zipFileToCreate);
                    }

                    // Verify the number of files in the zip
                    Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), n,
                                         "Wrong number of entries in the zip file {0}",
                                         zipFileToCreate);

                    using (ZipFile zip2 = FileSystemZip.Read(zipFileToCreate))
                    {
                        foreach (var e in zip2)
                        {
                            TestContext.WriteLine("Check entry: {0}", e.FileName);
                            Assert.AreEqual<String>(directoryName,
                                                    Path.GetDirectoryName(e.FileName),
                                                    "Wrong directory on zip entry {0}",
                                                    e.FileName);
                        }
                    }
                }

                // add progressively more files to the fodder directory
                TestUtilities.GenerateFilesFlat(subdir);
            }

        }


        [TestMethod]
        public void CreateZip_AddFile_LeadingDot()
        {
            // select the name of the zip file
            string zipFileToCreate = "CreateZip_AddFile_LeadingDot.zip";
            string[] filesToZip = TestUtilities.GenerateFilesFlat(".");
            using (ZipFile zip1 = new ZipFile())
            {
                for (int i = 0; i < filesToZip.Length; i++)
                {
                    zip1.AddFile(filesToZip[i]);
                }
                zip1.Save(zipFileToCreate);
            }

            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                 filesToZip.Length);
        }




        [TestMethod]
        public void CreateZip_AddFiles_LeadingDot_Array()
        {
            // select the name of the zip file
            string zipFileToCreate = "CreateZip_AddFiles_LeadingDot_Array.zip";
            string[] filesToZip = TestUtilities.GenerateFilesFlat(".");

            // Create the zip archive
            using (ZipFile zip1 = new ZipFile())
            {
                zip1.AddFiles(filesToZip);
                zip1.Save(zipFileToCreate);
            }

            // Verify the number of files in the zip
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                 filesToZip.Length);
        }



        [TestMethod]
        public void CreateZip_AddFiles_PreserveDirHierarchy()
        {
            // select the name of the zip file
            string zipFileToCreate = "CreateZip_AddFiles_PreserveDirHierarchy.zip";
            string dirToZip = "zipthis";

            // create a bunch of files
            int subdirCount;
            int entries = TestUtilities.GenerateFilesOneLevelDeep(TestContext, "PreserveDirHierarchy", dirToZip, null, out subdirCount);

            string[] filesToZip = Directory.GetFiles(".", "*.*", SearchOption.AllDirectories);

            Assert.AreEqual<int>(filesToZip.Length, entries);

            // Create the zip archive
            using (ZipFile zip1 = new ZipFile())
            {
                zip1.AddFiles(filesToZip, "", true);
                zip1.Save(zipFileToCreate);
            }

            // Verify the number of files in the zip
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                 filesToZip.Length);
        }

        private bool ArraysAreEqual(byte[] b1, byte[] b2)
        {
            return (CompareArrays(b1, b2) == 0);
        }


        private int CompareArrays(byte[] b1, byte[] b2)
        {
            if (b1 == null && b2 == null) return 0;
            if (b1 == null || b2 == null) return 0;
            if (b1.Length > b2.Length) return 1;
            if (b1.Length < b2.Length) return -1;
            for (int i = 0; i < b1.Length; i++)
            {
                if (b1[i] > b2[i]) return 1;
                if (b1[i] < b2[i]) return -1;
            }
            return 0;
        }


        [TestMethod]
        public void CreateZip_AddEntry_ByteArray()
        {
            // select the name of the zip file
            string zipFileToCreate = "CreateZip_AddEntry_ByteArray.zip";
            int entriesToCreate = _rnd.Next(42) + 12;
            var dict = new Dictionary<string, byte[]>();

            // Create the zip archive
            using (ZipFile zip1 = new ZipFile())
            {
                for (int i = 0; i < entriesToCreate; i++)
                {
                    var b = new byte[_rnd.Next(1000) + 1000];
                    _rnd.NextBytes(b);
                    string filename = String.Format("Filename{0:D3}.bin", i);
                    var e = zip1.AddEntry(Path.Combine("data", filename), b);
                    dict.Add(e.FileName, b);
                }
                zip1.Save(zipFileToCreate);
            }

            // Verify the number of files in the zip
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                 entriesToCreate);

            using (ZipFile zip1 = FileSystemZip.Read(zipFileToCreate))
            {
                foreach (var e in zip1)
                {
                    // extract to a stream
                    using (var ms1 = new MemoryStream())
                    {
                        e.Extract(ms1);
                        Assert.IsTrue(ArraysAreEqual(ms1.ToArray(), dict[e.FileName]));
                    }
                }
            }
        }




        [TestMethod]
        public void CreateZip_AddFile_AddItem()
        {
            string zipFileToCreate = "CreateZip_AddFile_AddItem.zip";
            string subdir = "files";
            string[] filesToZip = TestUtilities.GenerateFilesFlat(subdir);

            // use the parameterized ctor
            using (ZipFile zip1 = new ZipFile())
            {
                for (int i = 0; i < filesToZip.Length; i++)
                {
                    if (_rnd.Next(2) == 0)
                        zip1.AddFile(filesToZip[i], "files");
                    else
                        zip1.AddItem(filesToZip[i], "files");
                }
                zip1.Save(zipFileToCreate);
            }

            // Verify the number of files in the zip
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                 filesToZip.Length);
        }


        private void DumpZipFile(ZipFile z)
        {
            TestContext.WriteLine("found {0} entries", z.Entries.Count);
            TestContext.WriteLine("RequiresZip64: '{0}'", z.RequiresZip64.HasValue ? z.RequiresZip64.Value.ToString() : "not set");
            TestContext.WriteLine("listing the entries in {0}...", "(zipfile)");
            foreach (var e in z)
            {
                TestContext.WriteLine("{0}", e.FileName);
            }
        }



        [TestMethod]
        public void CreateZip_ZeroEntries()
        {
            // select the name of the zip file
            string zipFileToCreate = "CreateZip_ZeroEntries.zip";

            using (ZipFile zip1 = new ZipFile())
            {
                zip1.Save(zipFileToCreate);
                DumpZipFile(zip1);
            }
            
            using (ZipFile zip1 = FileSystemZip.Read(zipFileToCreate))
            {
                DumpZipFile(zip1);
            }

            // Verify the number of files in the zip
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), 0);
        }



        [TestMethod]
        public void CreateZip_Basic_ParameterizedSave()
        {
            string zipFileToCreate = "CreateZip_Basic_ParameterizedSave.zip";
            string subdir = "files";
            int numFilesToCreate = _rnd.Next(23) + 14;
            string[] filesToZip = TestUtilities.GenerateFilesFlat(subdir, numFilesToCreate);

            using (ZipFile zip1 = new ZipFile())
            {
                //zip.StatusMessageTextWriter = System.Console.Out;
                for (int i = 0; i < filesToZip.Length; i++)
                {
                    if (_rnd.Next(2) == 0)
                        zip1.AddFile(filesToZip[i], "files");
                    else
                        zip1.AddItem(filesToZip[i], "files");
                }
                zip1.Save(zipFileToCreate);
            }

            // Verify the number of files in the zip
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                 filesToZip.Length);
        }


        [TestMethod]
        public void CreateZip_AddFile_OnlyZeroLengthFiles()
        {
            _Internal_ZeroLengthFiles(_rnd.Next(33) + 3, "CreateZip_AddFile_OnlyZeroLengthFiles", null);
        }

        [TestMethod]
        public void CreateZip_AddFile_OnlyZeroLengthFiles_Password()
        {
            _Internal_ZeroLengthFiles(_rnd.Next(33) + 3, "CreateZip_AddFile_OnlyZeroLengthFiles", Path.GetRandomFileName());
        }

        [TestMethod]
        public void CreateZip_AddFile_OneZeroLengthFile()
        {
            _Internal_ZeroLengthFiles(1, "CreateZip_AddFile_OneZeroLengthFile", null);
        }


        [TestMethod]
        public void CreateZip_AddFile_OneZeroLengthFile_Password()
        {
            _Internal_ZeroLengthFiles(1, "CreateZip_AddFile_OneZeroLengthFile_Password", Path.GetRandomFileName());
        }


        [TestMethod]
        public void CreateZip_KeepStreamOpen()
        {
            int i;
            string zipFileToCreate = "CreateZip_KeepStreamOpen.zip";
            string subdir = "files";
            string[] filesToZip = TestUtilities.GenerateFilesFlat(subdir);

            using (ZipFile zip1 = new ZipFile())
            {
                for (i = 0; i < filesToZip.Length; i++)
                    zip1.AddFile(filesToZip[i], "files");
                zip1.Save(zipFileToCreate);
            }

            // Verify the number of files in the zip
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                 filesToZip.Length);

            using (ZipFile zip1 = FileSystemZip.Read(zipFileToCreate))
            {
                foreach (var e in zip1)
                {
                    // extract to a stream
                    using (var ms1 = new MemoryStream())
                    {
                        e.Extract(ms1);

                        // check if stream is kept open after extracting the file
                        Assert.AreEqual<bool>(true, ms1.CanRead);
                    }
                }
            }
        }


        private void _Internal_ZeroLengthFiles(int fileCount, string nameStub, string password)
        {
            string zipFileToCreate = Path.Combine(TopLevelDir, nameStub + ".zip");

            int i;
            string[] filesToZip = new string[fileCount];
            for (i = 0; i < fileCount; i++)
                filesToZip[i] = TestUtilities.CreateUniqueFile("zerolength", TopLevelDir);

            var sw = new StringWriter();
            using (ZipFile zip = new ZipFile())
            {
                zip.StatusMessageTextWriter = sw;
                zip.Password = password;
                for (i = 0; i < filesToZip.Length; i++)
                    zip.AddFile(filesToZip[i]);
                zip.Save(zipFileToCreate);
            }

            string status = sw.ToString();
            TestContext.WriteLine("save output: " + status);

            BasicVerifyZip(zipFileToCreate, password);

            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), filesToZip.Length,
                                 "The zip file created has the wrong number of entries.");
        }


        [TestMethod]
        public void CreateZip_UpdateDirectory()
        {
            int i, j;

            string zipFileToCreate = "CreateZip_UpdateDirectory.zip";
            string dirToZip = "zipthis";
            Directory.CreateDirectory(dirToZip);

            TestContext.WriteLine("\n------------\nCreating files ...");

            int entries = 0;
            int subdirCount = _rnd.Next(17) + 14;
            //int subdirCount = _rnd.Next(3) + 2;
            var FileCount = new Dictionary<string, int>();
            var checksums = new Dictionary<string, byte[]>();

            for (i = 0; i < subdirCount; i++)
            {
                string subdirShort = String.Format("dir{0:D4}", i);
                string subdir = Path.Combine(dirToZip, subdirShort);
                Directory.CreateDirectory(subdir);

                int filecount = _rnd.Next(11) + 17;
                //int filecount = _rnd.Next(2) + 2;
                FileCount[subdirShort] = filecount;
                for (j = 0; j < filecount; j++)
                {
                    string filename = String.Format("file{0:D4}.x", j);
                    string fqFilename = Path.Combine(subdir, filename);
                    TestUtilities.CreateAndFillFile(fqFilename, _rnd.Next(1000) + 100);

                    var chk = TestUtilities.ComputeChecksum(fqFilename);
                    var t1 = Path.GetFileName(dirToZip);
                    var t2 = Path.Combine(t1, subdirShort);
                    var key = Path.Combine(t2, filename);
                    key = TestUtilities.TrimVolumeAndSwapSlashes(key);
                    TestContext.WriteLine("chk[{0}]= {1}", key,
                                          TestUtilities.CheckSumToString(chk));
                    checksums.Add(key, chk);
                    entries++;
                }
            }

            Directory.SetCurrentDirectory(TopLevelDir);

            TestContext.WriteLine("\n------------\nAdding files into the Zip...");

            // add all the subdirectories into a new zip
            using (ZipFile zip1 = new ZipFile())
            {
                zip1.AddDirectory(dirToZip, "zipthis");
                zip1.Save(zipFileToCreate);
            }

            TestContext.WriteLine("\n");

            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), entries,
                                 "The Zip file has an unexpected number of entries.");

            TestContext.WriteLine("\n------------\nExtracting and validating checksums...");

            // validate all the checksums
            using (ZipFile zip2 = FileSystemZip.Read(zipFileToCreate))
            {
                foreach (ZipEntry e in zip2)
                {
                    e.Extract("unpack");
                    string pathToExtractedFile = Path.Combine("unpack", e.FileName);

                    // if it is a file....
                    if (checksums.ContainsKey(e.FileName))
                    {
                        // verify the checksum of the file is correct
                        string expectedCheckString = TestUtilities.CheckSumToString(checksums[e.FileName]);
                        string actualCheckString = TestUtilities.GetCheckSumString(pathToExtractedFile);
                        Assert.AreEqual<String>
                            (expectedCheckString,
                             actualCheckString,
                             "Unexpected checksum on extracted filesystem file ({0}).",
                             pathToExtractedFile);
                    }
                }
            }

            TestContext.WriteLine("\n------------\nCreating some new files ...");

            // now, update some of the existing files
            dirToZip = Path.Combine(TopLevelDir, "updates");
            Directory.CreateDirectory(dirToZip);

            for (i = 0; i < subdirCount; i++)
            {
                string subdirShort = String.Format("dir{0:D4}", i);
                string subdir = Path.Combine(dirToZip, subdirShort);
                Directory.CreateDirectory(subdir);

                int filecount = FileCount[subdirShort];
                for (j = 0; j < filecount; j++)
                {
                    if (_rnd.Next(2) == 1)
                    {
                        string filename = String.Format("file{0:D4}.x", j);
                        TestUtilities.CreateAndFillFile(Path.Combine(subdir, filename),
                                                        _rnd.Next(1000) + 100);
                        string fqFilename = Path.Combine(subdir, filename);

                        var chk = TestUtilities.ComputeChecksum(fqFilename);
                        //var t1 = Path.GetFileName(dirToZip);
                        var t2 = Path.Combine("zipthis", subdirShort);
                        var key = Path.Combine(t2, filename);
                        key = TestUtilities.TrimVolumeAndSwapSlashes(key);

                        TestContext.WriteLine("chk[{0}]= {1}", key,
                                              TestUtilities.CheckSumToString(chk));

                        checksums.Remove(key);
                        checksums.Add(key, chk);
                    }
                }
            }


            TestContext.WriteLine("\n------------\nUpdating some of the files in the zip...");
            // add some new content
            using (ZipFile zip3 = FileSystemZip.Read(zipFileToCreate))
            {
                zip3.UpdateDirectory(dirToZip, "zipthis");
                //String[] dirs = Directory.GetDirectories(dirToZip);

                //foreach (String d in dirs)
                //{
                //    string dir = Path.Combine(Path.GetFileName(dirToZip), Path.GetFileName(d));
                //    //string root = Path.Combine("zipthis", Path.GetFileName(d));
                //    zip3.UpdateDirectory(dir, "zipthis");
                //}
                zip3.Save(zipFileToCreate);
            }

            TestContext.WriteLine("\n------------\nValidating the checksums for all of the files ...");

            // validate all the checksums again
            using (ZipFile zip4 = FileSystemZip.Read(zipFileToCreate))
            {
                foreach (ZipEntry e in zip4)
                    TestContext.WriteLine("Found entry: {0}", e.FileName);

                foreach (ZipEntry e in zip4)
                {
                    e.Extract("unpack2");
                    if (!e.IsDirectory)
                    {
                        string pathToExtractedFile = Path.Combine("unpack2", e.FileName);

                        // verify the checksum of the file is correct
                        string expectedCheckString = TestUtilities.CheckSumToString(checksums[e.FileName]);
                        string actualCheckString = TestUtilities.GetCheckSumString(pathToExtractedFile);
                        Assert.AreEqual<String>
                            (expectedCheckString,
                             actualCheckString,
                             "Unexpected checksum on extracted filesystem file ({0}).",
                             pathToExtractedFile);
                    }
                }
            }
        }



        [TestMethod]
        public void CreateZip_AddDirectory_OnlyZeroLengthFiles()
        {
            string zipFileToCreate = "CreateZip_AddDirectory_OnlyZeroLengthFiles.zip";
            string dirToZip = "zipthis";
            Directory.CreateDirectory(dirToZip);

            int entries = 0;
            int subdirCount = _rnd.Next(8) + 8;
            for (int i = 0; i < subdirCount; i++)
            {
                string subdir = Path.Combine(dirToZip, "dir" + i);
                Directory.CreateDirectory(subdir);
                int n = _rnd.Next(6) + 2;
                for (int j=0; j < n; j++)
                {
                    TestUtilities.CreateUniqueFile("bin", subdir);
                    entries++;
                }
            }

            using (var zip = new ZipFile())
            {
                zip.AddDirectory(Path.GetFileName(dirToZip));
                zip.Save(zipFileToCreate);
            }

            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), entries);
        }



        [TestMethod]
        public void CreateZip_AddDirectory_OneZeroLengthFile()
        {
            string zipFileToCreate = "CreateZip_AddDirectory_OneZeroLengthFile.zip";
            string dirToZip = "zipthis";
            Directory.CreateDirectory(dirToZip);

            // one empty file
            string file = TestUtilities.CreateUniqueFile("ZeroLengthFile.txt", dirToZip);

            using (ZipFile zip = new ZipFile())
            {
                zip.AddDirectory(Path.GetFileName(dirToZip));
                zip.Save(zipFileToCreate);
            }

            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), 1);
        }


        [TestMethod]
        public void CreateZip_AddDirectory_OnlyEmptyDirectories()
        {
            string zipFileToCreate = "CreateZip_AddDirectory_OnlyEmptyDirectories.zip";
            string dirToZip = "zipthis";
            Directory.CreateDirectory(dirToZip);

            int subdirCount = _rnd.Next(28) + 18;
            for (int i = 0; i < subdirCount; i++)
            {
                string subdir = Path.Combine(dirToZip, "EmptyDir" + i);
                Directory.CreateDirectory(subdir);
            }

            using (ZipFile zip = new ZipFile())
            {
                zip.AddDirectory(Path.GetFileName(dirToZip));
                zip.Save(zipFileToCreate);
            }

            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), 0);
        }


        [TestMethod]
        public void CreateZip_AddDirectory_OneEmptyDirectory()
        {
            string zipFileToCreate = "CreateZip_AddDirectory_OneEmptyDirectory.zip";
            string dirToZip = "zipthis";
            Directory.CreateDirectory(dirToZip);

            using (ZipFile zip = new ZipFile())
            {
                zip.AddDirectory(Path.GetFileName(dirToZip));
                zip.Save(zipFileToCreate);
            }

            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), 0);
            BasicVerifyZip(zipFileToCreate);
        }


        [TestMethod]
        public void CreateZip_WithEmptyDirectory()
        {
            string zipFileToCreate = "Create_WithEmptyDirectory.zip";
            string subdir = "EmptyDirectory";
            Directory.CreateDirectory(subdir);
            using (ZipFile zip = new ZipFile())
            {
                zip.AddDirectory(subdir, "");
                zip.Save(zipFileToCreate);
            }
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), 0);
            BasicVerifyZip(zipFileToCreate);
        }



        [TestMethod]
        public void CreateZip_AddDirectory_CheckStatusTextWriter()
        {
            string zipFileToCreate = "CreateZip_AddDirectory_CheckStatusTextWriter.zip";
            string dirToZip = "zipthis";
            Directory.CreateDirectory(dirToZip);

            int entries = 0;
            int subdirCount = _rnd.Next(8) + 8;
            for (int i = 0; i < subdirCount; i++)
            {
                string subdir = Path.Combine(dirToZip, "Dir" + i);
                Directory.CreateDirectory(subdir);
                // a few files per subdir
                int fileCount = _rnd.Next(12) + 4;
                for (int j = 0; j < fileCount; j++)
                {
                    string file = Path.Combine(subdir, "File" + j);
                    TestUtilities.CreateAndFillFile(file, 1020);
                    entries++;
                }
            }

            var sw = new StringWriter();
            using (ZipFile zip = new ZipFile())
            {
                zip.StatusMessageTextWriter = sw;
                zip.AddDirectory(Path.GetFileName(dirToZip));
                zip.Save(zipFileToCreate);
            }

            string status = sw.ToString();
            TestContext.WriteLine("save output: " + status);

            Assert.IsTrue(status.Length > 24 * entries, "status messages? ({0}!>{1})",
                          status.Length, 24 * entries);

            int n = TestUtilities.CountEntries(zipFileToCreate);
            Assert.AreEqual<int>(n, entries,
                                 "wrong number of entries. ({0}!={1})", n, entries);

            BasicVerifyZip(zipFileToCreate);
        }


        struct TestTrial
        {
            public string arg;
            public string re;
        }


        [TestMethod]
        public void CreateZip_AddDirectory()
        {
            TestTrial[] trials = {
                new TestTrial { arg=null, re="^file(\\d+).ext$"},
                new TestTrial { arg="", re="^file(\\d+).ext$"},
                new TestTrial { arg=null, re="^file(\\d+).ext$"},
                new TestTrial { arg="Xabf", re="(?s)^Xabf/(file(\\d+).ext)?$"},
                new TestTrial { arg="AAAA/BBB", re="(?s)^AAAA/BBB/(file(\\d+).ext)?$"}
            };

            for (int k = 0; k < trials.Length; k++)
            {
                TestContext.WriteLine("\n--------------------------------\n\n\n");
                string zipFileToCreate = Path.Combine(TopLevelDir, String.Format("CreateZip_AddDirectory-{0}.zip", k));

                Assert.IsFalse(File.Exists(zipFileToCreate), "The temporary zip file '{0}' already exists.", zipFileToCreate);

                string dirToZip = String.Format("DirectoryToZip.{0}.test", k);
                Directory.CreateDirectory(dirToZip);

                int fileCount = _rnd.Next(5) + 4;
                for (int i = 0; i < fileCount; i++)
                {
                    String file = Path.Combine(dirToZip, String.Format("file{0:D3}.ext", i));
                    TestUtilities.CreateAndFillFile(file, _rnd.Next(2000) + 500);
                }

                var sw = new StringWriter();
                using (ZipFile zip = new ZipFile())
                {
                    zip.StatusMessageTextWriter = sw;
                    if (k == 0)
                        zip.AddDirectory(dirToZip);
                    else
                        zip.AddDirectory(dirToZip, trials[k].arg);
                    zip.Save(zipFileToCreate);
                }
                TestContext.WriteLine(sw.ToString());
                Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), fileCount,
                                     String.Format("The zip file created in cycle {0} has the wrong number of entries.", k));

                //TestContext.WriteLine("");
                // verify that the entries in the zip are in the top level directory!!
                using (ZipFile zip2 = FileSystemZip.Read(zipFileToCreate))
                {
                    foreach (ZipEntry e in zip2)
                        TestContext.WriteLine("found entry: {0}", e.FileName);
                    foreach (ZipEntry e in zip2)
                    {
                        //Assert.IsFalse(e.FileName.StartsWith("dir"),
                        //       String.Format("The Zip entry '{0}' is not rooted in top level directory.", e.FileName));

                        // check the filename:
                        //RE.Match m0 = RE.Regex.Match(e.FileName, fnameRegex[k]);
                        // Assert.IsTrue(m0 != null, "No match");
                        // Assert.AreEqual<int>(m0.Groups.Count, 2,
                        //    String.Format("In cycle {0}, Matching {1} against {2}, Wrong number of matches ({3})",
                        //        k, e.FileName, fnameRegex[k], m0.Groups.Count));

                        Assert.IsTrue(RE.Regex.IsMatch(e.FileName, trials[k].re),
                                      String.Format("In cycle {0}, Matching {1} against {2}", k, e.FileName, trials[k].re));
                    }
                }
            }
        }


        [TestMethod]
        public void CreateZip_AddDirectory_Nested()
        {
            // Each trial provides a directory name into which to add
            // files, and a regex, used for verification after the zip
            // is created, to match the names on any added entries.
            TestTrial[] trials = {
                new TestTrial { arg=null, re="^dir(\\d){3}/(file(\\d+).ext)?$"},
                new TestTrial { arg="", re="^dir(\\d){3}/(file(\\d+).ext)?$"},
                new TestTrial { arg=null, re="^dir(\\d){3}/(file(\\d+).ext)?$"},
                new TestTrial { arg="rtdha", re="(?s)^rtdha/(dir(\\d){3}/(file(\\d+).ext)?)?$"},
                new TestTrial { arg="sdfjk/BBB", re="(?s)^sdfjk/BBB/(dir(\\d){3}/(file(\\d+).ext)?)?$"}
            };

            for (int k = 0; k < trials.Length; k++)
            {
                TestContext.WriteLine("\n--------------------------------\n\n\n");
                string zipFileToCreate = Path.Combine(TopLevelDir, String.Format("CreateZip_AddDirectory_Nested-{0}.zip", k));

                Assert.IsFalse(File.Exists(zipFileToCreate), "The temporary zip file '{0}' already exists.", zipFileToCreate);

                string dirToZip = String.Format("DirectoryToZip.{0}.test", k);
                Directory.CreateDirectory(dirToZip);

                int i, j;
                int entries = 0;

                int subdirCount = _rnd.Next(23) + 7;
                for (i = 0; i < subdirCount; i++)
                {
                    string subdir = Path.Combine(dirToZip, String.Format("dir{0:D3}", i));
                    Directory.CreateDirectory(subdir);

                    int fileCount = _rnd.Next(8);  // sometimes zero
                    for (j = 0; j < fileCount; j++)
                    {
                        String file = Path.Combine(subdir, String.Format("file{0:D3}.ext", j));
                        TestUtilities.CreateAndFillFile(file, _rnd.Next(10750) + 50);
                        entries++;
                    }
                }

                var sw = new StringWriter();
                using (ZipFile zip = new ZipFile())
                {
                    zip.StatusMessageTextWriter = sw;
                    if (k == 0)
                        zip.AddDirectory(dirToZip);
                    else
                        zip.AddDirectory(dirToZip, trials[k].arg);
                    zip.Save(zipFileToCreate);
                }
                TestContext.WriteLine(sw.ToString());

                Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), entries,
                                     String.Format("The zip file created in cycle {0} has the wrong number of entries.", k));

                // verify that the entries in the zip are in the top level directory!!
                using (ZipFile zip2 = FileSystemZip.Read(zipFileToCreate))
                {
                    foreach (ZipEntry e in zip2)
                        TestContext.WriteLine("found entry: {0}", e.FileName);
                    foreach (ZipEntry e in zip2)
                    {
                        Assert.IsTrue(RE.Regex.IsMatch(e.FileName, trials[k].re),
                                      String.Format("In cycle {0}, Matching {1} against {2}", k, e.FileName, trials[k].re));
                    }

                }
            }
        }


        [TestMethod]
        public void Basic_SaveToFileStream()
        {
            // from small numbers of files to larger numbers of files
            for (int k = 0; k < 3; k++)
            {
                string zipFileToCreate = String.Format("SaveToFileStream-t{0}.zip", k);
                string dirToZip =  Path.GetRandomFileName();
                Directory.CreateDirectory(dirToZip);

                int filesToAdd = _rnd.Next(k * 10 + 3) + k * 10 + 3;
                for (int i = 0; i < filesToAdd; i++)
                {
                    var s = Path.Combine(dirToZip, String.Format("tempfile-{0}.bin", i));
                    int sz = _rnd.Next(10000) + 5000;
                    TestContext.WriteLine("  Creating file: {0} sz({1})", s, sz);
                    TestUtilities.CreateAndFillFileBinary(s, sz);
                }

                using (var fileStream = File.Create(zipFileToCreate))
                {
                    using (ZipFile zip1 = new ZipFile())
                    {
                        zip1.AddDirectory(dirToZip);
                        zip1.Comment = "This is a Comment On the Archive (AM/PM)";
                        zip1.Save(fileStream);
                    }
                }

                // Verify the files are in the zip
                Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                     filesToAdd,
                                     "Trial {0}: file {1} wrong number of entries.",
                                     k, zipFileToCreate);
            }
        }



        [TestMethod]
        public void Basic_IsText()
        {
            // from small numbers of files to larger numbers of files
            for (int k = 0; k < 3; k++)
            {
                string zipFileToCreate = String.Format("Basic_IsText-trial{0}.zip", k);
                string dirToZip = Path.GetRandomFileName();
                Directory.CreateDirectory(dirToZip);

                int filesToAdd = _rnd.Next(33) + 11;
                for (int i = 0; i < filesToAdd; i++)
                {
                    var s = Path.Combine(dirToZip, String.Format("tempfile-{0}.txt", i));
                    int sz = _rnd.Next(10000) + 5000;
                    TestContext.WriteLine("  Creating file: {0} sz({1})", s, sz);
                    TestUtilities.CreateAndFillFileText(s, sz);
                }

                using (ZipFile zip1 = new ZipFile())
                {
                    int count = 0;
                    var filesToZip = Directory.GetFiles(dirToZip);
                    foreach (var f in filesToZip)
                    {
                        var e = zip1.AddFile(f, "files");
                        switch (k)
                        {
                            case 0: break;
                            case 1: if ((count % 2) == 0) e.IsText = true; break;
                            case 2: if ((count % 2) != 0) e.IsText = true; break;
                            case 3: e.IsText = true; break;
                        }
                        count++;
                    }
                    zip1.Comment = "This is a Comment On the Archive (AM/PM)";
                    zip1.Save(zipFileToCreate);
                }

                // Verify the files are in the zip
                Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),
                                     filesToAdd,
                                     "trial {0}: file {1} number of entries.",
                                     k, zipFileToCreate);

                // verify the isText setting
                using (ZipFile zip2 = FileSystemZip.Read(zipFileToCreate))
                {
                    int count = 0;
                    foreach (var e in zip2)
                    {
                        switch (k)
                        {
                            case 0: Assert.IsFalse(e.IsText); break;
                            case 1: Assert.AreEqual<bool>((count % 2) == 0, e.IsText); break;
                            case 2: Assert.AreEqual<bool>((count % 2) != 0, e.IsText); break;
                            case 3: Assert.IsTrue(e.IsText); break;
                        }
                        count++;
                    }
                }
            }
        }


        [TestMethod]
        public void CreateZip_VerifyThatStreamRemainsOpenAfterSave()
        {
            Ionic.Zlib.CompressionLevel[] compressionLevelOptions = {
                Ionic.Zlib.CompressionLevel.None,
                Ionic.Zlib.CompressionLevel.BestSpeed,
                Ionic.Zlib.CompressionLevel.Default,
                Ionic.Zlib.CompressionLevel.BestCompression,
            };

            string[] Passwords = { null, Path.GetRandomFileName() };

            for (int j = 0; j < Passwords.Length; j++)
            {
                for (int k = 0; k < compressionLevelOptions.Length; k++)
                {
                    TestContext.WriteLine("\n\n---------------------------------\n" +
                                          "Trial ({0},{1}):  Password='{2}' Compression={3}\n",
                                          j, k, Passwords[j], compressionLevelOptions[k]);
                    string dirToZip = Path.GetRandomFileName();
                    Directory.CreateDirectory(dirToZip);

                    int filesAdded = _rnd.Next(3) + 3;
                    for (int i = 0; i < filesAdded; i++)
                    {
                        var s = Path.Combine(dirToZip, String.Format("tempfile-{0}-{1}-{2}.bin", j, k, i));
                        int sz = _rnd.Next(10000) + 5000;
                        TestContext.WriteLine("  Creating file: {0} sz({1})", s, sz);
                        TestUtilities.CreateAndFillFileBinary(s, sz);
                    }

                    TestContext.WriteLine("\n");

                    //string dirToZip = Path.GetFileName(TopLevelDir);
                    var ms = new MemoryStream();
                    Assert.IsTrue(ms.CanSeek, String.Format("Trial {0}: The output MemoryStream does not do Seek.", k));
                    using (ZipFile zip1 = new ZipFile())
                    {
                        zip1.CompressionLevel = compressionLevelOptions[k];
                        zip1.Password = Passwords[j];
                        zip1.Comment = String.Format("Trial ({0},{1}):  Password='{2}' Compression={3}\n",
                                                     j, k, Passwords[j], compressionLevelOptions[k]);
                        zip1.AddDirectory(dirToZip);
                        zip1.Save(ms);
                    }

                    Assert.IsTrue(ms.CanSeek, String.Format("Trial {0}: After writing, the OutputStream does not do Seek.", k));
                    Assert.IsTrue(ms.CanRead, String.Format("Trial {0}: The OutputStream cannot be Read.", k));

                    // seek to the beginning
                    ms.Seek(0, SeekOrigin.Begin);
                    int filesFound = 0;
                    using (ZipFile zip2 = ZipFile.Read(ms))
                    {
                        foreach (ZipEntry e in zip2)
                        {
                            TestContext.WriteLine("  Found entry: {0} isDir({1}) sz_c({2}) sz_unc({3})", e.FileName, e.IsDirectory, e.CompressedSize, e.UncompressedSize);
                            if (!e.IsDirectory)
                                filesFound++;
                        }
                    }
                    Assert.AreEqual<int>(filesFound, filesAdded,
                                         "Trial {0}", k);
                }
            }
        }


        [TestMethod]
        public void CreateZip_AddFile_VerifyCrcAndContents()
        {
            string filename = null;
            int entriesAdded = 0;
            string repeatedLine = null;
            int j;

            string zipFileToCreate = "CreateZip_AddFile_VerifyCrcAndContents.zip";
            string subdir = "A";
            Directory.CreateDirectory(subdir);

            // create the files
            int numFilesToCreate = _rnd.Next(10) + 8;
            for (j = 0; j < numFilesToCreate; j++)
            {
                filename = Path.Combine(subdir, String.Format("file{0:D3}.txt", j));
                repeatedLine = String.Format("This line is repeated over and over and over in file {0}",
                                             Path.GetFileName(filename));
                TestUtilities.CreateAndFillFileText(filename, repeatedLine, _rnd.Next(34000) + 5000);
                entriesAdded++;
            }

            using (ZipFile zip1 = new ZipFile())
            {
                String[] filenames = Directory.GetFiles("A");
                foreach (String f in filenames)
                    zip1.AddFile(f, "");
                zip1.Comment = "UpdateTests::CreateZip_AddFile_VerifyCrcAndContents(): This archive will be updated.";
                zip1.Save(zipFileToCreate);
            }

            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate),entriesAdded);

            // now extract the files and verify their contents
            using (ZipFile zip2 = FileSystemZip.Read(zipFileToCreate))
            {
                foreach (string s in zip2.EntryFileNames)
                {
                    repeatedLine = String.Format("This line is repeated over and over and over in file {0}", s);
                    zip2[s].Extract("extract");

                    // verify the content of the updated file.
                    var sr = new StreamReader(Path.Combine("extract", s));
                    string actualLine = sr.ReadLine();
                    sr.Close();

                    Assert.AreEqual<string>(repeatedLine, actualLine,
                                            "Updated file ({0}) is incorrect.", s);
                }
            }
        }




        [TestMethod]
        public void Extract_IntoMemoryStream()
        {
            string filename = null;
            int entriesAdded = 0;
            string repeatedLine = null;
            int j;

            string zipFileToCreate = "Extract_IntoMemoryStream.zip";
            string subdir = "A";
            Directory.CreateDirectory(subdir);

            // create the files
            int numFilesToCreate = _rnd.Next(10) + 8;
            for (j = 0; j < numFilesToCreate; j++)
            {
                filename = Path.Combine(subdir, String.Format("file{0:D3}.txt", j));
                repeatedLine = String.Format("This line is repeated over and over and over in file {0}",
                                             Path.GetFileName(filename));
                TestUtilities.CreateAndFillFileText(filename, repeatedLine, _rnd.Next(34000) + 5000);
                entriesAdded++;
            }

            // Create the zip file
            using (ZipFile zip1 = new ZipFile())
            {
                String[] filenames = Directory.GetFiles("A");
                foreach (String f in filenames)
                    zip1.AddFile(f, "");
                zip1.Comment = "BasicTests::Extract_IntoMemoryStream()";
                zip1.Save(zipFileToCreate);
            }

            // Verify the files are in the zip
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), entriesAdded,
                                 "The Zip file has the wrong number of entries.");

            // now extract the files into memory streams, checking only the length of the file.
            using (ZipFile zip2 = FileSystemZip.Read(zipFileToCreate))
            {
                foreach (string s in zip2.EntryFileNames)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        zip2[s].Extract(ms);
                        byte[] a = ms.ToArray();
                        string f = Path.Combine(subdir, s);
                        var fi = new FileInfo(f);
                        Assert.AreEqual<int>((int)(fi.Length), a.Length, "Unequal file lengths.");
                    }
                }
            }
        }


        [TestMethod]
        public void Retrieve_ViaIndexer2_wi11056()
        {
            string fileName = "wi11056.dwf";
            string entryName = @"com.autodesk.dwf.ePlot_5VFMLy3OdEetAPFe7uWXYg\descriptor.xml";

            TestContext.WriteLine("Current Dir: {0}", CurrentDir);

            string filename = Path.Combine(SourceDir, TestUtilities.GetBinDir("Zip.Portable.Tests"), "zips", fileName);

            TestContext.WriteLine("Reading zip file: '{0}'", filename);
            using (ZipFile zip = FileSystemZip.Read(filename))
            {
                var e = zip[entryName];
                Assert.IsFalse(e == null,
                               "Retrieval by stringindex failed.");
            }
        }



        [TestMethod]
        public void Retrieve_ViaIndexer()
        {
            // select the name of the zip file
            string zipFileToCreate = Path.Combine(TopLevelDir, "Retrieve_ViaIndexer.zip");
            string filename = null;
            int entriesAdded = 0;
            string repeatedLine = null;
            int j;

            // create the subdirectory
            string subdir = "A";
            Directory.CreateDirectory(subdir);

            // create the files
            int numFilesToCreate = _rnd.Next(10) + 8;
            for (j = 0; j < numFilesToCreate; j++)
            {
                filename = Path.Combine(subdir, String.Format("File{0:D3}.txt", j));
                repeatedLine = String.Format("This line is repeated over and over and over in file {0}",
                                             Path.GetFileName(filename));
                TestUtilities.CreateAndFillFileText(filename, repeatedLine, _rnd.Next(23000) + 4000);
                entriesAdded++;
            }

            // Create the zip file
            using (ZipFile zip1 = new ZipFile())
            {
                String[] filenames = Directory.GetFiles("A");
                foreach (String f in filenames)
                    zip1.AddFile(f, "");
                zip1.Comment = "BasicTests::Retrieve_ViaIndexer()";
                zip1.Save(zipFileToCreate);
            }

            // Verify the files are in the zip
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), entriesAdded,
                                 "The Zip file has the wrong number of entries.");

            // now extract the files into memory streams, checking only the length of the file.
            // We do 4 combinations:  case-sensitive on or off, and filename conversion on or off.
            for (int m = 0; m < 2; m++)
            {
                for (int n = 0; n < 2; n++)
                {
                    using (ZipFile zip2 = FileSystemZip.Read(zipFileToCreate))
                    {
                        if (n == 1) zip2.CaseSensitiveRetrieval = true;
                        foreach (string s in zip2.EntryFileNames)
                        {
                            var s2 = (m == 1) ? s.ToUpper() : s;
                            using (MemoryStream ms = new MemoryStream())
                            {
                                try
                                {
                                    zip2[s2].Extract(ms);
                                    byte[] a = ms.ToArray();
                                    string f = Path.Combine(subdir, s2);
                                    var fi = new FileInfo(f);
                                    Assert.AreEqual<int>((int)(fi.Length), a.Length, "Unequal file lengths.");
                                }
                                catch
                                {
                                    Assert.AreEqual<int>(1, n * m, "Indexer retrieval failed unexpectedly.");
                                }
                            }
                        }
                    }
                }
            }
        }




        [TestMethod]
        public void CreateZip_SetFileComments()
        {
            string zipFileToCreate = "FileComments.zip";
            string FileCommentFormat = "Comment Added By Test to file '{0}'";
            string commentOnArchive = "Comment added by FileComments() method.";

            int fileCount = _rnd.Next(3) + 3;
            string[] filesToZip = new string[fileCount];
            for (int i = 0; i < fileCount; i++)
            {
                filesToZip[i] = Path.Combine(TopLevelDir, String.Format("file{0:D3}.bin", i));
                TestUtilities.CreateAndFillFile(filesToZip[i], _rnd.Next(10000) + 5000);
            }

            using (ZipFile zip = new ZipFile())
            {
                //zip.StatusMessageTextWriter = System.Console.Out;
                for (int i = 0; i < filesToZip.Length; i++)
                {
                    // use the local filename (not fully qualified)
                    ZipEntry e = zip.AddFile(Path.GetFileName(filesToZip[i]));
                    e.Comment = String.Format(FileCommentFormat, e.FileName);
                }
                zip.Comment = commentOnArchive;
                zip.Save(zipFileToCreate);
            }

            int entries = 0;
            using (ZipFile z2 = FileSystemZip.Read(zipFileToCreate))
            {
                Assert.AreEqual<String>(commentOnArchive, z2.Comment, "Unexpected comment on ZipFile.");
                foreach (ZipEntry e in z2)
                {
                    string expectedComment = String.Format(FileCommentFormat, e.FileName);
                    Assert.AreEqual<string>(expectedComment, e.Comment, "Unexpected comment on ZipEntry.");
                    entries++;
                }
            }
            Assert.AreEqual<int>(entries, filesToZip.Length, "Unexpected file count. Expected {0}, got {1}.",
                                 filesToZip.Length, entries);
        }





        private DateTime AdjustTime_Win32ToDotNet(DateTime time)
        {
            // If I read a time from a file with GetLastWriteTime() (etc), I need
            // to adjust it for display in the .NET environment.
            if (time.Kind == DateTimeKind.Utc) return time;
            DateTime adjusted = time;
            if (DateTime.Now.IsDaylightSavingTime() && !time.IsDaylightSavingTime())
                adjusted = time + new System.TimeSpan(1, 0, 0);

            else if (!DateTime.Now.IsDaylightSavingTime() && time.IsDaylightSavingTime())
                adjusted = time - new System.TimeSpan(1, 0, 0);

            return adjusted;
        }



        [TestMethod]
        public void CreateZip_AddDirectory_NoFilesInRoot()
        {
            string zipFileToCreate = "CreateZip_AddDirectory_NoFilesInRoot.zip";
            string zipThis = "ZipThis";
            Directory.CreateDirectory(zipThis);

            int i, j;
            int entries = 0;

            int subdirCount = _rnd.Next(4) + 4;
            for (i = 0; i < subdirCount; i++)
            {
                string subdir = Path.Combine(zipThis, "DirectoryToZip.test." + i);
                Directory.CreateDirectory(subdir);

                int fileCount = _rnd.Next(3) + 3;
                for (j = 0; j < fileCount; j++)
                {
                    String file = Path.Combine(subdir, "file" + j);
                    TestUtilities.CreateAndFillFile(file, _rnd.Next(1000) + 1500);
                    entries++;
                }
            }

            using (ZipFile zip = new ZipFile())
            {
                zip.AddDirectory(zipThis);
                zip.Save(zipFileToCreate);
            }
            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), entries,
                                 "The Zip file has the wrong number of entries.");
        }


        [TestMethod]
        public void CreateZip_AddDirectory_OneCharOverrideName()
        {
            int entries = 0;
            String filename = null;

            // set the name of the zip file to create
            string zipFileToCreate = "CreateZip_AddDirectory_OneCharOverrideName.zip";
            String commentOnArchive = "BasicTests::CreateZip_AddDirectory_OneCharOverrideName(): This archive override the name of a directory with a one-char name.";

            string subdir = "A";
            Directory.CreateDirectory(subdir);

            int numFilesToCreate = _rnd.Next(23) + 14;
            var checksums = new Dictionary<string, string>();
            for (int j = 0; j < numFilesToCreate; j++)
            {
                filename = Path.Combine(subdir, String.Format("file{0:D3}.txt", j));
                TestUtilities.CreateAndFillFileText(filename, _rnd.Next(12000) + 5000);
                var chk = TestUtilities.ComputeChecksum(filename);

                var relativePath = Path.Combine(Path.GetFileName(subdir), Path.GetFileName(filename));
                //var key = Path.Combine("A", filename);
                var key = TestUtilities.TrimVolumeAndSwapSlashes(relativePath);
                checksums.Add(key, TestUtilities.CheckSumToString(chk));

                entries++;
            }

            using (ZipFile zip1 = new ZipFile())
            {
                zip1.AddDirectory(subdir, Path.GetFileName(subdir));
                zip1.Comment = commentOnArchive;
                zip1.Save(zipFileToCreate);
            }

            Assert.AreEqual<int>(TestUtilities.CountEntries(zipFileToCreate), entries);

            // validate all the checksums
            using (ZipFile zip2 = FileSystemZip.Read(zipFileToCreate))
            {
                foreach (ZipEntry e in zip2)
                {
                    e.Extract("unpack");
                    if (checksums.ContainsKey(e.FileName))
                    {
                        string pathToExtractedFile = Path.Combine("unpack", e.FileName);

                        // verify the checksum of the file is correct
                        string expectedCheckString = checksums[e.FileName];
                        string actualCheckString = TestUtilities.GetCheckSumString(pathToExtractedFile);
                        Assert.AreEqual<String>
                            (expectedCheckString,
                             actualCheckString,
                             "Unexpected checksum on extracted filesystem file ({0}).",
                             pathToExtractedFile);
                    }
                }
            }

        }



        [TestMethod]
        public void CreateZip_CompressionLevelZero_AllEntries()
        {
            string zipFileToCreate = "CompressionLevelZero.zip";
            String commentOnArchive = "BasicTests::CompressionLevelZero(): This archive override the name of a directory with a one-char name.";
            int entriesAdded = 0;
            String filename = null;
            string subdir = "A";
            Directory.CreateDirectory(subdir);

            int fileCount = _rnd.Next(10) + 10;
            for (int j = 0; j < fileCount; j++)
            {
                filename = Path.Combine(subdir, String.Format("file{0:D3}.txt", j));
                TestUtilities.CreateAndFillFileText(filename, _rnd.Next(34000) + 5000);
                entriesAdded++;
            }

            using (ZipFile zip = new ZipFile())
            {
                zip.CompressionLevel = Ionic.Zlib.CompressionLevel.None;
                zip.AddDirectory(subdir, Path.GetFileName(subdir));
                zip.Comment = commentOnArchive;
                zip.Save(zipFileToCreate);
            }

            int entriesFound = 0;
            using (ZipFile zip = FileSystemZip.Read(zipFileToCreate))
            {
                foreach (ZipEntry e in zip)
                {
                    if (!e.IsDirectory) entriesFound++;
                    Assert.AreEqual<short>(0, (short)e.CompressionMethod,
                                           "compression method");
                }
            }
            Assert.AreEqual<int>(entriesAdded, entriesFound,
                                 "unexpected number of entries.");
            BasicVerifyZip(zipFileToCreate);
        }



        [TestMethod]
        public void CreateZip_ForceNoCompressionSomeEntries()
        {
            string zipFileToCreate = "ForceNoCompression.zip";
            String filename = null;
            string subdir = "A";
            Directory.CreateDirectory(subdir);
            int fileCount = _rnd.Next(13) + 13;
            for (int j = 0; j < fileCount; j++)
            {
                filename = Path.Combine(subdir, String.Format("{0}-file{1:D3}.txt", (_rnd.Next(2) == 0) ? "C":"U", j));
                TestUtilities.CreateAndFillFileText(filename, _rnd.Next(34000) + 5000);
            }

            using (ZipFile zip = new ZipFile())
            {
                String[] filenames = Directory.GetFiles("A");
                foreach (String f in filenames)
                {
                    ZipEntry e = zip.AddFile(f, "");
                    if (e.FileName.StartsWith("U"))
                        e.CompressionMethod = 0x0;
                }
                zip.Comment = "Some of these files do not use compression.";
                zip.Save(zipFileToCreate);
            }

            int entriesFound = 0;
            using (ZipFile zip = FileSystemZip.Read(zipFileToCreate))
            {
                foreach (ZipEntry e in zip)
                {
                    if (!e.IsDirectory) entriesFound++;
                    Assert.AreEqual<Int32>((e.FileName.StartsWith("U"))?0x00:0x08,
                                           (Int32)e.CompressionMethod,
                                           "Unexpected compression method on text file ({0}).", e.FileName);
                }
            }
            Assert.AreEqual<int>(fileCount, entriesFound, "The created Zip file has an unexpected number of entries.");

            BasicVerifyZip(zipFileToCreate);
        }



        [TestMethod]
        public void AddFile_CompressionMethod_None_wi9208()
        {
            string zipFileToCreate = "AddFile_CompressionMethod_None_wi9208.zip";
            string subdir = "A";
            Directory.CreateDirectory(subdir);
            using (ZipFile zip = new ZipFile())
            {
                string filename = Path.Combine(subdir, String.Format("FileToBeAdded-{0:D2}.txt", _rnd.Next(1000)));
                TestUtilities.CreateAndFillFileText(filename, _rnd.Next(34000) + 5000);
                var e = zip.AddFile(filename,"zipped");
                e.CompressionMethod = CompressionMethod.None;
                zip.Save(zipFileToCreate);
            }

            TestContext.WriteLine("File zipped!...");
            TestContext.WriteLine("Reading...");

            using (ZipFile zip = FileSystemZip.Read(zipFileToCreate))
            {
                foreach (var e in  zip)
                {
                    Assert.AreEqual<String>("None", e.CompressionMethod.ToString());
                }
            }
            BasicVerifyZip(zipFileToCreate);
        }



        [TestMethod]
        public void GetInfo()
        {
            TestContext.WriteLine("GetInfo");
            string zipFileToCreate = "GetInfo.zip";
            String filename = null;
            int n;
            string subdir = Path.Combine(TopLevelDir,
                                         TestUtilities.GenerateRandomAsciiString(9));
            Directory.CreateDirectory(subdir);

            int fileCount = _rnd.Next(27) + 23;
            TestContext.WriteLine("Creating {0} files", fileCount);
            for (int j = 0; j < fileCount; j++)
            {
                filename = Path.Combine(subdir, String.Format("file{0:D3}.txt", j));
                if (_rnd.Next(7)!=0)
                    TestUtilities.CreateAndFillFileText(filename, _rnd.Next(34000) + 5000);
                else
                {
                    // create an empty file
                    using (var fs = File.Create(filename)) { }
                }
            }

            TestContext.WriteLine("Creating a zip file");
            using (ZipFile zip = new ZipFile())
            {
                zip.Password = TestUtilities.GenerateRandomPassword(11);
                var filenames = Directory.GetFiles(subdir);
                foreach (String f in filenames)
                {
                    ZipEntry e = zip.AddFile(f, "");
                    if (_rnd.Next(3)==0)
                        e.CompressionMethod = 0x0;
                    n = _rnd.Next(crypto.Length);
                    e.Encryption = crypto[n];
                }
                zip.Save(zipFileToCreate);
            }

            TestContext.WriteLine("Calling ZipFile::Info_get");
            using (ZipFile zip = FileSystemZip.Read(zipFileToCreate))
            {
                string info = zip.Info;
                TestContext.WriteLine("Info: (len({0}))", info.Length);
                foreach (var line in info.Split('\n'))
                    TestContext.WriteLine(line);

                Assert.IsTrue(info.Length > 300,
                              "Suspect info string (length({0}))",
                              info.Length);
            }
        }



        [TestMethod]
        public void Create_WithChangeDirectory()
        {
            string zipFileToCreate = "Create_WithChangeDirectory.zip";
            String filename = "Testfile.txt";
            TestUtilities.CreateAndFillFileText(filename, _rnd.Next(34000) + 5000);

            string cwd = Directory.GetCurrentDirectory();
            using (var zip = new ZipFile())
            {
                zip.AddFile(filename, "");
                Directory.SetCurrentDirectory(SourceDir);
                zip.AddFile(Path.Combine(SourceDir, "Zip.Portable.Tests", "BasicTests.cs"), "");
                Directory.SetCurrentDirectory(cwd);
                zip.Save(zipFileToCreate);
            }
        }

        [TestMethod]
        public void Create_WithAbsolutePath()
        {
            string zipFileToCreate = "Create_WithAbsolutePath.zip";
            String filename1 = "Testfile1.txt";
            String filename2 = "Testfile2.txt";
            String filename3 = "Testfile3.txt";
            String filename4 = "Testfile4.txt";
            TestUtilities.CreateAndFillFileText(filename1, _rnd.Next(34000) + 5000);
            TestUtilities.CreateAndFillFileText(filename2, _rnd.Next(34000) + 5000);
            TestUtilities.CreateAndFillFileText(filename3, _rnd.Next(34000) + 5000);
            TestUtilities.CreateAndFillFileText(filename4, _rnd.Next(34000) + 5000);
            String fullPath3 = Path.Combine(TopLevelDir, filename3);
            String fullPath4 = Path.Combine(TopLevelDir, filename4);

            // add a full path
            using (var zip = new ZipFile(System.Text.Encoding.UTF8))
            {
                zip.AddFile(filename1, ""); // should be at the root
                zip.AddFile(filename2);     // should be at the root
                zip.AddFile(fullPath3, ""); // should be at the root
                zip.AddFile(fullPath4);     // should be deep
                zip.Save(zipFileToCreate);
            }

            // make sure the path is the full one
            using (var zip = FileSystemZip.Read(zipFileToCreate))
            {
                var items = zip.EntryFileNames.ToArray();

                var expected1 = filename1;
                var expected2 = filename2;
                var expected3 = filename3;
                // remove the root as it is not used
                var expected4 = fullPath4.Split('/', '\\').Skip(1).ToArray();

                Assert.AreEqual(expected1, items[0]);
                Assert.AreEqual(expected2, items[1]);
                Assert.AreEqual(expected3, items[2]);
                CollectionAssert.AreEqual(expected4, items[3].Split('/', '\\'));
            }
        }




        private void VerifyEntries(string zipFile,
                                   Variance variance,
                                   int[] values,
                                   EncryptionAlgorithm[] a,
                                   int stage,
                                   int compFlavor,
                                   int encryptionFlavor)
        {
            using (var zip = FileSystemZip.Read(zipFile))
            {
                foreach (ZipEntry e in zip)
                {
                    var compCheck = false;
                    if (variance == Variance.Method)
                    {
                        compCheck = (e.CompressionMethod == (CompressionMethod)values[stage]);
                    }
                    else
                    {
                        // Variance.Level
                        CompressionMethod expectedMethod =
                            ((Ionic.Zlib.CompressionLevel)values[stage] == Ionic.Zlib.CompressionLevel.None)
                            ? CompressionMethod.None
                            : CompressionMethod.Deflate;
                        compCheck = (e.CompressionMethod == expectedMethod);
                    }

                    Assert.IsTrue(compCheck,
                                  "Unexpected compression method ({0}) on entry ({1}) variance({2}) flavor({3},{4}) stage({5})",
                                  e.CompressionMethod,
                                  e.FileName,
                                  variance,
                                  compFlavor, encryptionFlavor,
                                  stage
                                  );

                    var cryptoCheck = (e.Encryption == a[stage]);

                    Assert.IsTrue(cryptoCheck,
                                  "Unexpected encryption ({0}) on entry ({1}) variance({2}) flavor({3},{4}) stage({5})",
                                  e.Encryption,
                                  e.FileName,
                                  variance,
                                  compFlavor,
                                  encryptionFlavor,
                                  stage
                                  );
                }
            }
        }



        private void QuickCreateZipAndChecksums(string zipFile,
                                                Variance variance,
                                                object compressionMethodOrLevel,
                                                EncryptionAlgorithm encryption,
                                                string password,
                                                out string[] files,
                                                out Dictionary<string, byte[]> checksums
                                                )
        {
            string srcDir = Path.Combine(SourceDir, "Zip.Portable.Tests");
            files = Directory.GetFiles(srcDir, "*.cs", SearchOption.TopDirectoryOnly);
            checksums = new Dictionary<string, byte[]>();
            foreach (string f in  files)
            {
                var chk = TestUtilities.ComputeChecksum(f);
                var key = Path.GetFileName(f);
                checksums.Add(key, chk);
            }

            using (var zip = new ZipFile())
            {
                if (variance == Variance.Level)
                {
                    zip.CompressionLevel= (Ionic.Zlib.CompressionLevel) compressionMethodOrLevel;
                }
                else
                {
                    if ((Ionic.Zip.CompressionMethod)compressionMethodOrLevel == CompressionMethod.None)
                        zip.CompressionLevel = Ionic.Zlib.CompressionLevel.None;
                }

                if (password != null)
                {
                    zip.Password = password;
                    zip.Encryption = encryption;
                }
                zip.AddFiles(files, "");
                zip.Save(zipFile);
            }

            int count = TestUtilities.CountEntries(zipFile);
            Assert.IsTrue(count > 5,
                          "Unexpected number of entries ({0}) in the zip file.", count);
        }

        enum Variance
        {
            Method = 0 ,
            Level = 1
        }


        private string GeneratePassword()
        {
            return TestUtilities.GenerateRandomPassword();
            //return TestUtilities.GenerateRandomAsciiString(9);
            //return "Alphabet";
        }

        private void _Internal_Resave(string zipFile,
                                      Variance variance,
                                      int[] values,
                                      EncryptionAlgorithm[] cryptos,
                                      int compFlavor,
                                      int encryptionFlavor
                                      )
        {
            // Create a zip file, then re-save it with changes in compression methods,
            // compression levels, and/or encryption.  The methods/levels, cryptos are
            // for original and re-saved values. This tests whether we can update a zip
            // entry with changes in those properties.

            string[] passwords = new string[2];
            passwords[0]= (cryptos[0]==EncryptionAlgorithm.None) ? null : GeneratePassword();
            passwords[1]= passwords[0] ?? ((cryptos[1]==EncryptionAlgorithm.None) ? null : GeneratePassword());

            //TestContext.WriteLine("  crypto: '{0}'  '{1}'", crypto[0]??"-NONE-", passwords[1]??"-NONE-");
            TestContext.WriteLine("  crypto: '{0}'  '{1}'", cryptos[0], cryptos[1]);


            // first, create a zip file
            string[] filesToZip;
            Dictionary<string, byte[]> checksums;
            QuickCreateZipAndChecksums(zipFile, variance, values[0], cryptos[0], passwords[0], out filesToZip, out checksums);


            // check that the zip was constructed as expected
            VerifyEntries(zipFile, variance, values, cryptos, 0, compFlavor, encryptionFlavor);


            // modify some properties (CompressionLevel, CompressionMethod, and/or Encryption) on each entry
            using (var zip = FileSystemZip.Read(zipFile))
            {
                zip.Password = passwords[1];
                foreach (ZipEntry e in zip)
                {
                    if (variance == Variance.Method)
                        e.CompressionMethod = (CompressionMethod)values[1];
                    else
                        e.CompressionLevel = (Ionic.Zlib.CompressionLevel)values[1];

                    e.Encryption = cryptos[1];
                }
                zip.Save(zipFile);
            }


            // Check that the zip was modified as expected
            VerifyEntries(zipFile, variance, values, cryptos, 1, compFlavor, encryptionFlavor);

            // now extract the items and verify checksums
            string extractDir = "ex";
            int c=0;
            while (Directory.Exists(extractDir + c)) c++;
            extractDir += c;

            // extract
            using (var zip = FileSystemZip.Read(zipFile))
            {
                zip.Password = passwords[1];
                zip.ExtractAll(extractDir);
            }

            VerifyChecksums(extractDir, filesToZip, checksums);
        }





        EncryptionAlgorithm[][] CryptoPairs = {
            new EncryptionAlgorithm[] {EncryptionAlgorithm.None, EncryptionAlgorithm.None},
#if AESCRYPTO
            new EncryptionAlgorithm[] {EncryptionAlgorithm.None, EncryptionAlgorithm.WinZipAes128},
            new EncryptionAlgorithm[] {EncryptionAlgorithm.WinZipAes128, EncryptionAlgorithm.None},
            new EncryptionAlgorithm[] {EncryptionAlgorithm.WinZipAes128, EncryptionAlgorithm.WinZipAes128},
#endif
            new EncryptionAlgorithm[] {EncryptionAlgorithm.None, EncryptionAlgorithm.PkzipWeak},
            new EncryptionAlgorithm[] {EncryptionAlgorithm.PkzipWeak, EncryptionAlgorithm.None},
#if AESCRYPTO
            new EncryptionAlgorithm[] {EncryptionAlgorithm.WinZipAes128, EncryptionAlgorithm.PkzipWeak},
            new EncryptionAlgorithm[] {EncryptionAlgorithm.PkzipWeak, EncryptionAlgorithm.WinZipAes128}
#endif
        };

        int[][][] VariancePairs = {
            new int[][] {
                new int[] {(int)CompressionMethod.Deflate, (int)CompressionMethod.Deflate},
                new int[] {(int)CompressionMethod.Deflate, (int)CompressionMethod.None},
                new int[] {(int)CompressionMethod.None, (int)CompressionMethod.Deflate},
                new int[] {(int)CompressionMethod.None, (int)CompressionMethod.None}
            },
            new int[][] {
                new int[] {(int)Ionic.Zlib.CompressionLevel.Default, (int)Ionic.Zlib.CompressionLevel.Default},
                new int[] {(int)Ionic.Zlib.CompressionLevel.Default, (int)Ionic.Zlib.CompressionLevel.None},
                new int[] {(int)Ionic.Zlib.CompressionLevel.None, (int)Ionic.Zlib.CompressionLevel.Default},
                new int[] {(int)Ionic.Zlib.CompressionLevel.None, (int)Ionic.Zlib.CompressionLevel.None}
            }
        };

        private void _Internal_Resave(Variance variance, int compFlavor, int encryptionFlavor)
        {
            // Check that re-saving a zip, after modifying properties on
            // each entry, actually does what we want.
            if (encryptionFlavor == 0)
                TestContext.WriteLine("Resave workdir: {0}", TopLevelDir);

            string rootname = String.Format("Resave_Compression{0}_{1}_Encryption_{2}.zip",
                                            variance, compFlavor, encryptionFlavor);

            string zipFileToCreate = Path.Combine(TopLevelDir, rootname);
            int[] values = VariancePairs[(int)variance][compFlavor];

            TestContext.WriteLine("Resave {0} {1} {2} file({3})", variance, compFlavor, encryptionFlavor, Path.GetFileName(zipFileToCreate));

            _Internal_Resave(zipFileToCreate, variance, values, CryptoPairs[encryptionFlavor], compFlavor, encryptionFlavor);
        }


        [TestMethod]
        public void Resave_CompressionMethod_0()
        {
            for (int i=0; i<CryptoPairs.Length;  i++)
            {
                _Internal_Resave(Variance.Method, 0, i);
            }
        }


        [TestMethod]
        public void Resave_CompressionMethod_1()
        {
            for (int i=0; i<CryptoPairs.Length;  i++)
            {
                if (i!=3)
                    _Internal_Resave(Variance.Method, 1, i);
            }
        }

        [TestMethod]
        public void Resave_CompressionMethod_2()
        {
            for (int i=0; i<CryptoPairs.Length;  i++)
            {
                _Internal_Resave(Variance.Method, 2, i);
            }
        }

        [TestMethod]
        public void Resave_CompressionMethod_3()
        {
            for (int i=0; i<CryptoPairs.Length;  i++)
            {
                _Internal_Resave(Variance.Method, 3, i);
            }
        }


        [TestMethod]
        public void Resave_CompressionLevel_0()
        {
            for (int i=0; i<CryptoPairs.Length;  i++)
            {
                _Internal_Resave(Variance.Level, 0, i);
            }
        }


        [TestMethod]
        public void Resave_CompressionLevel_1()
        {
            for (int i=0; i<CryptoPairs.Length;  i++)
            {
                if (i!=3)
                    _Internal_Resave(Variance.Level, 1, i);
            }
        }

        [TestMethod]
        public void Resave_CompressionLevel_2()
        {
            for (int i=0; i<CryptoPairs.Length;  i++)
            {
                _Internal_Resave(Variance.Level, 2, i);
            }
        }

        [TestMethod]
        public void Resave_CompressionLevel_3()
        {
            for (int i=0; i<CryptoPairs.Length;  i++)
            {
                _Internal_Resave(Variance.Level, 3, i);
            }
        }

        [TestMethod]
        public void CreateZip_Resave_Resave()
        {
            TestContext.WriteLine("Creating fodder files...");

            int entriesAdded = 0;
            String filename = null;
            string subdir = "A";
            string extractDir = "extract";
            Directory.CreateDirectory(subdir);
            int fileCount = _rnd.Next(10) + 10;
            for (int j = 0; j < fileCount; j++)
            {
                filename = Path.Combine(subdir, String.Format("file{0:D3}.txt", j));
                TestUtilities.CreateAndFillFileText(filename, _rnd.Next(34000) + 5000);
                entriesAdded++;
            }

            var filesToAdd = new List<String>(Directory.GetFiles(subdir));

            string zipFile1 = "InitialSave.zip";
            string zipFile2 = "Updated.zip";
            string zipFile3 = "Again.zip";

            TestContext.WriteLine("");
            TestContext.WriteLine("Creating zip...");
            using (var zip1 = new ZipFile())
            {
                zip1.AddFiles(filesToAdd, "");
                zip1.Save(zipFile1);
            }

            TestContext.WriteLine("");
            TestContext.WriteLine("Re-saving...");
            using (var zip2 = FileSystemZip.Read(zipFile1))
            {
                zip2.Save(zipFile2);
            }

            TestContext.WriteLine("");
            TestContext.WriteLine("Deleting initial archive...");
            string[] filesToDelete = Directory.GetFiles(".", "InitialSave.*");
            foreach (var file in filesToDelete)
            {
                File.Delete(file);
            }

            TestContext.WriteLine("");
            TestContext.WriteLine("Re-saving again...");
            using (var zip3 = FileSystemZip.Read(zipFile2))
            {
                zip3.Save(zipFile3);
            }

            TestContext.WriteLine("");
            TestContext.WriteLine("Extracting...");
            Directory.CreateDirectory(extractDir);
            using (var zip4 = FileSystemZip.Read(zipFile3))
            {
                foreach (var e in zip4)
                {
                    TestContext.WriteLine(" {0}", e.FileName);
                    e.Extract(extractDir);
                }
            }

            string[] filesUnzipped = Directory.GetFiles(extractDir);
            Assert.AreEqual<int>(filesToAdd.Count, filesUnzipped.Length,
                                 "Incorrect number of files extracted.");
        }

        [TestMethod]
        public void CreateZip_Resave_Extract()
        {
            TestContext.WriteLine("Creating fodder files...");

            int entriesAdded = 0;
            String filename = null;
            string subdir = "A";
            string extractDir = "extract";
            Directory.CreateDirectory(subdir);
            int fileCount = _rnd.Next(10) + 10;
            for (int j = 0; j < fileCount; j++)
            {
                filename = Path.Combine(subdir, String.Format("file{0:D3}.txt", j));
                TestUtilities.CreateAndFillFileText(filename, _rnd.Next(34000) + 5000);
                entriesAdded++;
            }

            var filesToAdd = new List<String>(Directory.GetFiles(subdir));

            string zipFile1 = "InitialSave.zip";
            string zipFile2 = "Updated.zip";
            string zipFile3 = "Again.zip";

            TestContext.WriteLine("");
            TestContext.WriteLine("Creating zip...");
            using (var zip1 = new ZipFile())
            {
                zip1.AddFiles(filesToAdd, "");
                zip1.Save(zipFile1);
            }

            TestContext.WriteLine("");
            TestContext.WriteLine("Re-saving...");
            using (var zip2 = FileSystemZip.Read(zipFile1))
            {
                zip2.Save(zipFile2);

                // make sure we aren't accidentally using InitialSave.zip data
                TestContext.WriteLine("");
                TestContext.WriteLine("Deleting initial archive...");
                string[] filesToDelete = Directory.GetFiles(".", "InitialSave.*");
                foreach (var file in filesToDelete)
                {
                    File.Delete(file);
                }
                
                // this should be using the Updated.zip file stream
                TestContext.WriteLine("");
                TestContext.WriteLine("Extracting...");
                Directory.CreateDirectory(extractDir);
                foreach (var e in zip2)
                {
                    TestContext.WriteLine(" {0}", e.FileName);
                    e.Extract(extractDir);
                }
            }

            string[] filesUnzipped = Directory.GetFiles(extractDir);
            Assert.AreEqual<int>(filesToAdd.Count, filesUnzipped.Length,
                                 "Incorrect number of files extracted.");
        }
    }
}

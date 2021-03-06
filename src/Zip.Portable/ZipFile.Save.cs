// ZipFile.Save.cs
// ------------------------------------------------------------------
//
// Copyright (c) 2009 Dino Chiesa.
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
// Time-stamp: <2011-August-05 13:31:23>
//
// ------------------------------------------------------------------
//
// This module defines the methods for Save operations on zip files.
//
// ------------------------------------------------------------------
//


using System;
using System.IO;
using System.Collections.Generic;

namespace Ionic.Zip
{

    public partial class ZipFile
    {



        /// <summary>
        ///   Saves the Zip archive to a file, specified by the Name property of the
        ///   <c>ZipFile</c>.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   The <c>ZipFile</c> instance is written to storage, typically a zip file
        ///   in a filesystem, only when the caller calls <c>Save</c>.  In the typical
        ///   case, the Save operation writes the zip content to a temporary file, and
        ///   then renames the temporary file to the desired name. If necessary, this
        ///   method will delete a pre-existing file before the rename.
        /// </para>
        ///
        /// <para>
        ///   The <see cref="ZipFile.Name"/> property is specified either explicitly,
        ///   or implicitly using one of the parameterized ZipFile constructors.  For
        ///   COM Automation clients, the <c>Name</c> property must be set explicitly,
        ///   because COM Automation clients cannot call parameterized constructors.
        /// </para>
        ///
        /// <para>
        ///   When using a filesystem file for the Zip output, it is possible to call
        ///   <c>Save</c> multiple times on the <c>ZipFile</c> instance. With each
        ///   call the zip content is re-written to the same output file.
        /// </para>
        ///
        /// <para>
        ///   Data for entries that have been added to the <c>ZipFile</c> instance is
        ///   written to the output when the <c>Save</c> method is called. This means
        ///   that the input streams for those entries must be available at the time
        ///   the application calls <c>Save</c>.  If, for example, the application
        ///   adds entries with <c>AddEntry</c> using a dynamically-allocated
        ///   <c>MemoryStream</c>, the memory stream must not have been disposed
        ///   before the call to <c>Save</c>. See the <see
        ///   cref="ZipEntry.InputStream"/> property for more discussion of the
        ///   availability requirements of the input stream for an entry, and an
        ///   approach for providing just-in-time stream lifecycle management.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <seealso cref="Ionic.Zip.ZipFile.AddEntry(String, System.IO.Stream)"/>
        ///
        /// <exception cref="Ionic.Zip.BadStateException">
        ///   Thrown if you haven't specified a location or stream for saving the zip,
        ///   either in the constructor or by setting the Name property, or if you try
        ///   to save a regular zip archive to a filename with a .exe extension.
        /// </exception>
        ///
        /// <exception cref="System.OverflowException">
        ///   Thrown if <see cref="MaxOutputSegmentSize"/> is non-zero, and the number
        ///   of segments that would be generated for the spanned zip file during the
        ///   save operation exceeds 99.  If this happens, you need to increase the
        ///   segment size.
        /// </exception>
        ///
        internal void Save()
        {
            try
            {
                bool thisSaveUsedZip64 = false;
                _saveOperationCanceled = false;
                _numberOfSegmentsForMostRecentSave = 0;
                OnSaveStarted();

                if (WriteStream == null)
                    throw new BadStateException("You haven't specified where to save the zip.");


                // check if modified, before saving.
                if (!_contentsChanged)
                {
                    OnSaveCompleted();
                    if (Verbose) StatusMessageTextWriter.WriteLine("No save is necessary....");
                    return;
                }

                Reset(true);

                if (Verbose) StatusMessageTextWriter.WriteLine("saving....");

                // validate the number of entries
                if (_entries.Count >= 0xFFFF && _zip64 == Zip64Option.Never)
                    throw new ZipException("The number of entries is 65535 or greater. Consider setting the UseZip64WhenSaving property on the ZipFile instance.");


                // write an entry in the zip for each file
                int n = 0;
                // workitem 9831
                ICollection<ZipEntry> c = (SortEntriesBeforeSaving) ? EntriesSorted : Entries;
                foreach (ZipEntry e in c) // _entries.Values
                {
                    OnSaveEntry(n, e, true);
                    e.Write(WriteStream);
                    if (_saveOperationCanceled)
                        break;

                    n++;
                    OnSaveEntry(n, e, false);
                    if (_saveOperationCanceled)
                        break;

                    // Some entries can be skipped during the save.
                    if (e.IncludedInMostRecentSave)
                        thisSaveUsedZip64 |= e.OutputUsedZip64.Value;
                }



                if (_saveOperationCanceled)
                    return;

                var zss = WriteStream as ZipSegmentedStream;

                _numberOfSegmentsForMostRecentSave = (zss!=null)
                    ? zss.CurrentSegment
                    : 0;

                bool directoryNeededZip64 =
                    ZipOutput.WriteCentralDirectoryStructure
                    (WriteStream,
                     c,
                     (zss != null) ? zss.CurrentSegment : 1,
                     _zip64,
                     Comment,
                     new ZipContainer(this));

                OnSaveEvent(ZipProgressEventType.Saving_AfterSaveTempArchive);

                _hasBeenSaved = true;
                _contentsChanged = false;

                thisSaveUsedZip64 |= directoryNeededZip64;
                _OutputUsesZip64 = new Nullable<bool>(thisSaveUsedZip64);



                NotifyEntriesSaveComplete(c);
                OnSaveCompleted();
                _JustSaved = true;
            }

            // workitem 5043
            finally
            {
                CleanupAfterSaveOperation();
            }

            return;
        }



        private static void NotifyEntriesSaveComplete(ICollection<ZipEntry> c)
        {
            foreach (ZipEntry e in  c)
            {
                e.NotifySaveComplete();
            }
        }




        private void CleanupAfterSaveOperation()
        {
            if (_WriteStreamIsOurs)
            {
                // close the stream if there is a file behind it.
                if (_writestream != null)
                {
                    try
                    {
                        // workitem 7704
#if NETCF
                        _writestream.Close();
#else
                        _writestream.Dispose();
#endif
                    }
                    catch (System.IO.IOException) { }
                }
                _writestream = null;
                _writeSegmentsManager = null;

            }
        }


        /// <summary>
        ///   Save the zip archive to the specified stream.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   The <c>ZipFile</c> instance is written to storage - typically a zip file
        ///   in a filesystem, but using this overload, the storage can be anything
        ///   accessible via a writable stream - only when the caller calls <c>Save</c>.
        /// </para>
        ///
        /// <para>
        ///   Use this method to save the zip content to a stream directly.  A common
        ///   scenario is an ASP.NET application that dynamically generates a zip file
        ///   and allows the browser to download it. The application can call
        ///   <c>Save(Response.OutputStream)</c> to write a zipfile directly to the
        ///   output stream, without creating a zip file on the disk on the ASP.NET
        ///   server.
        /// </para>
        ///
        /// <para>
        ///   Be careful when saving a file to a non-seekable stream, including
        ///   <c>Response.OutputStream</c>. When DotNetZip writes to a non-seekable
        ///   stream, the zip archive is formatted in such a way that may not be
        ///   compatible with all zip tools on all platforms.  It's a perfectly legal
        ///   and compliant zip file, but some people have reported problems opening
        ///   files produced this way using the Mac OS archive utility.
        /// </para>
        ///
        /// </remarks>
        ///
        /// <example>
        ///
        ///   This example saves the zipfile content into a MemoryStream, and
        ///   then gets the array of bytes from that MemoryStream.
        ///
        /// <code lang="C#">
        /// using (var zip = new Ionic.Zip.ZipFile())
        /// {
        ///     zip.CompressionLevel= Ionic.Zlib.CompressionLevel.BestCompression;
        ///     zip.Password = "VerySecret.";
        ///     zip.Encryption = EncryptionAlgorithm.WinZipAes128;
        ///     zip.AddFile(sourceFileName);
        ///     MemoryStream output = new MemoryStream();
        ///     zip.Save(output);
        ///
        ///     byte[] zipbytes = output.ToArray();
        /// }
        /// </code>
        /// </example>
        ///
        /// <example>
        /// <para>
        ///   This example shows a pitfall you should avoid. DO NOT read
        ///   from a stream, then try to save to the same stream.  DO
        ///   NOT DO THIS:
        /// </para>
        ///
        /// <code lang="C#">
        /// using (var fs = new FileSteeam(filename, FileMode.Open))
        /// {
        ///   using (var zip = Ionic.Zip.ZipFile.Read(inputStream))
        ///   {
        ///     zip.AddEntry("Name1.txt", "this is the content");
        ///     zip.Save(inputStream);  // NO NO NO!!
        ///   }
        /// }
        /// </code>
        ///
        /// <para>
        ///   Better like this:
        /// </para>
        ///
        /// <code lang="C#">
        /// using (var zip = Ionic.Zip.ZipFile.Read(filename))
        /// {
        ///     zip.AddEntry("Name1.txt", "this is the content");
        ///     zip.Save();  // YES!
        /// }
        /// </code>
        ///
        /// </example>
        ///
        /// <param name="outputStream">
        ///   The <c>System.IO.Stream</c> to write to. It must be
        ///   writable. If you created the ZipFile instanct by calling
        ///   ZipFile.Read(), this stream must not be the same stream
        ///   you passed to ZipFile.Read().
        /// </param>
        public void Save(Stream outputStream)
        {
            if (outputStream == null)
                throw new ArgumentNullException("outputStream");
            if (!outputStream.CanWrite)
                throw new ArgumentException("Must be a writable stream.", "outputStream");

            // if we had a filename to save to, we are now obliterating it.
            _name = null;

            _writeSegmentsManager = null;
            _writestream = new CountingStream(outputStream);

            _contentsChanged = true;
            _WriteStreamIsOurs = false;
            Save();
        }

        public void Save(ZipSegmentedStreamManager segmentsManager)
        {
            if (segmentsManager == null)
                throw new ArgumentNullException("segmentsManager");

            // if we had a filename to save to, we are now obliterating it.
            _name = null;

            _writeSegmentsManager = segmentsManager;
            _writestream = null;

            _contentsChanged = true;
            _WriteStreamIsOurs = false;
            Save();
        }

    }



    internal static class ZipOutput
    {
        public static bool WriteCentralDirectoryStructure(Stream s,
                                                          ICollection<ZipEntry> entries,
                                                          uint numSegments,
                                                          Zip64Option zip64,
                                                          String comment,
                                                          ZipContainer container)
        {
            var zss = s as ZipSegmentedStream;
            if (zss != null)
                zss.ContiguousWrite = true;

            // write to a memory stream in order to keep the
            // CDR contiguous
            Int64 aLength = 0;
            using (var ms = new MemoryStream())
            {
                foreach (ZipEntry e in entries)
                {
                    if (e.IncludedInMostRecentSave)
                    {
                        // this writes a ZipDirEntry corresponding to the ZipEntry
                        e.WriteCentralDirectoryEntry(ms);
                    }
                }
                var a = ms.ToArray();
                s.Write(a, 0, a.Length);
                aLength = a.Length;
            }


            // We need to keep track of the start and
            // Finish of the Central Directory Structure.

            // Cannot always use WriteStream.Length or Position; some streams do
            // not support these. (eg, ASP.NET Response.OutputStream) In those
            // cases we have a CountingStream.

            // Also, we cannot just set Start as s.Position bfore the write, and Finish
            // as s.Position after the write.  In a split zip, the write may actually
            // flip to the next segment.  In that case, Start will be zero.  But we
            // don't know that til after we know the size of the thing to write.  So the
            // answer is to compute the directory, then ask the ZipSegmentedStream which
            // segment that directory would fall in, it it were written.  Then, include
            // that data into the directory, and finally, write the directory to the
            // output stream.

            var output = s as CountingStream;
            long Finish = (output != null) ? output.ComputedPosition : s.Position;  // BytesWritten
            long Start = Finish - aLength;

            // need to know which segment the EOCD record starts in
            UInt32 startSegment = (zss != null)
                ? zss.CurrentSegment
                : 0;

            Int64 SizeOfCentralDirectory = Finish - Start;

            int countOfEntries = CountEntries(entries);

            bool needZip64CentralDirectory =
                zip64 == Zip64Option.Always ||
                countOfEntries >= 0xFFFF ||
                SizeOfCentralDirectory > 0xFFFFFFFF ||
                Start > 0xFFFFFFFF;

            byte[] a2 = null;

            // emit ZIP64 extensions as required
            if (needZip64CentralDirectory)
            {
                if (zip64 == Zip64Option.Never)
                {
                    throw new ZipException("The archive requires a ZIP64 Central Directory. Consider enabling ZIP64 extensions.");
                }

                var a = GenZip64EndOfCentralDirectory(Start, Finish, countOfEntries, numSegments);
                a2 = GenCentralDirectoryFooter(Start, Finish, zip64, countOfEntries, comment, container);
                if (startSegment != 0)
                {
                    UInt32 thisSegment = zss.ComputeSegment(a.Length + a2.Length);
                    int i = 16;
                    // number of this disk
                    Array.Copy(BitConverter.GetBytes(thisSegment), 0, a, i, 4);
                    i += 4;
                    // number of the disk with the start of the central directory
                    //Array.Copy(BitConverter.GetBytes(startSegment), 0, a, i, 4);
                    Array.Copy(BitConverter.GetBytes(thisSegment), 0, a, i, 4);

                    i = 60;
                    // offset 60
                    // number of the disk with the start of the zip64 eocd
                    Array.Copy(BitConverter.GetBytes(thisSegment), 0, a, i, 4);
                    i += 4;
                    i += 8;

                    // offset 72
                    // total number of disks
                    Array.Copy(BitConverter.GetBytes(thisSegment), 0, a, i, 4);
                }
                s.Write(a, 0, a.Length);
            }
            else
                a2 = GenCentralDirectoryFooter(Start, Finish, zip64, countOfEntries, comment, container);


            // now, the regular footer
            if (startSegment != 0)
            {
                // The assumption is the central directory is never split across
                // segment boundaries.

                UInt16 thisSegment = (UInt16) zss.ComputeSegment(a2.Length);
                int i = 4;
                // number of this disk
                Array.Copy(BitConverter.GetBytes(thisSegment), 0, a2, i, 2);
                i += 2;
                // number of the disk with the start of the central directory
                //Array.Copy(BitConverter.GetBytes((UInt16)startSegment), 0, a2, i, 2);
                Array.Copy(BitConverter.GetBytes(thisSegment), 0, a2, i, 2);
                i += 2;
            }

            s.Write(a2, 0, a2.Length);

            // reset the contiguous write property if necessary
            if (zss != null)
                zss.ContiguousWrite = false;

            return needZip64CentralDirectory;
        }


        private static System.Text.Encoding GetEncoding(ZipContainer container, string t)
        {
            switch (container.AlternateEncodingUsage)
            {
                case ZipOption.Always:
                    return container.AlternateEncoding;
                case ZipOption.Never:
                    return container.DefaultEncoding;
            }

            // AsNecessary is in force
            var e = container.DefaultEncoding;
            if (t == null) return e;

            var bytes = e.GetBytes(t);
            var t2 = e.GetString(bytes,0,bytes.Length);
            if (t2.Equals(t)) return e;
            return container.AlternateEncoding;
        }



        private static byte[] GenCentralDirectoryFooter(long StartOfCentralDirectory,
                                                        long EndOfCentralDirectory,
                                                        Zip64Option zip64,
                                                        int entryCount,
                                                        string comment,
                                                        ZipContainer container)
        {
            System.Text.Encoding encoding = GetEncoding(container, comment);
            int j = 0;
            int bufferLength = 22;
            byte[] block = null;
            Int16 commentLength = 0;
            if ((comment != null) && (comment.Length != 0))
            {
                block = encoding.GetBytes(comment);
                commentLength = (Int16)block.Length;
            }
            bufferLength += commentLength;
            byte[] bytes = new byte[bufferLength];

            int i = 0;
            // signature
            byte[] sig = BitConverter.GetBytes(ZipConstants.EndOfCentralDirectorySignature);
            Array.Copy(sig, 0, bytes, i, 4);
            i+=4;

            // number of this disk
            // (this number may change later)
            bytes[i++] = 0;
            bytes[i++] = 0;

            // number of the disk with the start of the central directory
            // (this number may change later)
            bytes[i++] = 0;
            bytes[i++] = 0;

            // handle ZIP64 extensions for the end-of-central-directory
            if (entryCount >= 0xFFFF || zip64 == Zip64Option.Always)
            {
                // the ZIP64 version.
                for (j = 0; j < 4; j++)
                    bytes[i++] = 0xFF;
            }
            else
            {
                // the standard version.
                // total number of entries in the central dir on this disk
                bytes[i++] = (byte)(entryCount & 0x00FF);
                bytes[i++] = (byte)((entryCount & 0xFF00) >> 8);

                // total number of entries in the central directory
                bytes[i++] = (byte)(entryCount & 0x00FF);
                bytes[i++] = (byte)((entryCount & 0xFF00) >> 8);
            }

            // size of the central directory
            Int64 SizeOfCentralDirectory = EndOfCentralDirectory - StartOfCentralDirectory;

            if (SizeOfCentralDirectory >= 0xFFFFFFFF || StartOfCentralDirectory >= 0xFFFFFFFF)
            {
                // The actual data is in the ZIP64 central directory structure
                for (j = 0; j < 8; j++)
                    bytes[i++] = 0xFF;
            }
            else
            {
                // size of the central directory (we just get the low 4 bytes)
                bytes[i++] = (byte)(SizeOfCentralDirectory & 0x000000FF);
                bytes[i++] = (byte)((SizeOfCentralDirectory & 0x0000FF00) >> 8);
                bytes[i++] = (byte)((SizeOfCentralDirectory & 0x00FF0000) >> 16);
                bytes[i++] = (byte)((SizeOfCentralDirectory & 0xFF000000) >> 24);

                // offset of the start of the central directory (we just get the low 4 bytes)
                bytes[i++] = (byte)(StartOfCentralDirectory & 0x000000FF);
                bytes[i++] = (byte)((StartOfCentralDirectory & 0x0000FF00) >> 8);
                bytes[i++] = (byte)((StartOfCentralDirectory & 0x00FF0000) >> 16);
                bytes[i++] = (byte)((StartOfCentralDirectory & 0xFF000000) >> 24);
            }


            // zip archive comment
            if ((comment == null) || (comment.Length == 0))
            {
                // no comment!
                bytes[i++] = (byte)0;
                bytes[i++] = (byte)0;
            }
            else
            {
                // the size of our buffer defines the max length of the comment we can write
                if (commentLength + i + 2 > bytes.Length) commentLength = (Int16)(bytes.Length - i - 2);
                bytes[i++] = (byte)(commentLength & 0x00FF);
                bytes[i++] = (byte)((commentLength & 0xFF00) >> 8);

                if (commentLength != 0)
                {
                    // now actually write the comment itself into the byte buffer
                    for (j = 0; (j < commentLength) && (i + j < bytes.Length); j++)
                    {
                        bytes[i + j] = block[j];
                    }
                    i += j;
                }
            }

            //   s.Write(bytes, 0, i);
            return bytes;
        }



        private static byte[] GenZip64EndOfCentralDirectory(long StartOfCentralDirectory,
                                                            long EndOfCentralDirectory,
                                                            int entryCount,
                                                            uint numSegments)
        {
            const int bufferLength = 12 + 44 + 20;

            byte[] bytes = new byte[bufferLength];

            int i = 0;
            // signature
            byte[] sig = BitConverter.GetBytes(ZipConstants.Zip64EndOfCentralDirectoryRecordSignature);
            Array.Copy(sig, 0, bytes, i, 4);
            i+=4;

            // There is a possibility to include "Extensible" data in the zip64
            // end-of-central-dir record.  I cannot figure out what it might be used to
            // store, so the size of this record is always fixed.  Maybe it is used for
            // strong encryption data?  That is for another day.
            long DataSize = 44;
            Array.Copy(BitConverter.GetBytes(DataSize), 0, bytes, i, 8);
            i += 8;

            // offset 12
            // VersionMadeBy = 45;
            bytes[i++] = 45;
            bytes[i++] = 0x00;

            // VersionNeededToExtract = 45;
            bytes[i++] = 45;
            bytes[i++] = 0x00;

            // offset 16
            // number of the disk, and the disk with the start of the central dir.
            // (this may change later)
            for (int j = 0; j < 8; j++)
                bytes[i++] = 0x00;

            // offset 24
            long numberOfEntries = entryCount;
            Array.Copy(BitConverter.GetBytes(numberOfEntries), 0, bytes, i, 8);
            i += 8;
            Array.Copy(BitConverter.GetBytes(numberOfEntries), 0, bytes, i, 8);
            i += 8;

            // offset 40
            Int64 SizeofCentraldirectory = EndOfCentralDirectory - StartOfCentralDirectory;
            Array.Copy(BitConverter.GetBytes(SizeofCentraldirectory), 0, bytes, i, 8);
            i += 8;
            Array.Copy(BitConverter.GetBytes(StartOfCentralDirectory), 0, bytes, i, 8);
            i += 8;

            // offset 56
            // now, the locator
            // signature
            sig = BitConverter.GetBytes(ZipConstants.Zip64EndOfCentralDirectoryLocatorSignature);
            Array.Copy(sig, 0, bytes, i, 4);
            i+=4;

            // offset 60
            // number of the disk with the start of the zip64 eocd
            // (this will change later)  (it will?)
            uint x2 = (numSegments==0)?0:(uint)(numSegments-1);
            Array.Copy(BitConverter.GetBytes(x2), 0, bytes, i, 4);
            i+=4;

            // offset 64
            // relative offset of the zip64 eocd
            Array.Copy(BitConverter.GetBytes(EndOfCentralDirectory), 0, bytes, i, 8);
            i += 8;

            // offset 72
            // total number of disks
            // (this will change later)
            Array.Copy(BitConverter.GetBytes(numSegments), 0, bytes, i, 4);
            i+=4;

            return bytes;
        }



        private static int CountEntries(ICollection<ZipEntry> _entries)
        {
            // Cannot just emit _entries.Count, because some of the entries
            // may have been skipped.
            int count = 0;
            foreach (var entry in _entries)
                if (entry.IncludedInMostRecentSave) count++;
            return count;
        }




    }
}

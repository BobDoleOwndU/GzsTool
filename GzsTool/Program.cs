﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using GzsTool.Common;
using GzsTool.Fpk;
using GzsTool.Gzs;
using GzsTool.Utility;

namespace GzsTool
{
    internal static class Program
    {
        private static readonly XmlSerializer ArchiveSerializer = new XmlSerializer(typeof (ArchiveFile),
            new[] {typeof (FpkFile), typeof (GzsFile)});

        private static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                ReadDictionaries();
                string path = args[0];
                if (File.Exists(path))
                {
                    if (path.EndsWith(".g0s", StringComparison.CurrentCultureIgnoreCase))
                    {
                        ReadGzsArchive(path);
                        return;
                    }
                    if (path.EndsWith(".fpk", StringComparison.CurrentCultureIgnoreCase) ||
                        path.EndsWith(".fpkd", StringComparison.CurrentCultureIgnoreCase))
                    {
                        ReadFpkArchive(path);
                        return;
                    }
                    if (path.EndsWith(".xml", StringComparison.CurrentCultureIgnoreCase))
                    {
                        WriteArchive(path);
                        return;
                    }
                }
                else if (Directory.Exists(path))
                {
                    ReadFpkArchives(path);
                    return;
                }
            }
            ShowUsageInfo();
        }

        private static void ReadDictionaries()
        {
            string executingAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            const string gzsDictionaryName = "gzs_dictionary.txt";
            const string fpkDictionaryName = "fpk_dictionary.txt";
            // TODO: Enable reading the ps3 file when there is actually a need for it.
            ////Hashing.ReadPs3PathIdFile(Path.Combine(executingAssemblyLocation, "pathid_list_ps3.bin"));
            try
            {
                Console.WriteLine("Reading {0}", gzsDictionaryName);
                Hashing.ReadDictionary(Path.Combine(executingAssemblyLocation, gzsDictionaryName));
            }
            catch (Exception e)
            {
                Console.WriteLine("Error reading {0}: {1}", gzsDictionaryName, e.Message);
            }
            try
            {
                Console.WriteLine("Reading {0}", fpkDictionaryName);
                Hashing.ReadMd5Dictionary(Path.Combine(executingAssemblyLocation, fpkDictionaryName));
            }
            catch (Exception e)
            {
                Console.WriteLine("Error reading {0}: {1}", fpkDictionaryName, e.Message);
            }
        }

        private static void ShowUsageInfo()
        {
            Console.WriteLine("GzsTool by Atvaark\n" +
                              "  A tool for unpacking and repacking g0s, fpk and fpkd files\n" +
                              "Usage:\n" +
                              "  GzsTool file_path|folder_path\n" +
                              "Examples:\n" +
                              "  GzsTool file_path.g0s      - Unpacks the g0s file\n" +
                              "  GzsTool file_path.fpk      - Unpacks the fpk file\n" +
                              "  GzsTool file_path.fpkd     - Unpacks the fpkd file\n" +
                              "  GzsTool folder_path        - Unpacks all fpk and fpkd files in the folder\n" +
                              "  GzsTool file_path.g0s.xml  - Repacks the g0s file\n" +
                              "  GzsTool file_path.fpk.xml  - Repacks the fpk file\n" +
                              "  GzsTool file_path.fpkd.xml - Repacks the fpkd file");
        }

        private static void ReadGzsArchive(string path)
        {
            string fileDirectory = Path.GetDirectoryName(path);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            string outputDirectory = Path.Combine(fileDirectory, fileNameWithoutExtension);
            string xmlOutputPath = Path.Combine(fileDirectory,
                string.Format("{0}.xml", Path.GetFileName(path)));

            using (FileStream input = new FileStream(path, FileMode.Open))
            using (FileStream xmlOutput = new FileStream(xmlOutputPath, FileMode.Create))
            {
                GzsFile gzsFile = GzsFile.ReadGzsFile(input);
                gzsFile.Name = Path.GetFileName(path);
                foreach (var exportedFile in gzsFile.ExportFiles(input))
                {
                    Console.WriteLine(exportedFile.FileName);
                    WriteExportedFile(exportedFile, outputDirectory);
                }
                ArchiveSerializer.Serialize(xmlOutput, gzsFile);
            }
        }

        private static void ReadFpkArchives(string path)
        {
            var extensions = new List<string>
            {
                ".fpk",
                ".fpkd"
            };
            var files = GetFilesWithExtension(new DirectoryInfo(path), extensions);
            foreach (var file in files)
            {
                ReadFpkArchive(file.FullName);
            }
        }

        private static void ReadFpkArchive(string path)
        {
            string fileDirectory = Path.GetDirectoryName(path);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path).Replace(".", "");
            string outputDirectory = string.Format("{0}\\{1}_{2}", fileDirectory, fileNameWithoutExtension, extension);
            string xmlOutputPath = Path.Combine(fileDirectory,
                string.Format("{0}.xml", Path.GetFileName(path)));

            using (FileStream input = new FileStream(path, FileMode.Open))
            using (FileStream xmlOutput = new FileStream(xmlOutputPath, FileMode.Create))
            {
                FpkFile fpkFile = FpkFile.ReadFpkFile(input);
                fpkFile.Name = Path.GetFileName(path);
                foreach (var exportedFile in fpkFile.ExportFiles())
                {
                    Console.WriteLine(exportedFile.FileName);
                    WriteExportedFile(exportedFile, outputDirectory);
                }
                ArchiveSerializer.Serialize(xmlOutput, fpkFile);
            }
        }

        private static void WriteExportedFile(FileDataContainer fileDataContainer, string outputDirectory)
        {
            string outputPath = Path.Combine(outputDirectory, fileDataContainer.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            using (FileStream output = new FileStream(outputPath, FileMode.Create))
            {
                output.Write(fileDataContainer.Data, 0, fileDataContainer.Data.Length);
            }
        }

        private static void WriteArchive(string path)
        {
            var directory = Path.GetDirectoryName(path);
            using (FileStream xmlInput = new FileStream(path, FileMode.Open))
            {
                object file = ArchiveSerializer.Deserialize(xmlInput);
                FpkFile fpkFile = file as FpkFile;
                if (fpkFile != null)
                {
                    WriteFpkArchive(fpkFile, directory);
                }
                GzsFile gzsFile = file as GzsFile;
                if (gzsFile != null)
                {
                    WriteGzsArchive(gzsFile, directory);
                }
            }
        }

        private static void WriteGzsArchive(GzsFile gzsFile, string directory)
        {
            string outputPath = Path.Combine(directory, gzsFile.Name + ".test");
            string inputDirectory = Path.Combine(directory, Path.GetFileNameWithoutExtension(gzsFile.Name));
            using (FileStream output = new FileStream(outputPath, FileMode.Create))
            {
                gzsFile.Write(output, inputDirectory);
            }
        }

        private static void WriteFpkArchive(FpkFile fpkFile, string directory)
        {
            string outputPath = Path.Combine(directory, fpkFile.Name + ".test");
            string inputDirectory = string.Format("{0}\\{1}_{2}", directory,
                Path.GetFileNameWithoutExtension(fpkFile.Name), Path.GetExtension(fpkFile.Name).Replace(".", ""));
            using (FileStream output = new FileStream(outputPath, FileMode.Create))
            {
                fpkFile.Write(output, inputDirectory);
            }
        }

        private static IEnumerable<FileInfo> GetFilesWithExtension(DirectoryInfo fileDirectory,
            ICollection<string> extensions)
        {
            foreach (var file in fileDirectory.GetFiles("*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file.FullName);
                if (extensions.Contains(extension, StringComparer.InvariantCultureIgnoreCase))
                    yield return file;
            }
        }
    }
}
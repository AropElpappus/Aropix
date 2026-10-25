// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PixelArt
{
    // Each invocation owns a fresh directory, never a personal project's location.
    static class TestArtifacts
    {
        static string directory;
        static readonly List<string> results = new List<string>();
        static int passed, failed;

        public static string DirectoryPath
        {
            get
            {
                if (directory == null)
                {
                    string root = Environment.GetEnvironmentVariable("AROPIX_TEST_OUTPUT_ROOT");
                    if (string.IsNullOrWhiteSpace(root))
                        root = Path.Combine(Path.GetTempPath(), "Aropix-tests");
                    directory = Path.Combine(Path.GetFullPath(root), Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(directory);
                }
                return directory;
            }
        }

        public static void Run(string name, Action test)
        {
            Stopwatch clock = Stopwatch.StartNew();
            try
            {
                test();
                passed++;
                results.Add("APROBADA | " + name + " | " + clock.ElapsedMilliseconds + " ms");
            }
            catch (Exception error)
            {
                failed++;
                results.Add("FALLIDA | " + name + "\n" + error);
                throw;
            }
            finally { WriteReport(); }
        }

        static void WriteReport()
        {
            File.WriteAllText(Path.Combine(DirectoryPath, "resultados.txt"),
                "Aropix: grupos aprobados=" + passed + ", fallidos=" + failed +
                "\nSe detiene ante el primer fallo; los grupos posteriores no se ejecutan.\n\n" +
                string.Join("\n", results.ToArray()));
        }

        public static int RunAll(string[] args)
        {
            try
            {
                Run("Suite integrada", Tests.Run);
                if (args.Length > 1)
                {
                    Run("Proyecto indicado (original de solo lectura)", delegate
                    {
                        using (Editor editor = new Editor(true)) editor.TestExistingProject(args[1]);
                    });
                }
                return 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(DirectoryPath, "error.txt"), error.ToString());
                return 1;
            }
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="IntegrationTests.cs" company="Ace Olszowka">
//  Copyright (c) Ace Olszowka 2020. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
namespace VisualStudioSolutionSorter.Tests
{
    using System;
    using System.Collections;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    using NUnit.Framework;

    /// <summary>
    /// Performs Integration Level Testing
    /// </summary>
    [TestFixture]
    public class IntegrationTests
    {
        string EXECUTABLE_PATH = FindExecutable();

        /// <summary>
        /// Internal Method to find the location of the Executable to Invoke in our Integration Tests
        /// </summary>
        /// <returns>The path to the executable to be passed to dotnet for testing.</returns>
        private static string FindExecutable()
        {
            string projectRoot = TestContext.CurrentContext.TestDirectory;

            // FRAGILE - What happens if this file gets renamed/changed?
            while (!Directory.EnumerateFiles(projectRoot, "VisualStudioSolutionSorter.sln").Any())
            {
                string parentDirectory = Directory.GetParent(projectRoot).FullName;
                projectRoot = parentDirectory;
            }

            // Now that we have the root find the Executable
            string[] allPossibleExecutables =
                Directory
                    .EnumerateFiles(projectRoot, "VisualStudioSolutionSorter.dll", SearchOption.AllDirectories)
                    .ToArray();

            string exectuablePath =
                allPossibleExecutables
                    .Where(possiblePath => possiblePath.Contains("bin"))
                    .FirstOrDefault();

            if (exectuablePath == null)
            {
                throw new InvalidOperationException("Could not find executable!");
            }

            return exectuablePath;
        }

        [TestCaseSource(typeof(Validate_Command_Tests))]
        public void Validate_Command(string targetFile, int expectedExitCode)
        {
            int actualExitCode = -1;
            string commandLineArgument = string.Empty;

            using (Process p = new Process())
            {
                p.StartInfo.FileName = "dotnet";
                p.StartInfo.Arguments = $"\"{EXECUTABLE_PATH}\" \"{targetFile}\" --validate";

                // For debugging purposes save the commandline argument
                commandLineArgument = $"{p.StartInfo.FileName} {p.StartInfo.Arguments}";

                p.Start();
                p.WaitForExit();
                actualExitCode = p.ExitCode;
            }

            Assert.That(actualExitCode, Is.EqualTo(expectedExitCode), $"Commandline: `{commandLineArgument}`");
        }

        [TestCaseSource(typeof(Execute_Command_SingleFile_Tests))]
        public void Execute_Command_SingleFile(string testFilePath, string expectedFileOutputPath, int expectedExitCode)
        {
            int actualExitCode = -1;
            string commandLineArgument = string.Empty;

            // Because this file will be modified we make a temporary copy
            // of the file being formatted and run the command on that
            string newTemporaryFilePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"{Guid.NewGuid().ToString("N")}.sln");
            if (File.Exists(newTemporaryFilePath))
            {
                File.Delete(newTemporaryFilePath);
            }
            File.Copy(testFilePath, newTemporaryFilePath);

            using (Process p = new Process())
            {
                p.StartInfo.FileName = "dotnet";
                p.StartInfo.Arguments = $"\"{EXECUTABLE_PATH}\" \"{newTemporaryFilePath}\"";

                // For debugging purposes save the commandline argument
                commandLineArgument = $"{p.StartInfo.FileName} {p.StartInfo.Arguments}";

                p.Start();
                p.WaitForExit();
                actualExitCode = p.ExitCode;
            }

            // Now Read botht he modified file and the expected file
            var actualFileOutput = File.ReadAllText(newTemporaryFilePath);
            var expectedFileOutput = File.ReadAllText(expectedFileOutputPath);

            Assert.That(actualExitCode, Is.EqualTo(expectedExitCode), $"Exit Code Unexpected. Commandline: `{commandLineArgument}`");
            Assert.That(actualFileOutput, Is.EqualTo(expectedFileOutput), $"Expected File Output Did Not Match. Commandline: `{commandLineArgument}`");
        }
    }

    internal class Execute_Command_SingleFile_Tests : IEnumerable
    {
        string TESTDATA_PATH = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");

        public IEnumerator GetEnumerator()
        {
            yield return new TestCaseData(Path.Combine(TESTDATA_PATH, "Sorted", "dotnet-format.sln"), Path.Combine(TESTDATA_PATH, "Sorted", "dotnet-format.sln"), 0);
            yield return new TestCaseData(Path.Combine(TESTDATA_PATH, "Sorted", "roslyn-Compilers.sln"), Path.Combine(TESTDATA_PATH, "Sorted", "roslyn-Compilers.sln"), 0);
            yield return new TestCaseData(Path.Combine(TESTDATA_PATH, "Unsorted", "dotnet-format.sln"), Path.Combine(TESTDATA_PATH, "Sorted", "dotnet-format.sln"), 0);
            yield return new TestCaseData(Path.Combine(TESTDATA_PATH, "Unsorted", "roslyn-Compilers.sln"), Path.Combine(TESTDATA_PATH, "Sorted", "roslyn-Compilers.sln"), 0);
        }
    }

    internal class Validate_Command_Tests : IEnumerable
    {
        string TESTDATA_PATH = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData");

        public IEnumerator GetEnumerator()
        {
            yield return new TestCaseData(Path.Combine(TESTDATA_PATH, "Sorted", "dotnet-format.sln"), 0);
            yield return new TestCaseData(Path.Combine(TESTDATA_PATH, "Sorted", "roslyn-Compilers.sln"), 0);
            yield return new TestCaseData(Path.Combine(TESTDATA_PATH, "Unsorted", "dotnet-format.sln"), 1);
            yield return new TestCaseData(Path.Combine(TESTDATA_PATH, "Unsorted", "roslyn-Compilers.sln"), 1);
        }
    }
}

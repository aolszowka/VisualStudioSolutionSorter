// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Ace Olszowka">
//  Copyright (c) Ace Olszowka 2019-2020. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
namespace VisualStudioSolutionSorter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using VisualStudioSolutionSorter.Properties;

    class Program
    {
        static void Main(string[] args)
        {
            // Always Error Unless Successful
            int errorCode = -808;

            if (args.Any())
            {
                string command = args.First().ToLowerInvariant();

                if (command.Equals("-?") || command.Equals("/?") || command.Equals("-help") || command.Equals("/help"))
                {
                    errorCode = ShowUsage();
                }
                else if (command.Equals("validate"))
                {
                    if (args.Length < 2)
                    {
                        string error = "You must provide either a file or directory as a second argument to use validate";
                        Console.WriteLine(error);
                        errorCode = 1;
                    }
                    else
                    {
                        string targetArgument = args[1];

                        if (Directory.Exists(targetArgument))
                        {
                            string[] ignoredSolutionPatterns = new string[0];

                            if (args.Length == 2)
                            {
                                string validatingAllSolutions = $"Validating all solutions in `{targetArgument}`";
                                Console.WriteLine(validatingAllSolutions);
                            }
                            else
                            {
                                string ignoredSolutionsArgument = args[2];
                                string validatingAllSolutions = $"Validating all solutions in `{targetArgument}` except those filtered by `{ignoredSolutionsArgument}`";
                                Console.WriteLine(validatingAllSolutions);

                                // Because we're going to constantly use this for lookups save it off
                                ignoredSolutionPatterns = _GetIgnoredSolutionPatterns(ignoredSolutionsArgument).ToArray();

                                Console.WriteLine($"These are the ignored patterns (From: {ignoredSolutionsArgument})");
                                foreach (string ignoredSolutionPattern in ignoredSolutionPatterns)
                                {
                                    Console.WriteLine("{0}", ignoredSolutionPattern);
                                }
                            }

                            errorCode = SortSolutionDirectory(targetArgument, ignoredSolutionPatterns, false);
                        }
                        else if (File.Exists(targetArgument))
                        {
                            string validatingSingleFile = $"Validating solution `{targetArgument}`";
                            Console.WriteLine(validatingSingleFile);
                            errorCode = SortSolution(targetArgument, false);
                        }
                        else
                        {
                            string error = $"The provided path `{targetArgument}` is not a folder or file.";
                            errorCode = 9009;
                        }
                    }
                }
                else
                {
                    string targetPath = command;

                    if (Directory.Exists(targetPath))
                    {
                        IEnumerable<string> ignoredSolutionPatterns = new string[0];

                        if (args.Length == 1)
                        {
                            string sortingAllSolutionsInDirectory = $"Sorting all Visual Studio Solutions (*.sln) in `{targetPath}`";
                            Console.WriteLine(sortingAllSolutionsInDirectory);
                        }
                        else
                        {
                            string ignoredSolutionsArgument = args[1];
                            string sortingAllSolutionsInDirectory = $"Sorting all solutions in `{targetPath}` except those filtered by `{ignoredSolutionsArgument}`";
                            Console.WriteLine(sortingAllSolutionsInDirectory);

                            // Because we're going to constantly use this for lookups save it off
                            ignoredSolutionPatterns = _GetIgnoredSolutionPatterns(ignoredSolutionsArgument).ToArray();

                            Console.WriteLine($"These are the ignored patterns (From: {ignoredSolutionsArgument})");
                            foreach (string ignoredSolutionPattern in ignoredSolutionPatterns)
                            {
                                Console.WriteLine("{0}", ignoredSolutionPattern);
                            }
                        }

                        SortSolutionDirectory(targetPath, ignoredSolutionPatterns, true);

                        // We don't care what happened here; we always return 0
                        // because we assume that the version control system
                        // will indicate changed files.
                        errorCode = 0;

                    }
                    else if (File.Exists(targetPath))
                    {
                        string updatingSingleFile = $"Sorting solution `{targetPath}`";
                        Console.WriteLine(updatingSingleFile);
                        SortSolution(targetPath, true);

                        // We don't care what happened here; we always return 0
                        // because we assume that the version control system
                        // will indicate changed files.
                        errorCode = 0;
                    }
                    else
                    {
                        string error = $"The specified path `{targetPath}` is not valid.";
                        Console.WriteLine(error);
                        errorCode = 1;
                    }
                }
            }
            else
            {
                // This was a bad command
                errorCode = ShowUsage();
            }

            Environment.Exit(errorCode);

        }

        /// <summary>
        /// Load the Solution Ignore Patterns from the given Text File.
        /// </summary>
        /// <param name="targetIgnoreFile">The Text File that contains the ignore patterns.</param>
        /// <returns>An IEnumerable of strings that contain the patterns for solutions to ignore.</returns>
        private static IEnumerable<string> _GetIgnoredSolutionPatterns(string targetIgnoreFile)
        {
            if (!File.Exists(targetIgnoreFile))
            {
                string exceptionMessage = $"The specified ignore pattern file at `{targetIgnoreFile}` did not exist or was not accessible.";
                throw new InvalidOperationException(exceptionMessage);
            }

            IEnumerable<string> ignoredPatterns =
                File
                .ReadLines(targetIgnoreFile)
                .Where(currentLine => !currentLine.StartsWith("#"));

            return ignoredPatterns;
        }

        /// <summary>
        /// Given a Solution File and a list of Patterns determine if the solution matches any of the patterns.
        /// </summary>
        /// <param name="targetSolution">The solution to evaluate.</param>
        /// <param name="ignoredSolutionPatterns">The RegEx of patterns to ignore.</param>
        /// <returns><c>true</c> if the solution should be processed; otherwise, <c>false</c>.</returns>
        private static bool _ShouldProcessSolution(string targetSolution, IEnumerable<string> ignoredSolutionPatterns)
        {
            bool shouldProcessSolution = true;

            bool isSolutionIgnored =
                ignoredSolutionPatterns
                .Any(ignoredPatterns => Regex.IsMatch(targetSolution, ignoredPatterns));

            if (isSolutionIgnored)
            {
                shouldProcessSolution = false;
            }

            return shouldProcessSolution;
        }

        /// <summary>
        /// Prints the Usage of this Utility to the Console.
        /// </summary>
        /// <returns>An Exit Code Indicating that Help was Shown</returns>
        private static int ShowUsage()
        {
            Console.WriteLine(Resources.HelpMessage);
            return 21;
        }

        private static int SortSolutionDirectory(string targetDirectory, IEnumerable<string> ignoredSolutionPatterns, bool saveChanges)
        {
            IEnumerable<string> filteredSolutions =
                Directory
                .EnumerateFiles(targetDirectory, "*.sln", SearchOption.AllDirectories)
                .Where(targetSolution => _ShouldProcessSolution(targetSolution, ignoredSolutionPatterns));

            int solutionsModified = 0;

            Parallel.ForEach(filteredSolutions, targetSolution =>
            {
                try
                {
                    bool projectHadToBeSorted = SolutionSorter.ProcessSingleProject(targetSolution, saveChanges);

                    if (projectHadToBeSorted)
                    {
                        Console.WriteLine($"Had to Sort: `{targetSolution}`");
                        solutionsModified++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed To Process Solution: `{targetSolution}`");
                    Console.Error.WriteLine(ex.ToString());
                }
            }
            );

            return solutionsModified;
        }

        private static int SortSolution(string targetSolution, bool saveChanges)
        {
            int modifiedStatus = 0;

            if (SolutionSorter.ProcessSingleProject(targetSolution, saveChanges))
            {
                modifiedStatus++;
            }

            return modifiedStatus;
        }
    }
}

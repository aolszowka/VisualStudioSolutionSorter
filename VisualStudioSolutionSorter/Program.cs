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

    using NDesk.Options;

    using VisualStudioSolutionSorter.Properties;

    public class Program
    {
        static void Main(string[] args)
        {
            // Always Error Unless Successful
            string targetArgument = string.Empty;
            string ignoreFileArgument = string.Empty;
            bool validateOnly = false;
            bool showHelp = false;

            OptionSet p = new OptionSet()
            {
                { "<>", Strings.TargetArgumentDescription, v => targetArgument = v },
                { "validate", Strings.ValidateDescription, v => validateOnly = v != null },
                { "ignore=", Strings.IgnoreDescription, v => ignoreFileArgument = v },
                { "?|h|help", Strings.HelpDescription, v => showHelp = v != null },
            };

            try
            {
                p.Parse(args);
            }
            catch (OptionException)
            {
                Console.WriteLine(Strings.ShortUsageMessage);
                Console.WriteLine($"Try `{Strings.ProgramName} --help` for more information.");
                Environment.ExitCode = 21;
                return;
            }

            if (showHelp || string.IsNullOrEmpty(targetArgument))
            {
                Environment.ExitCode = ShowUsage(p);
            }
            else if (!Directory.Exists(targetArgument) && !File.Exists(targetArgument))
            {
                Console.WriteLine(Strings.InvalidTargetArgument, targetArgument);
                Environment.ExitCode = 9009;
            }
            else if (!string.IsNullOrEmpty(ignoreFileArgument) && !File.Exists(ignoreFileArgument))
            {
                Console.WriteLine(Strings.InvalidIgnoreFileArgument, ignoreFileArgument);
                Environment.ExitCode = 9009;
            }
            else
            {
                bool saveChanges = validateOnly == false;

                // First see if we have an ignore file
                string[] ignoredSolutionPatterns = new string[0];

                if (!string.IsNullOrEmpty(ignoreFileArgument))
                {
                    // Because we're going to constantly use this for lookups save it off
                    ignoredSolutionPatterns = _GetIgnoredSolutionPatterns(ignoreFileArgument).ToArray();
                }

                if (Directory.Exists(targetArgument))
                {
                    if (ignoredSolutionPatterns.Any())
                    {
                        string message = $"{(validateOnly ? "Validating" : "Sorting")} all Visual Studio Solutions (*.sln) in `{targetArgument}` except those filtered by `{ignoreFileArgument}`";
                        Console.WriteLine(message);

                        Console.WriteLine($"These are the ignored patterns (From: {ignoreFileArgument})");
                        foreach (string ignoredSolutionPattern in ignoredSolutionPatterns)
                        {
                            Console.WriteLine("{0}", ignoredSolutionPattern);
                        }
                    }
                    else
                    {
                        string message = $"{(validateOnly ? "Validating" : "Sorting")} all Visual Studio Solutions (*.sln) in `{targetArgument}`";
                        Console.WriteLine(message);
                    }

                    Environment.ExitCode = SortSolutionDirectory(targetArgument, ignoredSolutionPatterns, saveChanges);
                }
                else if (File.Exists(targetArgument))
                {
                    string message = $"{(validateOnly ? "Validating" : "Sorting")} solution `{targetArgument}`";
                    Console.WriteLine(message);
                    Environment.ExitCode = SortSolution(targetArgument, saveChanges);
                }
                else
                {
                    // It should not be possible to reach this point
                    throw new InvalidOperationException($"The provided path `{targetArgument}` is not a folder or file.");
                }

                // If in Fix Mode We don't care what happend; we always
                // return 0 because we assume that the version control
                // system will indicate changed files.
                if (saveChanges)
                {
                    Environment.ExitCode = 0;
                }
            }
        }

        /// <summary>
        /// Prints the Usage of this Utility to the Console.
        /// </summary>
        /// <param name="p">The <see cref="OptionSet"/> for this program.</param>
        /// <returns>An Exit Code Indicating that Help was Shown</returns>
        private static int ShowUsage(OptionSet p)
        {
            Console.WriteLine(Strings.ShortUsageMessage);
            Console.WriteLine();
            Console.WriteLine(Strings.LongDescription);
            Console.WriteLine();
            Console.WriteLine($"               <>            {Strings.TargetArgumentDescription}");
            p.WriteOptionDescriptions(Console.Out);
            return 21;
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

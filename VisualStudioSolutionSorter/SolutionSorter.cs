// -----------------------------------------------------------------------
// <copyright file="SolutionSorter.cs" company="Ace Olszowka">
//  Copyright (c) Ace Olszowka 2019. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace VisualStudioSolutionSorter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    class SolutionSorter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetSolution"></param>
        /// <param name="saveChanges"></param>
        /// <returns><c>true</c> if changes were made to this project; otherwise, returns <c>false</c>.</returns>
        internal static bool ProcessSingleProject(string targetSolution, bool saveChanges)
        {
            bool changesMade = false;

            // For now because these solution files are "small enough" write
            // the contents to memory. A longer term solution might be to
            // write the contents to a temporary file and then replace once
            // the sort has completed.
            string[] sortedLines = SortSolution(targetSolution).ToArray();

            // Compare this to the existing file; if they are not equal
            // then a change was made.
            IEnumerable<string> existingFileLines = File.ReadLines(targetSolution);
            changesMade = !sortedLines.SequenceEqual(existingFileLines);

            if (saveChanges && changesMade)
            {
                File.WriteAllLines(targetSolution, sortedLines, Encoding.UTF8);
            }

            return changesMade;
        }

        internal static IEnumerable<string> SortSolution(string solutionFile)
        {
            IEnumerable<string> solutionLines = File.ReadLines(solutionFile);

            IEnumerator<string> solutionLineEnumerator = solutionLines.GetEnumerator();

            while (solutionLineEnumerator.MoveNext())
            {
                if (solutionLineEnumerator.Current.StartsWith("Project"))
                {
                    // We're in the "ProjectSection" we need to gather each project specification into an array and sort it
                    SortedDictionary<string, IEnumerable<string>> projectSectionsInSolution = new SortedDictionary<string, IEnumerable<string>>();

                    do
                    {
                        List<string> currentProjectLines = new List<string>();

                        do
                        {
                            // Extract a Single Project
                            currentProjectLines.Add(solutionLineEnumerator.Current);
                            solutionLineEnumerator.MoveNext();
                        } while (!solutionLineEnumerator.Current.StartsWith("Project") && !solutionLineEnumerator.Current.StartsWith("Global"));

                        // This will alphabetize the solutions by their first
                        // line, which usually means by project name.
                        projectSectionsInSolution.Add(currentProjectLines.First(), currentProjectLines);

                    } while (!solutionLineEnumerator.Current.StartsWith("Global"));

                    // Now that they're sorted flush them out to the buffer
                    foreach (KeyValuePair<string, IEnumerable<string>> projectSection in projectSectionsInSolution)
                    {
                        foreach (string projectLine in projectSection.Value)
                        {
                            yield return projectLine;
                        }
                    }

                    yield return solutionLineEnumerator.Current;
                }
                else if (solutionLineEnumerator.Current.Trim().StartsWith("GlobalSection(ProjectConfigurationPlatforms) = postSolution"))
                {
                    // Give back the ProjectConfiguration Line
                    yield return solutionLineEnumerator.Current;
                    solutionLineEnumerator.MoveNext();

                    // Now process the section
                    SortedSet<string> sortedConfigurations = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
                    do
                    {
                        sortedConfigurations.Add(solutionLineEnumerator.Current);
                        solutionLineEnumerator.MoveNext();
                    } while (!solutionLineEnumerator.Current.Trim().StartsWith("EndGlobalSection"));

                    // yield back the sorted values
                    foreach (string sortedConfiguration in sortedConfigurations)
                    {
                        yield return sortedConfiguration;
                    }

                    // Give the EndGlobalSection back
                    yield return solutionLineEnumerator.Current;
                }
                else if (solutionLineEnumerator.Current.Trim().StartsWith("GlobalSection(NestedProjects) = preSolution"))
                {
                    // Give back the NestedProjects Line
                    yield return solutionLineEnumerator.Current;
                    solutionLineEnumerator.MoveNext();

                    // Now process the section
                    SortedSet<string> sortedNestedProjects = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
                    do
                    {
                        sortedNestedProjects.Add(solutionLineEnumerator.Current);
                        solutionLineEnumerator.MoveNext();
                    } while (!solutionLineEnumerator.Current.Trim().StartsWith("EndGlobalSection"));

                    // yield back the sorted values
                    foreach (string sortedNestedProject in sortedNestedProjects)
                    {
                        yield return sortedNestedProject;
                    }

                    // Give the EndGlobalSection back
                    yield return solutionLineEnumerator.Current;
                }
                else
                {
                    yield return solutionLineEnumerator.Current;
                }
            }

        }
    }
}

# VisualStudioSolutionSorter
Utility to deterministically sort Visual Studio Solution Files.

## When To Use This Tool
This tool should be used after any addition to the solution file.

This is because Visual Studio does not attempt to insert into the Solution File in any particular order; rather it inserts at the bottom of each section. This is valid from Visual Studio's perspective because the ordering of these projects is not important.

However from a Version Control System (VCS) perspective this is important, especially as you maintain multiple branches as these ordering issues can cause merge conflicts.

This tool attempts to avoid these conflicts by applying a deterministic sort to these files as described below.

## Operation
This tool will:

* (If Given a Single Solution) Perform the below operation for just this Solution
* (If Given a Directory) Will scan the given directory and all subdirectories for Visual Studio Solution files (*.sln) and perform the operation below.

For Each Solution File

* Sort the "Project" Section alphabetically by its first line
* Sort the "Configuration" Section alphabetically
* Sort the "NestedProjects" Section alphabetically

To understand what each of these means you need to understand a little about the [Microsoft Docs: Solution (.sln) file](https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file?view=vs-2019) format. This is explained in detail below.

In addition this tool has the ability to run in a `validate` mode which will run the logic but not make changes, the return code is the number of solutions that WOULD HAVE been modified by the tooling in addition to printing them out to the console.

When this tool is ran in `modify` mode (the default setting) the projects are modified and the return code is ALWAYS 0, the solutions that are modified are output to the console.

## Visual Studio Solution Format
While not explicitly documented a careful reading of the above documentation shows how the solution file is split into various sections. The following is my interpretation of the file format based on what is documented and observed behavior of Visual Studio 2017:

### Project Section
The first section (after the header) is referred to in this document as the "Project" section and generally looks like this:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ProjectName", "Relative\Path\ViaSolution\ToProject.csproj", "{B8C26B83-12CE-487A-85DD-E0AF792E9583}"
EndProject
```

There is a separate entry for each project and the GUIDs have distinct meanings:

* The first token is a Guid (in this example `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`) -In this case the above GUID indicates that this is a C# Project Type. There are several well known Project Types (for example Solution Folders are `{2150E333-8FDC-42A3-9474-1A3956D46DE8}`)
* The next "token" is the name of the project, by default this is the file name of the linked project, but this is not a requirement. It is believed that when a "rename" is performed within Visual Studio only this token is changed (not the name of the Project File)
* The next token after this is the relative path (via the location of the solution file) to the project to include. For Solution Folders this is just the name of the folder repeated
* The Last token (in this example `{B8C26B83-12CE-487A-85DD-E0AF792E9583}`) is the GUID assigned to this project, it is used in several places further along to serve as a cross reference to the project in question.

In addition while not documented it is possible to have entries nested under the `Project` type as in the following example:

```
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Folder", "Folder", "{2BAE1264-9CD3-4706-BBB7-6CF4F0AA59F4}"
	ProjectSection(SolutionItems) = preProject
		Relative\Path\README.md = Relative\Path\README.md
	EndProjectSection
EndProject
```

The exact rules are not documented, but in testing this appears to happen when you add "solution items" to a specific folder.

For the purposes of this tooling it assumes that when it sees a line that starts with `Project` that all lines until it sees an `EndProject` should be captured to be sorted in some deterministic manner. As of the writing of this documentation this is done with a simple alphabetical sort on the first line.

This has the side affect of ordering these projects:

1. First by their project type. So in the example above the Solution Folders are grouped together then the CSPROJ Files.
2. Then by the name that is displayed within Visual Studio
3. Then by their relative path via the Solution File
4. Finally by their Project Guid

### Configuration Section
The next section that is of interest to this tool is the Configuration Section which looks similar to this:

```
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{B8C26B83-12CE-487A-85DD-E0AF792E9583}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{B8C26B83-12CE-487A-85DD-E0AF792E9583}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{B8C26B83-12CE-487A-85DD-E0AF792E9583}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{B8C26B83-12CE-487A-85DD-E0AF792E9583}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
```

This section indicates to Visual Studio the various configurations supported by the project. The projects are identified by their Project Guids which are cross referenced above. This is what allows Visual Studio to exclude some projects from being build for certain Solution Configurations. This is probably the most complex section of the Solution file because every combination of `Configuration` and `Platform` must exist for every Project type. In addition it appears that each one of these combinations must exist twice.

Visual Studio does appear to helpfully add new Configurations in the same "section" when they are created in Visual Studio. For example if you were to create a new Platform `x64` and you had multiple projects the new `x64` entries would be appended after each existing project configuration section.

This has been shown internally to be the most difficult portion to manually resolve in a merge for Developers. Thankfully Visual Studio WILL REGENERATE this ENTIRE section if it is deleted. HOWEVER you will LOSE any configuration information by doing so.

For the purposes of this tool a simple alphabetical sort is applied to this entire GlobalSection. Because the first part of this line is the Project Guid, and because the ProjectGuid does not have any meaning towards the name of the project the ordering of this section has no correlation with the ordering of the project section.

### NestedProject Section
The last section that is of interest to this tool is the NestedProject section which looks similar to this:

```
	GlobalSection(NestedProjects) = preSolution
		{B8C26B83-12CE-487A-85DD-E0AF792E9583} = {2BAE1264-9CD3-4706-BBB7-6CF4F0AA59F4}\
	EndGlobalSection
```

As of the writing of this document this is not documented on Microsoft Site (See https://github.com/MicrosoftDocs/visualstudio-docs/issues/4202 for the request for documentation).

Testing has shown that this is a KeyValuePair in the form {GUID_OF_PROJECT} = {GUID_OF_SOLUTION_FOLDER} which instructs Visual Studio on how to properly "nest" projects.

In the above example the Project (`{B8C26B83-12CE-487A-85DD-E0AF792E9583}`) from the examples above is being nested into a Solution Folder ().

For the purposes of this tool a simple alphabetical sort is applied to this section as well. This has the side affect that projects are not grouped by their folder, but rather by their Project Guid. This might be an ideal place for hacking/changes to this tool.

You need to be careful of invalid `NestedProject` Entries which may have been previously added; due to this bug https://github.com/microsoft/msbuild/issues/4835 in MSBuild running this tool may result in uncovering these bad solutions.

## Usage
```text
Usage: VisualStudioSolutionSorter [validate] directory/solution [ignore.txt]

Given either a Visual Studio Solution (*.sln) or a Directory to Scan; sort the
solution files in a deterministic way.

Invalid Command/Arguments. Valid commands are:

Directory-Solution [IgnorePatterns.txt]
    [MODIFIES] If given a solution file or a directory find all solution files.
    Then opening each solution, grab all projects contained within the solution,
    modifying the target solution to be sorted in a deterministic manner.

validate Directory-Solution [IgnorePatterns.txt]
    [READS] Performs the above operation but instead the return code represents
    the number of solution files that would be sorted by this tool.

In all cases you can provide an optional argument of IgnorePatterns.txt (you can
use any filename) which should be a plain text file of regular expression
filters of solution files you DO NOT want this tool to operate on.

The sorting is pseudo alphabetical; the details of which are documented in the
source.
```

## Hacking
### Changing the Sort Order
The way this tool is currently implemented is by reading in a forward only manner the Solution File format and then creating a `SortedDictionary` / `SortedSet` to order the individual sections once they are encountered.

This also means that the tool is very dependent upon the Solution format being consistent. Because the Solution File format is not formally standardized this is a little like playing with fire. That being said it has not burned us yet.

Look at `SolutionSorter.SortSolution(string)` for the sorted dictionaries as a starting point for changing the sorting logic. Take a moment to read the above operations for better ideas on how to change the sort.

### Replacement
Long term if this idea is accepted: https://github.com/dotnet/cli/issues/12858 this project would cease to be useful.

## Contributing
Pull requests and bug reports are welcomed so long as they are MIT Licensed.

## License
This tool is MIT Licensed.

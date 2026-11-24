using System.Xml;
using System.Xml.Linq;
using FatCat.Toolkit;

namespace FatCat.CodeWorker.Cli.Commands.Verify.Engines.CSharp;

public interface IFindProjectBuild
{
	Task<ProjectBuildResult> Find(string sourceFilePath);
}

public class FindProjectBuild(IFileSystemTools fileSystemTools) : IFindProjectBuild
{
	private const string ProjectExtension = ".csproj";

	public async Task<ProjectBuildResult> Find(string sourceFilePath)
	{
		var folder = Path.GetDirectoryName(Path.GetFullPath(sourceFilePath));

		while (!string.IsNullOrEmpty(folder))
		{
			var projectFiles = ProjectFilesIn(folder);

			if (projectFiles.Count > 1)
			{
				return MultipleProjects(folder);
			}

			if (projectFiles.Count == 1)
			{
				return await ReadProject(projectFiles[0]);
			}

			folder = Path.GetDirectoryName(folder);
		}

		return ProjectNotFound(sourceFilePath);
	}

	private List<string> ProjectFilesIn(string folder)
	{
		return fileSystemTools
			.GetFiles(folder)
			.Where(file => string.Equals(Path.GetExtension(file), ProjectExtension, StringComparison.OrdinalIgnoreCase))
			.ToList();
	}

	private async Task<ProjectBuildResult> ReadProject(string projectFilePath)
	{
		XDocument project;

		try
		{
			project = XDocument.Parse(await fileSystemTools.ReadAllText(projectFilePath));
		}
		catch (XmlException exception)
		{
			return Failure(
				EngineError.ProjectFileUnreadable,
				$"The project file could not be read as XML: {projectFilePath} ({exception.Message})"
			);
		}

		return ReadProperties(projectFilePath, project);
	}

	private ProjectBuildResult ReadProperties(string projectFilePath, XDocument project)
	{
		var targetFramework = FirstPropertyValue(project, "TargetFramework");

		if (string.IsNullOrEmpty(targetFramework))
		{
			return Failure(
				EngineError.TargetFrameworkNotFound,
				$"No <TargetFramework> was found in the project file {projectFilePath}"
			);
		}

		var build = new ProjectBuild
		{
			ProjectFilePath = projectFilePath,
			ProjectDirectory = Path.GetDirectoryName(projectFilePath),
			AssemblyName = AssemblyNameOf(projectFilePath, project),
			TargetFramework = targetFramework,
		};

		return ChooseConfiguration(build);
	}

	private static string AssemblyNameOf(string projectFilePath, XDocument project)
	{
		var assemblyName = FirstPropertyValue(project, "AssemblyName");

		return string.IsNullOrEmpty(assemblyName) ? Path.GetFileNameWithoutExtension(projectFilePath) : assemblyName;
	}

	private static string FirstPropertyValue(XDocument project, string propertyName)
	{
		var element = project.Descendants().FirstOrDefault(descendant => descendant.Name.LocalName == propertyName);

		return element?.Value.Trim();
	}

	private ProjectBuildResult ChooseConfiguration(ProjectBuild build)
	{
		var configurationDirectory = fileSystemTools
			.GetDirectories(Path.Combine(build.ProjectDirectory, "bin"))
			.Where(directory => fileSystemTools.FileExists(AssemblyPath(directory, build)))
			.OrderByDescending(directory => fileSystemTools.GetFileMetaData(AssemblyPath(directory, build)).LastWriteTimeUtc)
			.FirstOrDefault();

		if (configurationDirectory is null)
		{
			return BuildOutputNotFound(build);
		}

		return Found(build, configurationDirectory);
	}

	private static string AssemblyPath(string configurationDirectory, ProjectBuild build)
	{
		return Path.Combine(configurationDirectory, build.TargetFramework, $"{build.AssemblyName}.dll");
	}

	private static ProjectBuildResult Found(ProjectBuild build, string configurationDirectory)
	{
		var configurationName = Path.GetFileName(configurationDirectory);

		build.OutputDirectory = Path.Combine(configurationDirectory, build.TargetFramework);
		build.IntermediateDirectory = Path.Combine(build.ProjectDirectory, "obj", configurationName, build.TargetFramework);

		return new ProjectBuildResult { Error = EngineError.None, Build = build };
	}

	private static ProjectBuildResult ProjectNotFound(string sourceFilePath)
	{
		return Failure(EngineError.ProjectNotFound, $"No project file ({ProjectExtension}) was found for {sourceFilePath}");
	}

	private static ProjectBuildResult MultipleProjects(string folder)
	{
		return Failure(
			EngineError.MultipleProjectsFound,
			$"More than one project file ({ProjectExtension}) was found in {folder}"
		);
	}

	private static ProjectBuildResult BuildOutputNotFound(ProjectBuild build)
	{
		return Failure(
			EngineError.BuildOutputNotFound,
			$"No build output was found for {build.ProjectFilePath}: expected bin/<configuration>/{build.TargetFramework}/{build.AssemblyName}.dll (build the project first)"
		);
	}

	private static ProjectBuildResult Failure(EngineError error, string message)
	{
		return new ProjectBuildResult { Error = error, Message = message };
	}
}

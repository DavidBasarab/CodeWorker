using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Settings;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class BuildTaskFoldersTests
{
	private readonly BuildTaskFolders buildTaskFolders;
	private readonly TaskSettings tasks;

	public BuildTaskFoldersTests()
	{
		buildTaskFolders = new BuildTaskFolders();

		tasks = new TaskSettings
		{
			TodoFolder = "tasks/todo",
			PendingFolder = "tasks/pending",
			DoneFolder = "tasks/done",
			BlockedFolder = "tasks/blocked",
			FailedFolder = "tasks/failed",
			ReferenceFolder = "tasks/reference",
		};
	}

	[Fact]
	public void BuildTodoFolderFromRepositoryPath()
	{
		var folders = buildTaskFolders.Build(@"C:\Projects\my-api", tasks);

		folders.Todo.Should().Be(Path.Combine(@"C:\Projects\my-api", "tasks/todo"));
	}

	[Fact]
	public void BuildPendingFolderFromRepositoryPath()
	{
		var folders = buildTaskFolders.Build(@"C:\Projects\my-api", tasks);

		folders.Pending.Should().Be(Path.Combine(@"C:\Projects\my-api", "tasks/pending"));
	}

	[Fact]
	public void BuildDoneFolderFromRepositoryPath()
	{
		var folders = buildTaskFolders.Build(@"C:\Projects\my-api", tasks);

		folders.Done.Should().Be(Path.Combine(@"C:\Projects\my-api", "tasks/done"));
	}

	[Fact]
	public void BuildBlockedFolderFromRepositoryPath()
	{
		var folders = buildTaskFolders.Build(@"C:\Projects\my-api", tasks);

		folders.Blocked.Should().Be(Path.Combine(@"C:\Projects\my-api", "tasks/blocked"));
	}

	[Fact]
	public void BuildFailedFolderFromRepositoryPath()
	{
		var folders = buildTaskFolders.Build(@"C:\Projects\my-api", tasks);

		folders.Failed.Should().Be(Path.Combine(@"C:\Projects\my-api", "tasks/failed"));
	}

	[Fact]
	public void BuildReferenceFolderFromRepositoryPath()
	{
		var folders = buildTaskFolders.Build(@"C:\Projects\my-api", tasks);

		folders.Reference.Should().Be(Path.Combine(@"C:\Projects\my-api", "tasks/reference"));
	}
}

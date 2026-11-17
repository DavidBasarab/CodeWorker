using FatCat.CodeWorker.Commands;

namespace Testing.FatCat.CodeWorker.Commands;

public class RequiredTaskFoldersTests
{
	[Fact]
	public void ContainTodo()
	{
		RequiredTaskFolders.All.AsEnumerable().Should().Contain("todo");
	}

	[Fact]
	public void ContainPending()
	{
		RequiredTaskFolders.All.AsEnumerable().Should().Contain("pending");
	}

	[Fact]
	public void ContainDone()
	{
		RequiredTaskFolders.All.AsEnumerable().Should().Contain("done");
	}

	[Fact]
	public void ContainBlocked()
	{
		RequiredTaskFolders.All.AsEnumerable().Should().Contain("blocked");
	}

	[Fact]
	public void ContainFailed()
	{
		RequiredTaskFolders.All.AsEnumerable().Should().Contain("failed");
	}

	[Fact]
	public void ContainReference()
	{
		RequiredTaskFolders.All.AsEnumerable().Should().Contain("reference");
	}

	[Fact]
	public void ContainLogs()
	{
		RequiredTaskFolders.All.AsEnumerable().Should().Contain("logs");
	}

	[Fact]
	public void ContainExactlySevenFolders()
	{
		RequiredTaskFolders.All.AsEnumerable().Should().HaveCount(7);
	}
}

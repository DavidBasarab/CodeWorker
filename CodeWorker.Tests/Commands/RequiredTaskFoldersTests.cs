using FatCat.CodeWorker.Commands;

namespace Testing.FatCat.CodeWorker.Commands;

public class RequiredTaskFoldersTests
{
	[Fact]
	public void ContainTodo()
	{
		RequiredTaskFolders.All.Should().Contain("todo");
	}

	[Fact]
	public void ContainPending()
	{
		RequiredTaskFolders.All.Should().Contain("pending");
	}

	[Fact]
	public void ContainDone()
	{
		RequiredTaskFolders.All.Should().Contain("done");
	}

	[Fact]
	public void ContainBlocked()
	{
		RequiredTaskFolders.All.Should().Contain("blocked");
	}

	[Fact]
	public void ContainFailed()
	{
		RequiredTaskFolders.All.Should().Contain("failed");
	}

	[Fact]
	public void ContainReference()
	{
		RequiredTaskFolders.All.Should().Contain("reference");
	}

	[Fact]
	public void ContainExactlySixFolders()
	{
		RequiredTaskFolders.All.Should().HaveCount(6);
	}
}

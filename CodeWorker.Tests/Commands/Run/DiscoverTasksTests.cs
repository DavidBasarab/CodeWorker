using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.FileSystem;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class DiscoverTasksTests
{
	private readonly IListFiles listFiles;
	private readonly ILogger logger;
	private readonly DiscoverTasks discoverTasks;

	public DiscoverTasksTests()
	{
		listFiles = A.Fake<IListFiles>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => listFiles.List(A<string>._)).Returns(Array.Empty<string>());

		discoverTasks = new DiscoverTasks(listFiles, logger);
	}

	[Fact]
	public void ListFilesInTheTodoFolder()
	{
		discoverTasks.Discover(@"C:\Projects\my-api\tasks\todo");

		A.CallTo(() => listFiles.List(@"C:\Projects\my-api\tasks\todo")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ReturnEmptyListWhenNoFilesExist()
	{
		var result = discoverTasks.Discover(@"C:\Projects\my-api\tasks\todo");

		result.Should().BeEmpty();
	}

	[Fact]
	public void ExcludeGitkeepFiles()
	{
		A.CallTo(() => listFiles.List(A<string>._)).Returns(new[] { @"C:\tasks\todo\.gitkeep" });

		var result = discoverTasks.Discover(@"C:\tasks\todo");

		result.Should().BeEmpty();
	}

	[Fact]
	public void ReturnSingleTaskFile()
	{
		A.CallTo(() => listFiles.List(A<string>._)).Returns(new[] { @"C:\tasks\todo\01_MyTask.md" });

		var result = discoverTasks.Discover(@"C:\tasks\todo");

		result.Should().HaveCount(1);
		result[0].Should().Be(@"C:\tasks\todo\01_MyTask.md");
	}

	[Fact]
	public void OrderByNumericPrefix()
	{
		A.CallTo(() => listFiles.List(A<string>._))
			.Returns(
				new[] { @"C:\tasks\todo\14_ThisTask.md", @"C:\tasks\todo\03_SecondTask.md", @"C:\tasks\todo\11_ThirdTask.md" }
			);

		var result = discoverTasks.Discover(@"C:\tasks\todo");

		result
			.Should()
			.BeEquivalentTo(
				new[] { @"C:\tasks\todo\03_SecondTask.md", @"C:\tasks\todo\11_ThirdTask.md", @"C:\tasks\todo\14_ThisTask.md" },
				options => options.WithStrictOrdering()
			);
	}

	[Fact]
	public void PlaceUnnumberedFilesAfterNumberedFiles()
	{
		A.CallTo(() => listFiles.List(A<string>._))
			.Returns(new[] { @"C:\tasks\todo\AlphaTask.md", @"C:\tasks\todo\02_NumberedTask.md" });

		var result = discoverTasks.Discover(@"C:\tasks\todo");

		result[0].Should().Be(@"C:\tasks\todo\02_NumberedTask.md");
		result[1].Should().Be(@"C:\tasks\todo\AlphaTask.md");
	}

	[Fact]
	public void OrderUnnumberedFilesAlphabetically()
	{
		A.CallTo(() => listFiles.List(A<string>._))
			.Returns(new[] { @"C:\tasks\todo\Zebra.md", @"C:\tasks\todo\Apple.md", @"C:\tasks\todo\Mango.md" });

		var result = discoverTasks.Discover(@"C:\tasks\todo");

		result
			.Should()
			.BeEquivalentTo(
				new[] { @"C:\tasks\todo\Apple.md", @"C:\tasks\todo\Mango.md", @"C:\tasks\todo\Zebra.md" },
				options => options.WithStrictOrdering()
			);
	}

	[Fact]
	public void HandleMixOfNumberedAndUnnumberedWithGitkeep()
	{
		A.CallTo(() => listFiles.List(A<string>._))
			.Returns(
				new[]
				{
					@"C:\tasks\todo\.gitkeep",
					@"C:\tasks\todo\Zebra.md",
					@"C:\tasks\todo\05_First.md",
					@"C:\tasks\todo\01_Second.md",
					@"C:\tasks\todo\Apple.md",
				}
			);

		var result = discoverTasks.Discover(@"C:\tasks\todo");

		result
			.Should()
			.BeEquivalentTo(
				new[]
				{
					@"C:\tasks\todo\01_Second.md",
					@"C:\tasks\todo\05_First.md",
					@"C:\tasks\todo\Apple.md",
					@"C:\tasks\todo\Zebra.md",
				},
				options => options.WithStrictOrdering()
			);
	}
}

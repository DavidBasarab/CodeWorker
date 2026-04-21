using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.FileSystem;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class CollectReferenceFilesTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly IListFiles listFiles;
	private readonly ILogger logger;
	private readonly CollectReferenceFiles collectReferenceFiles;
	private readonly string referenceFolder = @"C:\Projects\my-api\tasks\reference";

	public CollectReferenceFilesTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		listFiles = A.Fake<IListFiles>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => fileSystemTools.DirectoryExists(A<string>.Ignored)).Returns(true);
		A.CallTo(() => listFiles.List(A<string>.Ignored)).Returns(Array.Empty<string>());

		collectReferenceFiles = new CollectReferenceFiles(fileSystemTools, listFiles, logger);
	}

	[Fact]
	public async Task ReturnEmptyListWhenDirectoryDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(referenceFolder)).Returns(false);

		var result = await collectReferenceFiles.Collect(referenceFolder);

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task ReturnEmptyListWhenDirectoryIsEmpty()
	{
		A.CallTo(() => listFiles.List(referenceFolder)).Returns(Array.Empty<string>());

		var result = await collectReferenceFiles.Collect(referenceFolder);

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task ReadEachFileInTheReferenceFolder()
	{
		var files = new[] { @"C:\Projects\my-api\tasks\reference\context.md", @"C:\Projects\my-api\tasks\reference\schema.md" };

		A.CallTo(() => listFiles.List(referenceFolder)).Returns(files);
		A.CallTo(() => fileSystemTools.ReadAllText(files[0])).Returns(Task.FromResult("context content"));
		A.CallTo(() => fileSystemTools.ReadAllText(files[1])).Returns(Task.FromResult("schema content"));

		var result = await collectReferenceFiles.Collect(referenceFolder);

		result.Should().HaveCount(2);
	}

	[Fact]
	public async Task ReturnFileNameForEachReferenceFile()
	{
		var files = new[] { @"C:\Projects\my-api\tasks\reference\context.md" };

		A.CallTo(() => listFiles.List(referenceFolder)).Returns(files);
		A.CallTo(() => fileSystemTools.ReadAllText(files[0])).Returns(Task.FromResult("some content"));

		var result = await collectReferenceFiles.Collect(referenceFolder);

		result[0].Name.Should().Be("context.md");
	}

	[Fact]
	public async Task ReturnContentForEachReferenceFile()
	{
		var files = new[] { @"C:\Projects\my-api\tasks\reference\context.md" };

		A.CallTo(() => listFiles.List(referenceFolder)).Returns(files);
		A.CallTo(() => fileSystemTools.ReadAllText(files[0])).Returns(Task.FromResult("file content here"));

		var result = await collectReferenceFiles.Collect(referenceFolder);

		result[0].Content.Should().Be("file content here");
	}

	[Fact]
	public async Task CheckTheCorrectDirectoryForExistence()
	{
		await collectReferenceFiles.Collect(referenceFolder);

		A.CallTo(() => fileSystemTools.DirectoryExists(referenceFolder)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ListFilesFromTheCorrectDirectory()
	{
		await collectReferenceFiles.Collect(referenceFolder);

		A.CallTo(() => listFiles.List(referenceFolder)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotListFilesWhenDirectoryDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(referenceFolder)).Returns(false);

		await collectReferenceFiles.Collect(referenceFolder);

		A.CallTo(() => listFiles.List(A<string>.Ignored)).MustNotHaveHappened();
	}

	[Fact]
	public async Task LogTheReferenceFilesFound()
	{
		var files = new[] { @"C:\Projects\my-api\tasks\reference\context.md" };

		A.CallTo(() => listFiles.List(referenceFolder)).Returns(files);
		A.CallTo(() => fileSystemTools.ReadAllText(files[0])).Returns(Task.FromResult("content"));

		await collectReferenceFiles.Collect(referenceFolder);

		A.CallTo(() => logger.Information(A<string>.That.Contains("Found"), A<int>.Ignored, A<string>.Ignored))
			.MustHaveHappened();
	}
}

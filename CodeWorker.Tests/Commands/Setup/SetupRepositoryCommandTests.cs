using FatCat.CodeWorker.Commands.Setup;
using FatCat.CodeWorker.FileSystem;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Setup;

public class SetupRepositoryCommandTests
{
	private readonly ISetupRepository setupRepository;
	private readonly IGetWorkingDirectory getWorkingDirectory;
	private readonly ILogger logger;
	private readonly SetupRepositoryCommand command;
	private readonly string workingDirectory = @"C:\Projects\current";
	private readonly string explicitPath = @"C:\Projects\my-api";

	public SetupRepositoryCommandTests()
	{
		setupRepository = A.Fake<ISetupRepository>();
		getWorkingDirectory = A.Fake<IGetWorkingDirectory>();
		logger = A.Fake<ILogger>();

		A.CallTo(() => getWorkingDirectory.GetWorkingDirectory()).Returns(workingDirectory);

		command = new SetupRepositoryCommand(setupRepository, getWorkingDirectory, logger);
	}

	[Fact]
	public async Task UseProvidedPathWhenArgumentIsGiven()
	{
		await command.Execute(new[] { "setup", explicitPath });

		A.CallTo(() => setupRepository.Setup(explicitPath)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseWorkingDirectoryWhenNoPathArgumentProvided()
	{
		await command.Execute(new[] { "setup" });

		A.CallTo(() => setupRepository.Setup(workingDirectory)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotUseWorkingDirectoryWhenPathIsProvided()
	{
		await command.Execute(new[] { "setup", explicitPath });

		A.CallTo(() => getWorkingDirectory.GetWorkingDirectory()).MustNotHaveHappened();
	}
}

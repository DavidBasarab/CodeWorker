using FatCat.CodeWorker.Commands.Run;
using FatCat.CodeWorker.Settings;
using FatCat.Toolkit;
using Serilog;

namespace Testing.FatCat.CodeWorker.Commands.Run;

public class ValidateRepositoryTests
{
	private readonly IFileSystemTools fileSystemTools;
	private readonly ILogger logger;
	private readonly ValidateRepository validateRepository;
	private readonly RepositorySettings repositorySettings;

	public ValidateRepositoryTests()
	{
		fileSystemTools = A.Fake<IFileSystemTools>();
		logger = A.Fake<ILogger>();

		repositorySettings = new RepositorySettings { Path = @"C:\Projects\my-api", Enabled = true };

		A.CallTo(() => fileSystemTools.DirectoryExists(A<string>._)).Returns(true);
		A.CallTo(() => fileSystemTools.FileExists(A<string>._)).Returns(true);

		validateRepository = new ValidateRepository(fileSystemTools, logger);
	}

	[Fact]
	public void ReturnValidWhenAllPathsExist()
	{
		var result = validateRepository.Validate(repositorySettings);

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void ReturnInvalidWhenRepositoryPathDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(@"C:\Projects\my-api")).Returns(false);

		var result = validateRepository.Validate(repositorySettings);

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public void IncludeRepositoryPathInErrorsWhenMissing()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(@"C:\Projects\my-api")).Returns(false);

		var result = validateRepository.Validate(repositorySettings);

		result.Errors.Should().Contain(error => error.Contains(@"C:\Projects\my-api"));
	}

	[Fact]
	public void ReturnInvalidWhenTasksFolderDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(A<string>.That.EndsWith("tasks"))).Returns(false);

		var result = validateRepository.Validate(repositorySettings);

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public void ReturnInvalidWhenTodoFolderDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(A<string>.That.EndsWith("todo"))).Returns(false);

		var result = validateRepository.Validate(repositorySettings);

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public void ReturnInvalidWhenDoneFolderDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(A<string>.That.EndsWith("done"))).Returns(false);

		var result = validateRepository.Validate(repositorySettings);

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public void ReturnInvalidWhenBlockedFolderDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(A<string>.That.EndsWith("blocked"))).Returns(false);

		var result = validateRepository.Validate(repositorySettings);

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public void ReturnInvalidWhenPendingFolderDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(A<string>.That.EndsWith("pending"))).Returns(false);

		var result = validateRepository.Validate(repositorySettings);

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public void ReturnInvalidWhenFailedFolderDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(A<string>.That.EndsWith("failed"))).Returns(false);

		var result = validateRepository.Validate(repositorySettings);

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public void ReturnInvalidWhenReferenceFolderDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(A<string>.That.EndsWith("reference"))).Returns(false);

		var result = validateRepository.Validate(repositorySettings);

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public void ReturnInvalidWhenSettingsJsonDoesNotExist()
	{
		A.CallTo(() => fileSystemTools.FileExists(A<string>.That.EndsWith("settings.json"))).Returns(false);

		var result = validateRepository.Validate(repositorySettings);

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public void IncludeAllMissingPathsInErrors()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(A<string>._)).Returns(false);
		A.CallTo(() => fileSystemTools.FileExists(A<string>._)).Returns(false);

		var result = validateRepository.Validate(repositorySettings);

		result.Errors.Should().HaveCount(9);
	}

	[Fact]
	public void LogWarningWhenValidationFails()
	{
		A.CallTo(() => fileSystemTools.DirectoryExists(@"C:\Projects\my-api")).Returns(false);

		validateRepository.Validate(repositorySettings);

		A.CallTo(() => logger.Warning(A<string>._, A<string>._, A<string>._)).MustHaveHappened();
	}

	[Fact]
	public void NotLogWhenValidationPasses()
	{
		validateRepository.Validate(repositorySettings);

		A.CallTo(() => logger.Warning(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public void HaveNoErrorsWhenValid()
	{
		var result = validateRepository.Validate(repositorySettings);

		result.Errors.Should().BeEmpty();
	}
}

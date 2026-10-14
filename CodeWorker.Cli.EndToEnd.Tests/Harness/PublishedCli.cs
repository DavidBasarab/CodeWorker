using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

[ExcludeFromCodeCoverage(
	Justification = "Publishes the CLI via the dotnet CLI (System.Diagnostics.Process) — no business logic, exercised by the E2E tests."
)]
public class PublishedCli : IAsyncLifetime
{
	private string publishDirectory;

	public string ExecutablePath { get; private set; }

	public async Task InitializeAsync()
	{
		publishDirectory = Path.Combine(Path.GetTempPath(), $"codeworker-cli-e2e-{Guid.NewGuid():N}");

		var projectPath = Path.Combine(LocateRepositoryRoot(), "CodeWorker.Cli", "CodeWorker.Cli.csproj");

		await Publish(projectPath, publishDirectory);

		ExecutablePath = Path.Combine(publishDirectory, "FatCatCodeWorkerCli.exe");

		if (!File.Exists(ExecutablePath))
		{
			throw new FileNotFoundException($"Published CLI not found at {ExecutablePath}");
		}
	}

	public Task DisposeAsync()
	{
		try
		{
			Directory.Delete(publishDirectory, true);
		}
		catch
		{
			// ignored — best-effort cleanup of a temp directory
		}

		return Task.CompletedTask;
	}

	private static string LocateRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "CodeWorker.sln")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate CodeWorker.sln above the test output directory.");
	}

	private static async Task Publish(string projectPath, string outputDirectory)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = "dotnet",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		startInfo.ArgumentList.Add("publish");
		startInfo.ArgumentList.Add(projectPath);
		startInfo.ArgumentList.Add("-c");
		startInfo.ArgumentList.Add("Release");
		startInfo.ArgumentList.Add("-o");
		startInfo.ArgumentList.Add(outputDirectory);

		using var process = Process.Start(startInfo);

		var output = process.StandardOutput.ReadToEndAsync();
		var error = process.StandardError.ReadToEndAsync();

		await process.WaitForExitAsync();

		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException(
				$"dotnet publish failed (exit {process.ExitCode}).{Environment.NewLine}{await output}{Environment.NewLine}{await error}"
			);
		}
	}
}

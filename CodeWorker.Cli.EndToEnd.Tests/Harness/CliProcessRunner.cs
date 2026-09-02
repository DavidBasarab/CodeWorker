using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

[ExcludeFromCodeCoverage(
	Justification = "Direct wrapper over System.Diagnostics.Process — no business logic, exercised by the E2E tests that drive it."
)]
public class CliProcessRunner
{
	public async Task<CliResult> Run(string executablePath, params string[] arguments)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = executablePath,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		foreach (var argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		using var process = Process.Start(startInfo);

		var standardOutput = process.StandardOutput.ReadToEndAsync();
		var standardError = process.StandardError.ReadToEndAsync();

		await process.WaitForExitAsync();

		return new CliResult
		{
			ExitCode = process.ExitCode,
			StandardOutput = await standardOutput,
			StandardError = await standardError,
		};
	}
}

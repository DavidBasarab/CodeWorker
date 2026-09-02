using FatCat.Toolkit;

namespace FatCat.CodeWorker.Cli.Commands.Verify;

public interface IParseVerifyArguments
{
	VerifyArgumentsResult Parse(string[] args);
}

public class ParseVerifyArguments(IFileSystemTools fileSystemTools) : IParseVerifyArguments
{
	private const string IntentFlag = "--intent";
	private const string ProductionFlag = "--production";
	private const string TestsFlag = "--tests";
	private const string UsageLine = "Usage: verify --intent <intent.json> --production <Foo.cs> --tests <FooTests.cs>";

	public VerifyArgumentsResult Parse(string[] args)
	{
		if (!ContainsAnyFlag(args))
		{
			return Failure(VerifyUsageError.NoArguments, UsageLine);
		}

		if (!TryGetFlagValue(args, IntentFlag, out var intentPath))
		{
			return Failure(VerifyUsageError.MissingIntentFlag, $"Missing required flag {IntentFlag}. {UsageLine}");
		}

		if (!TryGetFlagValue(args, ProductionFlag, out var productionPath))
		{
			return Failure(VerifyUsageError.MissingProductionFlag, $"Missing required flag {ProductionFlag}. {UsageLine}");
		}

		if (!TryGetFlagValue(args, TestsFlag, out var testsPath))
		{
			return Failure(VerifyUsageError.MissingTestsFlag, $"Missing required flag {TestsFlag}. {UsageLine}");
		}

		if (!fileSystemTools.FileExists(intentPath))
		{
			return Failure(VerifyUsageError.IntentFileNotFound, $"Intent file not found: {intentPath}. {UsageLine}");
		}

		if (!fileSystemTools.FileExists(productionPath))
		{
			return Failure(
				VerifyUsageError.ProductionFileNotFound,
				$"Production file not found: {productionPath}. {UsageLine}"
			);
		}

		if (!fileSystemTools.FileExists(testsPath))
		{
			return Failure(VerifyUsageError.TestsFileNotFound, $"Tests file not found: {testsPath}. {UsageLine}");
		}

		return new VerifyArgumentsResult
		{
			IsValid = true,
			Error = VerifyUsageError.None,
			Message = null,
			IntentPath = intentPath,
			ProductionPath = productionPath,
			TestsPath = testsPath,
		};
	}

	private static bool ContainsAnyFlag(string[] args)
	{
		return args.Contains(IntentFlag) || args.Contains(ProductionFlag) || args.Contains(TestsFlag);
	}

	private static bool TryGetFlagValue(string[] args, string flag, out string value)
	{
		value = null;

		for (var index = 0; index < args.Length; index++)
		{
			if (args[index] != flag)
			{
				continue;
			}

			var hasFollowingValue = index + 1 < args.Length && !IsFlag(args[index + 1]);

			if (hasFollowingValue)
			{
				value = args[index + 1];

				return true;
			}
		}

		return false;
	}

	private static bool IsFlag(string token)
	{
		return token.StartsWith("--");
	}

	private static VerifyArgumentsResult Failure(VerifyUsageError error, string message)
	{
		return new VerifyArgumentsResult
		{
			IsValid = false,
			Error = error,
			Message = message,
		};
	}
}

namespace FatCat.CodeWorker.Cli.Commands.Verify.Gates;

public interface IVerificationGate
{
	string Name { get; }

	GateStage Stage { get; }

	Task<GateResult> Run(VerificationContext context);
}

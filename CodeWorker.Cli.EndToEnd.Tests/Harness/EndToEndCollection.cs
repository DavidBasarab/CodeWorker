namespace Testing.FatCat.CodeWorker.Cli.EndToEnd.Harness;

[CollectionDefinition(Name)]
public class EndToEndCollection : ICollectionFixture<PublishedCli>
{
	public const string Name = "CodeWorker CLI end-to-end";
}

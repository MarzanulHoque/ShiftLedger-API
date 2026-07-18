using Xunit;

namespace ShiftLedger.Api.IntegrationTests;

[CollectionDefinition("Database")]
public class DbCollection : ICollectionFixture<IntegrationTestFixture>;

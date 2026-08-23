namespace Talmidon.Tests;

/// <summary>מגדיר Fixture משותף (מארח + DB מוגר פעם אחת) לכל מחלקות הבדיקה באוסף הזה — מהיר יותר מהקמה מחדש לכל מחלקה.</summary>
[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<TalmidonWebApplicationFactory>
{
    public const string Name = "Integration";
}

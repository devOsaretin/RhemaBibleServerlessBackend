


using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson;


public static class MongoEnumStringConvention
{
    public static void RegisterEnumStringConvention()
    {
        var pack = new ConventionPack
        {
            new EnumRepresentationConvention(BsonType.String)
        };

        // Apply to all types globally
        ConventionRegistry.Register(
            "EnumStringConvention",
            pack,
            t => true // applies to all classes
        );
    }
}

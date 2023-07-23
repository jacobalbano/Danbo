using LiteDB;
using Newtonsoft.Json.Linq;

namespace Danbo.Models;

public abstract record class ModelBase
{
    [BsonId]
    public Guid Key { get; init; } = Guid.NewGuid();

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class IndexedAttribute : Attribute { }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
public class BsonConverterAttribute : Attribute
{
    public Type ConverterType { get; }

    public BsonConverterAttribute(Type converterType)
    {
        ConverterType = converterType;
    }
}

public interface IBsonConverter<T> : IBsonConverter
{
    new T Deserialize(BsonValue value);
    BsonValue Serialize(T value);

    object IBsonConverter.Deserialize(BsonValue value) => Deserialize(value)!;
    BsonValue IBsonConverter.Serialize(object o) => Serialize((T) o);
}

public interface IBsonConverter
{
    object Deserialize(BsonValue value);
    BsonValue Serialize(object o);
}
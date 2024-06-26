﻿using Danbo.Models;
using Danbo.Services;
using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using LiteDB;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;

namespace Danbo;

public abstract class Database
{
    protected Database(ScopedGuildId gid, string filename)
    {
        if (gid.Id != null)
            db = GetShared(gid.Id.Value, filename);
    }

    private static LiteDatabase GetShared(ulong id, string filename)
    {
        var directory = id.ToString();
        if (!Directory.Exists(id.ToString()))
            Directory.CreateDirectory(id.ToString());

        var key = new InstanceKey(id, filename);
        if (!instances.TryGetValue(key, out var db))
        {
            instances[key] = db = new LiteDatabase(Path.Combine(directory, $"{filename}.db"));
            db.Mapper.ResolveCollectionName = CollectionNameResolver;
            db.Checkpoint();
        }

        return db;
    }

    private record class InstanceKey(ulong GuildId, string FileName);
    private static readonly Dictionary<InstanceKey, LiteDatabase> instances = new();

    private static string CollectionNameResolver(Type t)
    {
        var result = t.Name;
        for (var decl = t.DeclaringType; decl != null; decl = decl.DeclaringType)
            result = $"{decl.Name}_{result}";

        return result;
    }

    public ILiteQueryable<T> Select<T>()
    {
        return Establish<T>().Query();
    }

    public SingletonWrapper<T> GetSingleton<T>() where T : ModelBase, new()
    {
        return new SingletonWrapper<T>(this);
    }

    public ISession BeginSession() => new SessionImpl(this);

    private ILiteCollection<T> Establish<T>()
    {
        var collection = db.GetCollection<T>();
        Optimizer<T>.Run(db.Mapper, collection);
        return collection;
    }

    public interface ISession : IDisposable
    {
        public T Insert<T>(T item) where T : ModelBase;
        public T InsertOrUpdate<T>(T item) where T : ModelBase;
        public int InsertOrUpdate<T>(IEnumerable<T> items) where T : ModelBase;
        public bool Delete<T>(T item) where T : ModelBase;
        public int Delete<T>(IEnumerable<T> items) where T : ModelBase;
        public int DeleteAll<T>() where T : ModelBase;
        public bool Update<T>(T item) where T : ModelBase;
        public int Update<T>(IEnumerable<T> items) where T : ModelBase;
        public ILiteQueryable<T> Select<T>();
        public SingletonWrapper<T> GetSingleton<T>() where T : ModelBase, new();
    }

    private class SessionImpl : ISession
    {
        public ILiteQueryable<T> Select<T>() => owner.Select<T>();
        public SingletonWrapper<T> GetSingleton<T>() where T : ModelBase, new() => owner.GetSingleton<T>();

        public SessionImpl(Database db)
        {
            owner = db;
        }

        public T Insert<T>(T item) where T : ModelBase
        {
            owner.Establish<T>().Insert(item);
            return item;
        }

        public T InsertOrUpdate<T>(T item) where T : ModelBase
        {
            owner.Establish<T>().Upsert(item);
            return item;
        }

        public int InsertOrUpdate<T>(IEnumerable<T> items) where T : ModelBase
        {
            return owner.Establish<T>().Upsert(items);
        }

        public bool Delete<T>(T item) where T : ModelBase
        {
            return owner.Establish<T>().Delete(item.Key);
        }

        public int Delete<T>(IEnumerable<T> items) where T : ModelBase
        {
            return items.Count(Delete);
        }

        public int DeleteAll<T>() where T : ModelBase
        {
            return owner.Establish<T>().DeleteAll();
        }

        public bool Update<T>(T item) where T : ModelBase
        {
            return owner.Establish<T>().Update(item);
        }

        public int Update<T>(IEnumerable<T> items) where T : ModelBase
        {
            return owner.Establish<T>().Update(items);
        }

        public void Dispose()
        {
            if (Program.BotConfig.CheckpointEveryMutation)
                owner.db.Checkpoint();
        }

        private readonly Database owner;
    }

    public class SingletonWrapper<T> : IDisposable where T : ModelBase, new()
    {
        public T Value { get; }

        public SingletonWrapper(Database database)
        {
            owner = database;
            Value = database.Select<T>().SingleOrDefault() ?? new T();
        }

        public void Dispose()
        {
            using var s = owner.BeginSession();
            s.InsertOrUpdate(Value);
        }

        private readonly Database owner;
    }

    private class Optimizer<T>
    {
        public static void Run(BsonMapper mapper, ILiteCollection<T> collection)
        {
            if (hasRun) return;

            foreach (var propInfo in typeof(T).GetProperties())
            {
                if (propInfo.GetCustomAttribute<ModelBase.IndexedAttribute>() != null)
                {
                    var param = Expression.Parameter(typeof(T));
                    var convert = Expression.TypeAs(Expression.Property(param, propInfo), typeof(object));
                    var getMethod = Expression.Lambda<Func<T, object>>(convert, param);
                    collection.EnsureIndex(getMethod);
                }

                if (propInfo.GetCustomAttribute<BsonConverterAttribute>() is BsonConverterAttribute attr)
                {
                    if (!converters.TryGetValue(attr.ConverterType, out var converter))
                    {
                        converter = (IBsonConverter)Activator.CreateInstance(attr.ConverterType)!;
                        converters[attr.ConverterType] = converter;
                    }

                    var type = propInfo.PropertyType;
                    if (Nullable.GetUnderlyingType(type) is Type underType)
                        type = underType;

                    mapper.RegisterType(type, converter.Serialize, converter.Deserialize);
                }
            }

            hasRun = true;
        }

        private static bool hasRun = false;
        private static readonly Dictionary<Type, IBsonConverter> converters = new();
    }

    private readonly LiteDatabase db;
}
﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Folke.Elm.Fluent;
using Folke.Elm.InformationSchema;
using Folke.Elm.Mapping;
using Microsoft.Extensions.Options;

namespace Folke.Elm
{
    public class FolkeConnection : IFolkeConnection
    {
        private readonly DbConnection connection;
        private DbTransaction transaction;
        private int stackedTransactions;
        private bool askRollback;
        private readonly ElmQueryProvider queryProvider;

        /// <summary>
        /// Creates a new instance of <see cref="FolkeConnection"/>. Intended to be used
        /// for IoC. Otherwise, use the Create method to construct a connection.
        /// </summary>
        /// <param name="databaseDriver"></param>
        /// <param name="mapper"></param>
        /// <param name="options"></param>
        public FolkeConnection(IDatabaseDriver databaseDriver, IMapper mapper, IOptions<ElmOptions> options):
            this(databaseDriver, mapper, options.Value.ConnectionString)
        {
        }

        private FolkeConnection(IDatabaseDriver databaseDriver, IMapper mapper, string connectionString = null)
        {
            Cache = new Dictionary<string, IDictionary<object, object>>();
            Driver = databaseDriver;
            connection = databaseDriver.CreateConnection(connectionString);
            Database = connection?.Database;
            Mapper = mapper;
            queryProvider = new ElmQueryProvider(this);
        }

        public static FolkeConnection Create(IDatabaseDriver databaseDriver, IMapper mapper, string connectionString = null)
        {
            return new FolkeConnection(databaseDriver, mapper, connectionString);
        }

        public IDictionary<string, IDictionary<object, object>> Cache { get; }

        public string Database { get; set; }

        public IDatabaseDriver Driver { get; set; }

        public IMapper Mapper { get; }
        
        public IFolkeCommand CreateCommand()
        {
            if (transaction == null)
                connection.Open();
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            return new FolkeCommand(this, command);
        }

        public FolkeTransaction BeginTransaction()
        {
            if (transaction == null)
            {
                connection.Open();
                transaction = connection.BeginTransaction();
                askRollback = false;
            }

            stackedTransactions++;
            return new FolkeTransaction(this);
        }

        public ISelectResult<T, FolkeTuple> Select<T>() where T : class, new()
        {
            return FluentBaseBuilder<T, FolkeTuple>.Select(new BaseQueryBuilder(this, typeof(T)));
        }

        public ISelectResult<T, TParameters> Select<T, TParameters>() where T : class, new()
        {
            return FluentBaseBuilder<T, TParameters>.Select(new BaseQueryBuilder(this, typeof(T), typeof(TParameters)));
        }
        
        public IFromResult<T, FolkeTuple> SelectAllFrom<T>() where T : class, new()
        {
            return Select<T>().All().From();
        }
        
        /// <summary>
        /// Select all the fields from the T type and the properties in parameter
        /// </summary>
        /// <typeparam name="T">The type to select on</typeparam>
        /// <param name="fetches">The other tables to select (using a left join)</param>
        /// <returns></returns>
        public IFromResult<T, FolkeTuple> SelectAllFrom<T>(params Expression<Func<T, object>>[] fetches)
            where T : class, new()
        {
            var query = FluentBaseBuilder<T, FolkeTuple>.Select(new BaseQueryBuilder(this, typeof(T)));
            var all = query.All();
            foreach (var fetch in fetches)
                query.All(fetch);
            var fromQuery = all.From();
            foreach (var fetch in fetches)
                fromQuery.LeftJoinOnId(fetch);
            return fromQuery;
        }
        
        public void Delete<T>(T value) where T : class, new()
        {
            var keyProperty = Mapper.GetTypeMapping(typeof(T)).Key.PropertyInfo;
            Delete<T>().From().Where(x => x.Key() == keyProperty.GetValue(value)).Execute();
        }

        public IDeleteResult<T, FolkeTuple> Delete<T>() where T : class, new()
        {
            return FluentBaseBuilder<T, FolkeTuple>.Delete(new BaseQueryBuilder(this, typeof(T)));
        }

        public async Task DeleteAsync<T>(T value) where T : class, new()
        {
            var keyProperty = Mapper.GetTypeMapping(typeof(T)).Key.PropertyInfo;
            await Delete<T>().From().Where(x => x.Key() == keyProperty.GetValue(value)).ExecuteAsync();
        }

        public void Update<T>(T value) where T : class, new()
        {
            var keyProperty = Mapper.GetTypeMapping(typeof(T)).Key.PropertyInfo;
            Update<T>().SetAll(value).Where(x => x.Key() == keyProperty.GetValue(value)).Execute();
        }

        public IUpdateResult<T, FolkeTuple> Update<T>()
        {
            return FluentBaseBuilder<T, FolkeTuple>.Update(new BaseQueryBuilder(this, typeof(T)));
        }

        public async Task UpdateAsync<T>(T value) where T : class, new()
        {
            var keyProperty = Mapper.GetTypeMapping(typeof(T)).Key.PropertyInfo;
            await Update<T>().SetAll(value).Where(x => x.Key() == keyProperty.GetValue(value)).ExecuteAsync();
        }

        public T Refresh<T>(T value) where T : class, new()
        {
            var keyProperty = Mapper.GetTypeMapping(typeof(T)).Key.PropertyInfo;
            return Select<T>().All().From().Where(x => x.Key() == keyProperty.GetValue(value)).Single();
        }

        public T Load<T>(object id) where T : class, new()
        {
            return Select<T>().All().From().Where(x => x.Key().Equals(id)).Single();
        }
        
        public T Load<T>(object id, params Expression<Func<T, object>>[] fetches) where T : class, new()
        {
            return CreateLoadOrGetQuery(fetches).Where(x => x.Key() == id).Single();
        }

        public async Task<T> LoadAsync<T>(object id, params Expression<Func<T, object>>[] fetches) where T : class, new()
        {
            return await CreateLoadOrGetQuery(fetches).Where(x => x.Key() == id).SingleAsync();
        }
        
        public T Get<T>(object id) where T : class, new()
        {
            return Select<T>().All().From().Where(x => x.Key() == id).FirstOrDefault();
        }

        public T Get<T>(object id, params Expression<Func<T, object>>[] fetches) where T : class, new()
        {
            return CreateLoadOrGetQuery(fetches).Where(x => x.Key() == id).FirstOrDefault();
        }

        public async Task<T> GetAsync<T>(object id, params Expression<Func<T, object>>[] fetches) where T : class, new()
        {
            return await CreateLoadOrGetQuery(fetches).Where(x => x.Key() == id).FirstOrDefaultAsync();
        }

        public IInsertIntoResult<T, FolkeTuple> InsertInto<T>() where T : class, new()
        {
            return FluentBaseBuilder<T, FolkeTuple>.InsertInto(new BaseQueryBuilder(this, typeof(T)));
        }

        public void Save<T>(T value) where T : class, new()
        {
            var keyProperty = Mapper.GetTypeMapping(typeof(T)).Key;
            if (keyProperty == null) throw new ElmException($"{typeof(T)} does not define a key. Add a Key attribute");
            bool automatic = keyProperty.IsAutomatic;
            using (var t = BeginTransaction())
            {
                CreateSaveQuery(value, automatic, keyProperty.PropertyInfo).Execute();
                UpdateSavedValue(value, automatic, keyProperty.PropertyInfo);
                t.Commit();
            }
        }

        public async Task SaveAsync<T>(T value) where T : class, new()
        {
            var keyProperty = Mapper.GetTypeMapping(typeof(T)).Key;
            bool automatic = keyProperty.IsAutomatic;
            using (var t = BeginTransaction())
            {
                await CreateSaveQuery(value, automatic, keyProperty.PropertyInfo).ExecuteAsync();
                UpdateSavedValue(value, automatic, keyProperty.PropertyInfo);
                t.Commit();
            }
        }

        private void UpdateSavedValue<T>(T value, bool automatic, PropertyInfo keyProperty) where T : class, new()
        {
            if (automatic)
            {
                var key = Select<FolkeTuple<int>>().Value(x => SqlFunctions.LastInsertedId(), x => x.Item0).Scalar();
                keyProperty.SetValue(value, Convert.ChangeType(key, keyProperty.PropertyType));
            }

            if (!Cache.ContainsKey(typeof(T).Name))
            {
                Cache[typeof(T).Name] = new Dictionary<object, object>();
            }

            Cache[typeof(T).Name][keyProperty.GetValue(value)] = value;
        }

        private IInsertedValuesResult<T, FolkeTuple> CreateSaveQuery<T>(T value, bool automatic, PropertyInfo keyProperty)
            where T : class, new()
        {
            if (automatic)
            {
                var defaultValue = Activator.CreateInstance(keyProperty.PropertyType);
                if (!keyProperty.GetValue(value).Equals(defaultValue))
                    throw new Exception("Id must be 0");
            }

            return InsertInto<T>().Values(value);
        }

        public void CreateTable<T>(bool drop = false) where T : class, new()
        {
            if (drop)
            {
                new SchemaQueryBuilder<T>(this).DropTable().TryExecute();
            }

            new SchemaQueryBuilder<T>(this).CreateTable().Execute();
        }
        
        public void DropTable<T>() where T : class, new()
        {
            new SchemaQueryBuilder<T>(this).DropTable().Execute();
        }
        
        public void CreateOrUpdateTable<T>() where T : class, new()
        {
            new SchemaUpdater(this).CreateOrUpdate<T>();
        }

        public void UpdateSchema(Assembly assembly)
        {
            var mappings = Mapper.GetTypeMappings(assembly).Where(x => !x.IsComplexType).ToList();
            var schemaUpdater = new SchemaUpdater(this);
            schemaUpdater.CreateOrUpdate(mappings);
        }

        public void UpdateSchema()
        {
            var schemaUpdater = new SchemaUpdater(this);
            schemaUpdater.CreateOrUpdate(Mapper.GetTypeMappings().Where(x => !x.IsComplexType).ToList());
        }

        /// <summary>
        /// Remove an old element, replacing each reference to this element
        /// with references to a new element
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="oldElement">The element to delete</param>
        /// <param name="newElement">The element that replaces the previous element</param>
        public void Merge<T>(T oldElement, T newElement) where T : class, IFolkeTable, new()
        {
            var type = typeof(T);
            var typeMap = Mapper.GetTypeMapping(type);

            var columns =
                SelectAllFrom<KeyColumnUsage>()
                    .Where(c => c.ReferencedTableName == typeMap.TableName && c.ReferencedTableSchema == typeMap.TableSchema)
                    .ToList();

            foreach (var column in columns)
            {
                using (var command = CreateCommand())
                {
                    command.CommandText = String.Format(
                        "UPDATE `{0}`.`{1}` SET `{2}` = {3} WHERE `{2}` = {4}",
                        column.TableSchema,
                        column.TableName,
                        column.ColumnName,
                        newElement.Id,
                        oldElement.Id);
                    command.ExecuteNonQuery();
                }
            }

            Delete(oldElement);
        }

        internal void CloseCommand()
        {
            if (transaction == null)
                connection.Close();
        }

        internal void RollbackTransaction()
        {
            stackedTransactions--;
            askRollback = true;

            if (stackedTransactions == 0) 
            {
                transaction.Rollback();
                transaction = null;
                connection.Close();
            }
        }

        internal void CommitTransaction()
        {
            stackedTransactions--;

            if (stackedTransactions == 0)
            {
                if (askRollback)
                    transaction.Rollback();
                else
                    transaction.Commit();
                transaction = null;
                connection.Close();
            }
        }
        
        internal void CreateTable(Type t, bool drop = false, IList<string> existingTables = null)
        {
            var mapping = Mapper.GetTypeMapping(t);
            if (drop)
            {
                new SchemaQueryBuilder<FolkeTuple>(this).DropTable(mapping).TryExecute();
            }

            new SchemaQueryBuilder<FolkeTuple>(this).CreateTable(mapping, existingTables).Execute();
        }

        private IFromResult<T, FolkeTuple> CreateLoadOrGetQuery<T>(Expression<Func<T, object>>[] fetches) where T : class, new()
        {
            var query = Select<T>().All();
            foreach (var fetch in fetches)
            {
                query.All(fetch);
            }

            var from = query.From();
            foreach (var fetch in fetches)
            {
                from.LeftJoinOnId(fetch);
            }

            return from;
        }

        public void Dispose()
        {
            transaction?.Dispose();
            connection?.Dispose();
        }

        public IFolkeCommand CreateCommand(string commandText, object[] commandParameters)
        {
            var command = CreateCommand();
            if (commandParameters != null)
            {
                command.SetParameters(commandParameters, Mapper, Driver);
            }
            command.CommandText = commandText;
            return command;
        }

        public IQueryable<T> Query<T>()
        {
            return new ElmQueryable<T>(queryProvider);
        }
    } 
}
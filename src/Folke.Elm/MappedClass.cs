using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using Folke.Elm.Mapping;

namespace Folke.Elm
{
    /// <summary>A class that has been mapped to selected fields</summary>
    public class MappedClass
    {
        public readonly IList<MappedField> fields = new List<MappedField>();
        public IList<MappedCollection> collections;
        /// <summary>The primary key mapping</summary>
        public MappedField idField;
        public ConstructorInfo constructor;

        public object Construct(IFolkeConnection connection, Type type, object id)
        {
            var ret = constructor.Invoke(null);

            idField?.propertyInfo.SetValue(ret, id);

            if (collections != null)
            {
                foreach (var collection in collections)
                {
                    collection.propertyInfo.SetValue(ret, collection.listConstructor.Invoke(new[] { connection, type, id, collection.listJoins }));
                }
            }
            return ret;
        }

        public object Read(IFolkeConnection folkeConnection, Type type, DbDataReader reader, object expectedId = null)
        {
            var cache = folkeConnection.Cache;
            object value;
            var idMappedField = idField;

            // If the key field is mapped or if its value is already known, create a new item and
            // store it in cache
            if (idMappedField != null && (idMappedField.selectedField != null || expectedId != null))
            {
                if (!cache.ContainsKey(type.Name))
                    cache[type.Name] = new Dictionary<object, object>();
                var typeCache = cache[type.Name];

                object id;

                if (idMappedField.selectedField != null)
                {
                    var index = idMappedField.selectedField.index;

                    if (expectedId == null && reader.IsDBNull(index))
                        return null;

                    // Do like this because GetTypedValue does not seem to work with MySql and System.Guid
                    id = folkeConnection.Driver.ConvertReaderValueToProperty(reader.GetValue(index), idMappedField.propertyInfo.PropertyType);
                    if (expectedId != null && !id.Equals(expectedId))
                        throw new Exception("Unexpected id");
                }
                else
                {
                    id = expectedId;
                }

                if (typeCache.ContainsKey(id))
                {
                    value = typeCache[id];
                }
                else
                {
                    value = Construct(folkeConnection, type, id);
                    typeCache[id] = value;
                }
            }
            else
            {
                value = Construct(folkeConnection, type, 0);
            }

            foreach (var mappedField in fields)
            {
                var fieldInfo = mappedField.selectedField;
                
                if (fieldInfo != null && reader.IsDBNull(fieldInfo.index))
                    continue;
                
                if (mappedField.mappedClass == null)
                {
                    if (fieldInfo == null)
                        throw new Exception("Unknown error");
                    object field = folkeConnection.Driver.ConvertReaderValueToValue(reader, mappedField.propertyInfo.PropertyType, fieldInfo.index);
                    mappedField.propertyInfo.SetValue(value, field);
                }
                else 
                {
                    object id = fieldInfo == null ? null : reader.GetValue(fieldInfo.index);
                    if (id != null)
                        id = folkeConnection.Driver.ConvertReaderValueToProperty(id, mappedField.mappedClass.idField.propertyInfo.PropertyType);
                    object other = mappedField.mappedClass.Read(folkeConnection, mappedField.propertyInfo.PropertyType, reader, id);
                    mappedField.propertyInfo.SetValue(value, other);
                }
            }
            return value;
        }

        public static MappedClass MapClass(IList<BaseQueryBuilder.FieldAlias> fieldAliases, TypeMapping type, string alias = null)
        {
            if (fieldAliases == null)
                return null;

            var mappedClass = new MappedClass();

            var idProperty = type.Key;
            mappedClass.constructor = type.Type.GetConstructor(Type.EmptyTypes);
            if (idProperty != null)
            {
                var selectedField = fieldAliases.SingleOrDefault(f => f.tableAlias == alias && f.PropertyMapping == idProperty);
                mappedClass.idField = new MappedField { selectedField = selectedField, propertyInfo = idProperty.PropertyInfo };
            }

            foreach (var pair in type.Columns)
            {
                var propertyMapping = pair.Value;
                if (idProperty != null && propertyMapping == idProperty)
                    continue;

                var fieldInfo = fieldAliases.SingleOrDefault(f => f.tableAlias == alias && f.PropertyMapping == propertyMapping);
                bool isForeign = propertyMapping.Reference != null;
                if (fieldInfo != null || (isForeign && (mappedClass.idField == null || mappedClass.idField.selectedField != null)))
                {
                    var mappedField = new MappedField { propertyInfo = propertyMapping.PropertyInfo, selectedField = fieldInfo };

                    if (isForeign)
                    {
                        mappedField.mappedClass = MapClass(fieldAliases, propertyMapping.Reference, alias == null ? pair.Key : alias + "." + pair.Key);
                    }
                    mappedClass.fields.Add(mappedField);
                }
            }

            if (type.Collections.Any())
            {
                mappedClass.collections = new List<MappedCollection>();
                foreach (var collection in type.Collections.Values)
                {
                    mappedClass.collections.Add(collection);
                }
            }
            
            return mappedClass;
        }
    }
}
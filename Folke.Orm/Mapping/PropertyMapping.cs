﻿using System.Reflection;

namespace Folke.Orm.Mapping
{
    public class PropertyMapping
    {
        public PropertyInfo PropertyInfo { get; set; }
        public string ColumnName { get; set; }
        public TypeMapping Reference { get; set; }
        public bool Readonly { get; set; }
        public bool Nullable { get; set; }
        public bool IsAutomatic { get; set; }
        public int MaxLength { get; set; }
        public string Index { get; set; }
        /// <summary>
        /// What to do when the referenced line is deleted
        /// </summary>
        public ConstraintEventEnum OnDelete { get; set; }
        /// <summary>
        /// What to do when the referenced line key is updated
        /// </summary>
        public ConstraintEventEnum OnUpdate { get; set; }

        public bool IsKey { get; set; }

        public PropertyMapping(PropertyInfo propertyInfo)
        {
            PropertyInfo = propertyInfo;
        }
    }
}
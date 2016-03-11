﻿using System;
using System.Text;

namespace Folke.Elm
{
    using Folke.Elm.Mapping;

    public class SqlStringBuilder
    {
        protected readonly StringBuilder stringBuilder = new StringBuilder();

        public SqlStringBuilder Append(string s)
        {
            stringBuilder.Append(s);
            return this;
        }

        public SqlStringBuilder AppendAfterSpace(string s)
        {
            AppendSpace();
            stringBuilder.Append(s);
            return this;
        }

        internal void AppendTableName(TypeMapping type)
        {
            AppendSpace();
            if (type.TableSchema != null)
            {
                AppendSymbol(type.TableSchema);
                Append('.');
            }

            AppendSymbol(type.TableName);
        }

        public virtual void AppendSpace()
        {
            if (stringBuilder.Length > 0 && stringBuilder[stringBuilder.Length - 1] != ' ')
                stringBuilder.Append(' ');
        }

        public SqlStringBuilder Append(char c)
        {
            stringBuilder.Append(c);
            return this;
        }

        public override string ToString()
        {
            return stringBuilder.ToString();
        }

        public void Clear()
        {
            stringBuilder.Clear();
        }

        public SqlStringBuilder Append(int i)
        {
            stringBuilder.Append(i);
            return this;
        }

        public virtual void AppendSymbol(string symbol)
        {
            stringBuilder.Append('"');
            stringBuilder.Append(symbol);
            stringBuilder.Append('"');
        }
        
        public virtual void AppendAutoIncrement()
        {
            stringBuilder.Append(" AUTO_INCREMENT");
        }

        public virtual void AppendLastInsertedId()
        {
            stringBuilder.Append(" last_insert_id()");
        }

        public virtual void AppendDropTable(string tableName)
        {
            Append("DROP TABLE ");
            AppendSymbol(tableName);
        }

        public virtual void BeforeLimit()
        {
            AppendAfterSpace("LIMIT ");
        }

        public virtual void DuringLimit()
        {
            Append(",");
        }

        public virtual void AfterLimit()
        {
        }

        public virtual void BeforeAddColumn()
        {
            AppendAfterSpace("ADD COLUMN ");
        }

        public virtual void BeforeAlterColumn(string previousColumnName)
        {
            Append(" CHANGE COLUMN ");
            AppendSymbol(previousColumnName);
            Append(" ");
        }
    }
}

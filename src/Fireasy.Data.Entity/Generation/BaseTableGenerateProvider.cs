﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
using Fireasy.Common.Extensions;
using Fireasy.Data.Entity.Metadata;
using Fireasy.Data.Provider;
using Fireasy.Data.Schema;
using Fireasy.Data.Syntax;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Fireasy.Data.Entity.Generation
{
    /// <summary>
    /// 基础的数据表生成器。
    /// </summary>
    public abstract class BaseTableGenerateProvider : ITableGenerateProvider
    {
        IProvider IProviderService.Provider { get; set; }

        /// <summary>
        /// 尝试创建实体类型对应的数据表。
        /// </summary>
        /// <param name="database">提供给当前插件的 <see cref="IDatabase"/> 对象。</param>
        /// <param name="metadata">实体元数据。</param>
        public void TryCreate(IDatabase database, EntityMetadata metadata)
        {
            var syntax = database.Provider.GetService<ISyntaxProvider>();
            if (syntax != null)
            {
                var properties = PropertyUnity.GetPersistentProperties(metadata.EntityType).ToArray();
                if (properties.Length > 0)
                {
                    var commands = BuildCreateTableCommands(syntax, metadata, properties);
                    BatchExecute(database, commands);
                }
            }
        }

        /// <summary>
        /// 尝试添加新的字段。
        /// </summary>
        /// <param name="database">提供给当前插件的 <see cref="IDatabase"/> 对象。</param>
        /// <param name="metadata">实体元数据。</param>
        public void TryAddFields(IDatabase database, EntityMetadata metadata)
        {
            var schema = database.Provider.GetService<ISchemaProvider>();
            var syntax = database.Provider.GetService<ISyntaxProvider>();
            if (schema == null || syntax == null)
            {
                return;
            }

            //查询目前数据表中的所有字段
            var columns = schema.GetSchemas<Column>(database, s => s.TableName == metadata.TableName).Select(s => s.Name).ToArray();
            
            //筛选出新的字段
            var properties = PropertyUnity.GetPersistentProperties(metadata.EntityType)
                .Where(s => !columns.Contains(s.Info.FieldName, StringComparer.CurrentCultureIgnoreCase)).ToArray();

            if (properties.Length != 0)
            {
                var commands = BuildAddFieldCommands(syntax, metadata, properties);
                BatchExecute(database, commands);
            }
        }

        /// <summary>
        /// 判断实体类型对应的数据表是否已经存在。
        /// </summary>
        /// <param name="database">提供给当前插件的 <see cref="IDatabase"/> 对象。</param>
        /// <param name="metadata">实体元数据。</param>
        /// <returns></returns>
        public bool IsExists(IDatabase database, EntityMetadata metadata)
        {
            var syntax = database.Provider.GetService<ISyntaxProvider>();
            if (metadata != null && syntax != null)
            {
                //判断表是否存在
                SqlCommand sql = syntax.ExistsTable(metadata.TableName);
                using (var reader = database.ExecuteReader(sql))
                {
                    return reader.Read();
                }
            }

            return false;
        }

        /// <summary>
        /// 构建创建表的语句。
        /// </summary>
        /// <param name="syntax"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        protected abstract SqlCommand[] BuildCreateTableCommands(ISyntaxProvider syntax, EntityMetadata metadata, IProperty[] properties);

        protected abstract SqlCommand[] BuildAddFieldCommands(ISyntaxProvider syntax, EntityMetadata metadata, IProperty[] properties);

        protected void AppendFieldToBuilder(StringBuilder builder, ISyntaxProvider syntax, IProperty property)
        {
            builder.AppendFormat(" {0}", property.Info.FieldName);

            //数据类型及长度精度等
            builder.AppendFormat(" {0}", syntax.Column((DbType)property.Info.DataType,
                property.Info.Length,
                property.Info.Precision,
                property.Info.Scale));

            //自增
            if (property.Info.GenerateType == IdentityGenerateType.AutoIncrement &&
                !string.IsNullOrEmpty(syntax.IdentityColumn))
            {
                builder.AppendFormat(" {0}", syntax.IdentityColumn);
            }

            //不可空
            if (!property.Info.IsNullable)
            {
                builder.AppendFormat(" not null");
            }

            //默认值
            if (!PropertyValue.IsEmpty(property.Info.DefaultValue))
            {
                if (property.Type == typeof(string))
                {
                    builder.AppendFormat(" default '{0}'", property.Info.DefaultValue);
                }
                else if (property.Type.IsEnum)
                {
                    builder.AppendFormat(" default {0}", (int)property.Info.DefaultValue);
                }
                else if (property.Type == typeof(bool) || property.Type == typeof(bool?))
                {
                    builder.AppendFormat(" default {0}", (bool)property.Info.DefaultValue ? 1 : 0);
                }
                else
                {
                    builder.AppendFormat(" default {0}", property.Info.DefaultValue);
                }
            }
        }

        private void BatchExecute(IDatabase database, SqlCommand[] commands)
        {
            commands.ForEach(s => database.ExecuteNonQuery(s));
        }
    }
}

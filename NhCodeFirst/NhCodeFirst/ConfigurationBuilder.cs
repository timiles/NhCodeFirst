using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Hosting;
using DependencySort;
using NhCodeFirst.NhCodeFirst.Conventions;
using NHibernate.Cfg;
using NHibernate.Dialect;
using NHibernate.Driver;
using urn.nhibernate.mapping.Item2.Item2;
using Environment = NHibernate.Cfg.Environment;

namespace NhCodeFirst.NhCodeFirst
{
    public interface IConfigurationNeedingDialect
     {
         IConfigurationNeedingEntities ForSql2008(string connectionString);
         IConfigurationNeedingEntities ForInMemorySqlite();
     }

    public interface IConfigurationNeedingEntities
    {
        Configuration MapEntities(IEnumerable<Type> rootEntityTypes, bool autodiscover = false);
        IConfigurationNeedingEntities With(Action<Configuration> transform);
    }

    public class ConfigurationBuilder : IConfigurationNeedingEntities, IConfigurationNeedingDialect
    {
        private readonly Configuration _cfg;
        private ConfigurationBuilder()
        {
            _cfg = new Configuration();
        }
        public static IConfigurationNeedingDialect New()
        {
            return new ConfigurationBuilder();
        }



        #region Dialect methods
        public IConfigurationNeedingEntities With(Action<Configuration> transform)
        {
            transform(_cfg);
            return this;
        }
        
        #endregion

        #region Dialect methods
        public IConfigurationNeedingEntities ForSql2008(string connectionString)
        {
            _cfg
                .SetProperty(Environment.Dialect, typeof (NHibernate.Dialect.MsSql2008Dialect).AssemblyQualifiedName)
                .SetProperty(Environment.ConnectionDriver, "NHibernate.Driver.SqlClientDriver")
                .SetProperty(Environment.ConnectionString, connectionString)
                .SetProperty(Environment.ConnectionProvider, "NHibernate.Connection.DriverConnectionProvider");
            return this;
        }
        public IConfigurationNeedingEntities ForInMemorySqlite()
        {
            _cfg
                .SetProperty(Environment.ReleaseConnections, "on_close")
                .SetProperty(Environment.Dialect, typeof (SQLiteDialect).AssemblyQualifiedName)
                .SetProperty(Environment.ConnectionDriver, typeof (SQLite20Driver).AssemblyQualifiedName)
                .SetProperty(Environment.ConnectionString, "data source=:memory:");
            return this;
        }
        #endregion


        IEnumerable<Type> GetEntityTypes(IEnumerable<Type> rootEntityTypes, bool autodiscover)
        {
            var entityTypesToBeChecked = new Queue<Type>(rootEntityTypes);

            var checkedTypes = new HashSet<Type>();
            var entityTypes = new HashSet<Type>();

            do
            {
                var typeToBeChecked = entityTypesToBeChecked.Dequeue();
                
                    entityTypes.Add(typeToBeChecked);
                
                if (autodiscover)
                {
                    var relatedEntities = typeToBeChecked.GetAllMembers()
                        .Select(m => m.ReturnType())
                        .Where(m => m != null)
                        .Select(t => t.GetTypeOrGenericArgumentTypeForICollection())
                        .Select(t => t.GetTypeOrGenericArgumentTypeForIQueryable())
                        .Where(m => m != null);

                    foreach (var e in relatedEntities)
                    {
                        if (e.GetProperty("Id") != null)
                        {
                            if (checkedTypes.Add(e))
                            {
                                entityTypesToBeChecked.Enqueue(e);
                            }
                        }
                    }
                }

            } while (entityTypesToBeChecked.Any());
            return entityTypes;
        }

        public Configuration MapEntities(IEnumerable<Type> rootEntityTypes, bool autodiscover)
        {
            var entityTypes = GetEntityTypes(rootEntityTypes, autodiscover);

            var mappingXDoc = new hibernatemapping(); //this creates the mapping xml document

            //create class xml elements for each entity type
            foreach (var type in entityTypes)
            {
                var @class = new @class()
                {
                    name = type.AssemblyQualifiedName,
                    table = type.Name.Pluralize(), //pluralized table names - could easily have checked for a [TableName("SomeTable")] attribute for custom overrides
                };
                mappingXDoc.@class.Add(@class);
            }

            var conventions =
                GetAll<IClassConvention>() //get all the conventions from the current project
                    .TopologicalSort() //sort them into a dependency tree
                    .ToList();

            //run througn all the conventions, updating the document as we go
            foreach (var convention in conventions)
            {
                foreach (var type in entityTypes)
                {
                    var @class = mappingXDoc.@class.Single(c => c.name == type.AssemblyQualifiedName);
                    convention.Apply(type, @class, entityTypes, mappingXDoc);
                }
            }

            var xml = mappingXDoc.ToString();
#if DEBUG
            File.WriteAllText(Path.Combine(HostingEnvironment.ApplicationPhysicalPath, "config.hbm.xml"), xml);
#endif
            _cfg.AddXml(xml);
            return _cfg;
        }

        static IEnumerable<T> GetAll<T>()
        {
            var unsortedConventionTypes = typeof(T).Assembly.GetTypesSafe()
                 .Where(t => typeof(T).IsAssignableFrom(t))
                 .Where(t => t.CanBeInstantiated());


            var conventionTypes = unsortedConventionTypes.TopologicalSort();
            return conventionTypes.Select(t => (T)Activator.CreateInstance(t)).ToList();
        }
    
        public Configuration Build(string connectionString, IEnumerable<Type> entityTypes)
        {
            var cfg = new ConfigurationBuilder()
                .ForSql2008(connectionString)
                .MapEntities(entityTypes);
            return cfg;
        }
    }
}
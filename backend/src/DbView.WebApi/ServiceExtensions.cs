using DbView.Application;
using DbView.Core;
using DbView.Infrastructure;
using DbView.Infrastructure.Services;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Swagger;
using Mapster;

public static class ServiceExtensions
{
    #region 注册FastEndpoint服务
    /// <summary>
    /// 注册自定义的服务，包括应用层和基础设施层的服务,认证服务
    /// </summary>
    /// <param name="services"></param>
    public static void RegisterFastEndpoint(this IServiceCollection services, IConfiguration configuration)
    {
       
        services.RegisterServicesFromDbViewCore();
        services.RegisterServicesFromDbViewApplication();
        services.RegisterServicesFromDbViewInfrastructure();
        services.AddScoped<DatabaseService>();
        services.AddScoped<ConnectionTestService>();

        services.AddAuthenticationJwtBearer(s => s.SigningKey = configuration.GetSection("Jwt:SecurityKey").Value) //add this
            .AddAuthorization(options =>
            {
                options.AddPolicy("admin", policy => policy.RequireRole("admin"));
                options.AddPolicy("AdminOrAgent", policy =>
                {
                    policy.RequireRole("admin", "agent");
                });
                options.AddPolicy("Agent", policy =>
                {
                    policy.RequireRole("agent");
                });
            });
        services.AddFastEndpoints();
        services.SwaggerDocument(o =>
        {
            o.DocumentSettings = s =>
            {
                s.Title = "Ips项目";
                s.Version = "v1";
            };
        });
    }
    #endregion
    
    #region 领域实体与数据库实体，领域实体与DTO映射服务
    /// <summary>
    /// 领域实体与数据库实体，领域实体与DTO映射服务
    /// </summary>
    /// <param name="services"></param>
    public static void AddEntityMapper(this IServiceCollection services)
    {
        // 配置 Mapster
        // 扫描基础设施层的Mapper
        TypeAdapterConfig.GlobalSettings.Scan(typeof(DbView.Application.Mappers.MapperBase).Assembly);
        TypeAdapterConfig.GlobalSettings.Scan(typeof(DbView.Infrastructure.Mappers.MapperBase).Assembly);
        services.AddSingleton<MapsterMapper.IMapper>(sp =>
            new MapsterMapper.Mapper(TypeAdapterConfig.GlobalSettings));
    }
    #endregion
     
    #region 添加Mediator
    public static void AddMediator(this IServiceCollection services, bool autoSync = false)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ServiceExtensions).Assembly));
    }
    #endregion
    
    #region 配置数据库
    public static void AddDatabase(this IServiceCollection services,bool autoSync = false)
    {
        IConfiguration configuration = services.BuildServiceProvider().GetService<IConfiguration>();
        var database =configuration.GetSection("Database").Value;
        if (database == "mysql")
        {
            services.AddDatabase(configuration, FreeSql.DataType.MySql);
        }
        if (database == "sqlite")
        {
            services.AddDatabase(configuration, FreeSql.DataType.Sqlite,true);
        }
        if (database == "sqlserver")
        {
            services.AddDatabase(configuration, FreeSql.DataType.SqlServer);
        }
        if (database == "postgresql")
        {
            services.AddDatabase(configuration, FreeSql.DataType.PostgreSQL);
        }
    }
    
    public static void AddDatabase(this IServiceCollection services, IConfiguration configuration, FreeSql.DataType dataType,bool autoSync=false)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection");
        Func<IServiceProvider, IFreeSql> fsqlFactory = r =>
        {
            IFreeSql fsql = new FreeSql.FreeSqlBuilder()
                .UseConnectionString(dataType, connectionString)
                .UseAdoConnectionPool(true)
                .UseMonitorCommand(cmd => Console.WriteLine($"Sql：{cmd.CommandText}"))
                .UseAutoSyncStructure(autoSync) //自动同步实体结构到数据库，只有CRUD时才会生成表
                .Build();
            return fsql;
        };
        services.AddSingleton<IFreeSql>(fsqlFactory);
    }
    #endregion
    
}





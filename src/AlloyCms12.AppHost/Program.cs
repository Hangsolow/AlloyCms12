using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

var builder = DistributedApplication.CreateBuilder(args);

var sqlPasswordDefault = new GenerateParameterDefault()
{
	MinLength = 32,
};

var sqlPassword = builder.AddParameter("sql-password", sqlPasswordDefault, secret: true, persist: true);

var sql = builder.AddSqlServer("sql")
.WithPassword(sqlPassword)
.WithLifetime(ContainerLifetime.Persistent)
.WithDataBindMount("obj/sql-data");

var episerverDb = sql.AddDatabase("EPiServerDB");

builder.AddProject<Projects.AlloyCms12>("AlloyCms12")
	.WaitFor(episerverDb)
	.WithReference(episerverDb);

builder.Build().Run();

var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sql")
.WithPassword(sqlPassword)
.WithLifetime(ContainerLifetime.Persistent)
.WithDataBindMount("obj/sql-data");

var episerverDb = sql.AddDatabase("EPiServerDB");

builder.AddProject<Projects.AlloyCms12>("AlloyCms12")
	.WaitFor(episerverDb)
	.WithReference(episerverDb);

builder.Build().Run();

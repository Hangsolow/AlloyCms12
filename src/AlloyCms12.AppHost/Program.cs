var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
.WithLifetime(ContainerLifetime.Persistent)
.WithDataVolume();

var episerverDb = sql.AddDatabase("EPiServerDB");

builder.AddProject<Projects.AlloyCms12>("AlloyCms12")
	.WaitFor(episerverDb)
	.WithReference(episerverDb);

builder.Build().Run();

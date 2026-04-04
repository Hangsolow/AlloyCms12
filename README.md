# Alloy MVC template

This template should not be seen as best practices, but as a great way to learn and test Optimizely CMS. 

## How to run

Local development now uses an Aspire 13.2 AppHost that provisions a disposable SQL Server instance and injects `ConnectionStrings:EPiServerDB` into the Alloy site at runtime.

### Local development with Aspire

Prerequisities
- .NET SDK 10+ and .NET SDK 8+ for the web app
- Podman or Docker with the container daemon running

```bash
$ dotnet run --project src/AlloyCms12.AppHost
````

Stop the local environment with `Ctrl+C`.

The SQL Server resource is disposable by default. If you run the web project outside Aspire, you must provide `ConnectionStrings__EPiServerDB` yourself.

### Any OS with Docker

Prerequisities
- Docker
- Enable Docker support when applying the template
- Review the .env file and make changes where necessary to the Docker-related variables

```bash
$ docker-compose up
````

> Note that this Docker setup is just configured for local development. Follow this [guide to enable HTTPS](https://github.com/dotnet/dotnet-docker/blob/main/samples/run-aspnetcore-https-development.md).

#### Reclaiming Docker Image Space

1. Backup the App_Data/\${DB_NAME}.mdf and App_Data/\${DB_NAME}.ldf DB restoration files for safety
2. Run `docker compose down --rmi all` to remove containers, networks, and images associated with the specific project instance
3. In the future, run `docker compose up` anytime you want to recreate the images and containers

### Any OS with external database server

Prerequisities
- .NET SDK 8+
- SQL Server 2016 (or later) on a external server, e.g. Azure SQL

Create an empty database on the external database server and update the connection string accordingly.

```bash
$ dotnet run
````

# Copilot Instructions for AlloyCms12

## Project Overview

This is an **Optimizely CMS 12 (EPiServer CMS)** MVC web application targeting .NET 8. It uses Aspire 13.2 for local development with a disposable SQL Server container, and Wangkanai.Detection for device/channel detection.

## Running Locally

Local development requires .NET SDK 10+, .NET SDK 8+ (for the web app), and Podman or Docker running.

```bash
dotnet run --project src/AlloyCms12.AppHost   # Ctrl+C to stop
```

The Aspire AppHost (`src/AlloyCms12.AppHost/AlloyCms12.AppHost.csproj`) provisions SQL Server and injects `ConnectionStrings:EPiServerDB` automatically. Running the web project directly without Aspire requires providing `ConnectionStrings__EPiServerDB` manually.

```bash
dotnet run --project src/AlloyCms12/AlloyCms12.csproj   # with external DB configured
```

## Architecture

### Content Type Hierarchy

All content types inherit from two base classes:
- **`SitePageData`** (`Models/Pages/SitePageData.cs`) — base for all pages; implements `ICustomCssInContentArea` and adds `MetaTitle`, `MetaDescription`, `MetaKeywords`, `TeaserText`, `PageImage`, `HideSiteHeader`, `HideSiteFooter`
- **`SiteBlockData`** (`Models/Blocks/SiteBlockData.cs`) — base for all blocks; extends `BlockData`

Use `[SiteContentType]` (instead of `[ContentType]`) on new content types to inherit the default `GroupName = Globals.GroupNames.Default`.

### Controller → View Pattern

Controllers inherit from `PageControllerBase<T>` → `PageController<T>` (Optimizely). Page types **without** a dedicated controller are handled by `DefaultPageController`, which resolves the view as `~/Views/{PageTypeClassName}/Index.cshtml`.

View locations (beyond standard MVC conventions) — registered in `SiteViewEngineLocationExpander`:
- Block partial views: `Views/Shared/Blocks/{BlockTypeName}.cshtml`
- Page partial views: `Views/Shared/PagePartials/{name}.cshtml`

### View Model Pattern

Every page view uses `PageViewModel<T>` (or `IPageViewModel<T>` for multi-type views) which carries:
- `CurrentPage` — the strongly-typed page instance
- `Layout` — a `LayoutModel` populated by `PageViewContextFactory` (navigation links, logotype, auth state, etc.)
- `Section` — the nearest ancestor section page

`PageContextActionFilter` populates `Layout` and `Section` on the view model automatically for all controllers inheriting `PageControllerBase<T>`.

### Template Registration

Non-auto-discovered templates (blocks or page partials with non-default names) are registered in `TemplateCoordinator` (`Business/Rendering/TemplateCoordinator.cs`) using `IViewTemplateModelRegistrator`. Content area display-width tags (`full`, `wide`, `half`, `narrow`) are defined in `Globals.ContentAreaTags` and configured as `DisplayOptions` in `ServiceCollectionExtensions.AddAlloy()`.

### Navigation / Site Settings

`StartPage` doubles as the site settings container. Navigation link collections (`ProductPageLinks`, `CompanyInformationPageLinks`, `NewsPageLinks`, `CustomerZonePageLinks`) and global page references (search, contacts, logotype) all live on `StartPage` and are loaded by `PageViewContextFactory.CreateLayoutModel()`.

## Key Conventions

- **GUIDs on content types**: Every `[ContentType]` must have a stable `GUID` attribute.
- **Property grouping**: Use constants from `Globals.GroupNames` (e.g., `Globals.GroupNames.MetaData`, `Globals.GroupNames.SiteSettings`) for `[Display(GroupName = ...)]`.
- **`[CultureSpecific]`**: Apply to any property that should vary per language.
- **`[BackingType]`**: Used for properties with a non-default EPiServer backing store (e.g., `[BackingType(typeof(PropertyStringList))]` for `IList<string>`).
- **`IContainerPage`**: Implement this interface on page types that should not be rendered as a full page (blocks only). `TemplateCoordinator.OnTemplateResolved` disables `DefaultPageController` for them.
- **Display options / tags**: Reference `Globals.ContentAreaTags` constants when adding `Tags` to `TemplateModel` registrations or `[ContentAreaConfiguration]` attributes.
- **`GetPropertyValue` / `SetPropertyValue`**: Used in property getters/setters on content types for EPiServer-backed properties (see `SitePageData.MetaTitle`).
- **Scheduling disabled in dev**: `SchedulerOptions.Enabled = false` in Development environment (`Startup.cs`).
- **Translations**: Embedded XML resources under `Resources/Translations/` are loaded via `.AddEmbeddedLocalization<Startup>()`.
- **Implicit usings**: `EPiServer`, `EPiServer.Core`, `EPiServer.DataAbstraction`, and `EPiServer.DataAnnotations` are globally imported (see `AlloyCms12.csproj`).

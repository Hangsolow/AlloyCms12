---
name: cms13-migration
description: "Guides migration from Optimizely CMS 12 to CMS 13, covering all breaking changes, API replacements, and code update patterns. USE FOR: upgrading from CMS 12 to CMS 13, fixing compile errors after CMS upgrade, understanding CMS 13 breaking changes, replacing removed APIs (SiteDefinition, IApplicationResolver, EPiServer.Applications namespace, PageReference, DynamicProperty, ContentArea.FilteredItems, XhtmlString.ToHtmlString, IContentRouteEvents, PlugInAttribute, IServiceLocator), migrating Newtonsoft.Json to System.Text.Json in CMS context, updating NuGet packages for CMS 13. DO NOT USE FOR: CMS 11 migrations, Optimizely Commerce upgrades, non-Optimizely .NET projects."
---

# Optimizely CMS 12 → CMS 13 Migration

## Gotchas

These are silent failures that won't produce obvious errors at the right moment — watch for them throughout the migration:

- **`IValidate<T>` validators are silently dropped at runtime.** Implementations are no longer auto-discovered. Every validator needs `services.AddCmsValidator<T>()` or it simply won't run — no warning, no error.
- **Tab/group names with spaces compile fine but break the DB.** The database auto-migrates invalid names on upgrade (e.g., `"Meta Data"` → `"MetaData"` with prefix `G_`), but your code constants still hold the old value. You'll get a mismatch silently — the group tab won't render the property.
- **`ScriptParserOptions` defaults changed.** `SavingMode` is now `ThrowException` — content that previously saved with inline scripts or unsafe HTML now throws `InvalidPropertyValueException`. Test content editing flows after upgrade.
- **`SiteDefinition.Current.StartPage` doesn't throw — it returns an empty reference.** Code that checks `!ContentReference.IsNullOrEmpty(...)` on it may silently fall through with no site settings loaded.
- **`IApplicationResolver` and `IRoutableApplication` live in `EPiServer.Applications`, not `EPiServer.Web`.** Every C# file using these types needs `using EPiServer.Applications;`. In Razor views, add both `@using EPiServer.Applications` and the fully-qualified inject directive (see Step 5). Missing this namespace causes a flood of "type or namespace not found" errors after migrating `SiteDefinition.Current`.
- **`ContentArea.FilteredItems` is obsolete but still compiles.** It won't respect rendering filters in CMS 13. Use `ContentArea.Items` or the filter will be ignored with no error.
- **The DI namespace change is a silent build failure.** If you have `using Microsoft.Extensions.DependencyInjection;` and call `AddCmsCore()`, the old extension method is gone — you get a confusing "no overload" error. Change to `using EPiServer.DependencyInjection;`.

## Migration workflow

Work through each area below in order. Many steps are independent but some (particularly framework/NuGet changes) should be done first to get the project compiling.

### Step 1 — Update .NET target framework

In every `.csproj` in the solution:

```xml
<TargetFramework>net10.0</TargetFramework>
```

Update Docker base images and CI/CD pipelines to use .NET 10 images.

---

### Step 2 — Update NuGet packages

Bump `EPiServer.CMS` (and related packages) to version 13.x. After updating, add explicit `<PackageReference>` entries for any previously-transitive packages you use:

```xml
<!-- Add if you use these namespaces -->
<PackageReference Include="EPiServer.CMS.UI.AspNetIdentity" Version="13.x.x" />
<PackageReference Include="EPiServer.Geolocation" Version="13.x.x" />       <!-- EPiServer.Personalization -->
<PackageReference Include="EPiServer.Blobs" Version="13.x.x" />             <!-- EPiServer.Framework.Blobs -->
<PackageReference Include="EPiServer.Cache" Version="13.x.x" />             <!-- EPiServer.Framework.Cache -->
<PackageReference Include="EPiServer.Events.ChangeNotification" Version="13.x.x" />
<PackageReference Include="EPiServer.Logging" Version="13.x.x" />
<PackageReference Include="EPiServer.HtmlParsing" Version="13.x.x" />
<!-- Add if you removed Episerver.Find and still need it -->
<PackageReference Include="Newtonsoft.Json" Version="..." />
```

Call the corresponding registration methods in `Startup.cs` / `Program.cs`:
```csharp
services.AddCmsBlobs();
services.AddCmsCache();
services.AddCmsGeolocation();
services.AddCmsChangeNotification();
```

See [`references/framework-and-platform.md`](references/framework-and-platform.md) for the full table of extracted packages and registration methods.

---

### Step 3 — Fix DI namespace for CMS registration methods

```csharp
// Before
using Microsoft.Extensions.DependencyInjection;

// After
using EPiServer.DependencyInjection;
```

Affected: `AddCmsCore()`, `AddCmsData()`, `AddCmsFramework()`, `AddTinyMce()`, `AddAdmin()`, `AddCmsUI()`, `AddCmsShell()`, `AddCmsShellUI()`, and the new extracted-package methods above.

---

### Step 4 — Remove service locator usages

Replace constructor injection of removed types (`IServiceLocator`, `ServiceLocationHelper`, etc.) with standard constructor injection from `Microsoft.Extensions.DependencyInjection`. See [`references/framework-and-platform.md`](references/framework-and-platform.md#removed-service-locator-types).

---

### Step 5 — Migrate `SiteDefinition` → `Application`

```csharp
// Before
private readonly ISiteDefinitionResolver _siteResolver;
var startPage = SiteDefinition.Current.StartPage;

// After — IApplicationResolver and IRoutableApplication are in EPiServer.Applications
using EPiServer.Applications;

private readonly IApplicationResolver _appResolver;
var startPage = (_appResolver.GetByContext() as IRoutableApplication)?.EntryPoint
    ?? ContentReference.RootPage;
```

Replace `ISiteDefinitionResolver` → `IApplicationResolver`, `ISiteDefinitionRepository` → `IApplicationRepository`.

#### Razor views

When `SiteDefinition.Current.StartPage` appears in `.cshtml` files, replace it with `@inject` and a local variable:

```cshtml
@using EPiServer.Applications
@inject EPiServer.Applications.IApplicationResolver AppResolver

@{
    var startPageRef = (AppResolver.GetByContext() as IRoutableApplication)?.EntryPoint
        ?? ContentReference.RootPage;
}

@* Use startPageRef wherever SiteDefinition.Current.StartPage was used *@
@Html.MenuList(startPageRef, ItemTemplate)
```

Note: both `@using EPiServer.Applications` **and** the fully-qualified type in `@inject` are required because Razor view compilation resolves namespaces differently than C# files.

See [`references/sites-and-routing.md`](references/sites-and-routing.md) for the full list.

---

### Step 6 — Replace `PageReference` with `ContentReference`

`PageReference` is obsolete. Global find-and-replace in most cases:
On pages and blocks, replace `PageReference` → `ContentReference`. Check properties with `[AllowedTypes]` — if they only allow page types, add `[AllowedTypes(typeof(PageData))]` to preserve validation.
```csharp
// Before
public virtual PageReference ContactPageLink { get; set; }

// After
[AllowedTypes(typeof(PageData))]
public virtual ContentReference ContactPageLink { get; set; }
```

everywhere else, replace `PageReference` → `ContentReference` without adding `[AllowedTypes]`.
```csharp
// Before
PageReference pageRef = page.ContentLink as PageReference;

// After
ContentReference pageRef = page.ContentLink;
```

Check `PageData` properties (`ContentLink`, `ParentLink`, `ArchiveLink`) — they now return `ContentReference`.

---

### Step 7 — Fix content type / tab naming

Check all `[GroupDefinitions]` classes and `[ContentType]` / `[TabDefinition]` attributes for names containing spaces or special characters. CMS 13 auto-migrates the database, but code must match:

```csharp
// Before
[Display(Name = "Meta Data")]
public const string MetaData = "Meta Data";  // space — invalid

// After
[Display(Name = "Meta Data")]
public const string MetaData = "MetaData";   // no space
```

See [`references/content-types-and-properties.md`](references/content-types-and-properties.md#content-type--property-naming-validation).

---

### Step 8 — Remove Dynamic Properties

Delete all usages of `DynamicProperty`, `DynamicPropertyCollection`, `DynamicPropertyBag`, and related types. No replacement exists — migrate data to regular content properties.

Read [`references/content-types-and-properties.md`](references/content-types-and-properties.md#dynamic-properties-removed-entirely) if you need the complete list of removed types.

---

### Step 9 — Fix `ContentArea` and `XhtmlString` usages

```csharp
// Before
var items = contentArea.FilteredItems;
var html = xhtmlString.ToHtmlString(User);

// After
var items = contentArea.Items; // or use IContentAreaItemsRenderingFilter
// For XhtmlString: use @Html.PropertyFor() / Tag Helpers in views
```

Replace `IContentAreaLoader.Get()` → `.LoadContent()`.
Replace `ContentAreaItemExtensions.GetContent()` → `.LoadContent()`.

Read [`references/content-management-and-repository.md`](references/content-management-and-repository.md) if you encounter other `ContentArea`, `XhtmlString`, `PropertyString`, or `PropertyLongString` members that don't compile.

---

### Step 10 — Fix `IContentTypeRepository` generic usages

```csharp
// Before
private readonly IContentTypeRepository<PageType> _repo;

// After
private readonly IContentTypeRepository _repo;
```

For generic usages of `IContentTypeRepository<T>`, remove the generic type parameter and cast the result of `Load()` to the expected type.
remember that the ContentType from `Load()` can be null if the type isn't found and the cast will also return null if the type does not match, so consider making the method return type nullable and adding null checks where appropriate.  

```csharp
// New way
var contentPageType = contentTypeRepository.Load(pageType) as PageType;
```
---

### Step 11 — Fix `PropertyString` / `PropertyLongString` accessors

```csharp
// Before: prop.PublicString, prop.PublicLongString
// After:  prop.String,       prop.LongString
```

---

### Step 12 — Register validators explicitly

```csharp
// Implementations of IValidate<T> are no longer auto-discovered
services.AddCmsValidator<MyContentValidator>();
```

---

### Step 13 — Fix routing events

```csharp
// Before
private readonly IContentRouteEvents _routeEvents;
_routeEvents.CreatingVirtualPath += OnCreatingVirtualPath;

// After
private readonly IContentUrlGeneratorEvents _generatorEvents;
_generatorEvents.GeneratingUrl += OnGeneratingUrl;
```

See [`references/sites-and-routing.md`](references/sites-and-routing.md#routing-events-icontentRouteevents-removed).

---

### Step 14 — Replace `SaveAction` usages

| Before | After |
|---|---|
| `SaveAction.None` | `SaveAction.Default` |
| `SaveAction.DelayedPublish` | `SaveAction.Schedule` |

---

### Step 15 — Replace `PlugInAttribute` / scheduled jobs

```csharp
// Before
[ScheduledPlugIn(DisplayName = "My Job")]
public class MyJob : JobBase { }

// After
[ScheduledJob(DisplayName = "My Job")]
public class MyJob : ScheduledJobBase { }
```

---

### Step 16 — Update UI URL references

The CMS UI base path changed from `/EPiServer/` to `/Optimizely/`. Update bookmarks, scripts, webhooks, and any hard-coded URL references. See [`references/sites-and-routing.md`](references/sites-and-routing.md#ui-url-path-change-episerver--optimizely) for the full mapping.

---

### Step 17 — Verify Newtonsoft.Json

If your project (or removed packages like `Episerver.Find`) used Newtonsoft.Json transitively, add an explicit reference:

```xml
<PackageReference Include="Newtonsoft.Json" Version="13.x.x" />
```

---

## Common compile-error patterns after upgrade

| Error | Fix |
|---|---|
| `IServiceLocator` not found | Replace with constructor injection |
| `SiteDefinition.Current` | Inject `IApplicationResolver` (`using EPiServer.Applications`) |
| `IApplicationResolver` / `IRoutableApplication` not found | Add `using EPiServer.Applications;` (or `@using EPiServer.Applications` in Razor views) |
| `PageReference` ambiguous/obsolete | Replace with `ContentReference` |
| `ContentArea.FilteredItems` | Use `ContentArea.Items` |
| `ToHtmlString(IPrincipal)` | Use Tag Helpers / `Html.PropertyFor()` |
| `BlockTypeRepository` / `PageTypeRepository` | Use `IContentTypeRepository` |
| `IContentRouteEvents` | Use `IContentUrlResolverEvents` / `IContentUrlGeneratorEvents` |
| `ScheduledPlugInAttribute` | Use `ScheduledJobAttribute` |
| `DynamicProperty` | Remove — no replacement |
| `AddCmsCore()` namespace error | `using EPiServer.DependencyInjection;` |
| `AddCmsAspNetIdentity()` not found | `using EPiServer.DependencyInjection;` (same namespace — add even if `AddCmsCore` already works) |
| `IValidate<T>` not picked up | `services.AddCmsValidator<T>()` |
| `'IContentRouteHelper' does not contain 'Page'` | Use `PageContext.Content` — in CMS 13, `PageContext` exposes `.Content` instead of `.Page`; no need to inject `IContentRouteHelper` separately in controller base classes |

If an API isn't covered above, consult [`references/api-replacement-map.md`](references/api-replacement-map.md) for the complete CMS 12 → 13 type, method, event, and namespace mapping.

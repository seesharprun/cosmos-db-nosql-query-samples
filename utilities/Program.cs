using System.Reflection;
using System.Text;
using AutoMapper;
using Humanizer;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Stubble.Compilation;
using Stubble.Compilation.Builders;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

string databaseCredential = configuration["AZURE_COSMOS_DB_CREDENTIAL"]
    ?? throw new InvalidOperationException("Database credential is not set. Please set the AZURE_COSMOS_DB_CREDENTIAL user secret.");

MapperConfiguration mapperConfiguration = new(config => config.CreateMap<NoSQLQueryReference, ReferenceTemplateContext>());
IMapper mapper = mapperConfiguration.CreateMapper();

IDeserializer yamlDeserializer = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .WithEnumNamingConvention(HyphenatedNamingConvention.Instance)
    .WithCaseInsensitivePropertyMatching()
    .IgnoreUnmatchedProperties()
    .Build();

ISerializer yamlSerializer = new SerializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
    .WithIndentedSequences()
    .Build();

using Stream referenceTemplateStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("utilities.templates.reference.mustache.tmpl")
    ?? throw new FileNotFoundException("Template file not found in embedded resources. Ensure the template is correctly embedded in the assembly and named \"utilities.templates.reference.mustache.tmpl\".");

using StreamReader referenceTemplateReader = new(referenceTemplateStream);

string referenceTemplateMustache = await referenceTemplateReader.ReadToEndAsync();

using Stream functionsLandingTemplateStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("utilities.templates.functions.mustache.tmpl")
    ?? throw new FileNotFoundException("Template file not found in embedded resources. Ensure the template is correctly embedded in the assembly and named \"utilities.templates.landing.mustache.tmpl\".");

using StreamReader functionsLandingTemplateReader = new(functionsLandingTemplateStream);

string functionsLandingTemplateMustache = await functionsLandingTemplateReader.ReadToEndAsync();

using Stream clausesLandingTemplateStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("utilities.templates.clauses.mustache.tmpl")
    ?? throw new FileNotFoundException("Template file not found in embedded resources. Ensure the template is correctly embedded in the assembly and named \"utilities.templates.landing.mustache.tmpl\".");

using StreamReader clausesLandingTemplateReader = new(clausesLandingTemplateStream);

string clausesLandingTemplateMustache = await clausesLandingTemplateReader.ReadToEndAsync();

using Stream keywordsLandingTemplateStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("utilities.templates.keywords.mustache.tmpl")
    ?? throw new FileNotFoundException("Template file not found in embedded resources. Ensure the template is correctly embedded in the assembly and named \"utilities.templates.keywords.mustache.tmpl\".");

using StreamReader keywordsLandingTemplateReader = new(keywordsLandingTemplateStream);

string keywordsLandingTemplateMustache = await keywordsLandingTemplateReader.ReadToEndAsync();

StubbleCompilationRenderer compiler = new StubbleCompilationBuilder()
    .Configure(settings =>
    {
        settings.SetIgnoreCaseOnKeyLookup(true);
    })
    .Build();

Func<ReferenceTemplateContext, string> referenceRenderer = await compiler.CompileAsync<ReferenceTemplateContext>(referenceTemplateMustache);

Func<LandingTemplateContextGrouped, string> functionsLandingRenderer = await compiler.CompileAsync<LandingTemplateContextGrouped>(functionsLandingTemplateMustache);

Func<LandingTemplateContextFlattened, string> clausesLandingRenderer = await compiler.CompileAsync<LandingTemplateContextFlattened>(clausesLandingTemplateMustache);

Func<LandingTemplateContextFlattened, string> keywordsLandingRenderer = await compiler.CompileAsync<LandingTemplateContextFlattened>(keywordsLandingTemplateMustache);

CosmosClientOptions options = new()
{
    SerializerOptions = new CosmosSerializationOptions
    {
        IgnoreNullValues = true,
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
    },
    //ConnectionMode = ConnectionMode.Gateway,
    AllowBulkExecution = true,
    MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30),
    MaxRetryAttemptsOnRateLimitedRequests = 30,
    //ServerCertificateCustomValidationCallback = (_, _, _) => true
};

using CosmosClient client = new(databaseCredential, options);

Container container = client.GetContainer("cosmicworks", "products");

static bool filter(string resource) =>
    resource.Contains(".reference", StringComparison.OrdinalIgnoreCase) &&
    resource.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

IEnumerable<string> resources = Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(filter);

Tree tree = new("[bold yellow]Generating Markdown files...[/]");

await AnsiConsole.Live(tree)
    .StartAsync(async console =>
    {
        Dictionary<string, NoSQLQueryReference> referenceDictionary = [];
        Dictionary<string, TreeNode> nodeDictionary = [];
        List<(string Group, string Title, string File, string Description)> links = [];

        foreach (string resource in resources)
        {
            TreeNode node = tree.AddNode($"[green]Reading [italic]{resource}[/][/]");
            console.Refresh();

            using Stream yamlStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Resource '{resource}' not found.");

            using StreamReader yamlReader = new(yamlStream);

            NoSQLQueryReference reference = yamlDeserializer.Deserialize<NoSQLQueryReference>(yamlReader)
                ?? throw new InvalidOperationException($"Failed to deserialize resource '{resource}'.");

            string referenceName = Patterns.FileNameToReferenceRegex().Match(resource).Groups[1].Value.Transform(To.LowerCase);
            referenceDictionary.Add(referenceName, reference);

            nodeDictionary.Add(reference.Name.ToLowerInvariant(), node);
        }

        foreach (NoSQLQueryReference reference in referenceDictionary.Values)
        {
            if (reference is not null)
            {
                ReferenceTemplateContext context = mapper.Map<ReferenceTemplateContext>(reference) with
                {
                    Date = $"{DateTime.UtcNow.Date:MM/dd/yyyy}",
                    Resources = reference.Related?.Select(r => new ReferenceTemplateContextResource
                    {
                        File = Path.ChangeExtension(r.Reference, extension: default),
                        Title = referenceDictionary.TryGetValue(Path.GetFileNameWithoutExtension(r.Reference).ToLowerInvariant(), out NoSQLQueryReference? relatedReference) ? relatedReference.Name : "<error>",
                    }) ?? [],
                    RenderArguments = reference.Arguments?.Any() ?? false,
                    RenderExamples = reference.Examples?.Any() ?? false,
                    UseSample = reference.ExamplesSample is not null,
                    RenderRemarks = reference.Remarks?.Any() ?? false,
                    RenderSummary = reference.Summary is not null
                };

                if (context.UseSample && context.ExamplesSample?.Query is not null)
                {
                    context = context with
                    {
                        SampleJson = await container.GetResultJsonAsync(context.ExamplesSample.Query)
                    };
                }

                string output = referenceRenderer(context);

                string outDir = Path.Combine(Directory.GetCurrentDirectory(), "out");

                if (!Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                string outFile = Path.Combine(outDir, $"{reference.Name.Transform(To.LowerCase).Kebaberize()}.md");

                string relativeOutFile = Path.GetRelativePath(outDir, outFile);

                TreeNode node = nodeDictionary[reference.Name.ToLowerInvariant()];
                node.AddNode($"[blue]Writing to [italic link]{relativeOutFile}[/][/]");
                console.Refresh();

                using FileStream fileStream = File.Open(outFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                using StreamWriter fileWriter = new(fileStream);
                await fileWriter.WriteAsync(output);

                links.Add(($"{reference.Group}", reference.Name, relativeOutFile, reference.Description));
            }
        }

        {
            TreeNode node = tree.AddNode("[green]Writing landing pages...[/]");
            console.Refresh();

            LandingTemplateContextGrouped functionsLandingContext = new()
            {
                Date = $"{DateTime.UtcNow.Date:MM/dd/yyyy}",
                Groups = links
                    .Where(r => r.Group != nameof(NoSQLQueryReferenceGroup.Clause) && r.Group != nameof(NoSQLQueryReferenceGroup.Keyword))
                    .GroupBy(r => r.Group)
                    .Select(g => new LandingTemplateContextGroup
                    {
                        Title = g.Key.GetHeader(),
                        Links = g.Select(r => new LandingTemplateContextLink
                        {
                            Title = r.Title,
                            File = r.File,
                            Description = r.Description,
                        }),
                    })
            };

            string functionsMarkdown = functionsLandingRenderer(functionsLandingContext);

            string functionsOutDir = Path.Combine(Directory.GetCurrentDirectory(), "out");

            if (!Directory.Exists(functionsOutDir))
            {
                Directory.CreateDirectory(functionsOutDir);
            }

            string functionsOutFile = Path.Combine(functionsOutDir, "functions.md");

            string functionsRelativeOutFile = Path.GetRelativePath(functionsOutDir, functionsOutFile);

            node.AddNode($"[blue]Writing to [italic link]{functionsRelativeOutFile}[/][/]");
            console.Refresh();

            using FileStream functionsFileStream = File.Open(functionsOutFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            using StreamWriter functionsFileWriter = new(functionsFileStream);
            await functionsFileWriter.WriteAsync(functionsMarkdown);

            string clausesSlug = nameof(NoSQLQueryReferenceGroup.Clause).Pluralize().Transform(To.LowerCase);

            LandingTemplateContextFlattened clausesLandingContext = new()
            {
                Date = $"{DateTime.UtcNow.Date:MM/dd/yyyy}",
                Links = links
                    .Where(r => r.Group == nameof(NoSQLQueryReferenceGroup.Clause))
                    .Select(r => new LandingTemplateContextLink
                    {
                        Title = r.Title,
                        File = r.File,
                        Description = r.Description
                    })
            };

            string clausesMarkdown = clausesLandingRenderer(clausesLandingContext);

            string clausesOutDir = Path.Combine(Directory.GetCurrentDirectory(), "out");

            if (!Directory.Exists(clausesOutDir))
            {
                Directory.CreateDirectory(clausesOutDir);
            }

            string clausesOutFile = Path.Combine(clausesOutDir, $"{clausesSlug}.md");

            string relativeClausesOutFile = Path.GetRelativePath(clausesOutDir, clausesOutFile);

            node.AddNode($"[blue]Writing to [italic link]{relativeClausesOutFile}[/][/]");
            console.Refresh();

            using FileStream clausesFileStream = File.Open(clausesOutFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            using StreamWriter clausesFileWriter = new(clausesFileStream);
            await clausesFileWriter.WriteAsync(clausesMarkdown);

            string keywordsSlug = nameof(NoSQLQueryReferenceGroup.Keyword).Pluralize().Transform(To.LowerCase);

            LandingTemplateContextFlattened keywordsLandingContext = new()
            {
                Date = $"{DateTime.UtcNow.Date:MM/dd/yyyy}",
                Links = links
                    .Where(r => r.Group == nameof(NoSQLQueryReferenceGroup.Keyword))
                    .Select(r => new LandingTemplateContextLink
                    {
                        Title = r.Title,
                        File = r.File,
                        Description = r.Description
                    })
            };

            string keywordsMarkdown = keywordsLandingRenderer(keywordsLandingContext);

            string keywordsOutDir = Path.Combine(Directory.GetCurrentDirectory(), "out");

            if (!Directory.Exists(keywordsOutDir))
            {
                Directory.CreateDirectory(keywordsOutDir);
            }

            string keywordsOutFile = Path.Combine(keywordsOutDir, $"{keywordsSlug}.md");

            string relativeKeywordsOutFile = Path.GetRelativePath(keywordsOutDir, keywordsOutFile);

            node.AddNode($"[blue]Writing to [italic link]{relativeKeywordsOutFile}[/][/]");
            console.Refresh();

            using FileStream keywordsFileStream = File.Open(keywordsOutFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            using StreamWriter keywordsFileWriter = new(keywordsFileStream);
            await keywordsFileWriter.WriteAsync(keywordsMarkdown);
        }

        {

            TreeNode node = tree.AddNode("[green]Writing table of contents (TOC)...[/]");
            console.Refresh();

            NavigationContext context = new()
            {
                Items = [
                    new NavigationContextItem
                    {
                        Name = "Functions documentation",
                        Href = "../functions.md"
                    },
                    ..links
                        .Where(r => r.Group != nameof(NoSQLQueryReferenceGroup.Clause) && r.Group != nameof(NoSQLQueryReferenceGroup.Keyword))
                        .GroupBy(r => r.Group)
                        .Select(g => new NavigationContextItem
                        {
                            Name = g.Key.GetHeader(),
                            Items = g.Select(r => new NavigationContextItem
                            {
                                Name = r.Title,
                                DisplayName = string.Join(
                                    ", ", (r.Title, r.Group).GetDisplayNames()
                                ),
                                Href = $"../{r.File}"
                            }),
                        })
                ]
            };

            string yaml = yamlSerializer.Serialize(context);
            StringBuilder output = new();
            output.AppendLine("### YamlMime:TOC");
            output.Append(yaml);

            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "out", "functions");

            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            string outFile = Path.Combine(outDir, "toc.yml");

            string relativeOutFile = Path.GetRelativePath(outDir, outFile);

            node.AddNode($"[blue]Writing to [italic link]{relativeOutFile}[/][/]");
            console.Refresh();

            using FileStream fileStream = File.Open(outFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            using StreamWriter fileWriter = new(fileStream);
            await fileWriter.WriteAsync(output.ToString());

            string clausesSlug = nameof(NoSQLQueryReferenceGroup.Clause).Pluralize().Transform(To.LowerCase);

            NavigationContext clausesContext = new()
            {
                Items = [
                    new NavigationContextItem
                    {
                        Name = $"{clausesSlug.Titleize()} documentation",
                        Href = $"../{clausesSlug}.md"
                    },
                    ..links
                        .Where(r => r.Group == nameof(NoSQLQueryReferenceGroup.Clause))
                        .Select(r => new NavigationContextItem
                        {
                            Name = r.Title,
                            DisplayName = string.Join(
                                ", ", (r.Title, r.Group).GetDisplayNames()
                            ),
                            Href = $"../{r.File}"
                        })
                ]
            };

            string clausesYaml = yamlSerializer.Serialize(clausesContext);
            StringBuilder clausesOutput = new();
            clausesOutput.AppendLine("### YamlMime:TOC");
            clausesOutput.Append(clausesYaml);

            string clausesOutDir = Path.Combine(Directory.GetCurrentDirectory(), "out", $"{clausesSlug}");

            if (!Directory.Exists(clausesOutDir))
            {
                Directory.CreateDirectory(clausesOutDir);
            }

            string clausesOutFile = Path.Combine(clausesOutDir, "toc.yml");

            string relativeClausesOutFile = Path.GetRelativePath(clausesOutDir, clausesOutFile);

            node.AddNode($"[blue]Writing to [italic link]{relativeClausesOutFile}[/][/]");
            console.Refresh();

            using FileStream clausesFileStream = File.Open(clausesOutFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            using StreamWriter clausesFileWriter = new(clausesFileStream);
            await clausesFileWriter.WriteAsync(clausesOutput.ToString());

            string keywordsSlug = nameof(NoSQLQueryReferenceGroup.Keyword).Pluralize().Transform(To.LowerCase);

            NavigationContext keywordsContext = new()
            {
                Items = [
                    new NavigationContextItem
                    {
                        Name = $"{keywordsSlug.Titleize()} documentation",
                        Href = $"../{keywordsSlug}.md"
                    },
                    ..links
                        .Where(r => r.Group == nameof(NoSQLQueryReferenceGroup.Keyword))
                        .Select(r => new NavigationContextItem
                        {
                            Name = r.Title,
                            DisplayName = string.Join(
                                ", ", (r.Title, r.Group).GetDisplayNames()
                            ),
                            Href = $"../{r.File}"
                        })
                ]
            };

            string keywordsYaml = yamlSerializer.Serialize(keywordsContext);
            StringBuilder keywordsOutput = new();
            keywordsOutput.AppendLine("### YamlMime:TOC");
            keywordsOutput.Append(keywordsYaml);

            string keywordsOutDir = Path.Combine(Directory.GetCurrentDirectory(), "out", $"{keywordsSlug}");

            if (!Directory.Exists(keywordsOutDir))
            {
                Directory.CreateDirectory(keywordsOutDir);
            }

            string keywordsOutFile = Path.Combine(keywordsOutDir, "toc.yml");

            string relativeKeywordsOutFile = Path.GetRelativePath(keywordsOutDir, keywordsOutFile);

            node.AddNode($"[blue]Writing to [italic link]{relativeKeywordsOutFile}[/][/]");
            console.Refresh();

            using FileStream keywordsFileStream = File.Open(keywordsOutFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            using StreamWriter keywordsFileWriter = new(keywordsFileStream);
            await keywordsFileWriter.WriteAsync(keywordsOutput.ToString());
        }
    });
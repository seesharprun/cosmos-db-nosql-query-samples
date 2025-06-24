using System.Configuration;
using System.Reflection;
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
    .Build();

using Stream referenceTemplateStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("utilities.templates.reference.mustache.tmpl")
    ?? throw new FileNotFoundException("Template file not found in embedded resources. Ensure the template is correctly embedded in the assembly and named \"utilities.templates.reference.mustache.tmpl\".");

using StreamReader referenceTemplateReader = new(referenceTemplateStream);

string referenceTemplateMustache = await referenceTemplateReader.ReadToEndAsync();

using Stream landingTemplateStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("utilities.templates.landing.mustache.tmpl")
    ?? throw new FileNotFoundException("Template file not found in embedded resources. Ensure the template is correctly embedded in the assembly and named \"utilities.templates.landing.mustache.tmpl\".");

using StreamReader landingTemplateReader = new(landingTemplateStream);

string landingTemplateMustache = await landingTemplateReader.ReadToEndAsync();

StubbleCompilationRenderer compiler = new StubbleCompilationBuilder()
    .Configure(settings =>
    {
        settings.SetIgnoreCaseOnKeyLookup(true);
    })
    .Build();

Func<ReferenceTemplateContext, string> referenceRenderer = await compiler.CompileAsync<ReferenceTemplateContext>(referenceTemplateMustache);

Func<LandingTemplateContext, string> landingRenderer = await compiler.CompileAsync<LandingTemplateContext>(landingTemplateMustache);

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
            TreeNode node = tree.AddNode("[green]Writing landing page...[/]");
            console.Refresh();

            LandingTemplateContext context = new()
            {
                Title = "Query language reference",
                Date = $"{DateTime.UtcNow.Date:MM/dd/yyyy}",
                Groups = links
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

            string output = landingRenderer(context);

            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "out");

            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            string outFile = Path.Combine(outDir, "index.md");

            string relativeOutFile = Path.GetRelativePath(outDir, outFile);

            node.AddNode($"[blue]Writing to [italic link]{relativeOutFile}[/][/]");
            console.Refresh();

            using FileStream fileStream = File.Open(outFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            using StreamWriter fileWriter = new(fileStream);
            await fileWriter.WriteAsync(output);
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
                        Href = "index.md"
                    },
                    ..links
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
                                Href = r.File,
                            }),
                        })
                ]
            };

            string output = yamlSerializer.Serialize(context.Items);

            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "out");

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
            await fileWriter.WriteAsync(output);
        }
    });
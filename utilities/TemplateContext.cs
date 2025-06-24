record LandingTemplateContext
{
    public required string Title { get; init; }

    public required string Date { get; init; }

    public required IEnumerable<LandingTemplateContextGroup> Groups { get; init; }
}

record LandingTemplateContextGroup
{
    public required string Title { get; init; }

    public required IEnumerable<LandingTemplateContextLink> Links { get; init; }
}

record LandingTemplateContextLink
{
    public required string Title { get; init; }

    public required string File { get; init; }

    public required string Description { get; init; }
}

record ReferenceTemplateContext : NoSQLQueryReference
{
    public required string Date { get; init; }

    public required IEnumerable<ReferenceTemplateContextResource> Resources { get; init; }

    public required bool RenderArguments { get; init; }

    public required bool RenderExamples { get; init; }

    public required bool UseSample { get; init; }

    public required string? SampleJson { get; init; }

    public required bool RenderRemarks { get; init; }

    public required bool RenderSummary { get; init; }

    public required IEnumerable<string> RemarksList { get; init; }
}

record ReferenceTemplateContextResource
{
    public required string Title { get; init; }

    public required string File { get; init; }
}
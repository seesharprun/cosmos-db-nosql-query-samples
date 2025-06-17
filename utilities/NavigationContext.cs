record NavigationContext
{
    public required IEnumerable<NavigationContextItem> Items { get; init; }
}

record NavigationContextItem
{
    public required string Name { get; init; }

    public string? DisplayName { get; init; }

    public string? Href { get; init; }

    public IEnumerable<NavigationContextItem>? Items { get; init; }
}
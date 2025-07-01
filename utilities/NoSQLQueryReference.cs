record NoSQLQueryReference
{
    public required string Name { get; init; }

    public required NoSQLQueryReferenceGroup Group { get; init; }

    public required string Description { get; init; }

    public required string? Summary { get; init; }

    public required string Syntax { get; init; }

    public required IEnumerable<NoSQLQueryReferenceArgument> Arguments { get; init; }

    public required string? ArgumentsNote { get; init; }

    public required string Returns { get; init; }

    public required string? ReturnsNote { get; init; }

    public required NoSQLQueryReferenceSample? ExamplesSample { get; init; }

    public required IEnumerable<NoSQLQueryReferenceExample> Examples { get; init; }

    public required IEnumerable<string> Remarks { get; init; }

    public required IEnumerable<NoSQLQueryReferenceRelated> Related { get; init; }
}

enum NoSQLQueryReferenceGroup
{
    Aggregation,

    Array,

    Clause,

    Keyword,

    Conditional,

    DateAndTime,

    FullTextSearch,

    Item,

    Mathematical,

    Spatial,

    String,

    TypeChecking,
}

record NoSQLQueryReferenceArgument
{
    public required string Name { get; init; }

    public required bool Required { get; init; }

    public required string? Description { get; init; }
}

record NoSQLQueryReferenceSample
{
    public required NoSQLQueryReferenceSampleSet Set { get; init; }

    public required string Query { get; init; }
}

enum NoSQLQueryReferenceSampleSet
{
    Products = default,

    Stores,

    Employees
}

record NoSQLQueryReferenceExample
{
    public required string Title { get; init; }

    public required string? Explanation { get; init; }

    public required string Description { get; init; }

    public required string Query { get; init; }

    public required string Result { get; init; }
}

record NoSQLQueryReferenceRelated
{
    public required string Reference { get; init; }
}
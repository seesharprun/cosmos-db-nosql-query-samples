record NoSQLQueryReference
{
    public required string Name { get; init; }

    public required NoSQLQueryReferenceGroup Group { get; init; }

    public required string Description { get; init; }

    public required string? Summary { get; init; }

    public required string Syntax { get; init; }

    public required string Returns { get; init; }

    public required IEnumerable<NoSQLQueryReferenceParameter> Parameters { get; init; }

    public required NoSQLQueryReferenceExampleSet Examples { get; init; }

    public required IEnumerable<string> Remarks { get; init; }

    public required IEnumerable<NoSQLQueryReferenceRelated> Related { get; init; }
}

enum NoSQLQueryReferenceGroup
{
    Aggregation,

    Array,

    Conditional,

    DateAndTime,

    FullTextSearch,

    Item,

    Mathematical,

    Spatial,

    String,

    TypeChecking,
}

record NoSQLQueryReferenceParameter
{
    public required string Name { get; init; }

    public required bool Required { get; init; }

    public required string? Description { get; init; }
}

record NoSQLQueryReferenceExampleSet
{
    public required NoSQLQueryReferenceSample? Sample { get; init; }

    public required IEnumerable<NoSQLQueryReferenceExample> Items { get; init; }
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
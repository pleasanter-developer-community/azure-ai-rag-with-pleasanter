using Microsoft.Extensions.VectorData;

namespace RAGWithPleasanter;

public class Ramen
{

    // Collection of float values representing text vector
    [VectorStoreRecordVector]
    public ReadOnlyMemory<float> TextVector { get; init; }

    // ID field
    [VectorStoreRecordKey]
    public required string ID { get; init; }

    // Store name
    [VectorStoreRecordData]
    public required string StoreName { get; init; }

    // Reviews
    [VectorStoreRecordData]
    public required string Reviews { get; init; }

    // Location
    [VectorStoreRecordData]
    public required string Location { get; init; }

    // Style of ramen
    [VectorStoreRecordData]
    public required string Style { get; init; }

    // Recommended menu
    [VectorStoreRecordData]
    public required string RecommendedMenu { get; init; }

    // Keyword for search
    [VectorStoreRecordData]
    public required string Keyword { get; init; }
}

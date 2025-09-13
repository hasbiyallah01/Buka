namespace AmalaSpotLocator.Data;

public class SeederConfiguration
{

    public DataSetType DataSetType { get; set; } = DataSetType.Production;

    public bool IncludeDevelopmentData { get; set; } = false;

    public bool ValidateAfterSeeding { get; set; } = true;

    public bool ForceReseed { get; set; } = false;

    public int PerformanceUserCount { get; set; } = 1000;

    public int PerformanceSpotCount { get; set; } = 5000;

    public int PerformanceReviewsPerSpot { get; set; } = 5;

    public bool IncludeEdgeCases { get; set; } = false;

    public int? RandomSeed { get; set; }

    public bool SkipIfDataExists { get; set; } = true;

    public static class Presets
    {
        public static SeederConfiguration Production => new()
        {
            DataSetType = DataSetType.Production,
            IncludeDevelopmentData = false,
            ValidateAfterSeeding = true,
            ForceReseed = false,
            SkipIfDataExists = true
        };

        public static SeederConfiguration Development => new()
        {
            DataSetType = DataSetType.Production,
            IncludeDevelopmentData = true,
            ValidateAfterSeeding = true,
            ForceReseed = false,
            SkipIfDataExists = true,
            IncludeEdgeCases = false
        };

        public static SeederConfiguration Testing => new()
        {
            DataSetType = DataSetType.Integration,
            IncludeDevelopmentData = false,
            ValidateAfterSeeding = true,
            ForceReseed = true,
            SkipIfDataExists = false,
            RandomSeed = 42
        };

        public static SeederConfiguration UnitTesting => new()
        {
            DataSetType = DataSetType.Minimal,
            IncludeDevelopmentData = false,
            ValidateAfterSeeding = false,
            ForceReseed = true,
            SkipIfDataExists = false,
            RandomSeed = 42
        };

        public static SeederConfiguration Performance => new()
        {
            DataSetType = DataSetType.Performance,
            IncludeDevelopmentData = false,
            ValidateAfterSeeding = false,
            ForceReseed = true,
            SkipIfDataExists = false,
            PerformanceUserCount = 1000,
            PerformanceSpotCount = 5000,
            PerformanceReviewsPerSpot = 5,
            RandomSeed = 42
        };

        public static SeederConfiguration EdgeCases => new()
        {
            DataSetType = DataSetType.EdgeCases,
            IncludeDevelopmentData = false,
            ValidateAfterSeeding = true,
            ForceReseed = true,
            SkipIfDataExists = false,
            IncludeEdgeCases = true,
            RandomSeed = 42
        };
    }
}

public enum DataSetType
{

    Production,

    Minimal,

    Integration,

    Performance,

    EdgeCases
}
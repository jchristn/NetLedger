namespace Test.Automated
{
    using System;

    /// <summary>
    /// Represents the result of a single test.
    /// </summary>
    internal class TestResult
    {
        /// <summary>
        /// Name of the test.
        /// </summary>
        public string TestName { get; set; } = String.Empty;

        /// <summary>
        /// Whether the test passed.
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Error message if the test failed.
        /// </summary>
        public string? Error { get; set; }
    }
}
